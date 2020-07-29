using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Globalization;

namespace Unitronics.ComDriver.Messages.ASCIIMessage
{
    class ReadBitsMessage : AbstractASCIIMessage
    {
        #region Constructor

        public ReadBitsMessage()
            : base()
        {
        }

        public ReadBitsMessage(int unitId, string commandCode, int? address, int? length)
            : base(unitId, commandCode, address, length)
        {
        }

        #endregion

        #region Public

        public override AbstractASCIIMessage GetMessage(string message)
        {
            List<object> values = new List<object>();
            char[] charBits = message.Substring(6, m_length.Value).ToCharArray();
            foreach (char c in charBits)
            {
                values.Add(Int16.Parse(c.ToString(), NumberStyles.HexNumber));
            }

            ArrayValues = values.ToArray();

            return this;
        }

        #endregion
    }
}