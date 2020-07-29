using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Unitronics.ComDriver
{
    internal class ComDriverLogEntry
    {
        #region Locals

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private uint m_Id;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private DateTime m_DateTime;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string m_LoggerChannel;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MessageDirection m_MessageDirection;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string m_CurrentRetry;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private object m_Message;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private LogType m_LogType;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string m_Text;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string m_RequestGuid;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string m_ParentID;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string m_RequestPropertiesId;

        #endregion

        #region Constructor

        internal ComDriverLogEntry()
        {
        }

        internal ComDriverLogEntry(uint _id, DateTime _dateTime, string _loggerChannel,
            MessageDirection _channelDirection, string _currentRetry, object _message)
        {
            m_Id = _id;
            m_DateTime = _dateTime;
            m_LoggerChannel = _loggerChannel;
            m_MessageDirection = _channelDirection;
            m_CurrentRetry = _currentRetry;
            m_Message = _message;
        }

        #endregion

        #region Properties

        internal uint Id
        {
            get { return m_Id; }
            set { m_Id = value; }
        }

        internal DateTime DateTime
        {
            get { return m_DateTime; }
            set { m_DateTime = value; }
        }

        internal string LoggerChannel
        {
            get { return m_LoggerChannel; }
            set { m_LoggerChannel = value; }
        }

        internal MessageDirection MessageDirection
        {
            get { return m_MessageDirection; }
            set { m_MessageDirection = value; }
        }

        internal string CurrentRetry
        {
            get { return m_CurrentRetry; }
            set { m_CurrentRetry = value; }
        }

        internal object Message
        {
            get { return m_Message; }
            set { m_Message = value; }
        }

        internal LogType LogType
        {
            get { return m_LogType; }
            set { m_LogType = value; }
        }

        internal string Text
        {
            get { return m_Text; }
            set { m_Text = value; }
        }

        internal string RequestGUID
        {
            get { return m_RequestGuid; }
            set { m_RequestGuid = value; }
        }

        internal string ParentID
        {
            get { return m_ParentID; }
            set { m_ParentID = value; }
        }

        public string RequestPropertiesId
        {
            get { return m_RequestPropertiesId; }
            set { m_RequestPropertiesId = value; }
        }

        #endregion
    }
}