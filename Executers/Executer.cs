using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver.Command;
using System.Runtime.Serialization;
using Unitronics.ComDriver.Messages;
using Unitronics.ComDriver.Messages.DataRequest;

namespace Unitronics.ComDriver
{
    abstract class Executer
    {
        #region Locals

        private readonly Channel m_channel;
        private int m_unitId;
        private readonly PlcVersion m_plcVersion;
        private bool m_BreakFlag;

        internal Guid PlcGuid { set; get; }

        #endregion

        #region Constructor

        public Executer(int unitId, Channel channel, PlcVersion plcVersion, Guid plcGuid)
        {
            m_channel = channel;
            m_unitId = unitId;
            m_plcVersion = plcVersion;
            m_BreakFlag = false;
            PlcGuid = plcGuid;
        }

        #endregion

        #region Properties

        protected Channel Channel
        {
            get { return m_channel; }
        }

        protected int UnitId
        {
            get { return m_unitId; }
        }

        protected PlcVersion PLCVersion
        {
            get { return m_plcVersion; }
        }

        public bool BreakFlag
        {
            get { return m_BreakFlag; }
            set
            {
                if (value == true)
                    m_channel.AbortSend(PlcGuid);

                m_BreakFlag = value;
            }
        }

        #endregion

        #region Internal

        internal void SetNewUnitId(int unitId)
        {
            this.m_unitId = unitId;
        }

        #endregion

        #region Abstract

        internal abstract void PerformReadWrite(ref ReadWriteRequest[] values, string parentID,
            bool suppressEthernetHeader);

        #endregion
    }
}