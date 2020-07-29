using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Xml.Serialization;

namespace Unitronics.ComDriver
{
    public class ListenerServer : Channel
    {
        #region Locals

        private int m_LocalPort;
        private Socket socket;

        private Unitronics.ComDriver.EthernetListener.ConnectionStatus m_ConnectionStatus =
            Unitronics.ComDriver.EthernetListener.ConnectionStatus.Disconnected;

        private object lockObj = new object();

        public delegate void ConnectionAcceptedDelegate(PLC plcFromListener);

        public delegate void ConnectionStatusChangedDelegate(
            Unitronics.ComDriver.EthernetListener.ConnectionStatus connectionStatus);

        public event ConnectionAcceptedDelegate OnConnectionAccepted;
        public event ConnectionStatusChangedDelegate OnConnectionStatusChanged;

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
                        if (Status == Unitronics.ComDriver.EthernetListener.ConnectionStatus.Disconnected)
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
            get
            {
                throw new NotImplementedException(
                    "This class acts only as a gate which creates ListenerClient object when needed.\r\nThe 'Connected' property is individual to each of the ListenerClient objects");
            }
        }

        [XmlIgnoreAttribute]
        public Unitronics.ComDriver.EthernetListener.ConnectionStatus Status
        {
            get { return m_ConnectionStatus; }
            private set
            {
                m_ConnectionStatus = value;
                string channelLog = Utils.HelperComDriverLogger.GetLoggerChannel(this);

                try
                {
                    switch (m_ConnectionStatus)
                    {
                        case EthernetListener.ConnectionStatus.Listening:
                            ComDriverLogger.LogConnectionState(DateTime.Now, channelLog,
                                Unitronics.ComDriver.ConnectionStatus.Listening.ToString());
                            break;
                        default:
                            ComDriverLogger.LogConnectionState(DateTime.Now, channelLog,
                                Unitronics.ComDriver.ConnectionStatus.ConnectionClosed.ToString());
                            break;
                    }
                }
                catch
                {
                }

                if (OnConnectionStatusChanged != null)
                {
                    OnConnectionStatusChanged(m_ConnectionStatus);
                }
            }
        }

        #endregion


        #region Constructor

        public ListenerServer()
            : base()
        {
        }

        public ListenerServer(Int32 localPort, int retry, int TimeOut)
            : base(retry, TimeOut)
        {
            m_LocalPort = localPort;
        }

        #endregion

        #region Public

        internal override string GetLoggerChannelText()
        {
            return Utils.HelperComDriverLogger.GetLoggerChannel(this);
        }

        internal override void Connect()
        {
        }

        internal void Listen()
        {
            lock (lockObj)
            {
                if (Status == EthernetListener.ConnectionStatus.Disconnected || socket == null)
                {
                    try
                    {
                        listen();
                        Status = EthernetListener.ConnectionStatus.Listening;
                    }
                    catch
                    {
                        throw new ComDriveExceptions(
                            "Failed binding local port " + m_LocalPort.ToString() +
                            ". Please check that the port is not in use",
                            ComDriveExceptions.ComDriveException.PortInUse);
                    }
                }
            }
        }


        /// <summary>
        /// Closes the Socket connection and releases all associated resources.
        /// </summary>
        public override void Disconnect()
        {
            if (base.QueueCount != 0)
                return;

            try
            {
                socket.Close();
                Status = EthernetListener.ConnectionStatus.Disconnected;
            }
            catch
            {
            }
        }

        internal override bool IsEquivalentChannel(Channel anotherChanel)
        {
            ListenerServer listenerServer = anotherChanel as ListenerServer;
            if (listenerServer.LocalPort == m_LocalPort)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal override void SendString(string text, byte messageEnumerator, bool isIdMessage = false,
            bool suppressEthernetHeader = false)
        {
            throw new NotImplementedException();
        }

        internal override void SendBytes(byte[] bytes, byte messageEnumerator)
        {
            throw new NotImplementedException();
        }

        internal override string ReceiveString()
        {
            throw new NotImplementedException();
        }

        internal override byte[] ReceiveBytes()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region privates

        private void listen()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, LocalPort);
            try
            {
                socket.Bind(ep);
            }
            catch
            {
                throw;
            }

            socket.Listen(100);
            socket.BeginAccept(onConnect, null);
        }

        private void onConnect(IAsyncResult ar)
        {
            try
            {
                socket.BeginAccept(onConnect, null);
            }
            catch
            {
                Status = EthernetListener.ConnectionStatus.Disconnected;
            }

            Socket worker = null;
            try
            {
                worker = socket.EndAccept(ar);
                ListenerClient client = new ListenerClient(worker, LocalPort, base.Retry, base.TimeOut);

                System.Threading.Thread.Sleep(1000);
                PLC plc = PLCFactory.GetPLC(client, 0);
                if (OnConnectionAccepted != null)
                    OnConnectionAccepted(plc);
            }
            catch
            {
                if (worker != null)
                    worker.Close();
            }
        }

        #endregion
    }
}