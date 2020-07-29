using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Unitronics.ComDriver.Messages;
using System.Threading;
using System.Security;
using System.Xml.Serialization;

namespace Unitronics.ComDriver
{
    public class EthernetListener : Channel
    {
        #region Locals

        private int m_LocalPort;
        private byte[] m_header;
        private byte[] receivedBuffer;
        private ConnectionFlag m_ConnectionFlag = ConnectionFlag.None;
        private ConnectionStatus m_ConnectionStatus = ConnectionStatus.Disconnected;
        WrappedSocket socket;
        private int transactionIdentifier = 0;
        private byte[] ethernetHeader;
        private bool messageReceived = false;
        InputMode inputMode = InputMode.None;
        private object lockObj = new object();
        private byte _messageEnumerator = 0;
        private bool _suppressEthernetHeader = false;

        private enum ConnectionFlag
        {
            None = 0,
            DisconnectedResumeListening = 1, // If the disconnected was from the client side then resume the listening
            DisconnectedStopListening = 2, // The Server closed the connection in order to stop listening
        }

        private enum InputMode
        {
            None,
            ASCII,
            Binary,
        }

        public enum ConnectionStatus
        {
            Listening = 0,
            Connected = 1,
            Disconnected = 2
        }

        // A delegate for Ethernet Listenet. Returns a PLC Object when connection arives.
        public delegate void ListenerConnectionAcceptedDelegate(PLC plcFromListener);

        public delegate void ListenerConnectionClosedDelegate(EthernetListener ethernetListener);

        public delegate void ListenerConnectionStatusChangedDelegate(ConnectionStatus connectionStatus);

        public event ListenerConnectionAcceptedDelegate OnListenerConnectionAccepted;
        public event ListenerConnectionClosedDelegate OnListenerConnectionClosed;
        public event ListenerConnectionStatusChangedDelegate OnListenerConnectionStatusChanged;


        private const String STX_STRING = "/_OPLC";

        #endregion


        #region Properties

        public int LocalPort
        {
            get { return m_LocalPort; }
            set
            {
                if (m_LocalPort != value)
                {
                    if (PLCFactory.ValidateChannelPropertyChange(this, value))
                    {
                        if (Status == ConnectionStatus.Disconnected)
                        {
                            m_LocalPort = value;
                        }
                        else
                        {
                            throw new ComDriveExceptions("Local Port cannot be modified on an opened connection",
                                ComDriveExceptions.ComDriveException.CommunicationParamsException);
                        }
                    }
                    else
                    {
                        throw new ComDriveExceptions(
                            "Cannot change Local Port since it will result a duplicated channel with the same Local Port",
                            ComDriveExceptions.ComDriveException.CommunicationParamsException);
                    }
                }
            }
        }

        public override bool Connected
        {
            get { return socket.Connected; }
        }

        [XmlIgnoreAttribute]
        public string RemoteIP
        {
            get
            {
                if (socket == null)
                {
                    return "";
                }
                else
                {
                    return socket.RemoteIP;
                }
            }
        }

        [XmlIgnoreAttribute]
        public ConnectionStatus Status
        {
            get
            {
                lock (lockObj)
                {
                    return m_ConnectionStatus;
                }
            }
            private set
            {
                ConnectionStatus localConnectionStatus = value;
                lock (lockObj)
                {
                    m_ConnectionStatus = value;
                }

                if (OnListenerConnectionStatusChanged != null)
                {
                    OnListenerConnectionStatusChanged(localConnectionStatus);
                }
            }
        }

        #endregion

        #region Constructor

        public EthernetListener()
            : base()
        {
            initSocket();
        }

        public EthernetListener(Int32 localPort, int retry, int TimeOut)
            : base(retry, TimeOut)
        {
            m_LocalPort = localPort;
            initSocket();
        }

        private void initSocket()
        {
            socket = new WrappedSocket();
            socket.OnConnect += new EventHandler(socket_OnConnect);
            socket.OnClose += new EventHandler(socket_OnClose);
            socket.OnSocektError += new WrappedSocket.SocektErrorDelegate(socket_OnSocektError);
            socket.OnDataReceived += new WrappedSocket.DataReceivedDelegate(socket_OnDataReceived);
        }

        #endregion

        #region Public

        internal override string GetLoggerChannelText()
        {
            return Utils.HelperComDriverLogger.GetLoggerChannel(this);
        }

        internal override void SendString(string text, byte messageEnumerator, bool isIdMessage = false,
            bool suppressEthernetHeader = false)
        {
            _messageEnumerator = messageEnumerator;
            _suppressEthernetHeader = suppressEthernetHeader;
            receivedBuffer = new byte[0];
            inputMode = InputMode.ASCII;
            lock (lockObj)
            {
                messageReceived = false;
            }

            if (!Connected)
                return;

            byte[] messageBuffer = strToByteArray(text.Replace(" ", ""));
            byte[] ethernetCommand;
            if (_suppressEthernetHeader)
            {
                ethernetCommand = messageBuffer;
            }
            else
            {
                ethernetCommand = getTcpCommand(messageBuffer, isIdMessage);
            }

            ethernetHeader = ethernetCommand.Take(4).ToArray();

            try
            {
                socket.Send(ethernetCommand);
            }
            catch
            {
                throw;
            }
            finally
            {
                transactionIdentifier++;
                if (transactionIdentifier == 0x2f2f)
                    transactionIdentifier++;
                transactionIdentifier = transactionIdentifier % 0xFFFF;
            }
        }

        internal override void SendBytes(byte[] bytes, byte messageEnumerator)
        {
            _messageEnumerator = messageEnumerator;
            receivedBuffer = new byte[0];
            inputMode = InputMode.Binary;
            lock (lockObj)
            {
                messageReceived = false;
            }

            if (!Connected)
                return;

            byte[] ethernetCommand = getTcpCommand(bytes, false);
            ethernetHeader = ethernetCommand.Take(4).ToArray();

            try
            {
                socket.Send(ethernetCommand);
            }
            catch
            {
                throw;
            }
            finally
            {
                transactionIdentifier++;
                if (transactionIdentifier == 0x2f2f)
                    transactionIdentifier++;
                transactionIdentifier = transactionIdentifier % 0xFFFF;
            }
        }

        internal override string ReceiveString()
        {
            lock (lockObj)
            {
                if (!messageReceived)
                    Monitor.Wait(lockObj, TimeOut);
            }

            inputMode = InputMode.None;

            lock (lockObj)
            {
                if (!messageReceived)
                {
                    throw new TimeoutException();
                }
            }

            return ASCIIEncoding.ASCII.GetString(receivedBuffer);
        }

        internal override byte[] ReceiveBytes()
        {
            lock (lockObj)
            {
                if (!messageReceived)
                    Monitor.Wait(lockObj, TimeOut);
            }

            inputMode = InputMode.None;

            lock (lockObj)
            {
                if (!messageReceived)
                {
                    throw new TimeoutException();
                }
            }

            return receivedBuffer;
        }

        internal void ListenerReceiveString()
        {
            bool bStxFound = false;
            bool bEtxFound = false;
            string incomingData = "";
            byte[] tempBuffer;
            byte[] tcpHeader;
            int index = 0;
            int checksum = 0;


            if (!Connected)
                throw new TimeoutException();

            tcpHeader = ethernetHeader;

            incomingData = ASCIIEncoding.ASCII.GetString(receivedBuffer);
            if (!_suppressEthernetHeader && incomingData.Length >= Utils.Lengths.LENGTH_TCP_HEADER)
            {
                if (receivedBuffer.Take(4).SequenceEqual(tcpHeader) == true)
                {
                    tempBuffer = new byte[incomingData.Length - 6];
                    Array.Copy(receivedBuffer, 6, tempBuffer, 0, receivedBuffer.Length - 6);
                    receivedBuffer = tempBuffer;
                    incomingData = ASCIIEncoding.ASCII.GetString(receivedBuffer);
                }
                else
                {
                    //throw new ComDriveExceptions("Ethernet Transaction ID mismatch", ComDriveExceptions.ComDriveException.TransactionIdMismatch);
                }
            }

            if (incomingData.Length > 0)
            {
                index = incomingData.IndexOf("/"); // find the STX
                if (index >= 0)
                {
                    incomingData = incomingData.Substring(index, incomingData.Length - index);
                    bStxFound = true;
                }

                index = incomingData.IndexOf("\r"); // find the ETX
                if (index >= 0)
                {
                    incomingData = incomingData.Substring(0, index + 1);
                    bEtxFound = true;
                }
            }

            if (!bStxFound || !bEtxFound)
            {
                return;
            }

            for (int i = 2; i < incomingData.Length - 3; i++)
            {
                checksum += Convert.ToInt32(Convert.ToChar(incomingData[i]));
            }

            string CRC = Utils.DecimalToHex(checksum % 256);

            if (CRC != incomingData.Substring(incomingData.Length - 3, 2))
            {
                throw new ComDriveExceptions("Wrong Data Checksum", ComDriveExceptions.ComDriveException.ChecksumError);
            }

            lock (lockObj)
            {
                messageReceived = true;
                Monitor.PulseAll(lockObj);
            }
        }

        internal void ListenerReceiveBytes()
        {
            bool bStxFound = false;
            byte[] tempBuffer;
            string bufferAsString;
            int index = 0;
            int totalLength = 0;
            byte[] tcpHeader;

            const int HEADER_LENGTH = 24;

            if (!Connected)
                throw new TimeoutException();

            tcpHeader = ethernetHeader;

            if (receivedBuffer.Length >= Utils.Lengths.LENGTH_TCP_HEADER)
            {
                if (receivedBuffer.Take(4).SequenceEqual(tcpHeader) == true)
                {
                    tempBuffer = new byte[receivedBuffer.Length - 6];
                    Array.Copy(receivedBuffer, 6, tempBuffer, 0, receivedBuffer.Length - 6);
                    receivedBuffer = tempBuffer;
                }
                else
                {
                    //throw new ComDriveExceptions("Ethernet Transaction ID mismatch", ComDriveExceptions.ComDriveException.TransactionIdMismatch);
                }
            }

            if (receivedBuffer.Length > 0)
            {
                bufferAsString = ASCIIEncoding.ASCII.GetString(receivedBuffer);
                index = bufferAsString.IndexOf(STX_STRING);
                if (index >= 0)
                {
                    tempBuffer = new byte[receivedBuffer.Length - index];
                    Array.Copy(receivedBuffer, index, tempBuffer, 0, receivedBuffer.Length - index);
                    receivedBuffer = tempBuffer;
                    bStxFound = true;
                }
            }

            if (!bStxFound)
            {
                return;
            }

            if (receivedBuffer.Length < HEADER_LENGTH)
            {
                return;
            }
            // checksum is not being checked on ethernet!!

            totalLength = BitConverter.ToUInt16(receivedBuffer, 20) +
                          HEADER_LENGTH + 3; // 3 for data checksum + ETX


            if (receivedBuffer.Length < totalLength)
            {
                return;
            }

            tempBuffer = new byte[totalLength];
            Array.Copy(receivedBuffer, 0, tempBuffer, 0, totalLength);
            receivedBuffer = tempBuffer;

            tempBuffer = null;

            if (receivedBuffer[totalLength - 1] != 92) // 92 is '\' which is the ETX
            {
                throw new ComDriveExceptions("ETX is missing", ComDriveExceptions.ComDriveException.ETXMissing);
            }

            lock (lockObj)
            {
                messageReceived = true;
                Monitor.PulseAll(lockObj);
            }
        }

        internal override void Connect()
        {
        }

        internal void Listen()
        {
            if (Status == ConnectionStatus.Disconnected)
            {
                m_ConnectionFlag = ConnectionFlag.None;

                try
                {
                    socket.Listen(m_LocalPort);
                    Status = ConnectionStatus.Listening;
                    string channelLog = Utils.HelperComDriverLogger.GetLoggerChannel(this);
                    try
                    {
                        ComDriverLogger.LogConnectionState(DateTime.Now, channelLog,
                            Unitronics.ComDriver.ConnectionStatus.Listening.ToString());
                    }
                    catch
                    {
                    }
                }
                catch
                {
                    throw new ComDriveExceptions(
                        "Failed binding local port " + m_LocalPort.ToString() +
                        ". Please check that the port is not in use", ComDriveExceptions.ComDriveException.PortInUse);
                }
            }
            else
            {
                //GetPLC was called. Therefore since we already listening then there are 2 options:
                //1. Socket is not connected and it is listening. When connection will arive then the PLC will be returned
                //2. Socket is connected. Therefore On Connect event will not happen and we need to return the PLC right now.
                Socket worker = socket.GetWorker();
                if (Status == ConnectionStatus.Connected)
                {
                    try
                    {
                        PLC plc = PLCFactory.GetPLC(this, 0);
                        if (OnListenerConnectionAccepted != null)
                        {
                            OnListenerConnectionAccepted(plc);
                        }
                    }
                    catch
                    {
                        try
                        {
                            worker.Close();
                        }
                        catch
                        {
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.Print("Listen request denied... Connection Status: " + Status.ToString());
                }
            }
        }

        void socket_OnConnect(object sender, EventArgs e)
        {
            Status = ConnectionStatus.Connected;
            Socket worker = socket.GetWorker();
            string channelLog = Utils.HelperComDriverLogger.GetLoggerChannel(this);
            try
            {
                ComDriverLogger.LogConnectionState(DateTime.Now, channelLog,
                    Unitronics.ComDriver.ConnectionStatus.ConnectionOpened.ToString());
            }
            catch (Exception ex)
            {
                // The PLC did not reply... Close the connection and return to Listen mode.
                {
                    worker.Close();
                    Console.Write(ex.Message);
                }
            }

            try
            {
                System.Threading.Thread.Sleep(1000);
                PLC plc = PLCFactory.GetPLC(this, 0);
                if (OnListenerConnectionAccepted != null)
                    OnListenerConnectionAccepted(plc);
            }
            catch (Exception ex)
            {
                // The PLC did not reply... Close the connection and return to Listen mode.
                {
                    worker.Close();
                    Console.Write(ex.Message);
                }
            }
        }

        void socket_OnClose(object sender, EventArgs e)
        {
            if (m_ConnectionFlag == ConnectionFlag.DisconnectedStopListening)
            {
                AbortAll();
                socket.Close();
            }
            else
            {
                AbortAll();
                System.Diagnostics.Debug.Print("Disconnected, back to listen");
                if (OnListenerConnectionClosed != null)
                    OnListenerConnectionClosed(this);

                m_ConnectionFlag = ConnectionFlag.DisconnectedResumeListening;
                Status = ConnectionStatus.Listening;
                socket.Close();
                try
                {
                    socket.Listen(m_LocalPort);
                }
                catch
                {
                    throw new ComDriveExceptions(
                        "Failed binding local port " + m_LocalPort.ToString() +
                        ". Please check that the port is not in use", ComDriveExceptions.ComDriveException.PortInUse);
                }

                try
                {
                    string channelLog = Utils.HelperComDriverLogger.GetLoggerChannel(this);

                    ComDriverLogger.LogConnectionState(DateTime.Now, channelLog,
                        Unitronics.ComDriver.ConnectionStatus.ConnectionClosed.ToString());
                    ComDriverLogger.LogConnectionState(DateTime.Now, channelLog,
                        Unitronics.ComDriver.ConnectionStatus.Listening.ToString());
                }
                catch
                {
                }
            }
        }

        void socket_OnSocektError(object sender, SocketError socketError)
        {
            // Socket error can be caused when Enfora closes the socket.
            if (m_ConnectionFlag == ConnectionFlag.DisconnectedStopListening)
            {
                AbortAll();
                socket.Close();
            }
            else
            {
                AbortAll();
                System.Diagnostics.Debug.Print("Disconnected, back to listen");
                if (OnListenerConnectionClosed != null)
                    OnListenerConnectionClosed(this);

                m_ConnectionFlag = ConnectionFlag.DisconnectedResumeListening;
                Status = ConnectionStatus.Listening;
                socket.Close();
                try
                {
                    socket.Listen(m_LocalPort);
                }
                catch
                {
                    throw new ComDriveExceptions(
                        "Failed binding local port " + m_LocalPort.ToString() +
                        ". Please check that the port is not in use", ComDriveExceptions.ComDriveException.PortInUse);
                }

                try
                {
                    string channelLog = Utils.HelperComDriverLogger.GetLoggerChannel(this);

                    ComDriverLogger.LogConnectionState(DateTime.Now, channelLog,
                        Unitronics.ComDriver.ConnectionStatus.ConnectionClosed.ToString());
                    ComDriverLogger.LogConnectionState(DateTime.Now, channelLog,
                        Unitronics.ComDriver.ConnectionStatus.Listening.ToString());
                }
                catch
                {
                }
            }
        }

        void socket_OnDataReceived(object sender, byte[] receivedBytes)
        {
            int cbRead = receivedBytes.Length;
            byte[] tmpBuffer = new byte[receivedBuffer.Length + cbRead];
            Array.Copy(receivedBuffer, 0, tmpBuffer, 0, receivedBuffer.Length);
            Array.Copy(receivedBytes, 0, tmpBuffer, receivedBuffer.Length, cbRead);
            receivedBuffer = tmpBuffer;

            ComDriverLogger.LogReceivedMessageChunk(DateTime.Now,
                Utils.HelperComDriverLogger.GetLoggerChannel(this), tmpBuffer);

            if (inputMode == InputMode.ASCII)
            {
                ListenerReceiveString();
            }
            else if (inputMode == InputMode.Binary)
            {
                ListenerReceiveBytes();
            }
        }

        /// <summary>
        /// Closes the Socket connection and releases all associated resources.
        /// </summary>
        public override void Disconnect()
        {
            if (base.QueueCount != 0)
                return;

            Socket worker = socket.GetWorker();
            try
            {
                AbortAll();
                m_ConnectionFlag = ConnectionFlag.DisconnectedStopListening;
                socket.Close();

                Status = ConnectionStatus.Disconnected;
            }
            catch
            {
            }

            string channelLog = Utils.HelperComDriverLogger.GetLoggerChannel(this);

            try
            {
                ComDriverLogger.LogConnectionState(DateTime.Now, channelLog,
                    Unitronics.ComDriver.ConnectionStatus.ConnectionClosed.ToString());
            }
            catch
            {
            }

            if (worker != null)
            {
                System.Threading.Thread.Sleep(100);
                while (worker.Connected)
                {
                    try
                    {
                        worker.Close();
                    }
                    catch
                    {
                    }

                    System.Threading.Thread.Sleep(100);
                }
            }
        }

        public void DisconnectClient()
        {
            if (base.QueueCount != 0)
                return;

            //System.Diagnostics.Debug.Print("Disconnected, back to listen");
            //if (OnListenerConnectionClosed != null)
            //    OnListenerConnectionClosed(this);

            lock (lockObj)
            {
                if (Status == ConnectionStatus.Connected)
                {
                    m_ConnectionFlag = ConnectionFlag.DisconnectedResumeListening;
                    //Status = ConnectionStatus.Listening;
                    socket.Close();
                }
            }
            //try
            //{
            //    socket.Listen(m_LocalPort);
            //}
            //catch
            //{
            //    throw new ComDriveExceptions("Failed binding local port " + m_LocalPort.ToString() + ". Please check that the port is not in use", ComDriveExceptions.ComDriveException.PortInUse);
            //}
            //try
            //{
            //    string channelLog = Utils.HelperComDriverLogger.GetLoggerChannel(this);

            //    ComDriverLogger.LogConnectionState(DateTime.Now, channelLog, Unitronics.ComDriver.ConnectionStatus.ConnectionClosed.ToString());
            //    ComDriverLogger.LogConnectionState(DateTime.Now, channelLog, Unitronics.ComDriver.ConnectionStatus.Listening.ToString());

            //}
            //catch { }
        }


        internal override bool IsEquivalentChannel(Channel anotherChanel)
        {
            EthernetListener ethernetListener = anotherChanel as EthernetListener;
            if (ethernetListener.LocalPort == m_LocalPort)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

        #region Private

        // Convert a string to a byte array.
        private static byte[] strToByteArray(string str)
        {
            System.Text.ASCIIEncoding encoding = new System.Text.ASCIIEncoding();
            return encoding.GetBytes(str);
        }

        // Convert a byte array to a string.
        private static string byteArrayToStr(byte[] dBytes)
        {
            string str;
            System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
            str = enc.GetString(dBytes);

            return str;
        }

        private byte[] getTcpCommand(byte[] command, bool isIdMessage)
        {
            int commandLenght = command.Length;
            byte lsb = Convert.ToByte(commandLenght & 0x00FF);
            byte msb = Convert.ToByte((commandLenght & 0xFF00) >> 8);

            byte[] transactionIdBytes;
            if (isIdMessage)
            {
                transactionIdBytes = BitConverter.GetBytes(1);
            }
            else
            {
                transactionIdBytes = BitConverter.GetBytes(transactionIdentifier);
            }

            byte[] tcpHeader = new byte[]
            {
                transactionIdBytes[0], transactionIdBytes[1],
                command[1] == 95 ? Utils.General.BINARY_PROTOCOL : Utils.General.ASCII_PROTOCOL,
                _messageEnumerator, lsb, msb
            };

            m_header = tcpHeader;

            byte[] tcpCommand = new byte[Utils.Lengths.LENGTH_TCP_HEADER + command.Length];
            for (int i = 0; i < tcpCommand.Length; i++)
            {
                tcpCommand[i] = (i < Utils.Lengths.LENGTH_TCP_HEADER)
                    ? tcpHeader[i]
                    : command[i - Utils.Lengths.LENGTH_TCP_HEADER];
            }

            return tcpCommand;
        }

        private void appendReceivedDataToArray(ref byte[] incomingData)
        {
            incomingData = receivedBuffer;
        }

        private void appendReceivedDataToString(ref string incomingData)
        {
            incomingData = ASCIIEncoding.ASCII.GetString(receivedBuffer);
        }

        #endregion
    }
}