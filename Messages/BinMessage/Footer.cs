using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Unitronics.ComDriver.Messages.BinMessage
{
    public class Footer
    {
        #region Locals

        private byte[] m_footerBytes;

        #endregion

        #region Constructor

        public Footer()
        {
            m_footerBytes = new byte[3];
        }

        /// <summary>
        /// Build's footer from PLC
        /// </summary>
        /// <param name="message">Message from PLC</param>
        public Footer(byte[] message)
            : this()
        {
            Array.Copy(message, message.Length - 3, m_footerBytes, 0, 3);
        }

        /// <summary>
        /// Build's footer to PLC
        /// </summary>
        /// <param name="checkSum">Message to PLC</param>
        public Footer(UInt16 checkSum)
            : this()
        {
            byte[] sum = BitConverter.GetBytes(checkSum);

            m_footerBytes[0] = sum[0];
            m_footerBytes[1] = sum[1];
            m_footerBytes[2] = BitConverter.GetBytes('\\')[0];
        }

        #endregion

        #region Properties

        public byte[] FooterBytes
        {
            get { return m_footerBytes; }
        }

        #endregion
    }
}