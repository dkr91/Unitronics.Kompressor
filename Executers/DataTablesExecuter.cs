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
    class DataTablesExecuter : Executer
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

        public DataTablesExecuter(int unitId, Channel channel, PlcVersion plcVersion, Guid plcGuid)
            : base(unitId, channel, plcVersion, plcGuid)
        {
        }

        #endregion

        internal override void PerformReadWrite(ref ReadWriteRequest[] values, string parentID,
            bool suppressEthernetHeader)
        {
            List<ReadWriteRequest> joinRequests = new List<ReadWriteRequest>();

            for (int iter = 0; iter < values.Length; iter++)
            {
                //current request is ReadDataTables
                if (values[iter] is ReadDataTables)
                {
                    ReadDataTables rdt = values[iter] as ReadDataTables;

                    joinRequests.Add(values[iter]);

                    //If the ReadDataTable fits the PLCBuffer Size then we just read
                    if (ReadDataTableFitsThePlcBuffer(values[iter]))
                    {
                        ReadWriteDataTable(ref joinRequests, parentID);
                    }
                    else
                    {
                        //Make the split and read
                        SplitAndReadDataTable(ref joinRequests, parentID);
                    }

                    joinRequests.Clear();
                }
                else if (values[iter] is WriteDataTables)
                {
                    joinRequests.Add(values[iter]);

                    //If the WriteDataTable fits the PLCBuffer Size then we just write
                    if (WriteDataTableFitsThePlcBuffer(values[iter]))
                    {
                        ReadWriteDataTable(ref joinRequests, parentID);
                    }
                    else
                    {
                        //Make the split and Write
                        SplitAndWriteDataTable(ref joinRequests, parentID);
                    }

                    joinRequests.Clear();
                }
                else
                {
                    throw new ComDriveExceptions(
                        "Unexpected error. Actual request type doesn't match the expected type",
                        ComDriveExceptions.ComDriveException.UnexpectedError);
                }
            }
        }

        private bool WriteDataTableFitsThePlcBuffer(ReadWriteRequest readWriteRequest)
        {
            WriteDataTables wdt = readWriteRequest as WriteDataTables;
            int wdtRequestSize = Utils.Lengths.LENGTH_HEADER_AND_FOOTER +
                                 Utils.Lengths.LENGTH_WRITE_DATA_TABLE_DETAILS +
                                 wdt.NumberOfBytesToWriteInRow * wdt.NumberOfRowsToWrite;

            return wdtRequestSize <= PLCVersion.PlcBuffer;
        }

        private void SplitAndReadDataTable(ref List<ReadWriteRequest> joinRequests, string parentID)
        {
            ReadDataTables rdt = joinRequests[0] as ReadDataTables;
            ushort maximumAvailableBytesNo = (ushort) (PLCVersion.PlcBuffer - Utils.Lengths.LENGTH_HEADER_AND_FOOTER);

            //Test if the no of bytes in one requested Row doesn't exceed the plcBuffer
            if (rdt.NumberOfBytesToReadInRow > maximumAvailableBytesNo)
            {
                SplitAndReadDataTableRowSizeExceedPLCBuffer(ref joinRequests, maximumAvailableBytesNo, parentID);
            }
            else //We can read atleast 1 complete requested row
            {
                SplitAndReadDataTableRows(ref joinRequests, maximumAvailableBytesNo, parentID);
            }
        }

        private void SplitAndReadDataTableRows(ref List<ReadWriteRequest> joinRequests, ushort maximumAvailableBytesNo,
            string parentID)
        {
            ReadDataTables rdt = joinRequests[0] as ReadDataTables;
            List<object> responseValues = new List<object>();
            List<ReadWriteRequest> splitDTList = new List<ReadWriteRequest>();

            ushort availableRowsNo = (ushort) (maximumAvailableBytesNo / rdt.NumberOfBytesToReadInRow);
            int remainingRowsNo = rdt.NumberOfRowsToRead;
            uint startAddress = rdt.StartAddress;

            rdt.RaiseProgressEvent(0, rdt.NumberOfRowsToRead * rdt.NumberOfBytesToReadInRow,
                RequestProgress.en_NotificationType.SetMinMax, 0, "");
            rdt.RaiseProgressEvent(0, rdt.NumberOfRowsToRead * rdt.NumberOfBytesToReadInRow,
                RequestProgress.en_NotificationType.ProgressChanged, 0, "");

            while (remainingRowsNo > 0)
            {
                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                ReadDataTables tmpRDT = new ReadDataTables(startAddress, rdt.NumberOfBytesToReadInRow, availableRowsNo,
                    (ushort) (rdt.RowSizeInBytes), rdt.PartOfProject, rdt.SubCommand);
                splitDTList.Add(tmpRDT);

                ReadWriteDataTable(ref splitDTList, parentID);
                responseValues.AddRange(tmpRDT.ResponseValues as object[]);
                splitDTList.Clear();

                startAddress += (uint) (availableRowsNo * rdt.RowSizeInBytes);
                remainingRowsNo -= availableRowsNo;

                if (remainingRowsNo < availableRowsNo && remainingRowsNo > 0)
                    availableRowsNo = (ushort) remainingRowsNo;

                rdt.RaiseProgressEvent(0, rdt.NumberOfRowsToRead * rdt.NumberOfBytesToReadInRow,
                    RequestProgress.en_NotificationType.ProgressChanged,
                    (rdt.NumberOfRowsToRead - remainingRowsNo) * rdt.NumberOfBytesToReadInRow, "");
            }

            rdt.RaiseProgressEvent(0, rdt.NumberOfRowsToRead * rdt.NumberOfBytesToReadInRow,
                RequestProgress.en_NotificationType.ProgressChanged,
                rdt.NumberOfRowsToRead * rdt.NumberOfBytesToReadInRow, "");

            if (responseValues != null)
                joinRequests[0].ResponseValues = responseValues.ToArray();
        }

        private void SplitAndReadDataTableRowSizeExceedPLCBuffer(ref List<ReadWriteRequest> joinRequests,
            ushort maximumAvailableBytesNo, string parentID)
        {
            ReadDataTables rdt = joinRequests[0] as ReadDataTables;
            List<ReadWriteRequest> splitDTList = new List<ReadWriteRequest>();
            List<object> responseValues = new List<object>();
            ushort remainingRowsNo = rdt.NumberOfRowsToRead;

            ushort maxAvailableReadRowBytesNo = maximumAvailableBytesNo;
            ushort remainingReadRowByteNo = (ushort) rdt.NumberOfBytesToReadInRow;
            uint startAddress = rdt.StartAddress;

            rdt.RaiseProgressEvent(0, rdt.NumberOfRowsToRead * rdt.NumberOfBytesToReadInRow,
                RequestProgress.en_NotificationType.SetMinMax, 0, "");
            rdt.RaiseProgressEvent(0, rdt.NumberOfRowsToRead * rdt.NumberOfBytesToReadInRow,
                RequestProgress.en_NotificationType.ProgressChanged, 0, "");

            while (remainingRowsNo > 0)
            {
                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                while (remainingReadRowByteNo > 0)
                {
                    if (this.BreakFlag)
                        throw new ComDriveExceptions("Request aborted by user",
                            ComDriveExceptions.ComDriveException.AbortedByUser);

                    ReadDataTables tmpRDT = new ReadDataTables(startAddress, maxAvailableReadRowBytesNo, 1,
                        (ushort) rdt.RowSizeInBytes, rdt.PartOfProject, rdt.SubCommand);
                    splitDTList.Add(tmpRDT);

                    ReadWriteDataTable(ref splitDTList, parentID);

                    if (this.BreakFlag)
                        throw new ComDriveExceptions("Request aborted by user",
                            ComDriveExceptions.ComDriveException.AbortedByUser);

                    responseValues.AddRange(tmpRDT.ResponseValues as object[]);
                    splitDTList.Clear();

                    startAddress += maxAvailableReadRowBytesNo;
                    remainingReadRowByteNo -= maxAvailableReadRowBytesNo;

                    if (remainingReadRowByteNo < maxAvailableReadRowBytesNo && remainingReadRowByteNo > 0)
                        maxAvailableReadRowBytesNo = remainingReadRowByteNo;
                }

                remainingRowsNo--;
                maxAvailableReadRowBytesNo = maximumAvailableBytesNo;
                remainingReadRowByteNo = (ushort) rdt.NumberOfBytesToReadInRow;
                startAddress =
                    (uint) (rdt.StartAddress + (rdt.NumberOfRowsToRead - remainingRowsNo) * rdt.RowSizeInBytes);

                rdt.RaiseProgressEvent(0, rdt.NumberOfRowsToRead * rdt.NumberOfBytesToReadInRow,
                    RequestProgress.en_NotificationType.ProgressChanged,
                    (rdt.NumberOfRowsToRead - remainingRowsNo) * rdt.NumberOfBytesToReadInRow, "");
            }

            rdt.RaiseProgressEvent(0, rdt.NumberOfRowsToRead * rdt.NumberOfBytesToReadInRow,
                RequestProgress.en_NotificationType.Completed, rdt.NumberOfRowsToRead * rdt.NumberOfBytesToReadInRow,
                "");

            if (responseValues != null)
                joinRequests[0].ResponseValues = responseValues.ToArray();
        }

        private bool ReadDataTableFitsThePlcBuffer(ReadWriteRequest readWriteRequest)
        {
            ReadDataTables rdt = readWriteRequest as ReadDataTables;
            int requestSize = rdt.NumberOfRowsToRead * rdt.NumberOfBytesToReadInRow;

            return requestSize <= PLCVersion.PlcBuffer - Utils.Lengths.LENGTH_HEADER_AND_FOOTER;
        }

        private void ReadWriteDataTable(ref List<ReadWriteRequest> dataTableDRs, string parentID)
        {
            PComB pComB = new PComB();
            BinaryCommand binaryCommand;
            ReadDataTables rdt = null;
            WriteDataTables wdt = null;

            if (this.BreakFlag)
                throw new ComDriveExceptions("Request aborted by user",
                    ComDriveExceptions.ComDriveException.AbortedByUser);

            foreach (ReadWriteRequest rwr in dataTableDRs)
            {
                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                if (rwr is ReadDataTables)
                {
                    binaryCommand = BinaryCommand.ReadDataTables;
                    rdt = rwr as ReadDataTables;

                    rdt.RaiseProgressEvent(0, rdt.NumberOfBytesToReadInRow * rdt.NumberOfRowsToRead,
                        RequestProgress.en_NotificationType.SetMinMax, 0, "");
                    rdt.RaiseProgressEvent(0, rdt.NumberOfBytesToReadInRow * rdt.NumberOfRowsToRead,
                        RequestProgress.en_NotificationType.ProgressChanged, 0, "");

                    if (rdt.PartOfProject)
                        binaryCommand = BinaryCommand.ReadPartOfProjectDataTables;

                    List<List<byte>> readDataTablesDataRequestBytes = new List<List<byte>>();
                    readDataTablesDataRequestBytes.Add(GetDataRequestBytesForReadDataTables(rdt));

                    if (rdt != null)
                    {
                        byte[] cmdDetails = new byte[6];
                        Array.Copy(BitConverter.GetBytes(rdt.StartAddress), cmdDetails, 4);

                        pComB.BuildBinaryCommand((byte) UnitId, binaryCommand, cmdDetails, dataTableDRs,
                            readDataTablesDataRequestBytes, (byte) rdt.SubCommand);

                        BinaryMessage receivedMessage =
                            ReceiveMessage(pComB, readDataTablesDataRequestBytes, parentID) as BinaryMessage;
                    }

                    rdt.RaiseProgressEvent(0, rdt.NumberOfBytesToReadInRow * rdt.NumberOfRowsToRead,
                        RequestProgress.en_NotificationType.Completed,
                        rdt.NumberOfBytesToReadInRow * rdt.NumberOfRowsToRead, "");
                }

                if (rwr is WriteDataTables)
                {
                    binaryCommand = BinaryCommand.WriteDataTables;
                    wdt = rwr as WriteDataTables;

                    wdt.RaiseProgressEvent(0, wdt.NumberOfBytesToWriteInRow * wdt.NumberOfRowsToWrite,
                        RequestProgress.en_NotificationType.SetMinMax, 0, "");
                    wdt.RaiseProgressEvent(0, wdt.NumberOfBytesToWriteInRow * wdt.NumberOfRowsToWrite,
                        RequestProgress.en_NotificationType.ProgressChanged, 0, "");

                    List<List<byte>> writeDataTablesDataRequestBytes = new List<List<byte>>();
                    writeDataTablesDataRequestBytes.Add(GetDataRequestBytesForWriteDataTables(wdt));

                    if (wdt != null)
                    {
                        byte[] cmdDetails = new byte[6];
                        Array.Copy(BitConverter.GetBytes(wdt.StartAddress), cmdDetails, 4);

                        pComB.BuildBinaryCommand((byte) UnitId, binaryCommand, cmdDetails, dataTableDRs,
                            writeDataTablesDataRequestBytes, (byte) wdt.SubCommand);

                        BinaryMessage receivedMessage = ReceiveMessage(pComB, null, parentID) as BinaryMessage;
                    }

                    wdt.RaiseProgressEvent(0, wdt.NumberOfBytesToWriteInRow * wdt.NumberOfRowsToWrite,
                        RequestProgress.en_NotificationType.ProgressChanged,
                        wdt.NumberOfBytesToWriteInRow * wdt.NumberOfRowsToWrite, "");
                }
            }
        }

        private void SplitAndWriteDataTable(ref List<ReadWriteRequest> joinRequests, string parentID)
        {
            WriteDataTables wdt = joinRequests[0] as WriteDataTables;
            ushort maximumAvailableBytesNo = (ushort) (PLCVersion.PlcBuffer - Utils.Lengths.LENGTH_HEADER_AND_FOOTER -
                                                       Utils.Lengths.LENGTH_WRITE_DATA_TABLE_DETAILS);

            //Test if the no of bytes in one write Row doesn't exceed the plcBuffer
            if (wdt.NumberOfBytesToWriteInRow > maximumAvailableBytesNo)
            {
                SplitAndWriteDataTableRowSizeExceedPLCBuffer(ref joinRequests, maximumAvailableBytesNo, parentID);
            }
            else //We can write atleast 1 complete row
            {
                SplitAndWriteDataTableRows(ref joinRequests, maximumAvailableBytesNo, parentID);
            }
        }

        private void SplitAndWriteDataTableRows(ref List<ReadWriteRequest> joinRequests, ushort maximumAvailableBytesNo,
            string parentID)
        {
            WriteDataTables wdt = joinRequests[0] as WriteDataTables;

            List<ReadWriteRequest> splitWDTList = new List<ReadWriteRequest>();
            int sourceIndex = 0;
            int length = 0;

            ushort availableRowsNo = (ushort) (maximumAvailableBytesNo / wdt.NumberOfBytesToWriteInRow);
            int remainingRowsNo = wdt.NumberOfRowsToWrite;
            uint startAddress = wdt.StartAddress;

            wdt.RaiseProgressEvent(0, wdt.NumberOfRowsToWrite * wdt.NumberOfBytesToWriteInRow,
                RequestProgress.en_NotificationType.SetMinMax, 0, "");
            wdt.RaiseProgressEvent(0, wdt.NumberOfRowsToWrite * wdt.NumberOfBytesToWriteInRow,
                RequestProgress.en_NotificationType.ProgressChanged, 0, "");

            while (remainingRowsNo > 0)
            {
                length = wdt.NumberOfBytesToWriteInRow * availableRowsNo;
                byte[] writeValues = new byte[length];
                Array.Copy(wdt.Values.ToArray(), sourceIndex, writeValues, 0, length);
                sourceIndex += length;

                WriteDataTables tmpWDT = new WriteDataTables(startAddress, wdt.NumberOfBytesToWriteInRow,
                    availableRowsNo, (ushort) (wdt.RowSizeInBytes), writeValues.ToList(), wdt.SubCommand);
                splitWDTList.Add(tmpWDT);
                ReadWriteDataTable(ref splitWDTList, parentID);
                splitWDTList.Clear();

                startAddress += (uint) (availableRowsNo * wdt.RowSizeInBytes);
                remainingRowsNo -= availableRowsNo;

                if (remainingRowsNo < availableRowsNo && remainingRowsNo > 0)
                    availableRowsNo = (ushort) remainingRowsNo;


                wdt.RaiseProgressEvent(0, wdt.NumberOfRowsToWrite * wdt.NumberOfBytesToWriteInRow,
                    RequestProgress.en_NotificationType.ProgressChanged,
                    (wdt.NumberOfRowsToWrite - remainingRowsNo) * wdt.NumberOfBytesToWriteInRow, "");
            }

            wdt.RaiseProgressEvent(0, wdt.NumberOfRowsToWrite * wdt.NumberOfBytesToWriteInRow,
                RequestProgress.en_NotificationType.Completed, wdt.NumberOfRowsToWrite * wdt.NumberOfBytesToWriteInRow,
                "");
            joinRequests[0].ResponseValues = String.Empty;
        }

        private void SplitAndWriteDataTableRowSizeExceedPLCBuffer(ref List<ReadWriteRequest> joinRequests,
            ushort maximumAvailableBytesNo, string parentID)
        {
            WriteDataTables wdt = joinRequests[0] as WriteDataTables;
            List<ReadWriteRequest> splitWDTList = new List<ReadWriteRequest>();
            ushort remainingRowsNo = wdt.NumberOfRowsToWrite;

            ushort maxAvailableWriteRowBytesNo = maximumAvailableBytesNo;
            ushort remainingWriteRowByteNo = (ushort) wdt.NumberOfBytesToWriteInRow;
            uint startAddress = wdt.StartAddress;

            int sourceIndex = 0;

            wdt.RaiseProgressEvent(0, wdt.NumberOfRowsToWrite * wdt.NumberOfBytesToWriteInRow,
                RequestProgress.en_NotificationType.SetMinMax, 0, "");
            wdt.RaiseProgressEvent(0, wdt.NumberOfRowsToWrite * wdt.NumberOfBytesToWriteInRow,
                RequestProgress.en_NotificationType.ProgressChanged, 0, "");

            while (remainingRowsNo > 0)
            {
                while (remainingWriteRowByteNo > 0)
                {
                    byte[] values = new byte[maxAvailableWriteRowBytesNo];
                    Array.Copy(wdt.Values.ToArray(), sourceIndex, values, 0, maxAvailableWriteRowBytesNo);
                    sourceIndex += maxAvailableWriteRowBytesNo;

                    WriteDataTables tmpWDT = new WriteDataTables(startAddress, maxAvailableWriteRowBytesNo, 1,
                        (ushort) wdt.RowSizeInBytes, values.ToList(), wdt.SubCommand);
                    splitWDTList.Add(tmpWDT);

                    ReadWriteDataTable(ref splitWDTList, parentID);
                    splitWDTList.Clear();

                    startAddress += maxAvailableWriteRowBytesNo;
                    remainingWriteRowByteNo -= maxAvailableWriteRowBytesNo;

                    if (remainingWriteRowByteNo < maxAvailableWriteRowBytesNo && remainingWriteRowByteNo > 0)
                        maxAvailableWriteRowBytesNo = remainingWriteRowByteNo;
                }

                remainingRowsNo--;
                maxAvailableWriteRowBytesNo = maximumAvailableBytesNo;
                remainingWriteRowByteNo = (ushort) wdt.NumberOfBytesToWriteInRow;
                startAddress =
                    (uint) (wdt.StartAddress + (wdt.NumberOfRowsToWrite - remainingRowsNo) * wdt.RowSizeInBytes);

                wdt.RaiseProgressEvent(0, wdt.NumberOfRowsToWrite * wdt.NumberOfBytesToWriteInRow,
                    RequestProgress.en_NotificationType.ProgressChanged,
                    (wdt.NumberOfRowsToWrite - remainingRowsNo) * wdt.NumberOfBytesToWriteInRow, "");
            }

            wdt.RaiseProgressEvent(0, wdt.NumberOfRowsToWrite * wdt.NumberOfBytesToWriteInRow,
                RequestProgress.en_NotificationType.Completed, wdt.NumberOfRowsToWrite * wdt.NumberOfBytesToWriteInRow,
                "");

            joinRequests[0].ResponseValues = String.Empty;
        }

        private List<byte> GetDataRequestBytesForReadDataTables(ReadDataTables rdt)
        {
            List<byte> result = new List<byte>();

            result.AddRange(BitConverter.GetBytes(rdt.NumberOfBytesToReadInRow));
            result.AddRange(BitConverter.GetBytes(rdt.NumberOfRowsToRead));
            result.AddRange(BitConverter.GetBytes(rdt.RowSizeInBytes));
            result.AddRange(new byte[24]);

            return result;
        }

        private IMessage ReceiveMessage(PComB pComB, List<List<byte>> readDataTablesDataRequestBytes, string parentID)
        {
            if (this.BreakFlag)
                throw new ComDriveExceptions("Request aborted by user",
                    ComDriveExceptions.ComDriveException.AbortedByUser);

            GuidClass guid = new GuidClass();

            lock (guid)
            {
                Channel.Send(pComB.MessageToPLC as byte[], ReceiveBytes, guid, parentID,
                    "Binary Protocol - Read/Write Data Tables", PlcGuid);
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

            pComB.DisAssembleBinaryResult(plcResponseMessage.responseBytesMessage, null);

            return pComB.MessageFromPLC;
        }

        private List<byte> GetDataRequestBytesForWriteDataTables(WriteDataTables wdt)
        {
            List<byte> result = new List<byte>();

            result.AddRange(BitConverter.GetBytes(wdt.NumberOfBytesToWriteInRow));
            result.AddRange(BitConverter.GetBytes(wdt.NumberOfRowsToWrite));
            result.AddRange(BitConverter.GetBytes(Convert.ToUInt32(wdt.RowSizeInBytes)));
            result.AddRange(new byte[24]);

            try
            {
                if (wdt.NumberOfBytesToWriteInRow * wdt.NumberOfRowsToWrite == wdt.Values.Count)
                    result.AddRange(wdt.Values);
                else
                {
                    throw new Exception();
                }
            }
            catch
            {
                throw new ComDriveExceptions("Invalid write values",
                    ComDriveExceptions.ComDriveException.UserInputException);
            }

            return result;
        }

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