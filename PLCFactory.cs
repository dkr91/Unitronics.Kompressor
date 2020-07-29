using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Collections;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Unitronics.ComDriver.Executers;
using Unitronics.ComDriver.Messages;
using System.Xml.Serialization;

namespace Unitronics.ComDriver
{
    public static class PLCFactory
    {
        #region Locals

        private static List<Channel> m_channels = new List<Channel>();
        private static object objectLocker = new object();
        private static bool m_ActivationService = false;
        private static bool m_WorkWithXmlInternally = false;
        private static ushort messageDelay = 0;

        #endregion

        #region IPLCFactory Members

        public static PLC GetPLC(Channel channel, int unitId, bool forceJazz = false)
        {
            bool isConnected = false;
            return getPLC(ref channel, unitId, ref isConnected, forceJazz);
        }


        /// <summary>
        /// if plcName is not null then after connecting to the PLC object
        /// it checks if the PLC Name matches plcName. If not then
        /// ComDriveException.PlcNameMismatch is being throws
        /// </summary>
        public static PLC GetPLC(Channel channel, int unitId, string plcName, bool forceJazz = false)
        {
            bool isConnected = false;
            PLC plc;
            plc = getPLC(ref channel, unitId, ref isConnected, forceJazz);

            // if plcName is not null then after connecting to the PLC object
            // it checks if the PLC Name matches plcName. If not then exception
            // is being throws

            // Prevent checking for PLC Name for U90 PLCs or when the PLC does not support Binary Commands
            // (Like in Preboot and such...)
            var plcVersion = plc.Version;
            if (plcVersion.SupportedExecuters[ExecutersType.BasicBinary] == true)
            {
                if (plcName != null)
                {
                    string plcNameOnPlc = plc.PlcName;
                    string plcNameWithoutEncoding = Encoding.UTF7.GetString(ASCIIEncoding.Default.GetBytes(plcName));
                    if (plcNameWithoutEncoding != plcNameOnPlc)
                    {
                        try
                        {
                            if ((!isConnected) && (channel.GetType() != typeof(EthernetListener)))
                                channel.Disconnect();
                        }
                        catch
                        {
                        }

                        throw new ComDriveExceptions(
                            "The given PLC name: '" + plcName +
                            "' doesn't match the actual PLC Name of the PLC you tried to connect to.",
                            ComDriveExceptions.ComDriveException.PlcNameMismatch);
                    }
                }
            }
            else if (plcVersion.SuppressEthernetHeader && plcName != null)
            {
                string plcNameOnPlc = plc.PlcName;
                string plcNameWithoutEncoding = Encoding.UTF7.GetString(ASCIIEncoding.Default.GetBytes(plcName));
                if (plcNameWithoutEncoding != plcNameOnPlc)
                {
                    try
                    {
                        if ((!isConnected) && (channel.GetType() != typeof(EthernetListener)))
                            channel.Disconnect();
                    }
                    catch
                    {
                    }

                    throw new ComDriveExceptions(
                        "The given PLC name: '" + plcName +
                        "' doesn't match the actual PLC Name of the PLC you tried to connect to.",
                        ComDriveExceptions.ComDriveException.PlcNameMismatch);
                }
            }

            return GetPLC(channel, unitId, forceJazz);
        }

        public static void GetPLC(EthernetListener ethernetListener)
        {
            try
            {
                bool isConnected = false;
                Channel plcChannel = getChannel(ethernetListener, ref isConnected);
                EthernetListener listener = plcChannel as EthernetListener;
                if (listener == null)
                    throw new ComDriveExceptions("Ethernet Listener could not be intialized due to unexpected error",
                        ComDriveExceptions.ComDriveException.UnexpectedError);

                listener.Listen();
            }
            catch (ComDriveExceptions ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                string exceptionText = ex.GetType().ToString() + ": " + ex.Message + "\n\n" + ex.StackTrace;
                ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
                throw;
            }
        }

        public static void GetPLC(ListenerServer litenerServer)
        {
            try
            {
                bool isConnected = false;
                Channel plcChannel = getChannel(litenerServer, ref isConnected);
                ListenerServer listener = plcChannel as ListenerServer;
                if (listener == null)
                    throw new ComDriveExceptions("Listener Server could not be intialized due to unexpected error",
                        ComDriveExceptions.ComDriveException.UnexpectedError);

                listener.Listen();
            }
            catch (ComDriveExceptions ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                string exceptionText = ex.GetType().ToString() + ": " + ex.Message + "\n\n" + ex.StackTrace;
                ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
                throw;
            }
        }

        public static PLC GetPLC(string xmlFilePath)
        {
            PlcComConfig plcComConfig;
            using (FileStream fs = new FileStream(xmlFilePath, FileMode.Open))
            {
                XmlSerializer xs = new XmlSerializer(typeof(PlcComConfig));
                plcComConfig = (PlcComConfig) xs.Deserialize(fs);
            }

            Channel channel = plcComConfig.Channel;
            int unitId = plcComConfig.UnitID;
            if (plcComConfig.RequirePlcName)
            {
                return GetPLC(channel, unitId, plcComConfig.PlcName);
            }
            else
            {
                return GetPLC(channel, unitId);
            }
        }

        public static PLC GetPLC(PlcComConfig plcComConfig)
        {
            Channel channel = plcComConfig.Channel;
            int unitId = plcComConfig.UnitID;

            bool forceJazz = false;
            if (channel is Ethernet || channel is EthernetListener || channel is ListenerClient)
            {
                forceJazz = plcComConfig.ForceJazz;
            }

            if (plcComConfig.RequirePlcName)
            {
                return GetPLC(channel, unitId, plcComConfig.PlcName, forceJazz);
            }
            else
            {
                return GetPLC(channel, unitId, forceJazz);
            }
        }

        internal static PLC getPLC(ref Channel channel, int unitId, ref bool isConnected, bool suppressEthernetHeader)
        {
            try
            {
                isConnected = false;
                if (unitId < 0 || unitId > 127)
                {
                    throw new ComDriveExceptions("UnitID out of range! The value must be between 0 and 127.",
                        ComDriveExceptions.ComDriveException.InvalidUnitID);
                }

                Channel plcChannel = getChannel(channel, ref isConnected);
                channel = plcChannel;

                try
                {
                    return new PLC(unitId, plcChannel, suppressEthernetHeader);
                }
                catch (ComDriveExceptions ex)
                {
                    if ((!isConnected) && (channel.GetType() != typeof(EthernetListener)))
                        plcChannel.Disconnect();

                    throw;
                }
                catch (Exception ex)
                {
                    string exceptionText = ex.GetType().ToString() + ": " + ex.Message + "\n\n" + ex.StackTrace;
                    ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
                    throw;
                }
            }
            catch (ComDriveExceptions ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                string exceptionText = ex.GetType().ToString() + ": " + ex.Message + "\n\n" + ex.StackTrace;
                ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
                throw;
            }
        }

        public static Channel GetChannel(Channel channel)
        {
            if (channel is Serial)
            {
                Serial serial = channel as Serial;
                return GetChannel(serial.PortName);
            }
            else if (channel is Ethernet)
            {
                Ethernet ethernet = channel as Ethernet;
                return GetChannel(ethernet.RemoteIP, ethernet.RemotePort);
            }
            else if (channel is EthernetListener)
            {
                EthernetListener listener = channel as EthernetListener;
                return GetChannel(listener.LocalPort);
            }
            else if (channel is ListenerServer)
            {
                ListenerServer listenerServer = channel as ListenerServer;
                ListenerServer result;
                GetChannel(listenerServer.LocalPort, out result);
                return result;
            }
            else
            {
                return null;
            }
        }

        public static void GetChannel(int port, out ListenerServer listenerServer)
        {
            try
            {
                foreach (Channel c in m_channels)
                {
                    if (c.GetType() == typeof(ListenerServer))
                    {
                        ListenerServer listener = c as ListenerServer;
                        if (listener.LocalPort == port)
                        {
                            listenerServer = listener;
                            return;
                        }
                    }
                }

                listenerServer = null;
            }
            catch (ComDriveExceptions ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                string exceptionText = ex.GetType().ToString() + ": " + ex.Message + "\n\n" + ex.StackTrace;
                ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
                throw;
            }
        }

        public static EthernetListener GetChannel(int port)
        {
            try
            {
                foreach (Channel c in m_channels)
                {
                    if (c.GetType() == typeof(EthernetListener))
                    {
                        EthernetListener listener = c as EthernetListener;
                        if (listener.LocalPort == port)
                            return listener;
                    }
                }

                return null;
            }
            catch (ComDriveExceptions ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                string exceptionText = ex.GetType().ToString() + ": " + ex.Message + "\n\n" + ex.StackTrace;
                ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
                throw;
            }
        }

        public static Ethernet GetChannel(string remoteIp, Int32 remotePort)
        {
            try
            {
                foreach (Channel c in m_channels)
                {
                    if (c.GetType() == typeof(Ethernet))
                    {
                        Ethernet ethernet = c as Ethernet;
                        if ((ethernet.RemoteIP == remoteIp) && (ethernet.RemotePort == remotePort))
                            return ethernet;
                    }
                }

                return null;
            }
            catch (ComDriveExceptions ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                string exceptionText = ex.GetType().ToString() + ": " + ex.Message + "\n\n" + ex.StackTrace;
                ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
                throw;
            }
        }

        public static Serial GetChannel(SerialPortNames portName)
        {
            try
            {
                foreach (Channel c in m_channels)
                {
                    if (c.GetType() == typeof(Serial))
                    {
                        Serial serial = c as Serial;
                        if (serial.PortName == portName)
                            return serial;
                    }
                }

                return null;
            }
            catch (ComDriveExceptions ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                string exceptionText = ex.GetType().ToString() + ": " + ex.Message + "\n\n" + ex.StackTrace;
                ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
                throw;
            }
        }

        internal static bool ValidateChannelPropertyChange(Serial channel, SerialPortNames newPortName)
        {
            if (!m_channels.Contains(channel))
            {
                return true;
            }
            else
            {
                Serial serial = GetChannel(newPortName);
                if (serial == null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        internal static bool ValidateChannelPropertyChange(Ethernet channel, string newIp, int newPort)
        {
            if (!m_channels.Contains(channel))
            {
                return true;
            }
            else
            {
                Ethernet ethernet = GetChannel(newIp, newPort);
                if (ethernet == null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        internal static bool ValidateChannelPropertyChange(EthernetListener channel, int newPort)
        {
            if (!m_channels.Contains(channel))
            {
                return true;
            }
            else
            {
                EthernetListener listener = GetChannel(newPort);
                if (listener == null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        internal static bool ValidateChannelPropertyChange(ListenerServer channel, int newPort)
        {
            if (!m_channels.Contains(channel))
            {
                return true;
            }
            else
            {
                ListenerServer listener;
                GetChannel(newPort, out listener);
                if (listener == null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public static bool WorkWithXmlInternally
        {
            get { return m_WorkWithXmlInternally; }
            set { m_WorkWithXmlInternally = value; }
        }

        public static ushort MessageDelay
        {
            get { return messageDelay; }
            set { messageDelay = value; }
        }

        #endregion

        #region ActivationService

        public static void ActivationService(Object activationService)
        {
            try
            {
                if (activationService is String)
                {
                    string activationString = activationService as String;
                    System.Security.Cryptography.MD5CryptoServiceProvider MD5 =
                        new System.Security.Cryptography.MD5CryptoServiceProvider();
                    byte[] encodedBytes = MD5.ComputeHash(ASCIIEncoding.ASCII.GetBytes(activationString));
                    activationString = Utils.HexEncoding.GetHexTwoCharsPerByte(encodedBytes);
                    if (activationString == "7C765F7F05BC73663FFA9ED33B9998D5")
                    {
                        m_ActivationService = true;
                    }
                    else
                    {
                        m_ActivationService = false;
                    }
                }
            }
            catch (ComDriveExceptions ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                string exceptionText = ex.GetType().ToString() + ": " + ex.Message + "\n\n" + ex.StackTrace;
                ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
                throw;
            }
        }

        internal static bool ActivationServiceEnabled
        {
            get { return m_ActivationService; }
        }

        #endregion

        #region ComDriverLogger

        public static void EnableLogger()
        {
            try
            {
                ComDriverLogger.Enable();
            }
            catch (ComDriveExceptions ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                string exceptionText = ex.GetType().ToString() + ": " + ex.Message + "\n\n" + ex.StackTrace;
                ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
                throw;
            }
        }

        public static void EnableLogger(LogTypesConfig logTypesConfig)
        {
            try
            {
                ComDriverLogger.Enable(logTypesConfig);
            }
            catch (ComDriveExceptions ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                string exceptionText = ex.GetType().ToString() + ": " + ex.Message + "\n\n" + ex.StackTrace;
                ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
                throw;
            }
        }


        public static void DisableLogger()
        {
            try
            {
                ComDriverLogger.Disable();
            }
            catch (ComDriveExceptions ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                string exceptionText = ex.GetType().ToString() + ": " + ex.Message + "\n\n" + ex.StackTrace;
                ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
                throw;
            }
        }

        public static void LoadLoggerSettingsFromConfig()
        {
            ComDriverLogger.Disable();
        }

        #endregion

        #region Private

        private static Channel getChannel(Channel channel, ref bool isConnected)
        {
            lock (objectLocker)
            {
                if (m_channels.Contains(channel) || channel is ListenerClient)
                {
                    //there is already a channel with the same configuration
                    try
                    {
                        isConnected = channel.Connected;
                    }
                    catch
                    {
                    }

                    return channel;
                }
                else
                {
                    if (m_channels.Count == 0) //no channels 
                    {
                        try
                        {
                            isConnected = channel.Connected;
                        }
                        catch
                        {
                        }

                        if (channel.GetType() == typeof(EthernetListener))
                        {
                            EthernetListener listener = channel as EthernetListener;
                            //listener.Listen();
                            listener.AlreadyInitialized = false;
                            m_channels.Add(channel);
                            return channel;
                        }
                        else
                        {
                            channel.Connect();
                            channel.AlreadyInitialized = false;
                            m_channels.Add(channel);
                            return channel;
                        }
                    }
                    else
                    {
                        foreach (Channel c in m_channels)
                        {
                            if (c.GetType() == channel.GetType())
                            {
                                if (c.IsEquivalentChannel(channel))
                                {
                                    try
                                    {
                                        isConnected = c.Connected;
                                    }
                                    catch
                                    {
                                    }

                                    // if Serial port name is the same or Ethernet IP:Port is the same then...
                                    if (c.GetType() == typeof(Serial))
                                    {
                                        Serial openedChannel = c as Serial;
                                        Serial newChannel = channel as Serial;
                                        if (c.Connected == true)
                                        {
                                            c.AlreadyInitialized = true;
                                            if ((openedChannel.PortName == newChannel.PortName) &&
                                                ((openedChannel.BaudRate != newChannel.BaudRate) ||
                                                 (openedChannel.Retry != newChannel.Retry) ||
                                                 (openedChannel.TimeOut != newChannel.TimeOut) ||
                                                 (openedChannel.Parity != newChannel.Parity) ||
                                                 (openedChannel.DataBits != newChannel.DataBits) ||
                                                 (openedChannel.StopBits != newChannel.StopBits)))
                                                throw new ComDriveExceptions(
                                                    "Opened Serial connection parameters cannot be modified",
                                                    ComDriveExceptions.ComDriveException.CommunicationParamsException);
                                        }
                                        else
                                        {
                                            // return the c channel and change its properties
                                            c.AlreadyInitialized = false;
                                            openedChannel.BaudRate = newChannel.BaudRate;
                                            openedChannel.Parity = newChannel.Parity;
                                            openedChannel.DataBits = newChannel.DataBits;
                                            openedChannel.StopBits = newChannel.StopBits;
                                            openedChannel.TimeOut = newChannel.TimeOut;
                                            openedChannel.Retry = newChannel.Retry;
                                            openedChannel.AutoDetectComParams = newChannel.AutoDetectComParams;
                                            openedChannel.Connect();
                                        }
                                    }
                                    else if (c.GetType() == typeof(Ethernet))
                                    {
                                        Ethernet newChannel = channel as Ethernet;
                                        Ethernet openedChannel = c as Ethernet;
                                        if (c.Connected == true)
                                        {
                                            c.AlreadyInitialized = true;
                                            if ((openedChannel.Retry != newChannel.Retry) ||
                                                (openedChannel.TimeOut != newChannel.TimeOut) ||
                                                (openedChannel.Protocol != newChannel.Protocol))
                                            {
                                                throw new ComDriveExceptions(
                                                    "Opened Ethernet parameters cannot be modified",
                                                    ComDriveExceptions.ComDriveException.CommunicationParamsException);
                                            }
                                        }
                                        else
                                        {
                                            // return the c channel and change its properties
                                            c.AlreadyInitialized = false;
                                            openedChannel.Protocol = newChannel.Protocol;
                                            openedChannel.TimeOut = newChannel.TimeOut;
                                            openedChannel.Retry = newChannel.Retry;
                                            openedChannel.Connect();
                                        }
                                    }
                                    else if (c.GetType() == typeof(EthernetListener))
                                    {
                                        EthernetListener newChannel = channel as EthernetListener;
                                        EthernetListener openedChannel = c as EthernetListener;
                                        if (c.Connected == true)
                                        {
                                            c.AlreadyInitialized = true;
                                            if ((openedChannel.Retry != newChannel.Retry) ||
                                                (openedChannel.TimeOut != newChannel.TimeOut))
                                            {
                                                throw new ComDriveExceptions(
                                                    "Opened Ethernet parameters cannot be modified",
                                                    ComDriveExceptions.ComDriveException.CommunicationParamsException);
                                            }
                                        }
                                        else
                                        {
                                            // return the c channel and change its properties
                                            c.AlreadyInitialized = false;
                                            openedChannel.TimeOut = newChannel.TimeOut;
                                            openedChannel.Retry = newChannel.Retry;
                                            //openedChannel.Listen();
                                        }
                                    }

                                    return c;
                                }
                            }
                        }

                        isConnected = channel.Connected;
                        if (!channel.Connected)
                        {
                            channel.Connect();
                            channel.AlreadyInitialized = false;
                            m_channels.Add(channel);
                            return channel;
                        }

                        return null;
                    }
                }
            }
        }

        #endregion
    }
}