using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver.Command;
using Unitronics.ComDriver.Messages;
using Unitronics.ComDriver.Messages.DataRequest;
using Unitronics.ComDriver;

namespace Unitronics.ComDriver.Messages.BinMessage
{
    public class BinaryMessage : IMessage
    {
        #region Locals

        private Header m_header;
        private Details m_details;
        private Footer m_footer;
        private List<byte> m_BinaryMessageBytes;

        #endregion

        #region Constructor

        public BinaryMessage(byte[] message, BinaryMessage requestedMessage, List<List<byte>> dataRequestsBytes)
        {
            m_BinaryMessageBytes = message.ToList();
            BuildMessage(requestedMessage, dataRequestsBytes);
        }

        /// <summary>
        /// Each Binary message contains the following sections:
        /// • Header
        /// • Details
        /// • Footer
        /// The format of each section is identical for both data requests and responses; 
        /// the contents of the sections vary according to the command number and the data that is contained by the message.
        /// </summary>
        /// <param name="unitId">Destination CANbus network ID and RS485</param>
        /// <param name="commandNumber">BinaryCommand Number</param>
        /// <param name="commandDetails">Specific for each Command Number</param>
        /// <param name="dataRequests">DataRequests</param>
        public BinaryMessage(byte unitId, BinaryCommand commandNumber, byte[] commandDetails,
            List<ReadWriteRequest> dataRequests, List<List<byte>> dataRequestsBytes)
        {
            List<ReadWriteRequest> query = null;
            //if (commandNumber == BinaryCommand.ReadWrite)
            //    query = dataRequests.OrderBy(dr => GetOperandSize(dr)).ToList();

            m_details = new Details(query == null ? dataRequests : query, dataRequestsBytes);
            m_header = new Header(unitId, commandNumber, commandDetails, (UInt16) m_details.DetailsBytes.Length);
            m_footer = new Footer(m_details.CheckSum);

            m_BinaryMessageBytes = new List<byte>();
            m_BinaryMessageBytes.AddRange(m_header.HeaderBytes);
            m_BinaryMessageBytes.AddRange(m_details.DetailsBytes);
            m_BinaryMessageBytes.AddRange(m_footer.FooterBytes);
        }

        public BinaryMessage(byte unitId, BinaryCommand commandNumber, byte[] commandDetails,
            List<ReadWriteRequest> dataRequests, List<List<byte>> dataRequestsBytes, byte subCommand)
        {
            List<ReadWriteRequest> query = null;
            //if (commandNumber == BinaryCommand.ReadWrite)
            //    query = dataRequests.OrderBy(dr => GetOperandSize(dr)).ToList();

            m_details = new Details(query == null ? dataRequests : query, dataRequestsBytes);
            m_header = new Header(unitId, commandNumber, commandDetails, (UInt16) m_details.DetailsBytes.Length,
                subCommand);
            m_footer = new Footer(m_details.CheckSum);

            m_BinaryMessageBytes = new List<byte>();
            m_BinaryMessageBytes.AddRange(m_header.HeaderBytes);
            m_BinaryMessageBytes.AddRange(m_details.DetailsBytes);
            m_BinaryMessageBytes.AddRange(m_footer.FooterBytes);
        }

        public BinaryMessage(byte unitId, int messageKey, int commandNumber, int subCommand, int address,
            int elementsCount, UInt16 outgoingDataLength, byte[] binaryData)
        {
            int sum = binaryData.Sum(x => (int) x);
            int checkSum = ~(sum % 0x10000) + 1;

            m_header = new Header(unitId, messageKey, commandNumber, subCommand, address, elementsCount,
                outgoingDataLength);
            m_footer = new Footer((UInt16) checkSum);

            m_BinaryMessageBytes = new List<byte>();
            m_BinaryMessageBytes.AddRange(m_header.HeaderBytes);
            m_BinaryMessageBytes.AddRange(binaryData);
            m_BinaryMessageBytes.AddRange(m_footer.FooterBytes);
        }

        #endregion

        #region Private

        private byte GetOperandSize(ReadWriteRequest dr)
        {
            if (dr is ReadOperands)
                return (dr as ReadOperands).OperandType.ToString().GetOperandIdByName().GetOperandSizeByValue();
            else
                return (dr as WriteOperands).OperandType.ToString().GetOperandIdByName().GetOperandSizeByValue();
        }

        private void BuildMessage(BinaryMessage requestedMessage, List<List<byte>> dataRequestsBytes)
        {
            m_header = GetHeader(m_BinaryMessageBytes);
            m_details = GetDetails(m_BinaryMessageBytes, requestedMessage, dataRequestsBytes);
            m_footer = GetFooter(m_BinaryMessageBytes);
        }

        private Footer GetFooter(List<byte> m_BinaryMessageBytes)
        {
            Footer footer = new Footer(m_BinaryMessageBytes.ToArray());
            return footer;
        }

        private Details GetDetails(List<byte> m_BinaryMessageBytes, BinaryMessage requestedMessage,
            List<List<byte>> dataRequestsBytes)
        {
            Details details = new Details(m_BinaryMessageBytes.ToArray(), requestedMessage, dataRequestsBytes);
            return details;
        }

        private Header GetHeader(List<byte> m_BinaryMessageBytes)
        {
            Header header = new Header(m_BinaryMessageBytes.ToArray());
            return header;
        }

        #endregion

        #region Properties

        public byte[] BinaryMessageBytes
        {
            get { return m_BinaryMessageBytes.ToArray(); }
        }

        public Header Header
        {
            get { return m_header; }
        }

        public Details Details
        {
            get { return m_details; }
        }

        public Footer Footer
        {
            get { return m_footer; }
        }

        #endregion
    }
}