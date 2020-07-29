using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Unitronics.ComDriver.Messages;
using System.Configuration;
using System.Security;
using System.Diagnostics;

namespace Unitronics.ComDriver
{
    public class Ethernet : Channel
    {
        #region Locals

        private int m_remotePort;
        private string m_remoteIPorHostName = "";
        private EthProtocol m_protocol;
        private int connectTimeOut = 5000;
        private Socket m_PLCSocket;
        private int m_bytesNo;
        private byte[] m_header;
        private int transactionIdentifier = 0;
        private byte[] ethernetHeader;
        private byte[] incomingData = new byte[0];
        private bool messageReceived = false;
        private SocketAsyncEventArgs socketAsyncEventArgs = new SocketAsyncEventArgs();
        private const String STX_STRING = "/_OPLC";
        private byte _messageEnumerator = 0;
        private bool _suppressEthernetHeader = false;

        #endregion

        #region Properties

        private class SocketState
        {
            public enum ConnectionState
            {
                Connecting,
                Connected,
                Failed,
            }

            public Socket Socket { get; set; }
            public ConnectionState State { get; set; }
        }

        public override bool Connected
        {
            get
            {
                if (m_PLCSocket == null)
                    return false;
                return m_PLCSocket.Connected;
            }
        }

        public int RemotePort
        {
            get { return m_remotePort; }
            set
            {
                if (m_remotePort != value)
                {
                    if (PLCFactory.ValidateChannelPropertyChange(this, m_remoteIPorHostName, value))
                    {
                        if ((m_PLCSocket == null) || (!m_PLCSocket.Connected))
                        {
                            m_remotePort = value;
                        }
                        else
                        {
                            throw new ComDriveExceptions("Remote Port cannot be modified on an opened connection",
                                ComDriveExceptions.ComDriveException.CommunicationParamsException);
                        }
                    }
                    else
                    {
                        throw new ComDriveExceptions(
                            "Cannot change Remote Port since it will result a duplicated channel with the same Remote Port and IP Address",
                            ComDriveExceptions.ComDriveException.CommunicationParamsException);
                    }
                }
            }
        }

        // Remote IP property is also used for Host Name.
        public string RemoteIP
        {
            get { return m_remoteIPorHostName; }
            set
            {
                if (m_remoteIPorHostName != value)
                {
                    if (PLCFactory.ValidateChannelPropertyChange(this, value, m_remotePort))
                    {
                        if ((m_PLCSocket == null) || (!m_PLCSocket.Connected))
                        {
                            m_remoteIPorHostName = value;
                        }
                        else
                        {
                            throw new ComDriveExceptions("Remote IP cannot be modified on an opened connection",
                                ComDriveExceptions.ComDriveException.CommunicationParamsException);
                        }
                    }
                    else
                    {
                        throw new ComDriveExceptions(
                            "Cannot change Remote IP since it will result a duplicated channel with the same Remote Port and IP Address",
                            ComDriveExceptions.ComDriveException.CommunicationParamsException);
                    }
                }
            }
        }

        public EthProtocol Protocol
        {
            get { return m_protocol; }
            set { m_protocol = value; }
        }

        public int ConnectTimeOut
        {
            get { return connectTimeOut; }
            set { connectTimeOut = value; }
        }

        #endregion

        #region Constructor

        public Ethernet()
            : base()
        {
        }

        public Ethernet(int retry, int TimeOut)
            : base(retry, TimeOut)
        {
            //Connect();
        }

        public Ethernet(string remoteIpOrHostName, Int32 remotePort, EthProtocol protocolType)
        {
            m_protocol = protocolType;
            m_remoteIPorHostName = remoteIpOrHostName;
            m_remotePort = remotePort;
            //Connect();
        }

        public Ethernet(string remoteIpOrHostName, Int32 remotePort, EthProtocol protocolType, int retry, int TimeOut)
            : base(retry, TimeOut)
        {
            m_protocol = protocolType;
            m_remoteIPorHostName = remoteIpOrHostName;
            m_remotePort = remotePort;
            //Connect();
        }

        public Ethernet(string remoteIpOrHostName, Int32 remotePort, EthProtocol protocolType, int retry, int TimeOut,
            int ConnectTimeOut)
            : base(retry, TimeOut)
        {
            m_protocol = protocolType;
            m_remoteIPorHostName = remoteIpOrHostName;
            m_remotePort = remotePort;
            //Connect();
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

            if (m_PLCSocket == null)
                throw new ComDriveExceptions("Socket is not initialized!",
                    ComDriveExceptions.ComDriveException.GeneralCommunicationError);

            if (!m_PLCSocket.Connected)
                Connect();

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
                m_bytesNo = m_PLCSocket.Send(ethernetCommand);
            }
            catch (ArgumentNullException)
            {
                throw;
            }
            catch (SocketException)
            {
                throw;
            }
            catch (ObjectDisposedException)
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
            if (m_PLCSocket == null)
                return;
            if (!m_PLCSocket.Connected)
                Connect();

            byte[] ethernetCommand = getTcpCommand(bytes, false);
            ethernetHeader = ethernetCommand.Take(4).ToArray();

            try
            {
                m_bytesNo = m_PLCSocket.Send(ethernetCommand);
            }
            catch (ArgumentNullException)
            {
                throw;
            }
            catch (SocketException)
            {
                throw;
            }
            catch (ObjectDisposedException)
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
            messageReceived = false;
            incomingData = new byte[0];
            if (m_PLCSocket == null || m_PLCSocket.Connected == false)
                throw new TimeoutException();

            byte[] receivedData = new byte[1024];

            m_PLCSocket.ReceiveTimeout = TimeOut;

            while (!messageReceived)
            {
                int received = m_PLCSocket.Receive(receivedData);
                socketReceivedString(receivedData, received);
            }

            if (!messageReceived)
            {
                //m_PLCSocket.Shutdown(SocketShutdown.Receive);
                throw new TimeoutException();
            }

            return ASCIIEncoding.ASCII.GetString(incomingData);
        }

        internal override byte[] ReceiveBytes()
        {
            messageReceived = false;
            incomingData = new byte[0];
            if (m_PLCSocket == null || m_PLCSocket.Connected == false)
                throw new TimeoutException();

            byte[] receivedData = new byte[1024];
            m_PLCSocket.ReceiveTimeout = TimeOut;
            try
            {
                while (!messageReceived)
                {
                    int received = m_PLCSocket.Receive(receivedData);
                    socketReceivedBytes(receivedData, received);
                }
            }
            catch
            {
            }

            if (!messageReceived)
            {
                throw new TimeoutException();
            }

            return incomingData;
        }


        private void socketReceivedString(byte[] incomingBytes, int count)
        {
            bool bStxFound = false;
            bool bEtxFound = false;
            string incomingString = "";
            byte[] tempBuffer;
            byte[] tcpHeader;
            int index = 0;
            int checksum = 0;

            try
            {
                int cbRead = 0;
                cbRead = count;
                if (cbRead > 0)
                {
                    byte[] temp = new byte[cbRead];
                    Array.Copy(incomingBytes, 0, temp, 0, cbRead);

                    ComDriverLogger.LogReceivedMessageChunk(DateTime.Now,
                        Utils.HelperComDriverLogger.GetLoggerChannel(this), temp);

                    byte[] tmpBuffer = new byte[incomingData.Length + cbRead];
                    Array.Copy(incomingData, 0, tmpBuffer, 0, incomingData.Length);
                    Array.Copy(temp, 0, tmpBuffer, incomingData.Length, cbRead);
                    incomingData = tmpBuffer;
                }
                else
                {
                    // Socket was closed?
                    m_PLCSocket.Close();
                }
            }
            catch
            {
                // Socket Error?
                m_PLCSocket.Close();
            }

            tcpHeader = ethernetHeader;
            incomingString = ASCIIEncoding.ASCII.GetString(incomingData);
            if (!_suppressEthernetHeader && incomingString.Length >= Utils.Lengths.LENGTH_TCP_HEADER)
            {
                if (incomingData.Take(4).SequenceEqual(tcpHeader) == true)
                {
                    tempBuffer = new byte[incomingString.Length - 6];
                    Array.Copy(incomingData, 6, tempBuffer, 0, incomingData.Length - 6);
                    incomingData = tempBuffer;
                    incomingString = ASCIIEncoding.ASCII.GetString(incomingData);
                }
                else
                {
                    //throw new ComDriveExceptions("Ethernet Transaction ID mismatch", ComDriveExceptions.ComDriveException.TransactionIdMismatch); 
                }
            }

            if (incomingString.Length > 0)
            {
                index = incomingString.IndexOf("/"); // find the STX
                if (index >= 0)
                {
                    incomingString = incomingString.Substring(index, incomingString.Length - index);
                    bStxFound = true;
                }
                else
                {
                    if (incomingString.Length > 100)
                    {
                        throw new ComDriveExceptions("STX is missing",
                            ComDriveExceptions.ComDriveException.CommunicationTimeout);
                    }
                }

                index = incomingString.IndexOf("\r"); // find the ETX
                if (index >= 0)
                {
                    incomingString = incomingString.Substring(0, index + 1);
                    bEtxFound = true;
                }
            }

            if (!bStxFound || !bEtxFound)
            {
                return;
            }

            for (int i = 2; i < incomingString.Length - 3; i++)
            {
                checksum += incomingData[i];
            }

            string CRC = Utils.DecimalToHex(checksum % 256);

            if (CRC != incomingString.Substring(incomingString.Length - 3, 2))
            {
                throw new ComDriveExceptions("Wrong Data Checksum", ComDriveExceptions.ComDriveException.ChecksumError);
            }

            messageReceived = true;
        }

        private void socketReceivedBytes(byte[] incomingBytes, int count)
        {
            bool bStxFound = false;
            byte[] tempBuffer;
            string bufferAsString;
            int index = 0;
            int totalLength = 0;
            byte[] tcpHeader;

            const int HEADER_LENGTH = 24;

            tcpHeader = ethernetHeader;

            try
            {
                int cbRead = 0;
                cbRead = count;
                if (cbRead > 0)
                {
                    byte[] temp = new byte[cbRead];
                    Array.Copy(incomingBytes, 0, temp, 0, cbRead);

                    ComDriverLogger.LogReceivedMessageChunk(DateTime.Now,
                        Utils.HelperComDriverLogger.GetLoggerChannel(this), temp);

                    byte[] tmpBuffer = new byte[incomingData.Length + cbRead];
                    Array.Copy(incomingData, 0, tmpBuffer, 0, incomingData.Length);
                    Array.Copy(temp, 0, tmpBuffer, incomingData.Length, cbRead);
                    incomingData = tmpBuffer;
                }
                else
                {
                    // Socket was closed?
                    m_PLCSocket.Close();
                }
            }
            catch
            {
                // Socket Error?
                m_PLCSocket.Close();
            }


            if (incomingData.Length >= Utils.Lengths.LENGTH_TCP_HEADER)
            {
                if (incomingData.Take(4).SequenceEqual(tcpHeader) == true)
                {
                    tempBuffer = new byte[incomingData.Length - 6];
                    Array.Copy(incomingData, 6, tempBuffer, 0, incomingData.Length - 6);
                    incomingData = tempBuffer;
                }
                else
                {
                    //throw new ComDriveExceptions("Ethernet Transaction ID mismatch", ComDriveExceptions.ComDriveException.TransactionIdMismatch);
                }
            }

            if (incomingData.Length > 0)
            {
                bufferAsString = ASCIIEncoding.ASCII.GetString(incomingData);
                index = bufferAsString.IndexOf(STX_STRING);
                if (index >= 0)
                {
                    tempBuffer = new byte[incomingData.Length - index];
                    Array.Copy(incomingData, index, tempBuffer, 0, incomingData.Length - index);
                    incomingData = tempBuffer;
                    bStxFound = true;
                }
                else
                {
                    if (incomingData.Length > 100)
                    {
                        throw new ComDriveExceptions("STX is missing",
                            ComDriveExceptions.ComDriveException.CommunicationTimeout);
                    }
                }
            }

            if (!bStxFound)
            {
                return;
            }

            if (incomingData.Length < HEADER_LENGTH)
            {
                return;
            }

            totalLength = BitConverter.ToUInt16(incomingData, 20) +
                          HEADER_LENGTH + 3; // 3 for data checksum + ETX

            if (incomingData.Length < totalLength)
            {
                return;
            }

            tempBuffer = new byte[totalLength];
            Array.Copy(incomingData, 0, tempBuffer, 0, totalLength);
            incomingData = tempBuffer;

            tempBuffer = null;

            if (incomingData[totalLength - 1] != 92) // 92 is '\' which is the ETX
            {
                throw new ComDriveExceptions("ETX is missing", ComDriveExceptions.ComDriveException.ETXMissing);
            }

            messageReceived = true;
        }

        internal override void Connect()
        {
            if (m_PLCSocket != null)
            {
                if (m_PLCSocket.Connected)
                    return;

                try
                {
                    m_PLCSocket.Close();
                }
                catch
                {
                }
            }

            if (Protocol == EthProtocol.TCP)
            {
                m_PLCSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }
            else
            {
                m_PLCSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            }

            SocketState socketState = new SocketState();
            socketState.Socket = m_PLCSocket;
            socketState.State = SocketState.ConnectionState.Connecting;

            bool isHostNameMode = false;
            IPAddress ipAddress = null;
            try
            {
                ipAddress = IPAddress.Parse(m_remoteIPorHostName);
            }
            catch
            {
                isHostNameMode = true;
            }

            IPEndPoint PLCIPEndPoint = null;

            if (!isHostNameMode)
            {
                PLCIPEndPoint = new IPEndPoint(ipAddress, Convert.ToInt32(m_remotePort));
            }

            try
            {
                if (!isHostNameMode)
                {
                    IAsyncResult result = m_PLCSocket.BeginConnect(PLCIPEndPoint, socketConnect, socketState);
                    bool success = result.AsyncWaitHandle.WaitOne(connectTimeOut, true);

                    lock (socketState)
                    {
                        if (success)
                        {
                            try
                            {
                                m_PLCSocket.EndConnect(result);
                            }
                            catch
                            {
                                socketState.State = SocketState.ConnectionState.Failed;
                                throw;
                            }
                        }
                        else
                        {
                            socketState.State = SocketState.ConnectionState.Failed;
                            throw new SocketException(10060);
                        }
                    }

                    // Enfora modems Suck. It require a sleep after the connection before data is being sent to it
                    // Otherwise the communication fails.
                    Thread.Sleep(500);
                }
                else
                {
                    IAsyncResult result = m_PLCSocket.BeginConnect(m_remoteIPorHostName, m_remotePort, socketConnect,
                        socketState);
                    bool success = result.AsyncWaitHandle.WaitOne(connectTimeOut, true);

                    lock (socketState)
                    {
                        if (success)
                        {
                            try
                            {
                                m_PLCSocket.EndConnect(result);
                            }
                            catch
                            {
                                socketState.State = SocketState.ConnectionState.Failed;
                                throw;
                            }
                        }
                        else
                        {
                            socketState.State = SocketState.ConnectionState.Failed;
                            throw new SocketException(10060);
                        }
                    }

                    // Enfora modems Suck. It require a sleep after the connection before data is being sent to it
                    // Otherwise the communication fails.
                    Thread.Sleep(500);
                }

                string text = ConnectionStatus.ConnectionOpened + " on port " + m_remotePort.ToString();
                ComDriverLogger.LogConnectionState(DateTime.Now, Utils.HelperComDriverLogger.GetLoggerChannel(this),
                    text);
            }
            catch (SocketException se)
            {
                if (se.Message.Contains(m_remoteIPorHostName))
                    throw new ComDriveExceptions(se.Message,
                        ComDriveExceptions.ComDriveException.GeneralCommunicationError);
                else
                    throw new ComDriveExceptions(se.Message + " " + m_remoteIPorHostName + ":" + m_remotePort,
                        ComDriveExceptions.ComDriveException.GeneralCommunicationError);
            }
        }

        private void socketConnect(IAsyncResult ar)
        {
            try
            {
                SocketState socketState = (SocketState) ar.AsyncState;
                lock (socketState)
                {
                    if (socketState.State == SocketState.ConnectionState.Failed)
                    {
                        socketState.Socket.Close();
                    }
                    else
                    {
                        socketState.State = SocketState.ConnectionState.Connected;
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Closes the Socket connection and releases all associated resources.
        /// </summary>
        public override void Disconnect()
        {
            if (base.QueueCount != 0)
                return;

            if (m_PLCSocket != null)
            {
                if (m_PLCSocket.Connected)
                {
                    m_PLCSocket.Close();

                    try
                    {
                        ComDriverLogger.LogConnectionState(DateTime.Now,
                            Utils.HelperComDriverLogger.GetLoggerChannel(this),
                            ConnectionStatus.ConnectionClosed.ToString());
                    }
                    catch
                    {
                    }
                }
            }
        }

        internal override bool IsEquivalentChannel(Channel anotherChanel)
        {
            Ethernet ethernet = anotherChanel as Ethernet;
            if ((ethernet.RemoteIP == m_remoteIPorHostName) && (ethernet.m_remotePort == m_remotePort))
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

        private void sendAyncronic_Callback(IAsyncResult ar)
        {
            Socket s = (Socket) ar.AsyncState;
            try
            {
                s.EndSend(ar);
            }
            catch (SocketException se)
            {
                throw new ComDriveExceptions(se.Message,
                    ComDriveExceptions.ComDriveException.GeneralCommunicationError);
            }
        }

        private void receiveAyncronic_Callback(IAsyncResult ar)
        {
            Socket s = (Socket) ar.AsyncState;
            try
            {
                s.EndReceive(ar);
            }
            catch (SocketException se)
            {
                throw new ComDriveExceptions(se.Message,
                    ComDriveExceptions.ComDriveException.GeneralCommunicationError);
            }
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
            byte[] tempBuffer;
            byte[] copyOfBuffer;
            int bytesToRead;
            bytesToRead = m_PLCSocket.Available;

            if (bytesToRead > 0)
            {
                tempBuffer = new byte[bytesToRead];
                m_PLCSocket.Receive(tempBuffer);
                copyOfBuffer = incomingData;
                incomingData = new byte[copyOfBuffer.Length + tempBuffer.Length];
                Array.Copy(copyOfBuffer, 0, incomingData, 0, copyOfBuffer.Length);
                Array.Copy(tempBuffer, 0, incomingData, copyOfBuffer.Length, tempBuffer.Length);

                ComDriverLogger.LogReceivedMessageChunk(DateTime.Now,
                    Utils.HelperComDriverLogger.GetLoggerChannel(this), tempBuffer);
            }
        }

        private void appendReceivedDataToString(ref string incomingData)
        {
            byte[] tempBuffer;
            string bufferAsString;
            int bytesToRead;
            bytesToRead = m_PLCSocket.Available;

            if (bytesToRead > 0)
            {
                tempBuffer = new byte[bytesToRead];
                m_PLCSocket.Receive(tempBuffer);
                bufferAsString = ASCIIEncoding.ASCII.GetString(tempBuffer);
                incomingData += bufferAsString;

                ComDriverLogger.LogReceivedMessageChunk(DateTime.Now,
                    Utils.HelperComDriverLogger.GetLoggerChannel(this), tempBuffer);
            }
        }

        #endregion
    }
}