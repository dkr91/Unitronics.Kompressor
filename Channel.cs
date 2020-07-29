using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Threading;
using Unitronics.ComDriver.Messages;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Unitronics.ComDriver
{
    // Please remember to add new channel types to the xml include if you want to be
    // able to serialize and deserialize it later... Very Important!!!
    [Serializable()]
    [System.Xml.Serialization.XmlInclude(typeof(Serial))]
    [System.Xml.Serialization.XmlInclude(typeof(Ethernet))]
    [System.Xml.Serialization.XmlInclude(typeof(EthernetListener))]
    [System.Xml.Serialization.XmlInclude(typeof(ListenerServer))]
    public abstract class Channel
    {
        #region Locals

        private int m_retry;
        private int m_TimeOut; // in miliseconds
        private Queue<MessageRecord> messageQueue;
        private bool m_AlreadyInitialized = false;
        private volatile bool m_threadIsRunning = false;
        private string m_Guid;
        private Thread QueueIteratorThread = null;
        private bool abortRetries = false;

        #endregion

        #region Constructor

        public Channel()
        {
            m_retry = Config.Retry;
            m_TimeOut = Config.TimeOut;
            messageQueue = new Queue<MessageRecord>();
            m_Guid = Guid.NewGuid().ToString();
        }

        public Channel(int retry, int timeOut)
            : this()
        {
            m_retry = retry;
            m_TimeOut = timeOut;
        }

        #endregion

        #region Destructor

        ~Channel()
        {
            this.Disconnect();
        }

        #endregion

        #region Public

        public delegate void ChannelRetryDelegate(int retryIndex, Exception exception);

        public event ChannelRetryDelegate OnRetry;

        internal void Send(byte[] bytes, ReceiveBytesDelegate receiveBytesDelegate, GuidClass messageGuid,
            string parentID, string description, Guid plcGuid)
        {
            MessageRecord message = new MessageRecord()
            {
                MessageGuid = messageGuid,
                PlcGuid = plcGuid,
                ParentID = parentID,
                UnitId = GetUnitIdFromBinaryMessage(bytes),
                IsSent = false,
                MessageRequest = bytes,
                RemainingRetries = m_retry,
                Description = description,
                ReceiveBytesDelegate = new ReceiveBytesDelegate(receiveBytesDelegate),
                ReceiveStringDelegate = null
            };
            lock (messageQueue)
            {
                abortRetries = false;
                messageQueue.Enqueue(message);
                messageQueue_Changed();
            }
        }

        internal void Send(byte[] bytes, ReceiveBytesDelegate receiveBytesDelegate, GuidClass messageGuid,
            string parentID, string description, Guid plcGuid, byte messageEnumerator)
        {
            MessageRecord message = new MessageRecord()
            {
                MessageGuid = messageGuid,
                PlcGuid = plcGuid,
                ParentID = parentID,
                UnitId = GetUnitIdFromBinaryMessage(bytes),
                IsSent = false,
                MessageRequest = bytes,
                RemainingRetries = m_retry,
                Description = description,
                ReceiveBytesDelegate = new ReceiveBytesDelegate(receiveBytesDelegate),
                ReceiveStringDelegate = null,
                MessageEnumerator = messageEnumerator
            };
            lock (messageQueue)
            {
                abortRetries = false;
                messageQueue.Enqueue(message);
                messageQueue_Changed();
            }
        }

        internal void Send(string text, ReceiveStringDelegate receiveStringDelegate, GuidClass messageGuid,
            string parentID, string description, Guid plcGuid, bool isIdMessage = false,
            bool suppressEthernetHeader = false)
        {
            MessageRecord message = new MessageRecord()
            {
                MessageGuid = messageGuid,
                PlcGuid = plcGuid,
                ParentID = parentID,
                UnitId = GetUnitIdFromStringMessage(text),
                IsSent = false,
                MessageRequest = text,
                RemainingRetries = m_retry,
                Description = description,
                IsIdMessage = isIdMessage,
                SuppressEthernetHeader = suppressEthernetHeader,
                ReceiveBytesDelegate = null,
                ReceiveStringDelegate = new ReceiveStringDelegate(receiveStringDelegate)
            };
            lock (messageQueue)
            {
                abortRetries = false;
                messageQueue.Enqueue(message);
                messageQueue_Changed();
            }
        }

        internal void AbortSend(Guid plcGuid)
        {
            lock (messageQueue)
            {
                var abortedMessages = (from val in messageQueue
                    where val.PlcGuid == plcGuid
                    select val).ToList();

                foreach (MessageRecord messageRecord in abortedMessages)
                {
                    if (messageRecord.ReceiveStringDelegate != null)
                    {
                        messageRecord.ReceiveStringDelegate(null, CommunicationException.Timeout,
                            messageRecord.MessageGuid);
                    }
                    else
                    {
                        messageRecord.ReceiveBytesDelegate(null, CommunicationException.Timeout,
                            messageRecord.MessageGuid);
                    }
                }

                var remainedMessages = (from val in messageQueue
                    where val.PlcGuid != plcGuid
                    select val).ToList();


                messageQueue.Clear();
                foreach (MessageRecord messageRecord in remainedMessages)
                {
                    messageQueue.Enqueue(messageRecord);
                }
            }
        }

        #endregion

        #region Private

        private void messageQueue_Changed()
        {
            lock (messageQueue)
            {
                abortRetries = false;
                if (!m_threadIsRunning)
                {
                    m_threadIsRunning = true;
                    //QueueIteratorThread = new Thread(SendQueueIterator);
                    ThreadPool.QueueUserWorkItem(SendQueueIterator);
                    //QueueIteratorThread.Start();
                }
            }
        }

        private void SendQueueIterator(Object objectState)
        {
            while (queueHasItems())
            {
                MessageRecord messsage = null;
                lock (messageQueue)
                {
                    if (messageQueue.Any())
                    {
                        abortRetries = false;
                        messsage = messageQueue.Dequeue();
                    }
                }

                if (messsage != null && !messsage.IsSent && messsage.MessageResponse == null)
                {
                    int retry = m_retry;
                    bool plcReplyReceived = false;
                    if (messsage.ReceiveStringDelegate != null)
                    {
                        while (!plcReplyReceived && retry > 0)
                        {
                            try
                            {
                                SendString(messsage.MessageRequest as string, messsage.MessageEnumerator,
                                    messsage.IsIdMessage, messsage.SuppressEthernetHeader);
                                //Log the requests
                                if (ComDriverLogger.Enabled)
                                {
                                    string retryLog =
                                        Utils.HelperComDriverLogger.GetLoggerCurrentRetry(m_retry - retry + 1, m_retry);

                                    ComDriverLogger.LogFullMessage(DateTime.Now, GetLoggerChannelText(),
                                        messsage.MessageGuid.ToString(), MessageDirection.Sent,
                                        retryLog, messsage.MessageRequest as string, messsage.ParentID,
                                        messsage.Description);
                                }

                                try
                                {
                                    messsage.MessageResponse = ReceiveString();
                                    plcReplyReceived = true;

                                    //Log the requests
                                    if (ComDriverLogger.Enabled)
                                    {
                                        string retryLog =
                                            Utils.HelperComDriverLogger.GetLoggerCurrentRetry(m_retry - retry + 1,
                                                m_retry);

                                        ComDriverLogger.LogFullMessage(DateTime.Now, GetLoggerChannelText(),
                                            messsage.MessageGuid.ToString(), MessageDirection.Received,
                                            retryLog, messsage.MessageResponse, messsage.ParentID,
                                            messsage.Description);
                                    }
                                }
                                catch (TimeoutException ex)
                                {
                                    if (abortRetries)
                                    {
                                        retry = 1;
                                    }

                                    if (OnRetry != null)
                                        OnRetry((m_retry - retry) + 1, ex);
                                    retry--;
                                    if (this is Serial)
                                    {
                                        try
                                        {
                                            bool channelInitialized = AlreadyInitialized;
                                            Disconnect();
                                            Connect();
                                            AlreadyInitialized = channelInitialized;
                                        }
                                        catch
                                        {
                                        }
                                    }
                                }
                                catch (ComDriveExceptions ex)
                                {
                                    if (abortRetries)
                                    {
                                        retry = 1;
                                    }

                                    if (OnRetry != null)
                                        OnRetry((m_retry - retry) + 1, ex);
                                    retry--;
                                    if (this is Serial)
                                    {
                                        try
                                        {
                                            bool channelInitialized = AlreadyInitialized;
                                            Disconnect();
                                            Connect();
                                            AlreadyInitialized = channelInitialized;
                                        }
                                        catch
                                        {
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                if (abortRetries)
                                {
                                    retry = 1;
                                }

                                string exceptionText =
                                    ex.GetType().ToString() + ": " + ex.Message + "\n\n" + ex.StackTrace;
                                ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
                                if (OnRetry != null)
                                    OnRetry((m_retry - retry) + 1, ex);
                                retry--;
                                if (this is Serial)
                                {
                                    try
                                    {
                                        bool channelInitialized = AlreadyInitialized;
                                        Disconnect();
                                        Connect();
                                        AlreadyInitialized = channelInitialized;
                                    }
                                    catch
                                    {
                                    }
                                }
                            }

                            if (PLCFactory.MessageDelay != 0)
                            {
                                System.Threading.Thread.Sleep(PLCFactory.MessageDelay);
                            }
                        }

                        if (retry <= 0)
                        {
                            messsage.ReceiveStringDelegate(null, CommunicationException.Timeout, messsage.MessageGuid);
                        }
                        else
                        {
                            messsage.ReceiveStringDelegate(messsage.MessageResponse as string,
                                CommunicationException.None, messsage.MessageGuid);
                        }
                    }
                    else
                    {
                        while (!plcReplyReceived && retry > 0)
                        {
                            try
                            {
                                SendBytes(messsage.MessageRequest as byte[], messsage.MessageEnumerator);
                                string retryLog =
                                    Utils.HelperComDriverLogger.GetLoggerCurrentRetry(m_retry - retry + 1, m_retry);

                                //Log the requests
                                if (ComDriverLogger.Enabled)
                                {
                                    ComDriverLogger.LogFullMessage(DateTime.Now, GetLoggerChannelText(),
                                        messsage.MessageGuid.ToString(), MessageDirection.Sent,
                                        retryLog, messsage.MessageRequest as byte[], messsage.ParentID,
                                        messsage.Description);
                                }

                                try
                                {
                                    messsage.MessageResponse = ReceiveBytes();
                                    plcReplyReceived = true;

                                    //Log the requests
                                    if (ComDriverLogger.Enabled)
                                    {
                                        ComDriverLogger.LogFullMessage(DateTime.Now, GetLoggerChannelText(),
                                            messsage.MessageGuid.ToString(), MessageDirection.Received,
                                            retryLog, messsage.MessageResponse, messsage.ParentID,
                                            messsage.Description);
                                    }
                                }
                                catch (TimeoutException ex)
                                {
                                    if (abortRetries)
                                    {
                                        retry = 1;
                                    }

                                    if (OnRetry != null)
                                        OnRetry((m_retry - retry) + 1, ex);
                                    retry--;
                                    if (this is Serial)
                                    {
                                        try
                                        {
                                            bool channelInitialized = AlreadyInitialized;
                                            Disconnect();
                                            Connect();
                                            AlreadyInitialized = channelInitialized;
                                        }
                                        catch
                                        {
                                        }
                                    }
                                }
                                catch (ComDriveExceptions ex)
                                {
                                    if (abortRetries)
                                    {
                                        retry = 1;
                                    }

                                    if (OnRetry != null)
                                        OnRetry((m_retry - retry) + 1, ex);
                                    retry--;
                                    if (this is Serial)
                                    {
                                        try
                                        {
                                            bool channelInitialized = AlreadyInitialized;
                                            Disconnect();
                                            Connect();
                                            AlreadyInitialized = channelInitialized;
                                        }
                                        catch
                                        {
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                if (abortRetries)
                                {
                                    retry = 1;
                                }

                                string exceptionText =
                                    ex.GetType().ToString() + ": " + ex.Message + "\n\n" + ex.StackTrace;
                                ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
                                if (OnRetry != null)
                                    OnRetry((m_retry - retry) + 1, ex);
                                retry--;
                                if (this is Serial)
                                {
                                    try
                                    {
                                        bool channelInitialized = AlreadyInitialized;
                                        Disconnect();
                                        Connect();
                                        AlreadyInitialized = channelInitialized;
                                    }
                                    catch
                                    {
                                    }
                                }
                            }

                            if (PLCFactory.MessageDelay != 0)
                            {
                                System.Threading.Thread.Sleep(PLCFactory.MessageDelay);
                            }
                        }

                        if (retry <= 0)
                        {
                            messsage.ReceiveBytesDelegate(null, CommunicationException.Timeout, messsage.MessageGuid);
                        }
                        else
                        {
                            messsage.ReceiveBytesDelegate(messsage.MessageResponse as byte[],
                                CommunicationException.None, messsage.MessageGuid);
                        }
                    }

                    messsage.IsSent = true;
                }
            }

            lock (messageQueue)
            {
                m_threadIsRunning = false;
                if (messageQueue.Any())
                {
                    messageQueue_Changed();
                }
            }
        }

        private bool queueHasItems()
        {
            lock (messageQueue)
            {
                return messageQueue.Any();
            }
        }

        private void abortAll()
        {
            try
            {
                lock (messageQueue)
                {
                    abortRetries = true;
                    var abortedMessages = (from val in messageQueue
                        select val).ToList();

                    foreach (MessageRecord messageRecord in abortedMessages)
                    {
                        if (messageRecord.ReceiveStringDelegate != null)
                        {
                            messageRecord.ReceiveStringDelegate(null, CommunicationException.Timeout,
                                messageRecord.MessageGuid);
                        }
                        else
                        {
                            messageRecord.ReceiveBytesDelegate(null, CommunicationException.Timeout,
                                messageRecord.MessageGuid);
                        }
                    }

                    messageQueue.Clear();
                }
            }
            catch
            {
            }
        }

        internal void AbortAll()
        {
            abortRetries = true;
            abortAll();
        }

        private int GetUnitIdFromStringMessage(string message)
        {
            return Convert.ToInt32(message.Substring(Utils.General.STX.Length, 2), 16);
        }

        private int GetUnitIdFromBinaryMessage(byte[] message)
        {
            return Convert.ToInt32(message[6]);
        }

        #endregion

        #region Properties

        public int Retry
        {
            get { return m_retry; }
            set { m_retry = value; }
        }

        public int TimeOut
        {
            get { return m_TimeOut; }
            set { m_TimeOut = value; }
        }

        [XmlIgnoreAttribute]
        protected int QueueCount
        {
            get { return messageQueue.Count; }
        }

        [XmlIgnoreAttribute]
        internal bool AlreadyInitialized
        {
            get { return m_AlreadyInitialized; }
            set { m_AlreadyInitialized = value; }
        }

        [XmlIgnoreAttribute] public abstract bool Connected { get; }

        #endregion

        #region Abstract

        internal abstract void SendString(string text, byte messageEnumerator, bool isIdMessage = false,
            bool suppressEthernetHeader = false);

        internal abstract void SendBytes(byte[] bytes, byte messageEnumerator);
        internal abstract string ReceiveString();
        internal abstract byte[] ReceiveBytes();
        internal abstract void Connect();
        public abstract void Disconnect();
        internal abstract bool IsEquivalentChannel(Channel anotherChanel);
        internal abstract string GetLoggerChannelText();

        #endregion
    }
}