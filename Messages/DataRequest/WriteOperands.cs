using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver.Messages.DataRequest;
using System.Diagnostics;

namespace Unitronics.ComDriver.Messages.DataRequest
{
    [Serializable]
    public class WriteOperands : ReadWriteRequest
    {
        #region Locals

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private object[] m_Values;

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

        public WriteOperands()
        {
        }

        public WriteOperands(UInt16 numberOfOperands, OperandTypes operandType, UInt16 startAddress, object[] values)
        {
            m_numberOfOperands = numberOfOperands;
            m_OperandType = operandType;
            m_StartAddress = startAddress;
            m_Values = values;
            m_timerValueFormat = TimerValueFormat.None;
        }

        public WriteOperands(UInt16 numberOfOperands, OperandTypes operandType, UInt16 startAddress, object[] values,
            TimerValueFormat timerValueFormat)
        {
            m_numberOfOperands = numberOfOperands;
            m_OperandType = operandType;
            m_StartAddress = startAddress;
            m_Values = values;
            m_timerValueFormat = timerValueFormat;
        }

        #endregion

        #region Properties

        public object[] Values
        {
            get { return m_Values; }
            set { m_Values = value; }
        }

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