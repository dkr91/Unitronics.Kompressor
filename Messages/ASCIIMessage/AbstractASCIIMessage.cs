using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using Unitronics.ComDriver;
using Unitronics.ComDriver.Messages;

namespace Unitronics.ComDriver.Messages.ASCIIMessage
{
    public abstract class AbstractASCIIMessage : IMessage
    {
        #region Locals

        protected int m_unitId;
        protected string m_commandCode = String.Empty;
        protected int? m_address;
        protected int? m_length;
        protected IEnumerable<object> m_values;
        private TimerValueFormat m_TimerValueFormat;

        #endregion

        #region Constructor

        public AbstractASCIIMessage()
        {
        }

        public AbstractASCIIMessage(int unitId, string commandCode)
            : this()
        {
            m_unitId = unitId;
            m_commandCode = commandCode;
            OperandType = commandCode.GetOperandTypeNameByCommandCode();
        }

        public AbstractASCIIMessage(int unitId, string commandCode, IEnumerable<object> values)
            : this(unitId, commandCode)
        {
            m_values = values;
        }

        public AbstractASCIIMessage(int unitId, string commandCode, int? address, int? length)
            : this(unitId, commandCode)
        {
            m_address = address;
            m_length = length;
        }

        public AbstractASCIIMessage(int unitId, string commandCode, int? address, int? length,
            IEnumerable<object> values)
            : this(unitId, commandCode, address, length)
        {
            m_address = address;
            m_length = length;
            m_values = values;
        }

        #endregion

        #region Public

        public abstract AbstractASCIIMessage GetMessage(string message);

        public virtual string GetMessage(TimerValueFormat timerValueFormat)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(STX);
            sb.Append(UnitId);
            sb.Append(AsciiCommandCode);
            sb.Append(Address);
            sb.Append(Length);
            sb.Append(Values);
            sb.Append(CRC);
            sb.Append(ETX);

            return sb.ToString();
        }

        #endregion

        #region Private

        public string GetString(IEnumerable<object> objects)
        {
            if (objects == null) return String.Empty;

            StringBuilder sb = new StringBuilder();
            IEnumerator<object> enumerator = objects.GetEnumerator();
            while (enumerator.MoveNext())
                sb.Append(enumerator.Current.ToString());

            return sb.ToString();
        }

        #endregion

        #region Properties

        public IEnumerable<object> ArrayValues
        {
            get { return m_values; }
            set { m_values = value; }
        }

        /// <summary>
        /// Hex value of UnitId
        /// </summary>
        public string UnitId
        {
            get { return m_unitId.ToString("X").PadLeft(Utils.Lengths.LENGTH_UNIT_ID, '0'); }
        }

        public string OperandType { get; set; }

        public string AsciiCommandCode
        {
            get
            {
                if (m_commandCode == null) return String.Empty;
                return m_commandCode.Substring(0, m_commandCode.Length); //m_commandCode.Substring(0,2)
            }
        }

        /// <summary>
        /// Hex value of Address
        /// </summary>
        public string Address
        {
            get
            {
                if (m_address == null) return String.Empty;
                return m_address.Value.ToString("X").PadLeft(Utils.Lengths.LENGTH_ADDRESS, '0');
            }
        }

        /// <summary>
        /// Hex value of Length
        /// </summary>
        public string Length
        {
            get
            {
                if (m_length == null) return String.Empty;
                return m_length.Value.ToString("X").PadLeft(Utils.Lengths.LENGTH_LENGTH, '0');
            }
        }

        /// <summary>
        /// String values of values
        /// </summary>
        public string Values
        {
            get { return GetString(m_values); }
        }

        /// <summary>
        /// STX="/"
        /// </summary>
        public string STX
        {
            get { return "/"; }
        }

        /// <summary>
        /// STX="/A"
        /// </summary>
        public string STX1
        {
            get { return "/A"; }
        }

        /// <summary>
        /// ETX = CR (Carriage Return, ASCII value: 13)
        /// </summary>
        public string ETX
        {
            get { return Utils.General.ETX; }
        }

        public string CRC
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(UnitId);
                sb.Append(AsciiCommandCode);
                sb.Append(Address);
                sb.Append(Length);
                sb.Append(Values);

                byte[] bytes = ASCIIEncoding.ASCII.GetBytes(sb.ToString());
                int sum = bytes.Sum(x => (int) x);
                string crcHexValue = (sum % 256).ToString("X");
                return crcHexValue.PadLeft(Utils.Lengths.LENGTH_CRC, '0');
            }
        }

        public TimerValueFormat TimerValueFormat
        {
            get { return m_TimerValueFormat; }
            set { m_TimerValueFormat = value; }
        }

        #endregion
    }
}