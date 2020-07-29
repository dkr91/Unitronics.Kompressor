using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Xml.Serialization;
using System.Threading;

namespace Unitronics.ComDriver
{
    public class ListenerClient : Channel
    {
        #region Locals

        private byte[] m_header;
        private byte[] receivedBuffer;
        private int transactionIdentifier = 0;
        private byte[] ethernetHeader;
        private bool messageReceived = false;
        InputMode inputMode = InputMode.None;
        private string remoteIP;
        private int localPort;
        private bool objectDisposed = false;
        Socket worker;
        private object lockObj = new object();
        private Guid guid = Guid.NewGuid();
        private byte _messageEnumerator;
        private bool _suppressEthernetHeader = false;

        private const String STX_STRING = "/_OPLC";

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

        private class SocketState
        {
            public Socket Socket { get; set; }
            public byte[] ReceivedData { get; set; }
            public SocketError ErrorCode;
        }

        public delegate void ConnectionClosedDelegate(ListenerClient ethernetListener);

        public event ConnectionClosedDelegate OnConnectionClosed;

        #endregion

        internal ListenerClient(Socket socket, int port)
        {
            initSocket(socket, port);
        }

        internal ListenerClient(Socket socket, int port, int retry, int TimeOut)
            : base(retry, TimeOut)
        {
            initSocket(socket, port);
        }

        ~ListenerClient()
        {
        }

        private void initSocket(Socket socket, int port)
        {
            try
            {
                worker = socket;
                localPort = port;
                IPEndPoint ep = worker.RemoteEndPoint as IPEndPoint;
                RemoteIP = ep.Address.ToString();

                string channelLog = Utils.HelperComDriverLogger.GetLoggerChannel(this);
                try
                {
                    ComDriverLogger.LogConnectionState(DateTime.Now, channelLog,
                        Unitronics.ComDriver.ConnectionStatus.ConnectionOpened.ToString() + ", Remote IP (Client): " +
                        RemoteIP + ", Client GUID: " + guid.ToString());
                }
                catch
                {
                }

                ListenerClientsInfo.IncrementCount(localPort);
                waitForData();
            }
            catch (Exception ex)
            {
                if (!Disposed)
                    ListenerClientsInfo.DecrementCount(localPort);
                try
                {
                    worker.Close();
                }
                catch
                {
                }

                if (!Disposed)
                {
                    string channelLog = Utils.HelperComDriverLogger.GetLoggerChannel(this);
                    try
                    {
                        ComDriverLogger.LogConnectionState(DateTime.Now, channelLog,
                            Unitronics.ComDriver.ConnectionStatus.ConnectionClosed.ToString() +
                            ", Remote IP (Client): " + RemoteIP + ", Client GUID: " + guid.ToString());
                    }
                    catch
                    {
                    }
                }

                Disposed = true;

                string exceptionText = "InnerListenerClientEcxeption (initSocket)" + " - " + ex.Message;
                ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);

                AbortAll();
            }
        }

        [XmlIgnoreAttribute]
        public string RemoteIP
        {
            get { return remoteIP; }
            private set { remoteIP = value; }
        }

        public int LocalPort
        {
            get { return localPort; }
        }

        internal void OnDataReceived(byte[] receivedBytes)
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
                ClientReceiveString();
            }
            else if (inputMode == InputMode.Binary)
            {
                ClientReceiveBytes();
            }
        }

        internal void ClientReceiveString()
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

        internal void ClientReceiveBytes()
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

        internal override void SendString(string text, byte messageEnumerator, bool isIdMessage = false,
            bool suppressEthernetHeader = false)
        {
            _messageEnumerator = messageEnumerator;
            _suppressEthernetHeader = suppressEthernetHeader;
            if (objectDisposed)
                throw new ObjectDisposedException("");

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
                worker.Send(ethernetCommand);
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
            if (objectDisposed)
                throw new ObjectDisposedException("");

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
                worker.Send(ethernetCommand);
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
            if (objectDisposed)
                throw new ObjectDisposedException("");

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
            if (objectDisposed)
                throw new ObjectDisposedException("");

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

        internal override void Connect()
        {
        }

        public override void Disconnect()
        {
            if (base.QueueCount != 0)
                return;

            Dispose();
        }

        internal void Dispose()
        {
            // This function literally kills the socket, no matter if the message queue is not empty.
            try
            {
                if (!Disposed)
                {
                    ListenerClientsInfo.DecrementCount(localPort);
                    Disposed = true;
                    worker.Close();
                    if (OnConnectionClosed != null)
                        OnConnectionClosed(this);

                    string channelLog = Utils.HelperComDriverLogger.GetLoggerChannel(this);
                    try
                    {
                        ComDriverLogger.LogConnectionState(DateTime.Now, channelLog,
                            Unitronics.ComDriver.ConnectionStatus.ConnectionClosed.ToString() +
                            ", Remote IP (Client): " + RemoteIP + ", Client GUID: " + guid.ToString());
                    }
                    catch
                    {
                    }

                    AbortAll();
                }
            }
            catch (Exception ex)
            {
                string exceptionText = "InnerListenerClientEcxeption (Dispose)" + " - " + ex.Message;
                ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
            }
        }


        public override bool Connected
        {
            get
            {
                if (Disposed)
                {
                    return false;
                }
                else
                {
                    try
                    {
                        return worker.Connected;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
        }

        internal override bool IsEquivalentChannel(Channel anotherChanel)
        {
            throw new NotImplementedException();
        }

        internal override string GetLoggerChannelText()
        {
            return Utils.HelperComDriverLogger.GetLoggerChannel(this);
        }

        public bool Disposed
        {
            get
            {
                lock (lockObj)
                {
                    return objectDisposed;
                }
            }
            internal set
            {
                lock (lockObj)
                {
                    objectDisposed = value;
                }
            }
        }

        private void waitForData()
        {
            SocketState socketState = new SocketState();
            socketState.Socket = worker;
            socketState.ReceivedData = new byte[1024];
            worker.BeginReceive(socketState.ReceivedData, 0, socketState.ReceivedData.Length, SocketFlags.None,
                out socketState.ErrorCode, onDataReceived, socketState);
        }


        private void onDataReceived(IAsyncResult ar)
        {
            SocketState socketState = null;
            try
            {
                socketState = (SocketState) ar.AsyncState;
                int cbRead = 0;
                cbRead = worker.EndReceive(ar);

                if (cbRead > 0)
                {
                    byte[] temp = new byte[cbRead];
                    Array.Copy(socketState.ReceivedData, 0, temp, 0, cbRead);
                    OnDataReceived(temp);
                    waitForData();
                }
                else
                {
                    try
                    {
                        if (!Disposed)
                            ListenerClientsInfo.DecrementCount(localPort);
                        worker.Close();
                    }
                    catch
                    {
                    }

                    if (!Disposed)
                    {
                        string channelLog = Utils.HelperComDriverLogger.GetLoggerChannel(this);
                        try
                        {
                            ComDriverLogger.LogConnectionState(DateTime.Now, channelLog,
                                Unitronics.ComDriver.ConnectionStatus.ConnectionClosed.ToString() +
                                ", Remote IP (Client): " + RemoteIP + ", Client GUID: " + guid.ToString());
                        }
                        catch
                        {
                        }

                        Disposed = true;

                        AbortAll();
                        if (OnConnectionClosed != null)
                            OnConnectionClosed(this);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                try
                {
                    if (!Disposed)
                        ListenerClientsInfo.DecrementCount(localPort);
                    worker.Close();
                }
                catch
                {
                }

                if (!Disposed)
                {
                    string channelLog = Utils.HelperComDriverLogger.GetLoggerChannel(this);
                    try
                    {
                        ComDriverLogger.LogConnectionState(DateTime.Now, channelLog,
                            Unitronics.ComDriver.ConnectionStatus.ConnectionClosed.ToString() +
                            ", Remote IP (Client): " + RemoteIP + ", Client GUID: " + guid.ToString());
                    }
                    catch
                    {
                    }

                    Disposed = true;

                    AbortAll();
                    if (OnConnectionClosed != null)
                        OnConnectionClosed(this);
                }
            }
            catch (SocketException se)
            {
                try
                {
                    if (!Disposed)
                        ListenerClientsInfo.DecrementCount(localPort);
                    worker.Close();
                }
                catch
                {
                }

                if (!Disposed)
                {
                    string channelLog = Utils.HelperComDriverLogger.GetLoggerChannel(this);
                    try
                    {
                        ComDriverLogger.LogConnectionState(DateTime.Now, channelLog,
                            Unitronics.ComDriver.ConnectionStatus.ConnectionClosed.ToString() +
                            ", Remote IP (Client): " + RemoteIP + ", Client GUID: " + guid.ToString());
                    }
                    catch
                    {
                    }

                    Disposed = true;

                    AbortAll();

                    if (OnConnectionClosed != null)
                        OnConnectionClosed(this);
                }
            }
            catch
            {
                try
                {
                    if (!Disposed)
                        ListenerClientsInfo.DecrementCount(localPort);
                    worker.Close();
                }
                catch
                {
                }

                if (!Disposed)
                {
                    string channelLog = Utils.HelperComDriverLogger.GetLoggerChannel(this);
                    try
                    {
                        ComDriverLogger.LogConnectionState(DateTime.Now, channelLog,
                            Unitronics.ComDriver.ConnectionStatus.ConnectionClosed.ToString() +
                            ", Remote IP (Client): " + RemoteIP);
                    }
                    catch
                    {
                    }

                    Disposed = true;

                    AbortAll();

                    if (OnConnectionClosed != null)
                        OnConnectionClosed(this);
                }
            }
        }

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
    }
}