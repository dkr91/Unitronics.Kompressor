using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver.Command;
using Unitronics.ComDriver.Messages;
using Unitronics.ComDriver;

namespace Unitronics.ComDriver.Messages.BinMessage
{
    public class Header
    {
        #region Locals

        private byte[] m_headerBytes;

        private byte m_destination;
        private byte m_commandNumber;
        private byte[] m_cmdDetails;
        private UInt16 m_dataLength;

        #endregion

        #region Constructor

        public Header()
        {
            m_headerBytes = new byte[24];
        }

        /// <summary>
        /// Build's header from PLC
        /// </summary>
        /// <param name="message">Message bytes from PLC</param>
        public Header(byte[] message)
            : this()
        {
            if (message.Length > 24)
                Array.Copy(message, m_headerBytes, 24);
        }

        /// <summary>
        /// Build's header to PLC
        /// </summary>
        /// <param name="destination">Destination CANbus network ID and RS485</param>
        /// <param name="commandNumber">Command Number</param>
        /// <param name="cmdDetails">Specific for each Command Number. Byte[6]</param>
        /// <param name="dataLength">Data length Number of bytes in the Details section. Byte[2]</param>
        public Header(byte destination, BinaryCommand commandNumber, byte[] cmdDetails, UInt16 dataLength)
            : this()
        {
            m_destination = destination;
            m_commandNumber = (byte) commandNumber;
            m_cmdDetails = cmdDetails;
            m_dataLength = dataLength;
            m_headerBytes[0] = BitConverter.GetBytes('/')[0];
            m_headerBytes[1] = BitConverter.GetBytes('_')[0];
            m_headerBytes[2] = BitConverter.GetBytes('O')[0];
            m_headerBytes[3] = BitConverter.GetBytes('P')[0];
            m_headerBytes[4] = BitConverter.GetBytes('L')[0];
            m_headerBytes[5] = BitConverter.GetBytes('C')[0];
            m_headerBytes[6] = destination;
            m_headerBytes[7] = 254; //Should always be 254 (FE hex)
            m_headerBytes[8] = 1; //Should always be 1
            m_headerBytes[9] = 0; //Should always be 0
            m_headerBytes[10] = 0;
            m_headerBytes[11] = 0;
            m_headerBytes[12] = (byte) commandNumber;
            m_headerBytes[13] = 0;

            if (cmdDetails != null && cmdDetails.Length == 6)
            {
                /*
                 * The address of the first element (to read from or to write into) must be arranged as 
                 * a 4 byte value and set into bytes 14,15,16,17 of the message header (byte 14 is the least significant byte).
                 */
                m_headerBytes[14] = cmdDetails[0];
                m_headerBytes[15] = cmdDetails[1];
                m_headerBytes[16] = cmdDetails[2];
                m_headerBytes[17] = cmdDetails[3];
                m_headerBytes[18] = cmdDetails[4];
                m_headerBytes[19] = cmdDetails[5];
            }

            m_headerBytes[20] = BitConverter.GetBytes(dataLength)[0];
            m_headerBytes[21] = BitConverter.GetBytes(dataLength)[1];

            byte[] checkSum = GetCheckSum();
            m_headerBytes[22] = checkSum[0];
            m_headerBytes[23] = checkSum[1];
        }

        public Header(byte destination, BinaryCommand commandNumber, byte[] cmdDetails, UInt16 dataLength,
            byte subCommand)
            : this()
        {
            m_destination = destination;
            m_commandNumber = (byte) commandNumber;
            m_cmdDetails = cmdDetails;
            m_dataLength = dataLength;
            m_headerBytes[0] = BitConverter.GetBytes('/')[0];
            m_headerBytes[1] = BitConverter.GetBytes('_')[0];
            m_headerBytes[2] = BitConverter.GetBytes('O')[0];
            m_headerBytes[3] = BitConverter.GetBytes('P')[0];
            m_headerBytes[4] = BitConverter.GetBytes('L')[0];
            m_headerBytes[5] = BitConverter.GetBytes('C')[0];
            m_headerBytes[6] = destination;
            m_headerBytes[7] = 254; //Should always be 254 (FE hex)
            m_headerBytes[8] = 1; //Should always be 1
            m_headerBytes[9] = 0; //Should always be 0
            m_headerBytes[10] = 0;
            m_headerBytes[11] = 0;
            m_headerBytes[12] = (byte) commandNumber;
            m_headerBytes[13] = subCommand;

            if (cmdDetails != null && cmdDetails.Length == 6)
            {
                /*
                 * The address of the first element (to read from or to write into) must be arranged as 
                 * a 4 byte value and set into bytes 14,15,16,17 of the message header (byte 14 is the least significant byte).
                 */
                m_headerBytes[14] = cmdDetails[0];
                m_headerBytes[15] = cmdDetails[1];
                m_headerBytes[16] = cmdDetails[2];
                m_headerBytes[17] = cmdDetails[3];
                m_headerBytes[18] = cmdDetails[4];
                m_headerBytes[19] = cmdDetails[5];
            }

            m_headerBytes[20] = BitConverter.GetBytes(dataLength)[0];
            m_headerBytes[21] = BitConverter.GetBytes(dataLength)[1];

            byte[] checkSum = GetCheckSum();
            m_headerBytes[22] = checkSum[0];
            m_headerBytes[23] = checkSum[1];
        }

        public Header(byte destination, int messageKey, int commandNumber, int subCommand, int address,
            int elementsCount, UInt16 outgoingDataLength)
            : this()
        {
            m_destination = destination;
            m_commandNumber = (byte) commandNumber;
            byte[] tmpByteArray;
            m_headerBytes[0] = BitConverter.GetBytes('/')[0];
            m_headerBytes[1] = BitConverter.GetBytes('_')[0];
            m_headerBytes[2] = BitConverter.GetBytes('O')[0];
            m_headerBytes[3] = BitConverter.GetBytes('P')[0];
            m_headerBytes[4] = BitConverter.GetBytes('L')[0];
            m_headerBytes[5] = BitConverter.GetBytes('C')[0];
            m_headerBytes[6] = destination;
            m_headerBytes[7] = 254; //Should always be 254 (FE hex)
            m_headerBytes[8] = 1; //Should always be 1
            m_headerBytes[9] = (byte) messageKey; //Should always be 0
            m_headerBytes[10] = 0;
            m_headerBytes[11] = 0;
            m_headerBytes[12] = (byte) commandNumber;
            m_headerBytes[13] = (byte) subCommand;

            tmpByteArray = BitConverter.GetBytes(address);
            Array.Copy(tmpByteArray, 0, m_headerBytes, 14, 4);

            tmpByteArray = BitConverter.GetBytes(elementsCount);
            Array.Copy(tmpByteArray, 0, m_headerBytes, 18, 2);

            tmpByteArray = BitConverter.GetBytes(outgoingDataLength);
            Array.Copy(tmpByteArray, 0, m_headerBytes, 20, 2);

            byte[] checkSum = GetCheckSum();
            m_headerBytes[22] = checkSum[0];
            m_headerBytes[23] = checkSum[1];
        }

        #endregion

        #region Private

        private byte[] GetCheckSum()
        {
            int sum = m_headerBytes.Sum(x => (int) x);
            //sum = sum - 47;
            int checkSum = ~(sum % 0x10000) + 1;
            //byte[] checkSumBytes = new byte[2];
            //Array.Copy(BitConverter.GetBytes((UInt16)checkSum),
            return BitConverter.GetBytes((UInt16) checkSum);
        }

        #endregion

        #region Properties

        public byte Destination
        {
            get { return m_destination; }
        }

        public byte CommandNumber
        {
            get { return m_commandNumber; }
        }

        public byte[] CommandDetails
        {
            get { return m_cmdDetails; }
        }

        public byte[] HeaderBytes
        {
            get { return m_headerBytes; }
            set { m_headerBytes = value; }
        }

        #endregion
    }
}