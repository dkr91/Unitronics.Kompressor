using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver;
using Unitronics.ComDriver.Messages.ASCIIMessage;
using Unitronics.ComDriver.Messages.DataRequest;
using Unitronics.ComDriver.Messages;

namespace Unitronics.ComDriver.Command
{
    public class PComA : CommadBuilder
    {
        #region Locals

        private string m_messageToPLC;
        private AbstractASCIIMessage m_messageFromPLC;

        #endregion

        #region Public

        public override void BuildAsciiCommand(int unitId, string commandCode, int? address, int? length,
            object[] values, TimerValueFormat timerValueFormat)
        {
            AbstractASCIIMessage message =
                ASCIIMessageFactory.GetMessageType(unitId, commandCode, address, length, values);
            m_messageToPLC = message.GetMessage(timerValueFormat);
        }

        public override void DisAssembleAsciiResult(string receivedMessage, int unitId, string commandCode,
            int? address, int? length, List<List<byte>> dataRequestBytes, TimerValueFormat timerValueFormat)
        {
            if (receivedMessage != null)
            {
                AbstractASCIIMessage message =
                    ASCIIMessageFactory.GetMessageType(unitId, commandCode, address, length, null);
                message.TimerValueFormat = timerValueFormat;
                m_messageFromPLC = message.GetMessage(receivedMessage);
            }
        }

        public override void BuildBinaryCommand(byte unitId, BinaryCommand commandNumber, byte[] cmdDetails,
            List<ReadWriteRequest> dataRequests, List<List<byte>> dataRequestsBytes)
        {
            throw new NotImplementedException();
        }

        public override void BuildBinaryCommand(byte unitId, BinaryCommand commandNumber, byte[] cmdDetails,
            List<ReadWriteRequest> dataRequests, List<List<byte>> dataRequestsBytes, byte subCommand)
        {
            throw new NotImplementedException();
        }

        public override void BuildBinaryCommand(byte unitId, int messageKey, int commandNumber, int subCommand,
            int address, int elementsCount, UInt16 outgoingDataLength, byte[] binaryData)
        {
            throw new NotImplementedException();
        }

        public override void DisAssembleBinaryResult(byte[] message, List<List<byte>> dataRequestBytes)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Properties

        public override object MessageToPLC
        {
            get { return m_messageToPLC as object; }
        }

        public override IMessage MessageFromPLC
        {
            get { return m_messageFromPLC as IMessage; }
        }

        #endregion
    }
}