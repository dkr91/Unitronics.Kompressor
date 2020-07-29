using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver.Command;
using System.Collections;
using Unitronics.ComDriver.Messages.ASCIIMessage;
using Unitronics.ComDriver.Messages.DataRequest;
using Unitronics.ComDriver.Messages;
using System.Diagnostics;
using System.Threading;

namespace Unitronics.ComDriver.Executers
{
    class AsciiExecuter : Executer
    {
        #region Locals

        private struct PlcResponseMessage
        {
            internal string response;
            internal CommunicationException comException;
        }

        private Dictionary<GuidClass, PlcResponseMessage> m_responseMessageQueue =
            new Dictionary<GuidClass, PlcResponseMessage>();

        private object _lockObj = new object();
        PComA pcomA;

        #endregion

        #region Constructor

        public AsciiExecuter(int unitId, Channel channel, PlcVersion plcVersion, Guid plcGuid)
            : base(unitId, channel, plcVersion, plcGuid)
        {
        }

        #endregion

        #region Public

        internal override void PerformReadWrite(ref ReadWriteRequest[] values, string parentID,
            bool suppressEthernetHeader)
        {
            if (pcomA == null)
                pcomA = new PComA();

            ReadWriteOperandsCommand(ref values, parentID, suppressEthernetHeader);
        }

        #endregion

        #region Private

        private void ReceiveString(string responseMessage, CommunicationException communicationException,
            GuidClass messageGuid)
        {
            if (this.BreakFlag)
                throw new ComDriveExceptions("Request aborted by user",
                    ComDriveExceptions.ComDriveException.AbortedByUser);

            Console.WriteLine("Message received in ASCII ReceiveString method : " + responseMessage);
            PlcResponseMessage plcResponseMessage = new PlcResponseMessage
            {
                comException = communicationException,
                response = responseMessage
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

        private void ReadWriteOperandsCommand(ref ReadWriteRequest[] values, string parentID,
            bool suppressEthernetHeader)
        {
            foreach (ReadWriteRequest operand in values)
            {
                if (operand is ReadOperands)
                {
                    if (receiveMessageSizeFitsThePLCBuffer(operand))
                    {
                        if (this.BreakFlag)
                        {
                            throw new ComDriveExceptions("Request aborted by user",
                                ComDriveExceptions.ComDriveException.AbortedByUser);
                        }

                        ReadWriteReadOperand(operand, parentID, suppressEthernetHeader);
                    }
                    else
                    {
                        //Split the ReadOperand request into a List of ReadOperands that fits the plc buffer
                        //or which doesn't exceed the 255(FF) number of operands
                        //After read each operand and add the response values to a object list.

                        List<ReadOperands> splitedReadOperands = GetSplitedReadOperands(operand);
                        List<object> responseValues = new List<object>();

                        for (int i = 0; i < splitedReadOperands.Count; i++)
                        {
                            if (this.BreakFlag)
                            {
                                throw new ComDriveExceptions("Request aborted by user",
                                    ComDriveExceptions.ComDriveException.AbortedByUser);
                            }

                            ReadWriteReadOperand(splitedReadOperands[i], parentID, suppressEthernetHeader);
                            responseValues.AddRange(splitedReadOperands[i].ResponseValues as object[]);
                        }

                        //Add all the response values to the initial ReadOperand
                        operand.ResponseValues = responseValues.ToArray();
                    }
                }
                else
                {
                    if (operand is WriteOperands)
                    {
                        if (sentMessageSizeFitsThePLCBuffer(operand))
                        {
                            if (this.BreakFlag)
                            {
                                throw new ComDriveExceptions("Request aborted by user",
                                    ComDriveExceptions.ComDriveException.AbortedByUser);
                            }

                            ReadWriteWriteOperand(operand, parentID, suppressEthernetHeader);
                        }
                        else
                        {
                            List<WriteOperands> splitedWriteOperands = GetSplitedWriteOperands(operand);

                            for (int i = 0; i < splitedWriteOperands.Count; i++)
                            {
                                if (this.BreakFlag)
                                {
                                    throw new ComDriveExceptions("Request aborted by user",
                                        ComDriveExceptions.ComDriveException.AbortedByUser);
                                }

                                ReadWriteWriteOperand(splitedWriteOperands[i], parentID, suppressEthernetHeader);
                            }

                            operand.ResponseValues = String.Empty;
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

        private List<WriteOperands> GetSplitedWriteOperands(ReadWriteRequest operand)
        {
            WriteOperands writeOperand = operand as WriteOperands;
            List<WriteOperands> results = new List<WriteOperands>();
            int operandSize = Utils.GetOperandSizeByCommandCode(writeOperand.OperandType.ToString());
            ushort startAddress = writeOperand.StartAddress;
            ushort remainingNoOfOperands = writeOperand.NumberOfOperands;
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
                    results.Add(new WriteOperands(remainingNoOfOperands, writeOperand.OperandType, startAddress,
                        splitValues, writeOperand.TimerValueFormat));
                    bDone = true;
                }
                else
                {
                    object[] splitValues = new object[splitMaxNoOfOperands];
                    Array.Copy(valuesToWrite, sourceIndex, splitValues, destinationIndex, splitMaxNoOfOperands);
                    results.Add(new WriteOperands(splitMaxNoOfOperands, writeOperand.OperandType, startAddress,
                        splitValues.ToArray(), writeOperand.TimerValueFormat));
                    startAddress += splitMaxNoOfOperands;
                    remainingNoOfOperands -= splitMaxNoOfOperands;
                    sourceIndex += splitMaxNoOfOperands;
                }
            }

            return results;
        }

        private List<ReadOperands> GetSplitedReadOperands(ReadWriteRequest operand)
        {
            ReadOperands readOpearnd = operand as ReadOperands;
            List<ReadOperands> results = new List<ReadOperands>();
            int operandSize = Utils.GetOperandSizeByCommandCode(readOpearnd.OperandType.ToString());
            ushort startAddress = readOpearnd.StartAddress;
            ushort remainingNoOfOperands = readOpearnd.NumberOfOperands;
            int splitMaxDataSize = PLCVersion.PlcBuffer - Utils.Lengths.LENGTH_ASCII_RECEIVE_MESSAGE;
            ushort splitMaxNoOfOperands = (ushort) (splitMaxDataSize / operandSize);
            if (splitMaxNoOfOperands > 255)
                splitMaxNoOfOperands = 255;

            bool bDone = false;

            while (!bDone)
            {
                if (remainingNoOfOperands <= splitMaxNoOfOperands)
                {
                    results.Add(new ReadOperands(remainingNoOfOperands, readOpearnd.OperandType, startAddress,
                        readOpearnd.TimerValueFormat));
                    bDone = true;
                }
                else
                {
                    results.Add(new ReadOperands(splitMaxNoOfOperands, readOpearnd.OperandType, startAddress,
                        readOpearnd.TimerValueFormat));
                    startAddress += splitMaxNoOfOperands;
                    remainingNoOfOperands -= splitMaxNoOfOperands;
                }
            }

            return results;
        }

        private bool receiveMessageSizeFitsThePLCBuffer(ReadWriteRequest operand)
        {
            ReadOperands readOperands = operand as ReadOperands;
            int operandSize = Utils.GetOperandSizeByCommandCode(readOperands.OperandType.ToString());
            int receiveDataSize = readOperands.NumberOfOperands * operandSize;
            int receiveMessageSize = Utils.Lengths.LENGTH_ASCII_RECEIVE_MESSAGE + receiveDataSize;

            return receiveMessageSize <= PLCVersion.PlcBuffer && readOperands.NumberOfOperands <= 255;
        }

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

        private void ReadWriteWriteOperand(ReadWriteRequest operand, string parentID, bool suppressEthernetHeader)
        {
            WriteOperands writeOperands = operand as WriteOperands;
            GuidClass guid = new GuidClass();
            pcomA.BuildAsciiCommand(UnitId,
                Utils.ASCIIOperandTypes[Enum.GetName(typeof(OperandTypes), writeOperands.OperandType)]
                    .CommandCodeForWrite,
                (operand as WriteOperands).StartAddress, writeOperands.NumberOfOperands, writeOperands.Values,
                (operand as WriteOperands).TimerValueFormat);

            string readMessage = pcomA.MessageToPLC as string;
            if (this.BreakFlag)
                throw new ComDriveExceptions("Request aborted by user",
                    ComDriveExceptions.ComDriveException.AbortedByUser);

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

            pcomA.DisAssembleAsciiResult(plcResponseMessage.response, UnitId,
                Utils.ASCIIOperandTypes[Enum.GetName(typeof(OperandTypes), writeOperands.OperandType)]
                    .CommandCodeForWrite,
                (operand as WriteOperands).StartAddress, writeOperands.NumberOfOperands, null,
                writeOperands.TimerValueFormat);
            if (pcomA.MessageFromPLC != null)
                operand.ResponseValues = (pcomA.MessageFromPLC as AbstractASCIIMessage).Values;
        }

        private void ReadWriteReadOperand(ReadWriteRequest operand, string parentID, bool suppressEthernetHeader)
        {
            ReadOperands readOperands = operand as ReadOperands;
            GuidClass guid = new GuidClass();
            pcomA.BuildAsciiCommand(UnitId,
                Utils.ASCIIOperandTypes[Enum.GetName(typeof(OperandTypes), readOperands.OperandType)]
                    .CommandCodeForRead,
                (operand as ReadOperands).StartAddress, readOperands.NumberOfOperands, null,
                readOperands.TimerValueFormat);

            string readMessage = pcomA.MessageToPLC as string;

            if (this.BreakFlag)
                throw new ComDriveExceptions("Request aborted by user",
                    ComDriveExceptions.ComDriveException.AbortedByUser);

            lock (guid)
            {
                Channel.Send(readMessage, ReceiveString, guid, parentID, "ASCII Protocol - Read Operands", PlcGuid,
                    suppressEthernetHeader);
                Monitor.Wait(guid);
            }

            if (this.BreakFlag)
                throw new ComDriveExceptions("Request aborted by user",
                    ComDriveExceptions.ComDriveException.AbortedByUser);

            PlcResponseMessage plcResponseMessage;
            {
                plcResponseMessage = m_responseMessageQueue[guid];
                m_responseMessageQueue.Remove(guid);
            }

            if (plcResponseMessage.comException == CommunicationException.Timeout)
            {
                throw new ComDriveExceptions("Cannot communicate with the PLC with the specified UnitID!",
                    ComDriveExceptions.ComDriveException.CommunicationTimeout);
            }

            pcomA.DisAssembleAsciiResult(plcResponseMessage.response, UnitId,
                Utils.ASCIIOperandTypes[Enum.GetName(typeof(OperandTypes), readOperands.OperandType)]
                    .CommandCodeForRead,
                (operand as ReadOperands).StartAddress, readOperands.NumberOfOperands, null,
                readOperands.TimerValueFormat);
            if (pcomA.MessageFromPLC != null)
            {
                operand.ResponseValues = (pcomA.MessageFromPLC as AbstractASCIIMessage).ArrayValues; //Values;
            }
        }

        #endregion
    }
}