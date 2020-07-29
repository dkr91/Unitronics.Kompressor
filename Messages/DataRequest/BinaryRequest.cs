using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Unitronics.ComDriver.Messages.DataRequest
{
    [Serializable]
    public class BinaryRequest : ReadWriteRequest
    {
        public enum ePlcReceiveResult
        {
            HeaderChecksumError = 1, // Software Bug
            DataChecksumError = 2, // Software Bug
            SequenceError = 3, // Software Bug
            MemoryCommandsDisabled = 4, // Software Bug
            FlashIsNotIdle = 5,
            InvalidAddress = 6, // Software Bug
            FlashError = 7, // Flash hardware problem
            WrongPassword = 10,
            PlcBlocked = 11,
            Illegal_MemoryMap = 12,
            Illegal_Length = 13,
            Sucsess = 100,
            Unknown = 0xFF,
        }

        #region delegatas and events

        [field: NonSerialized] public event ProgressStatusChangedDelegate OnProgressStatusChanged;

        #endregion

        #region Locals

        byte[] m_OutgoingBuffer = new byte[0];
        byte[] m_IncomingBuffer;
        int m_CommandCode = 0;
        int m_Address = 0;
        int m_MessageKey = 0;
        int m_CycledMessageKey = 0; // The message key that should be set after the message key want to 256.
        int m_SubCommand = 0;
        int m_ElementsCount = 0;
        int m_ChunkSizeAlignment = 0;
        int m_FlashBankSize = 0;
        bool m_WaitForIdle = false;
        private ePlcReceiveResult plcReceiveResult;
        private int m_DecodeValue;

        #endregion

        #region Constructor

        public BinaryRequest()
        {
        }

        public BinaryRequest(byte[] binaryData, int commandCode, int address, int elementsCount, ref int messageKey,
            int subCommand, bool waitForIdle, int ChunkSizeAlignment, int FlashBankSize)
        {
            m_OutgoingBuffer = binaryData;
            m_CommandCode = commandCode;
            m_Address = address;
            m_MessageKey = messageKey;
            m_SubCommand = subCommand;
            m_ElementsCount = elementsCount;
            m_WaitForIdle = waitForIdle;
            m_ChunkSizeAlignment = ChunkSizeAlignment;
            m_FlashBankSize = FlashBankSize;
        }

        #endregion

        #region Properties

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

        public byte[] OutgoingBuffer
        {
            get { return m_OutgoingBuffer; }
            set { m_OutgoingBuffer = value; }
        }

        public byte[] IncomingBuffer
        {
            get { return m_IncomingBuffer; }
            set { m_IncomingBuffer = value; }
        }

        public int CommandCode
        {
            get { return m_CommandCode; }
            set { m_CommandCode = value; }
        }

        public int Address
        {
            get { return m_Address; }
            set { m_Address = value; }
        }

        public int MessageKey
        {
            get { return m_MessageKey; }
            set { m_MessageKey = value; }
        }

        public int SubCommand
        {
            get { return m_SubCommand; }
            set { m_SubCommand = value; }
        }

        public int ElementsCount
        {
            get { return m_ElementsCount; }
            set { m_ElementsCount = value; }
        }

        public bool WaitForIdle
        {
            get { return m_WaitForIdle; }
            set { m_WaitForIdle = value; }
        }

        public int ChunkSizeAlignment
        {
            get { return m_ChunkSizeAlignment; }
            set { m_ChunkSizeAlignment = value; }
        }

        public int FlashBankSize
        {
            get { return m_FlashBankSize; }
            set { m_FlashBankSize = value; }
        }

        public ePlcReceiveResult PlcReceiveResult
        {
            get { return plcReceiveResult; }
            internal set { plcReceiveResult = value; }
        }

        public int CycledMessageKey
        {
            get { return m_CycledMessageKey; }
            set { m_CycledMessageKey = value; }
        }

        public int DecodeValue
        {
            get { return m_DecodeValue; }
            set { m_DecodeValue = value; }
        }

        internal bool IsInternal { get; set; }

        #endregion
    }
}