using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using Unitronics.ComDriver;
using Unitronics.ComDriver.Messages;

namespace Unitronics.ComDriver.Messages.ASCIIMessage
{
    class WriteIntegersMessage : AbstractASCIIMessage
    {
        #region Locals

        private int m_typeLength;

        #endregion

        #region Constructor

        public WriteIntegersMessage()
            : base()
        {
        }

        /// <summary>
        /// SW, SNL/D/H/J, SF - Write integers (16 and 32 bits)
        /// </summary>
        /// <param name="unitId">PLC's UnitID</param>
        /// <param name="commandCode">Command Code</param>
        /// <param name="address">Start address</param>
        /// <param name="length">Number of registers for write</param>
        /// <param name="values">Values for write</param>
        /// <param name="typeLength">Operand type length (16 or 32 bits)</param>
        public WriteIntegersMessage(int unitId, string commandCode, int? address, int? length,
            IEnumerable<object> values, int typeLength)
            : base(unitId, commandCode, address, length, values)
        {
            m_typeLength = typeLength;
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
                switch (OperandType)
                {
                    case Utils.Operands.OP_XDW:
                    case Utils.Operands.OP_DW:
                        UInt32 convertedInt32Value = Convert.ToUInt32(enumerator.Current);
                        string hexDW = convertedInt32Value.ToString("X").PadLeft(m_typeLength / 4, '0');
                        stringValues.Append(hexDW);
                        break;
                    case Utils.Operands.OP_XL:
                    case Utils.Operands.OP_ML:
                        Int32 convertedValue = Convert.ToInt32(enumerator.Current);
                        string hex = convertedValue.ToString("X").PadLeft(m_typeLength / 4, '0');
                        stringValues.Append(hex);
                        break;
                    case Utils.Operands.OP_SI:
                    case Utils.Operands.OP_MI:
                    case Utils.Operands.OP_COUNTER_PRESET:
                    case Utils.Operands.OP_COUNTER_CURRENT:
                    case Utils.Operands.OP_XI:
                        Int16 convertedInt16Value = Convert.ToInt16(enumerator.Current);
                        string hexMI = convertedInt16Value.ToString("X").PadLeft(m_typeLength / 4, '0');
                        stringValues.Append(hexMI);
                        break;
                    case Utils.Operands.OP_MF:
                        string tmpHex = Utils.HexEncoding.ConvertSingleToHex(Convert.ToSingle(enumerator.Current))
                            .PadRight(8, '0');
                        //Used for IEEE 754 standard. 
                        string hexF = tmpHex.Substring(4, 4) + tmpHex.Substring(0, 4);
                        stringValues.Append(hexF);
                        break;
                    case Utils.Operands.OP_TIMER_PRESET:
                    case Utils.Operands.OP_TIMER_CURRENT:
                        UInt32 convertedUInt32Value =
                            timerValueFormat.Equals(TimerValueFormat.TimeFormat)
                                ? Utils.z_GetSecondsValue(enumerator.Current as List<ushort>)
                                : Convert.ToUInt32(enumerator.Current);
                        string hexUint32 = convertedUInt32Value.ToString("X").PadLeft(m_typeLength / 4, '0');
                        stringValues.Append(hexUint32);
                        break;
                }
            }

            sb.Append(STX);
            sb.Append(UnitId);
            sb.Append(AsciiCommandCode);
            sb.Append(Address);
            sb.Append(Length);
            sb.Append(stringValues.ToString());
            //sb.Append(CRC);
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
            sb.Append(Address);
            sb.Append(Length);
            sb.Append(stringValues);

            byte[] bytes = ASCIIEncoding.ASCII.GetBytes(sb.ToString());
            int sum = bytes.Sum(x => (int) x);
            string crcHexValue = (sum % 256).ToString("X");
            return crcHexValue.PadLeft(Utils.Lengths.LENGTH_CRC, '0');
        }

        #endregion
    }
}