using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Runtime.Remoting.Messaging;

namespace Unitronics.ComDriver
{
    internal class WrappedSocket
    {
        private Socket socketListener;
        private Socket socketWorker;

        public event EventHandler OnConnect;
        public event EventHandler OnClose;

        public delegate void DataReceivedDelegate(object sender, byte[] receivedBytes);

        public event DataReceivedDelegate OnDataReceived;

        public delegate void SocektErrorDelegate(object sender, SocketError socketError);

        public event SocektErrorDelegate OnSocektError;

        public string RemoteIP { get; private set; }


        private class SocketState
        {
            public Socket Socket { get; set; }
            public byte[] ReceivedData { get; set; }
            public SocketError ErrorCode;
        }

        public WrappedSocket()
        {
            RemoteIP = "";
            socketListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public void Listen(int port)
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, port);
            try
            {
                socketListener.Bind(ep);
            }
            catch
            {
                throw;
            }

            socketListener.Listen(0);
            socketListener.BeginAccept(onConnect, null);
        }

        public void Close()
        {
            if (socketListener != null)
            {
                try
                {
                    socketListener.Close();
                }
                catch
                {
                }

                socketListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }

            if (socketWorker != null)
            {
                try
                {
                    socketWorker.Close();
                }
                catch
                {
                }
            }
        }

        public Socket GetWorker()
        {
            return socketWorker;
        }

        public void Send(byte[] buffer)
        {
            try
            {
                socketWorker.Send(buffer);
            }
            catch
            {
                throw;
            }
        }

        public bool Connected
        {
            get
            {
                if (socketWorker == null)
                {
                    return false;
                }
                else
                {
                    return socketWorker.Connected;
                }
            }
        }

        private void onConnect(IAsyncResult ar)
        {
            try
            {
                socketWorker = socketListener.EndAccept(ar);
                IPEndPoint ep = socketWorker.RemoteEndPoint as IPEndPoint;
                RemoteIP = ep.Address.ToString();
                socketListener.Close();
                waitForData();
                if (OnConnect != null)
                {
                    OnConnect(this, null);
                }
            }
            catch
            {
            }
        }

        private void waitForData()
        {
            SocketState socketState = new SocketState();
            socketState.Socket = socketWorker;
            socketState.ReceivedData = new byte[1024];
            socketWorker.BeginReceive(socketState.ReceivedData, 0, socketState.ReceivedData.Length, SocketFlags.None,
                out socketState.ErrorCode, onDataReceived, socketState);
        }

        private void onDataReceived(IAsyncResult ar)
        {
            SocketState socketState = null;
            Socket worker = null;
            try
            {
                socketState = (SocketState) ar.AsyncState;
                worker = socketState.Socket;
                int cbRead = 0;
                cbRead = worker.EndReceive(ar);

                if (cbRead > 0)
                {
                    if (OnDataReceived != null)
                    {
                        byte[] temp = new byte[cbRead];
                        Array.Copy(socketState.ReceivedData, 0, temp, 0, cbRead);
                        OnDataReceived(this, temp);
                    }

                    waitForData();
                }
                else
                {
                    try
                    {
                        worker.Close();
                    }
                    catch
                    {
                    }

                    if (OnClose != null)
                    {
                        RemoteIP = "";
                        OnClose(this, null);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                try
                {
                    if (worker != null)
                        worker.Close();
                    else
                        socketWorker.Close();
                }
                catch
                {
                }

                if (OnClose != null)
                {
                    RemoteIP = "";
                    OnClose(this, null);
                }
            }
            catch (SocketException se)
            {
                try
                {
                    if (worker != null)
                        worker.Close();
                    else
                        socketWorker.Close();
                }
                catch
                {
                }

                if (OnSocektError != null)
                {
                    RemoteIP = "";
                    OnSocektError(this, socketState.ErrorCode);
                }
            }
            catch
            {
                try
                {
                    if (worker != null)
                        worker.Close();
                    else
                        socketWorker.Close();
                }
                catch
                {
                }

                if (OnSocektError != null)
                {
                    RemoteIP = "";
                    OnSocektError(this, socketState.ErrorCode);
                }
            }
        }
    }
}