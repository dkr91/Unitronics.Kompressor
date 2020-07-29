using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Unitronics.ComDriver.Messages.DataRequest
{
    [Serializable]
    public class ReadOperands : ReadWriteRequest
    {
        #region Locals

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private UInt16 m_StartAddress;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private UInt16 m_numberOfOperands;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private OperandTypes m_OperandType;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private TimerValueFormat m_timerValueFormat;

        #endregion

        #region Constructor

        public ReadOperands()
        {
        }

        public ReadOperands(UInt16 numberOfOperands, OperandTypes operandType, UInt16 startAddress)
        {
            m_numberOfOperands = numberOfOperands;
            m_OperandType = operandType;
            m_StartAddress = startAddress;
            m_timerValueFormat = TimerValueFormat.None;
        }

        public ReadOperands(UInt16 numberOfOperands, OperandTypes operandType, UInt16 startAddress,
            TimerValueFormat timerValueFormat)
        {
            m_numberOfOperands = numberOfOperands;
            m_OperandType = operandType;
            m_StartAddress = startAddress;
            m_timerValueFormat = timerValueFormat;
        }

        #endregion

        #region Properties

        public UInt16 StartAddress
        {
            get { return m_StartAddress; }
            set { m_StartAddress = value; }
        }

        public UInt16 NumberOfOperands
        {
            get { return m_numberOfOperands; }
            set { m_numberOfOperands = value; }
        }

        public OperandTypes OperandType
        {
            get { return m_OperandType; }
            set { m_OperandType = value; }
        }

        public TimerValueFormat TimerValueFormat
        {
            get { return m_timerValueFormat; }
            set { m_timerValueFormat = value; }
        }

        #endregion
    }
}