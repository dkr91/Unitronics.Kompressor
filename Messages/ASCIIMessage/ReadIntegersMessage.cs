using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver.Messages;
using Unitronics.ComDriver;
using System.Globalization;

namespace Unitronics.ComDriver.Messages.ASCIIMessage
{
    class ReadIntegersMessage : AbstractASCIIMessage
    {
        #region Locals

        private int m_typeLength;

        #endregion

        #region Constructor

        public ReadIntegersMessage()
            : base()
        {
        }

        public ReadIntegersMessage(int unitId, string commandCode, int? address, int? length, int typeLength)
            : base(unitId, commandCode, address, length)
        {
            m_typeLength = typeLength;
        }

        #endregion

        #region Public

        public override AbstractASCIIMessage GetMessage(string message)
        {
            ArrayValues = GetValuesFromMessage(message).ToArray();

            return this;
        }

        #endregion

        #region Private

        private List<object> GetValuesFromMessage(string message)
        {
            int index = 0;
            List<object> values = new List<object>();

            index += Utils.Lengths.LENGTH_STX1;
            int unitId = Convert.ToInt32(message.Substring(index, Utils.Lengths.LENGTH_UNIT_ID), 16);

            index += Utils.Lengths.LENGTH_UNIT_ID;
            string commandCode = message.Substring(index, Utils.Lengths.LENGTH_COMMAND_CODE);

            int valuesLength = m_length.Value * (m_typeLength / 4);
            index += Utils.Lengths.LENGTH_COMMAND_CODE;

            switch (commandCode)
            {
                case CommandCode.ReadIntegers.GX: //COUNTER_CURRENT
                case CommandCode.ReadIntegers.GY: //COUNTER_PRESET
                case CommandCode.ReadIntegers.GF: //SI
                case CommandCode.ReadIntegers.RW: //MI
                    for (int i = 0; i < valuesLength; i += m_typeLength / 4)
                    {
                        values.Add(Int16.Parse(message.Substring(index, m_typeLength / 4), NumberStyles.HexNumber));
                        index += m_typeLength / 4;
                    }

                    break;
                case "RZ": //Fast Operands
                {
                    switch (m_commandCode) //sent CommandCode
                    {
                        case CommandCode.ReadIntegers.RZI: //XI
                        {
                            for (int i = 0; i < valuesLength; i += m_typeLength / 4)
                            {
                                values.Add(Int16.Parse(message.Substring(index, m_typeLength / 4),
                                    NumberStyles.HexNumber));
                                index += m_typeLength / 4;
                            }

                            break;
                        }
                        case CommandCode.ReadIntegers.RZL: //XL
                        {
                            for (int i = 0; i < valuesLength; i += m_typeLength / 4)
                            {
                                values.Add(Int32.Parse(message.Substring(index, m_typeLength / 4),
                                    NumberStyles.HexNumber));
                                index += m_typeLength / 4;
                            }

                            break;
                        }
                        case CommandCode.ReadIntegers.RZD: //XDW
                        {
                            for (int i = 0; i < valuesLength; i += m_typeLength / 4)
                            {
                                values.Add(UInt32.Parse(message.Substring(index, m_typeLength / 4),
                                    NumberStyles.HexNumber));
                                index += m_typeLength / 4;
                            }

                            break;
                        }
                    }
                }
                    break;
                case "RN":
                    switch (m_commandCode) //sent CommandCode
                    {
                        case CommandCode.ReadIntegers.RNJ: //SDW
                        case CommandCode.ReadIntegers.RND: //DW
                        {
                            for (int i = 0; i < valuesLength; i += m_typeLength / 4)
                            {
                                values.Add(UInt32.Parse(message.Substring(index, m_typeLength / 4),
                                    NumberStyles.HexNumber));
                                index += m_typeLength / 4;
                            }
                        }
                            break;
                        case CommandCode.ReadIntegers.RNH: //SL
                        case CommandCode.ReadIntegers.RNL: //ML
                            for (int i = 0; i < valuesLength; i += m_typeLength / 4)
                            {
                                values.Add(Int32.Parse(message.Substring(index, m_typeLength / 4),
                                    NumberStyles.HexNumber));
                                index += m_typeLength / 4;
                            }

                            break;
                        case CommandCode.ReadIntegers.RNF: //MF
                            for (int i = 0; i < valuesLength; i += m_typeLength / 4)
                            {
                                string hexValueIEEE754 = (message.Substring(index, m_typeLength / 4));
                                string actualHexValue =
                                    hexValueIEEE754.Substring(4, 4) + hexValueIEEE754.Substring(0, 4);
                                values.Add(Utils.HexEncoding.ConvertHexToSingle(actualHexValue));
                                index += m_typeLength / 4;
                            }

                            break;
                    }

                    break;

                case CommandCode.ReadIntegers.GT: //TIMER_CURRENT
                case CommandCode.ReadIntegers.GP: //TIMER_PRESET
                    if (TimerValueFormat.Equals(TimerValueFormat.TimeFormat))
                    {
                        for (int i = 0; i < valuesLength; i += m_typeLength / 4)
                        {
                            values.Add(Utils.z_GetTimeUnits(Int32.Parse(message.Substring(index, m_typeLength / 4),
                                NumberStyles.HexNumber)));
                            index += m_typeLength / 4;
                        }
                    }
                    else
                    {
                        //SecondsFormat
                        for (int i = 0; i < valuesLength; i += m_typeLength / 4)
                        {
                            values.Add(UInt32.Parse(message.Substring(index, m_typeLength / 4),
                                NumberStyles.HexNumber));
                            index += m_typeLength / 4;
                        }
                    }

                    break;
            }

            return values;
        }

        #endregion
    }
}