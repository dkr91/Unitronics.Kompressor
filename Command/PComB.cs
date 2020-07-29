using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver.Messages;
using Unitronics.ComDriver.Messages.BinMessage;
using Unitronics.ComDriver.Messages.DataRequest;
using Unitronics.ComDriver;

namespace Unitronics.ComDriver.Command
{
    public class PComB : CommadBuilder
    {
        #region Locals

        private BinaryMessage m_messageToPLC;
        private BinaryMessage m_messageFromPLC;

        #endregion

        #region Public

        public override void BuildBinaryCommand(byte unitId, BinaryCommand commandNumber, byte[] cmdDetails,
            List<ReadWriteRequest> dataRequests, List<List<byte>> dataRequestsBytes)
        {
            m_messageToPLC = new BinaryMessage(unitId, commandNumber, cmdDetails, dataRequests, dataRequestsBytes);
        }

        public override void BuildBinaryCommand(byte unitId, BinaryCommand commandNumber, byte[] cmdDetails,
            List<ReadWriteRequest> dataRequests, List<List<byte>> dataRequestsBytes, byte subCommand)
        {
            m_messageToPLC = new BinaryMessage(unitId, commandNumber, cmdDetails, dataRequests, dataRequestsBytes,
                subCommand);
        }

        public override void BuildBinaryCommand(byte unitId, int messageKey, int commandNumber, int subCommand,
            int address, int elementsCount, UInt16 outgoingDataLength, byte[] binaryData)
        {
            m_messageToPLC = new BinaryMessage(unitId, messageKey, commandNumber, subCommand, address, elementsCount,
                outgoingDataLength, binaryData);
        }

        public override void DisAssembleBinaryResult(byte[] message, List<List<byte>> dataRequestBytes)
        {
            if (message != null)
                m_messageFromPLC = new BinaryMessage(message, m_messageToPLC, dataRequestBytes);
        }

        public override void BuildAsciiCommand(int unitId, string commandCode, int? address, int? length,
            object[] values, TimerValueFormat timerValueFormat)
        {
            throw new NotImplementedException();
        }

        public override void DisAssembleAsciiResult(string message, int unitId, string commandCode, int? address,
            int? length, List<List<byte>> dataRequestBytes, TimerValueFormat timerValueFormat)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Properties

        public override IMessage MessageFromPLC
        {
            get { return m_messageFromPLC as IMessage; }
        }

        public override object MessageToPLC
        {
            get { return m_messageToPLC.BinaryMessageBytes as object; }
        }

        internal int SubCommand
        {
            set { }
            get { return m_messageToPLC.Header.HeaderBytes[13]; }
        }

        #endregion
    }
}