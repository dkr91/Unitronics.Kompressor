using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Unitronics.ComDriver.Messages.DataRequest
{
    [Serializable]
    public class WriteDataTables : ReadWriteRequest
    {
        #region delegatas and events

        public event ProgressStatusChangedDelegate OnProgressStatusChanged;

        #endregion

        #region Locals

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        UInt16 m_NumberOfBytesToWriteInRow;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        UInt32 m_RowSizeInBytes;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        UInt16 m_NumberOfRowsToWrite;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        List<byte> m_Values;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        UInt32 m_StartAddress;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        int m_SubCommand = 0;

        #endregion

        #region Constructor

        public WriteDataTables()
        {
            m_Values = new List<byte>();
        }

        public WriteDataTables(UInt32 startAddress, UInt16 numberOfBytesToWriteInRow, UInt16 numberOfRowsToWrite,
            UInt16 rowSizeInBytes, List<byte> values)
        {
            m_NumberOfBytesToWriteInRow = numberOfBytesToWriteInRow;
            m_NumberOfRowsToWrite = numberOfRowsToWrite;
            m_RowSizeInBytes = rowSizeInBytes;
            m_Values = values;
            m_StartAddress = startAddress;
        }

        public WriteDataTables(UInt32 startAddress, UInt16 numberOfBytesToWriteInRow, UInt16 numberOfRowsToWrite,
            UInt16 rowSizeInBytes, List<byte> values, int subCommand)
        {
            m_NumberOfBytesToWriteInRow = numberOfBytesToWriteInRow;
            m_NumberOfRowsToWrite = numberOfRowsToWrite;
            m_RowSizeInBytes = rowSizeInBytes;
            m_Values = values;
            m_StartAddress = startAddress;
            m_SubCommand = subCommand;
        }

        #endregion

        #region Properties

        public UInt16 NumberOfBytesToWriteInRow
        {
            get { return m_NumberOfBytesToWriteInRow; }
            set { m_NumberOfBytesToWriteInRow = value; }
        }

        public UInt32 RowSizeInBytes
        {
            get { return m_RowSizeInBytes; }
            set { m_RowSizeInBytes = value; }
        }

        public UInt16 NumberOfRowsToWrite
        {
            get { return m_NumberOfRowsToWrite; }
            set { m_NumberOfRowsToWrite = value; }
        }

        public List<byte> Values
        {
            get { return m_Values; }
            set { m_Values = value; }
        }

        public UInt32 StartAddress
        {
            get { return m_StartAddress; }
            set { m_StartAddress = value; }
        }

        public int SubCommand
        {
            get { return m_SubCommand; }
            set { m_SubCommand = value; }
        }

        internal void RaiseProgressEvent(int min, int max, RequestProgress.en_NotificationType notificationType,
            int value, string text)
        {
            RequestProgress requestProgress = new RequestProgress()
            {
                Minimum = min,
                Maximum = max,
                NotificationType = notificationType,
                Value = value,
                Text = text
            };

            if (OnProgressStatusChanged != null)
                OnProgressStatusChanged(requestProgress);
        }

        #endregion
    }
}