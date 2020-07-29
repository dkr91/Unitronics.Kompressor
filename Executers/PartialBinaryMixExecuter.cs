using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver.Command;
using Unitronics.ComDriver.Messages.ASCIIMessage;
using Unitronics.ComDriver.Messages;
using Unitronics.ComDriver.Messages.BinMessage;
using Unitronics.ComDriver.Messages.DataRequest;
using System.Threading;

namespace Unitronics.ComDriver.Executers
{
    class PartialBinaryMixExecuter : Executer
    {
        #region Locals

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

        public PartialBinaryMixExecuter(int unitId, Channel channel, PlcVersion plcVersion, Guid plcGuid)
            : base(unitId, channel, plcVersion, plcGuid)
        {
        }

        #endregion

        #region Public

        internal override void PerformReadWrite(ref ReadWriteRequest[] values, string parentID,
            bool suppressEthernetHeader)
        {
            List<ReadWriteRequest> joinRequests = new List<ReadWriteRequest>();
            int joinRequestsSize = Utils.Lengths.LENGTH_HEADER_AND_FOOTER;
            int joinSentDataSize = Utils.Lengths.LENGTH_HEADER_AND_FOOTER;
            int iterRequestSize = 0;
            int iterSentDataSize = 6; // 4 bytes on Address, Operand type etc + 2 bytes on address

            for (int iter = 0; iter < values.Length; iter++)
            {
                if (this.BreakFlag)
                {
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);
                }

                if (values[iter] is ReadOperands)
                {
                    #region ReadOperands

                    iterRequestSize = GetReadOperandReceiveSize(values[iter]);

                    //Test if precedent request is also ReadOperands.
                    if (iter > 0 && (values[iter - 1] is ReadOperands))
                    {
                        //Test if we can add current request to the joinRequestsList
                        if ((joinRequestsSize + iterRequestSize <= PLCVersion.PlcBuffer) &&
                            (joinSentDataSize + iterSentDataSize <= PLCVersion.PlcBuffer))
                        {
                            if (this.BreakFlag)
                            {
                                throw new ComDriveExceptions("Request aborted by user",
                                    ComDriveExceptions.ComDriveException.AbortedByUser);
                            }

                            joinRequests.Add(values[iter]);
                            joinRequestsSize += iterRequestSize;
                            joinSentDataSize += iterSentDataSize;
                        }
                        else
                        {
                            //Read joinRequests, clear joinRequests and add curernt request to joinRequest
                            if ((joinRequestsSize <= PLCVersion.PlcBuffer) &&
                                (joinSentDataSize <= PLCVersion.PlcBuffer))
                            {
                                if (joinRequests.Count > 0)
                                {
                                    if (this.BreakFlag)
                                    {
                                        throw new ComDriveExceptions("Request aborted by user",
                                            ComDriveExceptions.ComDriveException.AbortedByUser);
                                    }

                                    ReadOperations(ref joinRequests, parentID);
                                    joinRequests.Clear();
                                    joinRequestsSize = Utils.Lengths.LENGTH_HEADER_AND_FOOTER;
                                    joinSentDataSize = Utils.Lengths.LENGTH_HEADER_AND_FOOTER;
                                }

                                joinRequests.Add(values[iter]);
                                joinRequestsSize += iterRequestSize;
                                joinSentDataSize += iterSentDataSize;
                            }
                            else
                            {
                                //The joinRequest contains only 1 ReadOperands that exceed the plcBuffer
                                if (this.BreakFlag)
                                {
                                    throw new ComDriveExceptions("Request aborted by user",
                                        ComDriveExceptions.ComDriveException.AbortedByUser);
                                    ;
                                }

                                SplitAndReadReadOperands(ref joinRequests, parentID);
                                joinRequests.Clear();
                                joinRequestsSize = Utils.Lengths.LENGTH_HEADER_AND_FOOTER;
                                joinSentDataSize = Utils.Lengths.LENGTH_HEADER_AND_FOOTER;

                                joinRequests.Add(values[iter]);
                                joinRequestsSize += iterRequestSize;
                                joinSentDataSize += iterSentDataSize;
                            }
                        }
                    }
                    else
                    {
                        //Previous request doesn't exist or is not ReadOperands
                        //joinRequests is empty
                        if (this.BreakFlag)
                        {
                            throw new ComDriveExceptions("Request aborted by user",
                                ComDriveExceptions.ComDriveException.AbortedByUser);
                        }

                        joinRequests.Add(values[iter]);
                        joinRequestsSize += iterRequestSize;
                        joinSentDataSize += iterSentDataSize;
                        if ((joinRequestsSize > PLCVersion.PlcBuffer) || (joinSentDataSize > PLCVersion.PlcBuffer))
                        {
                            SplitAndReadReadOperands(ref joinRequests, parentID);
                            joinRequests.Clear();
                            joinRequestsSize = Utils.Lengths.LENGTH_HEADER_AND_FOOTER;
                            joinSentDataSize = Utils.Lengths.LENGTH_HEADER_AND_FOOTER;
                        }
                    }

                    //If this is the last request from user requests then 
                    //read the JoinRequests(if need)

                    if (iter == values.Length - 1 && joinRequests.Count > 0)
                    {
                        SplitAndReadReadOperands(ref joinRequests, parentID);
                    }

                    #endregion //ReadOperands
                }
                else //current request  is not ReadOperands)
                {
                    if (joinRequests.Count > 0)
                    {
                        SplitAndReadReadOperands(ref joinRequests, parentID);
                        joinRequests.Clear();
                    }

                    if (values[iter] is WriteOperands)
                    {
                        if (sentMessageSizeFitsThePLCBuffer(values[iter]))
                        {
                            if (this.BreakFlag)
                            {
                                throw new ComDriveExceptions("Request aborted by user",
                                    ComDriveExceptions.ComDriveException.AbortedByUser);
                            }

                            joinRequests.Add(values[iter]);
                            WriteOperations(ref joinRequests, parentID, suppressEthernetHeader);
                            joinRequests.Clear();
                        }
                        else
                        {
                            List<WriteOperands> splitedWriteOperands = GetSplitedWriteOperands(values[iter]);

                            for (int i = 0; i < splitedWriteOperands.Count; i++)
                            {
                                if (this.BreakFlag)
                                {
                                    throw new ComDriveExceptions("Request aborted by user",
                                        ComDriveExceptions.ComDriveException.AbortedByUser);
                                }

                                joinRequests.Add(splitedWriteOperands[i]);
                                WriteOperations(ref joinRequests, parentID, suppressEthernetHeader);
                                joinRequests.Clear();
                            }

                            values[iter].ResponseValues = String.Empty;
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
        }

        #endregion

        #region Private

        #region Split and Join

        private bool sentMessageSizeFitsThePLCBuffer(ReadWriteRequest operand)
        {
            WriteOperands writeOperands = operand as WriteOperands;

            int operandSize = Utils.GetOperandSizeByCommandCode(writeOperands.OperandType.ToString());
            int sentDataSize = writeOperands.NumberOfOperands * operandSize;
            int sentMessageSize = 0;
            sentMessageSize = Utils.Lengths.LENGTH_ASCII_SEND_MESSAGE + sentDataSize + Utils.Lengths.LENGTH_LENGTH +
                              Utils.Lengths.LENGTH_ADDRESS;
            return sentMessageSize <= PLCVersion.PlcBuffer && writeOperands.NumberOfOperands <= 255;
        }

        private List<WriteOperands> GetSplitedWriteOperands(ReadWriteRequest operand)
        {
            WriteOperands writeOperands = operand as WriteOperands;

            List<WriteOperands> results = new List<WriteOperands>();
            int operandSize = Utils.GetOperandSizeByCommandCode(writeOperands.OperandType.ToString());
            ushort startAddress = writeOperands.StartAddress;
            ushort remainingNoOfOperands = writeOperands.NumberOfOperands;
            int splitMaxDataSize = PLCVersion.PlcBuffer - Utils.Lengths.LENGTH_ASCII_SEND_MESSAGE -
                                   Utils.Lengths.LENGTH_ADDRESS;
            ushort splitMaxNoOfOperands = (ushort) (splitMaxDataSize / operandSize);
            object[] valuesToWrite = (operand as WriteOperands).Values;
            int sourceIndex = 0;
            int destinationIndex = 0;

            if (splitMaxNoOfOperands > 255)
                splitMaxNoOfOperands = 255;

            bool bDone = false;

            while (!bDone)
            {
                if (remainingNoOfOperands <= splitMaxNoOfOperands)
                {
                    object[] splitValues = new object[remainingNoOfOperands];
                    Array.Copy(valuesToWrite, sourceIndex, splitValues, destinationIndex, remainingNoOfOperands);
                    results.Add(new WriteOperands(remainingNoOfOperands, writeOperands.OperandType, startAddress,
                        splitValues, writeOperands.TimerValueFormat));
                    bDone = true;
                }
                else
                {
                    object[] splitValues = new object[splitMaxNoOfOperands];
                    Array.Copy(valuesToWrite, sourceIndex, splitValues, destinationIndex, splitMaxNoOfOperands);
                    results.Add(new WriteOperands(splitMaxNoOfOperands, writeOperands.OperandType, startAddress,
                        splitValues.ToArray(), writeOperands.TimerValueFormat));
                    startAddress += splitMaxNoOfOperands;
                    remainingNoOfOperands -= splitMaxNoOfOperands;
                    sourceIndex += splitMaxNoOfOperands;
                }
            }

            return results;
        }

        private void SplitAndReadReadOperands(ref List<ReadWriteRequest> readWriteRequest, string parentID)
        {
            if (this.BreakFlag)
                throw new ComDriveExceptions("Request aborted by user",
                    ComDriveExceptions.ComDriveException.AbortedByUser);

            if (readWriteRequest.Count == 1) //only 1 request that exceed the plcBuffer
            {
                ReadOperands readOperands = readWriteRequest[0] as ReadOperands;
                List<ReadWriteRequest> splitRequests = new List<ReadWriteRequest>();
                List<object> responseValues = new List<object>();

                int operandSize = readOperands.OperandType.GetOperandSizeByOperandTypeForFullBinarry();

                ushort maxNoOfOperands =
                    (ushort) ((PLCVersion.PlcBuffer - Utils.Lengths.LENGTH_HEADER_AND_FOOTER) / operandSize);
                ushort startAddress = (readWriteRequest[0] as ReadOperands).StartAddress;

                ushort remainingNoOfOperands = (ushort) (readOperands.NumberOfOperands);
                ushort remainingStartAddress = startAddress;

                while (remainingNoOfOperands > maxNoOfOperands)
                {
                    if (this.BreakFlag)
                        throw new ComDriveExceptions("Request aborted by user",
                            ComDriveExceptions.ComDriveException.AbortedByUser);

                    splitRequests.Add(new ReadOperands(maxNoOfOperands, readOperands.OperandType, remainingStartAddress,
                        readOperands.TimerValueFormat));

                    ReadOperations(ref splitRequests, parentID);

                    if (this.BreakFlag)
                        throw new ComDriveExceptions("Request aborted by user",
                            ComDriveExceptions.ComDriveException.AbortedByUser);

                    responseValues.AddRange((splitRequests[0].ResponseValues) as object[]);

                    splitRequests.Clear();
                    remainingNoOfOperands -= maxNoOfOperands;
                    remainingStartAddress += maxNoOfOperands;
                }

                if (remainingNoOfOperands > 0)
                {
                    if (this.BreakFlag)
                        throw new ComDriveExceptions("Request aborted by user",
                            ComDriveExceptions.ComDriveException.AbortedByUser);

                    splitRequests.Add(new ReadOperands(remainingNoOfOperands, readOperands.OperandType,
                        remainingStartAddress, readOperands.TimerValueFormat));

                    ReadOperations(ref splitRequests, parentID);

                    responseValues.AddRange((splitRequests[0].ResponseValues) as object[]);
                }

                readWriteRequest[0].ResponseValues = responseValues.ToArray();
            }
            else
            {
                //this is called only at the end of userRequests and when
                //joinRequests contains more than 1 requests

                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                ReadOperations(ref readWriteRequest, parentID);
            }
        }

        private int GetReadOperandReceiveSize(ReadWriteRequest readWriteRequest)
        {
            ReadOperands readOperands = readWriteRequest as ReadOperands;
            int operandSize = readOperands.OperandType.GetOperandSizeByOperandTypeForFullBinarry();
            int result = 0;

            if (operandSize == 1)
            {
                result = (readOperands.NumberOfOperands / 8);
                if (readOperands.NumberOfOperands % 8 != 0)
                    result++;
            }
            else
            {
                result = operandSize * readOperands.NumberOfOperands;
            }

            return result;
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

        private void WriteOperations(ref List<ReadWriteRequest> values, string parentID, bool suppressEthernetHeader)
        {
            PComA pcomA = new PComA();

            if (this.BreakFlag)
                throw new ComDriveExceptions("Request aborted by user",
                    ComDriveExceptions.ComDriveException.AbortedByUser);

            foreach (WriteOperands wo in values)
            {
                pcomA.BuildAsciiCommand(UnitId,
                    Utils.ASCIIOperandTypes[Enum.GetName(typeof(OperandTypes), wo.OperandType)].CommandCodeForWrite,
                    (wo as WriteOperands).StartAddress, wo.NumberOfOperands, (wo as WriteOperands).Values,
                    (wo as WriteOperands).TimerValueFormat);
                string readMessage = pcomA.MessageToPLC as string;

                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                GuidClass guid = new GuidClass();

                lock (guid)
                {
                    Channel.Send(readMessage, ReceiveString, guid, parentID, "ASCII Protocol - Write Operands", PlcGuid,
                        suppressEthernetHeader);
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

                pcomA.DisAssembleAsciiResult(plcResponseMessage.responseStringMessage, UnitId,
                    Utils.ASCIIOperandTypes[Enum.GetName(typeof(OperandTypes), wo.OperandType)].CommandCodeForWrite,
                    wo.StartAddress, wo.NumberOfOperands, null, wo.TimerValueFormat);
            }
        }

        private byte GetOperandSize(ReadWriteRequest dr)
        {
            if (dr is ReadOperands)
                return (dr as ReadOperands).OperandType.ToString().GetOperandIdByName().GetOperandSizeByValue();
            else
                return (dr as WriteOperands).OperandType.ToString().GetOperandIdByName().GetOperandSizeByValue();
        }

        private void ReadOperations(ref List<ReadWriteRequest> pdataRequestsForRead, string parentID)
        {
            List<ReadWriteRequest> dataRequestsForRead = null;
            dataRequestsForRead = pdataRequestsForRead.OrderBy(dr => GetOperandSize(dr)).ToList();

            List<List<byte>> dataRequestBytes = new List<List<byte>>();

            for (int i = 0; i < dataRequestsForRead.Count; i++)
            {
                if (dataRequestsForRead[i] is ReadOperands)
                    dataRequestBytes.Add(GetDataRequestBytes(dataRequestsForRead[i] as ReadOperands));
            }

            //set number of read requests in position 4 and 5 of command_details which in header will be 18 and 19
            byte[] command_detailes = new byte[6];
            byte[] count = BitConverter.GetBytes(dataRequestsForRead.Count);
            command_detailes[4] = count[0];
            command_detailes[5] = count[1];

            PComB pComB = new PComB();
            pComB.BuildBinaryCommand((byte) UnitId, BinaryCommand.ReadOperands, command_detailes,
                dataRequestsForRead, dataRequestBytes);

            if (this.BreakFlag)
                throw new ComDriveExceptions("Request aborted by user",
                    ComDriveExceptions.ComDriveException.AbortedByUser);

            GuidClass guid = new GuidClass();

            lock (guid)
            {
                Channel.Send(pComB.MessageToPLC as byte[], ReceiveBytes, guid, parentID,
                    "Binary Protocol - Read Operands (77)", PlcGuid);
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

            pComB.DisAssembleBinaryResult(plcResponseMessage.responseBytesMessage, dataRequestBytes);
        }

        private List<byte> GetDataRequestBytes(ReadOperands readOperandsExecuter)
        {
            bool isVectorial = true;
            List<byte> results = new List<byte>();

            results.AddRange(BitConverter.GetBytes(readOperandsExecuter.NumberOfOperands));
            OperandType operandType = Enum.GetName(typeof(OperandTypes), readOperandsExecuter.OperandType)
                .GetOperandTypeByName();
            results.Add(isVectorial ? operandType.VectorialValue : operandType.ByteValue);
            results.Add(255);

            results.AddRange(BitConverter.GetBytes(readOperandsExecuter.StartAddress));

            return results;
        }

        #endregion
    }
}