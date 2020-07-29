using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver.Command;
using Unitronics.ComDriver.Messages.BinMessage;
using Unitronics.ComDriver.Messages;
using Unitronics.ComDriver.Messages.DataRequest;
using System.Threading;
using System.Diagnostics;

namespace Unitronics.ComDriver.Executers
{
    class FullBinaryMixExecuter : Executer
    {
        #region Locals

        private struct PlcResponseMessage
        {
            internal byte[] responseBytesMessage;
            internal CommunicationException comException;
        }

        private Dictionary<GuidClass, PlcResponseMessage> m_responseMessageQueue =
            new Dictionary<GuidClass, PlcResponseMessage>();

        private int m_BufferSize;
        private object _lockObj = new object();

        #endregion

        #region Constructor

        public FullBinaryMixExecuter(int unitId, Channel channel, PlcVersion plcVersion, Guid plcGuid)
            : base(unitId, channel, plcVersion, plcGuid)
        {
            m_BufferSize = plcVersion.PlcBuffer;
        }

        #endregion

        #region Internal

        internal override void PerformReadWrite(ref ReadWriteRequest[] values, string parentID,
            bool suppressEthernetHeader)
        {
            SplitAndJoin splitAndJoin = new SplitAndJoin(m_BufferSize);
            List<ReadWriteRequest> splitRequestsList = new List<ReadWriteRequest>();

            for (int iter = 0; iter < values.Length; iter++)
            {
                switch (values[iter].GetType().ToString().Substring(42))
                {
                    case "ReadOperands":
                    case "WriteOperands":
                        splitAndJoin.AddNewRequest(values[iter], iter, false);
                        break;

                    default:
                        throw new ComDriveExceptions(
                            "Unexpected error. Actual request type doesn't match the expected type",
                            ComDriveExceptions.ComDriveException.UnexpectedError);
                }
            }

            if (splitAndJoin.FinishSpliting())
                ExecuteRequests(ref values, parentID, splitAndJoin);
        }

        #endregion

        #region Private

        private void ExecuteRequests(ref ReadWriteRequest[] values, string parentID, SplitAndJoin splitAndJoin)
        {
            try
            {
                List<object>[] resposeValuesList = new List<object>[values.Length];
                byte messageEnumerator = (byte) splitAndJoin.allRequestsList.Count;
                for (int i = 0; i < splitAndJoin.allRequestsList.Count; i++)
                {
                    ReadWriteRequest[] requestToExec = splitAndJoin.allRequestsList[i].ToArray();
                    ReadWriteOperandsCommand(ref requestToExec, parentID, messageEnumerator);
                    messageEnumerator--;
                }

                if (this.BreakFlag)
                {
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);
                }

                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i] is ReadOperands)
                    {
                        if (values[i].ResponseValues != null)
                        {
                            resposeValuesList[i] = new List<object>();
                            resposeValuesList[i].AddRange(values[i].ResponseValues as object[]);
                        }
                    }
                }

                for (int i = 0; i < splitAndJoin.splitDetailsList.Count; i++)
                {
                    int userReqPos = splitAndJoin.splitDetailsList[i].userRequestPosition;
                    int allReqPos = splitAndJoin.splitDetailsList[i].allRequestsPosition;
                    int splitReqPos = splitAndJoin.splitDetailsList[i].splitRequestPosition;

                    if (values[userReqPos] is WriteOperands)
                        values[userReqPos].ResponseValues = String.Empty;
                    else
                    {
                        if (resposeValuesList[userReqPos] == null)
                        {
                            resposeValuesList[userReqPos] = new List<object>();
                            resposeValuesList[userReqPos]
                                .AddRange(
                                    splitAndJoin.allRequestsList[allReqPos][splitReqPos].ResponseValues as object[]);
                        }
                        else
                        {
                            object[] responseValues =
                                splitAndJoin.allRequestsList[allReqPos][splitReqPos].ResponseValues as object[];
                            resposeValuesList[userReqPos].AddRange(responseValues);
                        }
                    }
                }


                for (int i = 0; i < splitAndJoin.splitDetailsList.Count; i++)
                {
                    int userReqPos = splitAndJoin.splitDetailsList[i].userRequestPosition;
                    int allReqPos = splitAndJoin.splitDetailsList[i].allRequestsPosition;
                    int splitReqPos = splitAndJoin.splitDetailsList[i].splitRequestPosition;

                    if (values[userReqPos] is ReadOperands)
                    {
                        values[userReqPos].ResponseValues = resposeValuesList[userReqPos].ToArray();
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                splitAndJoin.ClearLists();
            }
        }

        private void ReadWriteOperandsCommand(ref ReadWriteRequest[] values, string parentID, byte messageEnumerator)
        {
            PComB pComB = new PComB();
            List<List<byte>> dataRequestsBytes = new List<List<byte>>();

            #region Group Requests by type.

            List<ReadOperands> operandReads = new List<ReadOperands>();
            List<WriteOperands> operandWrites = new List<WriteOperands>();

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] is ReadOperands)
                    operandReads.Add(values[i] as ReadOperands);
                if (values[i] is WriteOperands)
                    operandWrites.Add(values[i] as WriteOperands);
            }

            #endregion

            #region Detail Area Header

            int totalNumberOfReads = 0;
            int totalNumberOfWrites = 0;

            foreach (ReadWriteRequest operand in values)
            {
                if (operand is ReadOperands)
                {
                    totalNumberOfReads += (operand as ReadOperands).NumberOfOperands;
                    dataRequestsBytes.Add(GetDataRequestsBytesForReadOperands(operand as ReadOperands));
                }

                if (operand is WriteOperands)
                {
                    totalNumberOfWrites += (operand as WriteOperands).NumberOfOperands;
                    dataRequestsBytes.Add(GetDataRequestsBytesForWriteOperands(operand as WriteOperands));
                }
            }

            byte[] command_detailes = new byte[4];

            byte[] NoOfReads = BitConverter.GetBytes(totalNumberOfReads);
            byte[] NoOfWrites = BitConverter.GetBytes(totalNumberOfWrites);

            command_detailes[0] = NoOfReads[0];
            command_detailes[1] = NoOfReads[1];

            command_detailes[2] = NoOfWrites[0];
            command_detailes[3] = NoOfWrites[1];

            #endregion

            if (operandReads.Count > 0 || operandWrites.Count > 0)
            {
                pComB.BuildBinaryCommand((byte) UnitId, BinaryCommand.ReadWrite, command_detailes,
                    values.ToList(), dataRequestsBytes);

                if (Channel != null)
                {
                    if (this.BreakFlag)
                    {
                        throw new ComDriveExceptions("Request aborted by user",
                            ComDriveExceptions.ComDriveException.AbortedByUser);
                    }

                    GuidClass guid = new GuidClass();

                    lock (guid)
                    {
                        Channel.Send(pComB.MessageToPLC as byte[], ReceiveBytes, guid, parentID,
                            "Binary Protocol - Read/Write Operands (80)", PlcGuid, messageEnumerator);
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

                    // receivedMessage[13] shows if there was occured an error.
                    if (plcResponseMessage.responseBytesMessage[13] == 0)
                        pComB.DisAssembleBinaryResult(plcResponseMessage.responseBytesMessage, dataRequestsBytes);
                }
            }
            else
            {
                //System.Diagnostics.Debug.Assert(false);
            }
        }

        private void ReceiveBytes(byte[] bytes, CommunicationException communicationException, GuidClass messageGuid)
        {
            PlcResponseMessage plcResponseMessage = new PlcResponseMessage
            {
                comException = communicationException,
                responseBytesMessage = bytes
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

        private List<byte> GetDataRequestsBytesForWriteOperands(WriteOperands writeOperandsExecuter)
        {
            List<byte> results = new List<byte>();

            results.AddRange(BitConverter.GetBytes((UInt16) 0));
            results.AddRange(
                BitConverter.GetBytes((writeOperandsExecuter.NumberOfOperands > 0) ? (UInt16) 1 : (UInt16) 0));

            if (writeOperandsExecuter.NumberOfOperands > 0)
                results.AddRange(UpdateWriteDataRequest(
                    writeOperandsExecuter.OperandType.ToString().GetOperandIdByNameForFullBinary(),
                    writeOperandsExecuter.NumberOfOperands, writeOperandsExecuter.StartAddress,
                    writeOperandsExecuter.Values));

            return results;
        }

        private IEnumerable<byte> UpdateWriteDataRequest(byte writeOperandId, ushort numberOfWrites,
            ushort writeStartAddress, object[] writeData)
        {
            List<byte> results = new List<byte>();
            results.Add(writeOperandId);

            UInt16 writeByteCount;
            if (writeOperandId.GetOperandSizeByValueForFullBinary() == 1)
                writeByteCount = numberOfWrites;
            else
                writeByteCount = (UInt16) (numberOfWrites *
                                           Convert.ToUInt16(writeOperandId.GetOperandSizeByValueForFullBinary() / 8));

            results.Add(Convert.ToByte(numberOfWrites));
            results.AddRange(BitConverter.GetBytes(writeStartAddress));
            results.AddRange(AddWriteData(writeData, writeOperandId));

            return results;
        }

        private IEnumerable<byte> AddWriteData(object[] writeData, byte writeOperandId)
        {
            List<byte> results = new List<byte>();

            string operandName = writeOperandId.GetOperandNameByValueForFullBinary();

            switch (operandName)
            {
                case "MB":
                case "SB":
                case "XB":
                case "INPUT":
                case "Output":
                case "TimerRunBit":
                case "CounterRunBit":
                case "RTC":
                    foreach (object value in writeData)
                    {
                        UInt16 bitValue = BitConverter.ToUInt16(BitConverter.GetBytes(Convert.ToSByte(value)), 0);
                        if (bitValue > 0)
                            results.Add(1);
                        else
                            results.Add(0);
                        if (writeData.Length == 1)
                            results.Add(0);
                    }

                    break;
                case "MI":
                case "SI":
                case "XI":
                case "CounterCurrent":
                case "CounterPreset":
                    foreach (object value in writeData)
                    {
                        results.AddRange(BitConverter.GetBytes(Convert.ToInt16(value)));
                    }

                    break;
                case "ML":
                case "SL":
                case "XL":
                    foreach (object value in writeData)
                    {
                        results.AddRange(BitConverter.GetBytes(Convert.ToInt32(value)));
                    }

                    break;
                case "TimerCurrent":
                case "TimerPreset":
                    foreach (object value in writeData)
                    {
                        if (value.GetType().Equals(typeof(List<UInt16>)))
                            results.AddRange(BitConverter.GetBytes(Utils.z_GetSecondsValue(value as List<UInt16>)));
                        else
                            results.AddRange(BitConverter.GetBytes(Convert.ToUInt32(value)));
                    }

                    break;
                case "MF":
                    foreach (object value in writeData)
                    {
                        //Switch to IEEE 754 standard
                        byte[] floatBytes = BitConverter.GetBytes(Convert.ToSingle(value));
                        Array.Reverse(floatBytes, 0, 4);
                        Array.Reverse(floatBytes, 0, 2);
                        Array.Reverse(floatBytes, 2, 2);
                        results.AddRange(floatBytes);
                    }

                    break;
                case "DW":
                case "SDW":
                case "XDW":
                    foreach (object value in writeData)
                    {
                        results.AddRange(BitConverter.GetBytes(Convert.ToUInt32(value)));
                    }

                    break;
            }

            return results;
        }

        private List<byte> GetDataRequestsBytesForReadOperands(ReadOperands readOperandsExecuter)
        {
            List<byte> results = new List<byte>();

            results.AddRange(
                BitConverter.GetBytes((readOperandsExecuter.NumberOfOperands > 0) ? (UInt16) 1 : (UInt16) 0));
            results.AddRange(BitConverter.GetBytes((UInt16) 0));

            if (readOperandsExecuter.NumberOfOperands > 0)
                results.AddRange(UpdateReadDataRequest(
                    readOperandsExecuter.OperandType.ToString().GetOperandIdByNameForFullBinary(),
                    readOperandsExecuter.NumberOfOperands, readOperandsExecuter.StartAddress));

            return results;
        }

        private IEnumerable<byte> UpdateReadDataRequest(byte readOperandId, ushort numberOfReads,
            ushort readStartAddress)
        {
            List<byte> results = new List<byte>();
            results.Add(readOperandId);
            UInt16 readByteCount;

            if (readOperandId.GetOperandSizeByValueForFullBinary() == 1)
                if (numberOfReads > 256)
                    readByteCount = 255;
                else
                    readByteCount = numberOfReads;
            else if (numberOfReads > 256)
                readByteCount = (UInt16) (
                    255 * Convert.ToUInt16(readOperandId.GetOperandSizeByValueForFullBinary() / 8));
            else
                readByteCount = (UInt16) (
                    numberOfReads * Convert.ToUInt16(readOperandId.GetOperandSizeByValueForFullBinary() / 8));


            if (numberOfReads > 255)
            {
                results.Add(Convert.ToByte(255));
            }
            else
                results.Add(Convert.ToByte(numberOfReads));

            results.AddRange(BitConverter.GetBytes(readStartAddress));

            return results;
        }

        #endregion
    }
}