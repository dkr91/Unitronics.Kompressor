using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver.Messages.ASCIIMessage;
using System.Collections;

namespace Unitronics.ComDriver.Messages.ASCIIMessage
{
    public class SetUnitIdMessage : AbstractASCIIMessage
    {
        #region Constructors

        public SetUnitIdMessage() : base()
        {
        }

        public SetUnitIdMessage(int unitId, string commandCode, IEnumerable<object> values)
            : base(unitId, commandCode, values)
        {
        }

        #endregion

        #region Public

        public override string GetMessage(TimerValueFormat timerValueFormat)
        {
            StringBuilder sb = new StringBuilder();
            StringBuilder stringValues = new StringBuilder();

            IEnumerator enumerator = m_values.GetEnumerator();
            while (enumerator.MoveNext())
            {
                int value = Convert.ToInt32(enumerator.Current);
                string hex = value.ToString("X").PadLeft(2, '0');
                stringValues.Append(hex);
            }

            sb.Append(STX);
            sb.Append(UnitId);
            sb.Append(AsciiCommandCode);
            sb.Append(stringValues.ToString());
            sb.Append(CrcOf(stringValues.ToString()));
            sb.Append(ETX);

            return sb.ToString();
        }

        public override AbstractASCIIMessage GetMessage(string message)
        {
            int index = 0;

            index += Utils.Lengths.LENGTH_STX1;
            int unitId = Convert.ToInt32(message.Substring(index, Utils.Lengths.LENGTH_UNIT_ID), 16);

            index += Utils.Lengths.LENGTH_UNIT_ID;
            string commandCode = message.Substring(index, Utils.Lengths.LENGTH_COMMAND_CODE);

            index += Utils.Lengths.LENGTH_COMMAND_CODE;
            string crc = message.Substring(index, Utils.Lengths.LENGTH_CRC);

            return this;
        }

        #endregion

        #region Private

        private string CrcOf(string stringValues)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(UnitId);
            sb.Append(AsciiCommandCode);
            sb.Append(stringValues);

            byte[] bytes = ASCIIEncoding.ASCII.GetBytes(sb.ToString());
            int sum = bytes.Sum(x => (int) x);
            string crcHexValue = (sum % 256).ToString("X");
            return crcHexValue.PadLeft(Utils.Lengths.LENGTH_CRC, '0');
        }

        #endregion
    }
}