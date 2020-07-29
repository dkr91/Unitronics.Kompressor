using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Unitronics.ComDriver.Messages.DataRequest
{
    [Serializable]
    public class ReadDataTables : ReadWriteRequest
    {
        #region delegatas and events

        public event ProgressStatusChangedDelegate OnProgressStatusChanged;

        #endregion

        #region Locals

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        UInt16 m_NumberOfBytesToReadInRow;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        UInt32 m_RowSizeInBytes;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        UInt16 m_NumberOfRowsToRead;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        UInt32 m_StartAddress;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool m_PartOfProject = false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        int m_SubCommand = 0;

        #endregion

        #region Constructor

        public ReadDataTables()
        {
        }

        public ReadDataTables(UInt32 startAddress, UInt16 numberOfBytesToReadInRow,
            UInt16 numberOfRowsToRead, UInt16 rowSizeInBytes, bool partOfProject)
        {
            m_NumberOfBytesToReadInRow = numberOfBytesToReadInRow;
            m_NumberOfRowsToRead = numberOfRowsToRead;
            m_RowSizeInBytes = rowSizeInBytes;
            m_StartAddress = startAddress;
            m_PartOfProject = partOfProject;
        }

        public ReadDataTables(UInt32 startAddress, UInt16 numberOfBytesToReadInRow,
            UInt16 numberOfRowsToRead, UInt16 rowSizeInBytes, bool partOfProject, int subCommand)
        {
            m_NumberOfBytesToReadInRow = numberOfBytesToReadInRow;
            m_NumberOfRowsToRead = numberOfRowsToRead;
            m_RowSizeInBytes = rowSizeInBytes;
            m_StartAddress = startAddress;
            m_PartOfProject = partOfProject;
            m_SubCommand = subCommand;
        }

        #endregion

        #region Properties

        public UInt16 NumberOfBytesToReadInRow
        {
            get { return m_NumberOfBytesToReadInRow; }
            set { m_NumberOfBytesToReadInRow = value; }
        }

        public UInt32 RowSizeInBytes
        {
            get { return m_RowSizeInBytes; }
            set { m_RowSizeInBytes = value; }
        }

        public UInt16 NumberOfRowsToRead
        {
            get { return m_NumberOfRowsToRead; }
            set { m_NumberOfRowsToRead = value; }
        }

        public UInt32 StartAddress
        {
            get { return m_StartAddress; }
            set { m_StartAddress = value; }
        }

        public bool PartOfProject
        {
            /// <summary>
            /// Part Of Project means that this part of the Data Table is burned into flash
            /// <remarks>
            /// Part Of project Data table can only be read. It is being written only when the project is being downloaded
            /// </remarks>
            /// </summary>
            get { return m_PartOfProject; }
            set { m_PartOfProject = value; }
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