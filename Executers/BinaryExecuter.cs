using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver.Messages.DataRequest;
using System.Threading;
using Unitronics.ComDriver.Command;
using System.Diagnostics;

namespace Unitronics.ComDriver.Executers
{
    class BinaryExecuter : Executer
    {
        #region Locals

        private enum MemoryType
        {
            // To be used as Sub Command when burning or reading from Flash/Ram on PLC
            None = 0,
            ExternalFlash = 1,
            SerialFlash = 2,
            InternalFlash = 3,
            SRAM = 4,
        }

        private struct PlcResponseMessage
        {
            internal string responseStringMessage;
            internal byte[] responseBytesMessage;
            internal CommunicationException comException;
        }

        private Dictionary<GuidClass, PlcResponseMessage> m_responseMessageQueue =
            new Dictionary<GuidClass, PlcResponseMessage>();

        private object _lockObj = new object();

        #endregion

        #region Constructor

        public BinaryExecuter(int unitId, Channel channel, PlcVersion plcVersion, Guid plcGuid)
            : base(unitId, channel, plcVersion, plcGuid)
        {
        }

        #endregion

        internal override void PerformReadWrite(ref ReadWriteRequest[] values, string parentID,
            bool suppressEthernetHeader)
        {
            for (int iter = 0; iter < values.Length; iter++)
            {
                if (values[iter] is BinaryRequest)
                {
                    if (this.BreakFlag)
                    {
                        throw new ComDriveExceptions("Request aborted by user",
                            ComDriveExceptions.ComDriveException.AbortedByUser);
                    }

                    if ((PLCFactory.ActivationServiceEnabled) || (values[iter] as BinaryRequest).IsInternal)
                    {
                        if (this.BreakFlag)
                        {
                            throw new ComDriveExceptions("Request aborted by user",
                                ComDriveExceptions.ComDriveException.AbortedByUser);
                        }

                        SplitAndSendBinaryData(values[iter], parentID);
                    }
                    else
                    {
                        throw new ComDriveExceptions("You are not authorized to use Binary Requests",
                            ComDriveExceptions.ComDriveException.UnauthorisedCommand);
                    }
                }
                else
                {
                    throw new ComDriveExceptions(
                        "Unexpected error. Actual request type doesn't match the expected type",
                        ComDriveExceptions.ComDriveException.UnexpectedError);
                }
            }
        }

        private void SplitAndSendBinaryData(ReadWriteRequest binaryRequest, string parentID)
        {
            BinaryRequest binRequest = binaryRequest as BinaryRequest;

            PComB pComB = new PComB();

            int offsetInBuffer = 0;
            int chunkSize = 0;
            int outgoingBufferSize = 0;
            int currentAddress = binRequest.Address;
            int messageKey = binRequest.MessageKey;
            const int MIN_TIME_BETWEEN_CHUNKS_WRITE = 20;

            GuidClass guid;
            PlcResponseMessage plcResponseMessage;

            byte[] outgoingChunk;

            if (binRequest.OutgoingBuffer != null)
            {
                outgoingBufferSize = binRequest.OutgoingBuffer.Length;
            }

            switch (binRequest.CommandCode)
            {
                case 0x1:
                case 0x41:
                case 75:
                case 5:

                    if ((binRequest.OutgoingBuffer == null) || (outgoingBufferSize == 0))
                    {
                        binRequest.RaiseProgressEvent(0, binRequest.ElementsCount,
                            RequestProgress.en_NotificationType.SetMinMax, 0, "");
                        binRequest.RaiseProgressEvent(0, binRequest.ElementsCount,
                            RequestProgress.en_NotificationType.ProgressChanged, 0, "");

                        if (this.BreakFlag)
                            throw new ComDriveExceptions("Request aborted by user",
                                ComDriveExceptions.ComDriveException.AbortedByUser);

                        if (binRequest.WaitForIdle)
                        {
                            WaitForFlashIdle((byte) UnitId, parentID);
                        }


                        byte[] arrivedData = new byte[binRequest.ElementsCount];
                        while (offsetInBuffer < binRequest.ElementsCount)
                        {
                            int sizeOfDataToCopy;

                            if (this.BreakFlag)
                                throw new ComDriveExceptions("Request aborted by user",
                                    ComDriveExceptions.ComDriveException.AbortedByUser);
                            chunkSize = binRequest.ElementsCount - offsetInBuffer;
                            sizeOfDataToCopy = chunkSize;

                            if (chunkSize > PLCVersion.PlcBuffer - Utils.Lengths.LENGTH_HEADER_AND_FOOTER)
                                chunkSize = PLCVersion.PlcBuffer - Utils.Lengths.LENGTH_HEADER_AND_FOOTER;

                            if (binRequest.ChunkSizeAlignment != 0)
                            {
                                // Chop the end of the chunk to fit into aligment
                                chunkSize -= (chunkSize % binRequest.ChunkSizeAlignment);
                            }

                            if (binRequest.FlashBankSize != 0)
                            {
                                // Chop the end of the chunk so the burned data will not be written on 2 different flash banks
                                if (((currentAddress + chunkSize) / binRequest.FlashBankSize) !=
                                    (currentAddress / binRequest.FlashBankSize))
                                {
                                    chunkSize -= ((currentAddress + chunkSize) % binRequest.FlashBankSize);
                                }
                            }

                            if (chunkSize == 0)
                            {
                                chunkSize = binRequest.ChunkSizeAlignment;
                            }
                            else
                            {
                                sizeOfDataToCopy = chunkSize;
                            }


                            pComB.BuildBinaryCommand((byte) UnitId, messageKey, binRequest.CommandCode,
                                binRequest.SubCommand, currentAddress, chunkSize, 0, new byte[0]);

                            guid = new GuidClass();

                            lock (guid)
                            {
                                Channel.Send(pComB.MessageToPLC as byte[], ReceiveBytes, guid, parentID,
                                    "Binary Protocol - Binary Request (" + binRequest.CommandCode.ToString() + ")",
                                    PlcGuid);
                                Monitor.Wait(guid);
                            }

                            if (this.BreakFlag)
                                throw new ComDriveExceptions("Request aborted by user",
                                    ComDriveExceptions.ComDriveException.AbortedByUser);

                            lock (_lockObj)
                            {
                                plcResponseMessage = m_responseMessageQueue[guid];
                                m_responseMessageQueue.Remove(guid);
                            }

                            if (plcResponseMessage.comException == CommunicationException.Timeout)
                            {
                                throw new ComDriveExceptions(
                                    "Cannot communicate with the PLC with the specified UnitID!",
                                    ComDriveExceptions.ComDriveException.CommunicationTimeout);
                            }

                            binRequest.IncomingBuffer =
                                new byte[plcResponseMessage.responseBytesMessage.Length -
                                         Utils.Lengths.LENGTH_HEADER_AND_FOOTER];

                            if (binRequest.IncomingBuffer.Length > 0)
                            {
                                Array.Copy(plcResponseMessage.responseBytesMessage, Utils.Lengths.LENGTH_HEADER,
                                    binRequest.IncomingBuffer, 0, binRequest.IncomingBuffer.Length);
                            }

                            Array.Copy(binRequest.IncomingBuffer, 0, arrivedData, offsetInBuffer, sizeOfDataToCopy);
                            if (plcResponseMessage.responseBytesMessage[12] == 0xFF)
                            {
                                binRequest.PlcReceiveResult =
                                    (BinaryRequest.ePlcReceiveResult) plcResponseMessage.responseBytesMessage[13];
                            }
                            else if (plcResponseMessage.responseBytesMessage[12] == binRequest.CommandCode + 0x80)
                            {
                                binRequest.PlcReceiveResult = BinaryRequest.ePlcReceiveResult.Sucsess;
                            }
                            else
                            {
                                binRequest.PlcReceiveResult = BinaryRequest.ePlcReceiveResult.Unknown;
                            }

                            offsetInBuffer += sizeOfDataToCopy;
                            currentAddress += sizeOfDataToCopy;
                            messageKey++; // Message Key is % 256. The BuildBinaryCommand takes care of that
                            binRequest.MessageKey = messageKey;
                            binRequest.MessageKey = binRequest.MessageKey % 256;

                            if (binRequest.MessageKey == 0)
                            {
                                messageKey = binRequest.CycledMessageKey;
                                binRequest.MessageKey = messageKey;
                            }

                            if (binRequest.WaitForIdle)
                            {
                                WaitForFlashIdle((byte) UnitId, parentID);
                            }

                            binRequest.RaiseProgressEvent(0, binRequest.ElementsCount,
                                RequestProgress.en_NotificationType.ProgressChanged, offsetInBuffer, "");
                        }

                        binRequest.IncomingBuffer = arrivedData;
                        binRequest.RaiseProgressEvent(0, binRequest.ElementsCount,
                            RequestProgress.en_NotificationType.Completed, binRequest.ElementsCount, "");
                    }
                    else
                    {
                        if (this.BreakFlag)
                            throw new ComDriveExceptions("Request aborted by user",
                                ComDriveExceptions.ComDriveException.AbortedByUser);

                        if (binRequest.WaitForIdle)
                        {
                            WaitForFlashIdle((byte) UnitId, parentID);
                        }

                        binRequest.RaiseProgressEvent(0, outgoingBufferSize,
                            RequestProgress.en_NotificationType.SetMinMax, 0, "");
                        binRequest.RaiseProgressEvent(0, outgoingBufferSize,
                            RequestProgress.en_NotificationType.ProgressChanged, 0, "");

                        //if (binRequest.SubCommand == (int)MemoryType.InternalFlash && binRequest.CommandCode == 0x41)
                        //{
                        //    if ((binRequest.OutgoingBuffer.Length % 8) != 0)
                        //    {
                        //        byte[] binDataToBurn = new byte[binRequest.OutgoingBuffer.Length + 8 - binRequest.OutgoingBuffer.Length % 8];
                        //        for (int i = binRequest.OutgoingBuffer.Length; i < binDataToBurn.Length; i++)
                        //        {
                        //            binDataToBurn[i] = 0xFF;
                        //        }

                        //        Array.Copy(binRequest.OutgoingBuffer, 0, binDataToBurn, 0, binRequest.OutgoingBuffer.Length);
                        //        binRequest.OutgoingBuffer = binDataToBurn;
                        //        outgoingBufferSize = binRequest.OutgoingBuffer.Length;
                        //    }
                        //}

                        Stopwatch sw = new Stopwatch();
                        sw.Start();
                        long lastTime = 0;
                        while (offsetInBuffer < outgoingBufferSize)
                        {
                            if (this.BreakFlag)
                                throw new ComDriveExceptions("Request aborted by user",
                                    ComDriveExceptions.ComDriveException.AbortedByUser);

                            chunkSize = outgoingBufferSize - offsetInBuffer;
                            if (chunkSize > PLCVersion.PlcBuffer - Utils.Lengths.LENGTH_HEADER_AND_FOOTER)
                                chunkSize = PLCVersion.PlcBuffer - Utils.Lengths.LENGTH_HEADER_AND_FOOTER;

                            if (binRequest.ChunkSizeAlignment != 0)
                            {
                                // Chop the end of the chunk to fit into aligment
                                chunkSize -= (chunkSize % binRequest.ChunkSizeAlignment);
                            }

                            if (binRequest.FlashBankSize != 0)
                            {
                                // Chop the end of the chunk so the burned data will not be written on 2 different flash banks
                                if (((currentAddress + chunkSize) / binRequest.FlashBankSize) !=
                                    (currentAddress / binRequest.FlashBankSize))
                                {
                                    chunkSize -= ((currentAddress + chunkSize) % binRequest.FlashBankSize);
                                }
                            }

                            //if (binRequest.SubCommand == (int)MemoryType.InternalFlash && binRequest.CommandCode == 0x41)
                            //{
                            //    if ((chunkSize % 8) != 0)
                            //        chunkSize -= chunkSize % 8;
                            //}

                            outgoingChunk = new byte[chunkSize];
                            Array.Copy(binRequest.OutgoingBuffer, offsetInBuffer, outgoingChunk, 0, chunkSize);

                            // Programming command (0x41)
                            // We want to put it on PLC even if the chunk is full with 0xff (Because it is not a flash memory)
                            if ((binRequest.CommandCode == 0x41 && binRequest.SubCommand == (int) MemoryType.SRAM) ||
                                IsValidChunkBufferData(outgoingChunk, binRequest.DecodeValue))
                            {
                                if (sw.ElapsedMilliseconds - lastTime < MIN_TIME_BETWEEN_CHUNKS_WRITE)
                                    Thread.Sleep((int) (MIN_TIME_BETWEEN_CHUNKS_WRITE -
                                                        (sw.ElapsedMilliseconds - lastTime)));

                                pComB.BuildBinaryCommand((byte) UnitId, messageKey, binRequest.CommandCode,
                                    binRequest.SubCommand, currentAddress, chunkSize, (ushort) chunkSize,
                                    outgoingChunk);

                                lastTime = sw.ElapsedMilliseconds;
                                messageKey++; // Message Key is % 256. The BuildBinaryCommand takes care of that
                                binRequest.MessageKey = messageKey;
                                binRequest.MessageKey = binRequest.MessageKey % 256;

                                if (binRequest.MessageKey == 0)
                                {
                                    messageKey = binRequest.CycledMessageKey;
                                    binRequest.MessageKey = messageKey;
                                }

                                guid = new GuidClass();

                                lock (guid)
                                {
                                    Channel.Send(pComB.MessageToPLC as byte[], ReceiveBytes, guid, parentID,
                                        "Binary Protocol - Binary Request (" + binRequest.CommandCode.ToString() + ")",
                                        PlcGuid);
                                    Monitor.Wait(guid);
                                }

                                if (this.BreakFlag)
                                    throw new ComDriveExceptions("Request aborted by user",
                                        ComDriveExceptions.ComDriveException.AbortedByUser);

                                lock (_lockObj)
                                {
                                    plcResponseMessage = m_responseMessageQueue[guid];
                                    m_responseMessageQueue.Remove(guid);
                                }

                                if (plcResponseMessage.comException == CommunicationException.Timeout)
                                {
                                    throw new ComDriveExceptions(
                                        "Cannot communicate with the PLC with the specified UnitID!",
                                        ComDriveExceptions.ComDriveException.CommunicationTimeout);
                                }

                                binRequest.IncomingBuffer =
                                    new byte[plcResponseMessage.responseBytesMessage.Length -
                                             Utils.Lengths.LENGTH_HEADER_AND_FOOTER];
                                if (binRequest.IncomingBuffer.Length > 0)
                                {
                                    Array.Copy(plcResponseMessage.responseBytesMessage, Utils.Lengths.LENGTH_HEADER,
                                        binRequest.IncomingBuffer, 0, binRequest.IncomingBuffer.Length);
                                }

                                if (plcResponseMessage.responseBytesMessage[12] == 0xFF)
                                {
                                    binRequest.PlcReceiveResult =
                                        (BinaryRequest.ePlcReceiveResult) plcResponseMessage.responseBytesMessage[13];
                                }
                                else if (plcResponseMessage.responseBytesMessage[12] == binRequest.CommandCode + 0x80)
                                {
                                    binRequest.PlcReceiveResult = BinaryRequest.ePlcReceiveResult.Sucsess;
                                }
                                else
                                {
                                    binRequest.PlcReceiveResult = BinaryRequest.ePlcReceiveResult.Unknown;
                                }

                                if (binRequest.PlcReceiveResult != BinaryRequest.ePlcReceiveResult.Sucsess)
                                    return;

                                if (binRequest.WaitForIdle)
                                {
                                    WaitForFlashIdle((byte) UnitId, parentID);
                                }
                                else
                                {
                                    if (binRequest.CommandCode == 0x41)
                                        Thread.Sleep(25);
                                }
                            }
                            else
                            {
                                Debug.Print("Not Valid");
                            }

                            offsetInBuffer += chunkSize;
                            currentAddress += chunkSize;

                            binRequest.RaiseProgressEvent(0, outgoingBufferSize,
                                RequestProgress.en_NotificationType.ProgressChanged, offsetInBuffer, "");
                        }

                        binRequest.RaiseProgressEvent(0, outgoingBufferSize,
                            RequestProgress.en_NotificationType.Completed, outgoingBufferSize, "");
                    }

                    break;

                default:

                    binRequest.RaiseProgressEvent(0, 100, RequestProgress.en_NotificationType.SetMinMax, 0, "");
                    binRequest.RaiseProgressEvent(0, 100, RequestProgress.en_NotificationType.ProgressChanged, 0, "");

                    pComB.BuildBinaryCommand((byte) UnitId, messageKey, binRequest.CommandCode,
                        binRequest.SubCommand, currentAddress, binRequest.ElementsCount, (ushort) outgoingBufferSize,
                        binRequest.OutgoingBuffer);

                    if (this.BreakFlag)
                        throw new ComDriveExceptions("Request aborted by user",
                            ComDriveExceptions.ComDriveException.AbortedByUser);

                    if (binRequest.WaitForIdle)
                    {
                        WaitForFlashIdle((byte) UnitId, parentID);
                    }

                    guid = new GuidClass();

                    lock (guid)
                    {
                        Channel.Send(pComB.MessageToPLC as byte[], ReceiveBytes, guid, parentID,
                            "Binary Protocol - Binary Request (" + binRequest.CommandCode.ToString() + ")", PlcGuid);
                        Monitor.Wait(guid);
                    }

                    if (this.BreakFlag)
                        throw new ComDriveExceptions("Request aborted by user",
                            ComDriveExceptions.ComDriveException.AbortedByUser);

                    lock (_lockObj)
                    {
                        plcResponseMessage = m_responseMessageQueue[guid];
                        m_responseMessageQueue.Remove(guid);
                    }

                    if (plcResponseMessage.comException == CommunicationException.Timeout)
                    {
                        throw new ComDriveExceptions("Cannot communicate with the PLC with the specified UnitID!",
                            ComDriveExceptions.ComDriveException.CommunicationTimeout);
                    }

                    binRequest.IncomingBuffer = new byte[plcResponseMessage.responseBytesMessage.Length -
                                                         Utils.Lengths.LENGTH_HEADER_AND_FOOTER];
                    if (binRequest.IncomingBuffer.Length > 0)
                    {
                        Array.Copy(plcResponseMessage.responseBytesMessage, Utils.Lengths.LENGTH_HEADER,
                            binRequest.IncomingBuffer, 0, binRequest.IncomingBuffer.Length);
                    }

                    if (plcResponseMessage.responseBytesMessage[12] == 0xFF)
                    {
                        binRequest.PlcReceiveResult =
                            (BinaryRequest.ePlcReceiveResult) plcResponseMessage.responseBytesMessage[13];
                    }
                    else if (plcResponseMessage.responseBytesMessage[12] == binRequest.CommandCode + 0x80)
                    {
                        binRequest.PlcReceiveResult = BinaryRequest.ePlcReceiveResult.Sucsess;
                    }
                    else
                    {
                        binRequest.PlcReceiveResult = BinaryRequest.ePlcReceiveResult.Unknown;
                    }

                    if (binRequest.WaitForIdle)
                    {
                        WaitForFlashIdle((byte) UnitId, parentID);
                    }

                    binRequest.RaiseProgressEvent(0, 100, RequestProgress.en_NotificationType.Completed, 100, "");

                    break;
            }
        }

        #region Flash

        private void WaitForFlashIdle(byte unitID, string parentID)
        {
            int idleCount = 0;
            const int READ_STATUS_COMMAND = 7;
            const int TOTAL_TIME_OUT = 300;
            byte[] incomingBuffer;
            FlashStatus flashStatus;
            DateTime dateTime = DateTime.Now;
            PComB pComB = new PComB();
            pComB.BuildBinaryCommand(unitID, 0, READ_STATUS_COMMAND,
                0, 0, 106, 0, new byte[0]);

            if (this.BreakFlag)
                throw new ComDriveExceptions("Request aborted by user",
                    ComDriveExceptions.ComDriveException.AbortedByUser);

            while ((DateTime.Now - dateTime).Seconds <= TOTAL_TIME_OUT)
            {
                GuidClass guid = new GuidClass();

                lock (guid)
                {
                    Channel.Send(pComB.MessageToPLC as byte[], ReceiveBytes, guid, parentID,
                        "Binary Protocol - Binary Request (" + READ_STATUS_COMMAND + ")", PlcGuid);
                    Monitor.Wait(guid);
                }

                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                PlcResponseMessage plcResponseMessage;

                lock (_lockObj)
                {
                    plcResponseMessage = m_responseMessageQueue[guid];
                    m_responseMessageQueue.Remove(guid);
                }

                if (plcResponseMessage.comException == CommunicationException.Timeout)
                {
                    throw new ComDriveExceptions("Cannot communicate with the PLC with the specified UnitID!",
                        ComDriveExceptions.ComDriveException.CommunicationTimeout);
                }

                incomingBuffer = new byte[plcResponseMessage.responseBytesMessage.Length -
                                          Utils.Lengths.LENGTH_HEADER_AND_FOOTER];
                if (incomingBuffer.Length > 0)
                {
                    Array.Copy(plcResponseMessage.responseBytesMessage, Utils.Lengths.LENGTH_HEADER, incomingBuffer, 0,
                        incomingBuffer.Length);
                }

                flashStatus = GetFlashStatus(incomingBuffer);

                if ((flashStatus.MemoryStatus != (MemoryStatus) 'I') || (flashStatus.eFlashStatus != eFlashStatus.Idle))
                {
                    Thread.Sleep(20);
                }
                else
                {
                    idleCount++;
                    if (idleCount > 1)
                    {
                        return;
                    }
                }
            }
        }

        private FlashStatus GetFlashStatus(byte[] receivedBytes)
        {
            FlashStatus flashStatus = new FlashStatus();
            if ((receivedBytes[0] == (byte) 'R') || (receivedBytes[0] == (byte) 'I'))
            {
                flashStatus.FlashRunStop = FlashStatusRunStop.Run;
            }
            else
            {
                flashStatus.FlashRunStop = FlashStatusRunStop.Stop;
            }

            flashStatus.eFlashStatus = (eFlashStatus) receivedBytes[1];
            flashStatus.MemoryStatus = (MemoryStatus) receivedBytes[2];
            flashStatus.CompilerStatus = (CompilerStatus) receivedBytes[3];
            flashStatus.CurrentAddress = BitConverter.ToInt32(receivedBytes, 4);
            flashStatus.SectorNumber = receivedBytes[8];
            flashStatus.DownloadEnded = (receivedBytes[9] != 0);
            flashStatus.SectorSize = BitConverter.ToInt32(receivedBytes, 10);

            if ((int) flashStatus.CompilerStatus >= 3)
            {
                flashStatus.CompilerError = (CompilerError) flashStatus.CompilerStatus;
                flashStatus.CompilerStatus = CompilerStatus.Error;
            }
            else
            {
                flashStatus.CompilerError = CompilerError.NoError;
            }

            flashStatus.ValidBitmap = new byte[4];
            Array.Copy(receivedBytes, 90, flashStatus.ValidBitmap, 0, 4);

            flashStatus.AllFlashValid = (receivedBytes[28] == 3);
            return flashStatus;
        }

        private bool IsValidChunkBufferData(byte[] chunk, int decodeValue)
        {
            short[] skipValues = new short[2];
            Buffer.BlockCopy(new int[] {decodeValue}, 0, skipValues, 0, 4);
            short skipValue = (short) (skipValues[0] ^ skipValues[1] ^ 0xFFFF);
            short[] bufferToScan;
            if ((chunk.Length % 2) != 0)
            {
                bufferToScan = new short[(chunk.Length + 1) / 2];
                Buffer.BlockCopy(chunk, 0, bufferToScan, 0, chunk.Length);
            }
            else
            {
                bufferToScan = new short[chunk.Length / 2];
                Buffer.BlockCopy(chunk, 0, bufferToScan, 0, chunk.Length);
            }

            for (int i = 0; i < bufferToScan.Length; i++)
            {
                if (bufferToScan[i] != skipValue)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        private void ReceiveBytes(byte[] responseBytes, CommunicationException communicationException,
            GuidClass messageGuid)
        {
            PlcResponseMessage plcResponseMessage = new PlcResponseMessage
            {
                comException = communicationException,
                responseBytesMessage = responseBytes
            };

            lock (_lockObj)
            {
                m_responseMessageQueue.Add(messageGuid, plcResponseMessage);
            }

            lock (messageGuid)
            {
                Monitor.PulseAll(messageGuid);
            }
        }

        private void ReceiveString(string responseMessage, CommunicationException communicationException,
            GuidClass messageGuid)
        {
            PlcResponseMessage plcResponseMessage = new PlcResponseMessage
            {
                comException = communicationException,
                responseStringMessage = responseMessage
            };

            lock (_lockObj)
            {
                m_responseMessageQueue.Add(messageGuid, plcResponseMessage);
            }

            lock (messageGuid)
            {
                Monitor.PulseAll(messageGuid);
            }
        }
    }
}