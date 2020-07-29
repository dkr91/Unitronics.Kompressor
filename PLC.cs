using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using Unitronics.ComDriver.Messages.DataRequest;
using Unitronics.ComDriver.Executers;
using Unitronics.ComDriver.Messages;
using System.Threading;

namespace Unitronics.ComDriver
{
    public class PLC : IDisposable
    {
        #region Locals

        private Channel m_channel;
        private PlcVersion m_version;
        private ExecutersContainer m_ExecutersContainer;
        private ForcedParams m_ForcedParams;
        private int m_unitId;
        private SD m_SD;

        private struct PlcResponseMessage
        {
            internal string responseStringMessage;
            internal byte[] responseBytesMessage;
            internal CommunicationException comException;
        }

        private Dictionary<GuidClass, PlcResponseMessage> m_responseMessageQueue =
            new Dictionary<GuidClass, PlcResponseMessage>();

        private object _lockObj = new object();

        private delegate void VoidInvoker();

        public delegate void AbortCompletedDelegate();

        public event AbortCompletedDelegate EventAbortCompleted;

        internal Guid plcGuid;

        #endregion

        public event RiseError EventRiseError;

        public delegate void RiseError(string errorMessage);

        #region Constructors

        internal PLC(int unitId, Channel channel, bool suppressEthernetHeader = false)
        {
            int originalRetry;
            m_unitId = unitId;
            m_channel = channel;
            originalRetry = m_channel.Retry;

            if ((m_channel.GetType() == typeof(Serial)) && (!m_channel.AlreadyInitialized)
                                                        && (unitId < 64) &&
                                                        ((m_channel as Serial).AutoDetectComParams == true))
            {
                m_channel.Retry = 1;
            }

            try
            {
                m_version = getVersion(unitId, channel, suppressEthernetHeader);
            }
            catch (Exception ex)
            {
                if (ex.GetType().Equals(typeof(ComDriveExceptions)) &&
                    ex.Message.Equals("File 'PLCModels.xml' is missing!"))
                    throw ex as ComDriveExceptions;

                if ((m_channel.GetType() == typeof(Serial)) && (!m_channel.AlreadyInitialized)
                                                            && (unitId < 64) &&
                                                            ((m_channel as Serial).AutoDetectComParams == true))
                {
                    // Break command comes here 
                    // Break command (Communication params synch) is not allowed on RS485 (Unit ID 64 to 127)
                    try
                    {
                        sendBreakCommand(false);
                    }
                    catch
                    {
                        sendBreakCommand(true);
                    }

                    m_channel.AlreadyInitialized = true;
                    m_version = getVersion(unitId, channel,
                        suppressEthernetHeader); // try resending the GetID command after the PLC has a new baudrate
                }
                else
                {
                    m_channel.Retry = originalRetry;
                    throw new ComDriveExceptions("Cannot communicate with the PLC with the specified UnitID!",
                        ComDriveExceptions.ComDriveException.CommunicationTimeout);
                }
            }
            finally
            {
                m_channel.Retry = originalRetry;
            }

            if (m_unitId != 0)
            {
                // Lets check if the unitID of the PLC which is connected directly is the same as the current PLC
                // If it does then the buffer size can stay as it is.
                // If it doesn't then the buffersize should be limited to 160
                if (m_unitId < 64)
                {
                    if (m_unitId != getUnitIdForPlcInDirectConnection())
                    {
                        m_version.SetBufferSize(160);
                    }
                }
                else
                {
                    m_version.SetBufferSize(160);
                }
            }

            if (m_version.OPLCModel == null)
            {
                throw new ComDriveExceptions("Unknown PLC Model", ComDriveExceptions.ComDriveException.UnknownPlcModel);
            }

            plcGuid = Guid.NewGuid();
            m_ExecutersContainer = new ExecutersContainer(plcGuid, unitId, channel, m_version);
            m_SD = new SD(this, m_version);

            setExecuters(unitId, channel);

            setForcedParams();
        }

        ~PLC()
        {
            PLCChannel = null;
        }

        #endregion

        #region Public

        public void ForceParams(ForcedParams forcedParams)
        {
            if (PLCChannel is ListenerClient)
            {
                ListenerClient listenerClient = PLCChannel as ListenerClient;
                if (listenerClient.Disposed)
                {
                    throw new ComDriveExceptions("PLC Object is disposed due to Connection close of Listener Client",
                        ComDriveExceptions.ComDriveException.ObjectDisposed);
                }
            }

            try
            {
                m_ForcedParams = forcedParams;
                setForcedParams();
                PlcVersion version = this.Version;
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

        public void ReadWrite(ref ReadWriteRequest[] values)
        {
            if (PLCChannel is ListenerClient)
            {
                ListenerClient listenerClient = PLCChannel as ListenerClient;
                if (listenerClient.Disposed)
                {
                    throw new ComDriveExceptions("PLC Object is disposed due to Connection close of Listener Client",
                        ComDriveExceptions.ComDriveException.ObjectDisposed);
                }
            }

            try
            {
                if (validateExecuter())
                    ReadWrite(ref values, null);
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

        public void ReadWrite(ref ReadWriteRequest[] values, AsyncCallback callbackFunction)
        {
            if (PLCChannel is ListenerClient)
            {
                ListenerClient listenerClient = PLCChannel as ListenerClient;
                if (listenerClient.Disposed)
                {
                    throw new ComDriveExceptions("PLC Object is disposed due to Connection close of Listener Client",
                        ComDriveExceptions.ComDriveException.ObjectDisposed);
                }
            }

            try
            {
                if (m_ExecutersContainer.BreakFlagCount < 0)
                {
                    System.Diagnostics.Debug.Assert(false);
                }
                else if (m_ExecutersContainer.BreakFlag)
                {
                    throw new ComDriveExceptions(
                        "The Com Drive is still aborting the communication to the PLC. Please try again soon.",
                        ComDriveExceptions.ComDriveException.GeneralException);
                }

                if (validateExecuter())
                {
                    bool suppressEthernetHeader = false;
                    if (SuppressEthernetHeader.HasValue)
                    {
                        suppressEthernetHeader = SuppressEthernetHeader.Value;
                    }

                    if (callbackFunction != null)
                    {
                        ReadWriteOperandsDelegate del = new ReadWriteOperandsDelegate(m_ExecutersContainer.ReadWrite);
                        del.BeginInvoke(ref values, suppressEthernetHeader, callbackFunction, del);
                    }
                    else
                    {
                        m_ExecutersContainer.ReadWrite(ref values, suppressEthernetHeader);
                    }
                }
            }
            catch (ComDriveExceptions)
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

        public void SetExecuter(OperandsExecuterType plcType)
        {
            if (PLCChannel is ListenerClient)
            {
                ListenerClient listenerClient = PLCChannel as ListenerClient;
                if (listenerClient.Disposed)
                {
                    throw new ComDriveExceptions("PLC Object is disposed due to Connection close of Listener Client",
                        ComDriveExceptions.ComDriveException.ObjectDisposed);
                }
            }

            try
            {
                m_version.z_OperandsExecuterType = plcType;
                switch (plcType)
                {
                    case OperandsExecuterType.ExecuterAscii:
                        m_ExecutersContainer.OperandsExecuter =
                            new AsciiExecuter(this.UnitId, m_channel, m_version, plcGuid);
                        break;
                    case OperandsExecuterType.ExecuterPartialBinaryMix:
                        m_ExecutersContainer.OperandsExecuter =
                            new PartialBinaryMixExecuter(this.UnitId, m_channel, m_version, plcGuid);
                        break;
                    case OperandsExecuterType.ExecuterFullBinaryMix:
                        FullBinaryMixExecuter fullBinaryMixExecuter =
                            new FullBinaryMixExecuter(this.UnitId, m_channel, m_version, plcGuid);
                        m_ExecutersContainer.OperandsExecuter = fullBinaryMixExecuter;
                        break;
                }
            }
            catch (ComDriveExceptions)
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

        public void Disconnect()
        {
            try
            {
                m_channel.Disconnect();
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

        public void Reset()
        {
            if (PLCChannel is ListenerClient)
            {
                ListenerClient listenerClient = PLCChannel as ListenerClient;
                if (listenerClient.Disposed)
                {
                    throw new ComDriveExceptions("PLC Object is disposed due to Connection close of Listener Client",
                        ComDriveExceptions.ComDriveException.ObjectDisposed);
                }
            }

            PlcResponseMessage plcResponseMessage;
            try
            {
                if (!m_channel.Connected)
                    m_channel.Connect();
                string resetCommand = Utils.ResetCommand(m_unitId);
                GuidClass guid = new GuidClass();
                lock (guid)
                {
                    bool suppressEthernetHeader = false;
                    if (SuppressEthernetHeader.HasValue)
                    {
                        suppressEthernetHeader = SuppressEthernetHeader.Value;
                    }

                    m_channel.Send(resetCommand, receiveString, guid, guid.ToString(), "ASCII Protocol - PLC Reset",
                        plcGuid, suppressEthernetHeader: suppressEthernetHeader);
                    Monitor.Wait(guid);
                }

                lock (_lockObj)
                {
                    plcResponseMessage = m_responseMessageQueue[guid];
                    m_responseMessageQueue.Remove(guid);
                }

                if (plcResponseMessage.comException == CommunicationException.Timeout)
                {
                    throw new ComDriveExceptions("Cannot communicate with the PLC with the specified UnitID!",
                        ComDriveExceptions.ComDriveException.CommunicationTimeout);
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType() != typeof(ComDriveExceptions))
                {
                    string exceptionText = ex.GetType().ToString() + ": " + ex.Message + "\n\n" + ex.StackTrace;
                    ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
                    throw;
                }
            }
        }

        public void Init()
        {
            if (PLCChannel is ListenerClient)
            {
                ListenerClient listenerClient = PLCChannel as ListenerClient;
                if (listenerClient.Disposed)
                {
                    throw new ComDriveExceptions("PLC Object is disposed due to Connection close of Listener Client",
                        ComDriveExceptions.ComDriveException.ObjectDisposed);
                }
            }

            PlcResponseMessage plcResponseMessage;
            try
            {
                if (!m_channel.Connected)
                    m_channel.Connect();
                string resetCommand = Utils.InitCommand(m_unitId);
                GuidClass guid = new GuidClass();
                lock (guid)
                {
                    bool suppressEthernetHeader = false;
                    if (SuppressEthernetHeader.HasValue)
                    {
                        suppressEthernetHeader = SuppressEthernetHeader.Value;
                    }

                    m_channel.Send(resetCommand, receiveString, guid, guid.ToString(), "ASCII Protocol - PLC Init",
                        plcGuid, suppressEthernetHeader: suppressEthernetHeader);
                    Monitor.Wait(guid);
                }

                lock (_lockObj)
                {
                    plcResponseMessage = m_responseMessageQueue[guid];
                    m_responseMessageQueue.Remove(guid);
                }

                if (plcResponseMessage.comException == CommunicationException.Timeout)
                {
                    throw new ComDriveExceptions("Cannot communicate with the PLC with the specified UnitID!",
                        ComDriveExceptions.ComDriveException.CommunicationTimeout);
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType() != typeof(ComDriveExceptions))
                {
                    string exceptionText = ex.GetType().ToString() + ": " + ex.Message + "\n\n" + ex.StackTrace;
                    ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
                    throw;
                }
            }
        }

        public void Stop()
        {
            if (PLCChannel is ListenerClient)
            {
                ListenerClient listenerClient = PLCChannel as ListenerClient;
                if (listenerClient.Disposed)
                {
                    throw new ComDriveExceptions("PLC Object is disposed due to Connection close of Listener Client",
                        ComDriveExceptions.ComDriveException.ObjectDisposed);
                }
            }

            PlcResponseMessage plcResponseMessage;
            try
            {
                if (!m_channel.Connected)
                    m_channel.Connect();
                string resetCommand = Utils.StopCommand(m_unitId);
                GuidClass guid = new GuidClass();
                lock (guid)
                {
                    bool suppressEthernetHeader = false;
                    if (SuppressEthernetHeader.HasValue)
                    {
                        suppressEthernetHeader = SuppressEthernetHeader.Value;
                    }

                    m_channel.Send(resetCommand, receiveString, guid, guid.ToString(), "ASCII Protocol - PLC Stop",
                        plcGuid, suppressEthernetHeader: suppressEthernetHeader);
                    Monitor.Wait(guid);
                }

                lock (_lockObj)
                {
                    plcResponseMessage = m_responseMessageQueue[guid];
                    m_responseMessageQueue.Remove(guid);
                }

                if (plcResponseMessage.comException == CommunicationException.Timeout)
                {
                    throw new ComDriveExceptions("Cannot communicate with the PLC with the specified UnitID!",
                        ComDriveExceptions.ComDriveException.CommunicationTimeout);
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType() != typeof(ComDriveExceptions))
                {
                    string exceptionText = ex.GetType().ToString() + ": " + ex.Message + "\n\n" + ex.StackTrace;
                    ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
                    throw;
                }
            }
        }

        public void Run()
        {
            if (PLCChannel is ListenerClient)
            {
                ListenerClient listenerClient = PLCChannel as ListenerClient;
                if (listenerClient.Disposed)
                {
                    throw new ComDriveExceptions("PLC Object is disposed due to Connection close of Listener Client",
                        ComDriveExceptions.ComDriveException.ObjectDisposed);
                }
            }

            PlcResponseMessage plcResponseMessage;
            try
            {
                if (!m_channel.Connected)
                    m_channel.Connect();
                string resetCommand = Utils.RunCommand(m_unitId);
                GuidClass guid = new GuidClass();
                lock (guid)
                {
                    bool suppressEthernetHeader = false;
                    if (SuppressEthernetHeader.HasValue)
                    {
                        suppressEthernetHeader = SuppressEthernetHeader.Value;
                    }

                    m_channel.Send(resetCommand, receiveString, guid, guid.ToString(), "ASCII Protocol - PLC Run",
                        plcGuid, suppressEthernetHeader: suppressEthernetHeader);
                    Monitor.Wait(guid);
                }

                lock (_lockObj)
                {
                    plcResponseMessage = m_responseMessageQueue[guid];
                    m_responseMessageQueue.Remove(guid);
                }

                if (plcResponseMessage.comException == CommunicationException.Timeout)
                {
                    throw new ComDriveExceptions("Cannot communicate with the PLC with the specified UnitID!",
                        ComDriveExceptions.ComDriveException.CommunicationTimeout);
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType() != typeof(ComDriveExceptions))
                {
                    string exceptionText = ex.GetType().ToString() + ": " + ex.Message + "\n\n" + ex.StackTrace;
                    ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
                    throw;
                }
            }
        }

        public void Abort()
        {
            m_ExecutersContainer.BreakFlag = true;
            m_SD.BreakFlag = true;

            VoidInvoker del;

            del = new VoidInvoker(waitForAbortToComplete);
            del.BeginInvoke(null, del);
        }

        public string SendString(string message)
        {
            if (PLCChannel is ListenerClient)
            {
                ListenerClient listenerClient = PLCChannel as ListenerClient;
                if (listenerClient.Disposed)
                {
                    throw new ComDriveExceptions("PLC Object is disposed due to Connection close of Listener Client",
                        ComDriveExceptions.ComDriveException.ObjectDisposed);
                }
            }

            return SendString(message, m_unitId);
        }

        public string SendString(string message, int unitId)
        {
            if (PLCChannel is ListenerClient)
            {
                ListenerClient listenerClient = PLCChannel as ListenerClient;
                if (listenerClient.Disposed)
                {
                    throw new ComDriveExceptions("PLC Object is disposed due to Connection close of Listener Client",
                        ComDriveExceptions.ComDriveException.ObjectDisposed);
                }
            }

            string result;
            PlcResponseMessage plcResponseMessage;
            GuidClass guid = new GuidClass();

            if (!m_channel.Connected)
                m_channel.Connect();

            string messageToSend = Utils.GetGenericCommand(unitId, message);

            lock (guid)
            {
                bool suppressEthernetHeader = false;
                if (SuppressEthernetHeader.HasValue)
                {
                    suppressEthernetHeader = SuppressEthernetHeader.Value;
                }

                m_channel.Send(messageToSend, receiveString, guid, guid.ToString(),
                    "ASCII Protocol - Generic Command sent by SendString", plcGuid,
                    suppressEthernetHeader: suppressEthernetHeader);
                Monitor.Wait(guid);
            }

            lock (_lockObj)
            {
                plcResponseMessage = m_responseMessageQueue[guid];
                m_responseMessageQueue.Remove(guid);
            }

            if (plcResponseMessage.comException == CommunicationException.Timeout)
            {
                throw new ComDriveExceptions("Cannot communicate with the PLC with the specified UnitID!",
                    ComDriveExceptions.ComDriveException.CommunicationTimeout);
            }

            result = getClearRespone(plcResponseMessage.responseStringMessage);
            return result;
        }

        #endregion

        #region Properties

        public int UnitId
        {
            get { return m_unitId; }
            set
            {
                try
                {
                    if (value < 1 || value > 127)
                    {
                        throw new ComDriveExceptions("UnitID out of range! The value must be between 1 and 127.",
                            ComDriveExceptions.ComDriveException.InvalidUnitID);
                    }

                    if (m_unitId == value) return;

                    if (PLCChannel is ListenerClient)
                    {
                        ListenerClient listenerClient = PLCChannel as ListenerClient;
                        if (listenerClient.Disposed)
                        {
                            throw new ComDriveExceptions(
                                "PLC Object is disposed due to Connection close of Listener Client",
                                ComDriveExceptions.ComDriveException.ObjectDisposed);
                        }
                    }

                    else
                    {
                        setNewUnitIdForPLC(m_unitId, value);
                        m_unitId = value;
                        m_ExecutersContainer.SetNewUnitId(m_unitId);
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
        }

        public PlcVersion Version
        {
            get
            {
                if (PLCChannel is ListenerClient)
                {
                    ListenerClient listenerClient = PLCChannel as ListenerClient;
                    if (listenerClient.Disposed)
                    {
                        throw new ComDriveExceptions(
                            "PLC Object is disposed due to Connection close of Listener Client",
                            ComDriveExceptions.ComDriveException.ObjectDisposed);
                    }
                }

                try
                {
                    if (m_channel != null)
                    {
                        string oldPlcModel = "";
                        string oldOsVersion = "";
                        string oldBootVersion = "";
                        string oldBinLibVersion = "";

                        if (m_version != null)
                        {
                            oldPlcModel = m_version.OPLCModel;
                            oldOsVersion = m_version.OSVersion;
                            oldBootVersion = m_version.Boot;
                            oldBinLibVersion = m_version.BinLib;
                        }

                        bool suppressEthernetHeader = false;
                        if (SuppressEthernetHeader.HasValue)
                        {
                            suppressEthernetHeader = SuppressEthernetHeader.Value;
                        }

                        m_version = getVersion(m_unitId, m_channel, suppressEthernetHeader);

                        if (oldPlcModel != m_version.OPLCModel ||
                            oldOsVersion != m_version.OSVersion ||
                            oldBootVersion != m_version.Boot ||
                            oldBinLibVersion != m_version.BinLib)
                        {
                            setExecuters(m_unitId, m_channel);
                        }

                        if (m_unitId != 0)
                        {
                            //Lets check if the unitID of the PLC which is connected directly is the same as the current PLC
                            //If it does then the buffer size can stay as it is.
                            //If it doesn't then the buffersize should be limited to 160
                            if (m_unitId < 64)
                            {
                                if (m_unitId != getUnitIdForPlcInDirectConnection())
                                {
                                    m_version.SetBufferSize(160);
                                }
                            }
                            else
                            {
                                m_version.SetBufferSize(160);
                            }
                        }

                        m_ExecutersContainer.OperandsExecuter = getOperandsExecuter(m_unitId, m_channel, m_version);
                        setForcedParams();
                    }

                    return m_version;
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
            set { m_version = value; }
        }

        public DateTime RTC
        {
            get
            {
                if (PLCChannel is ListenerClient)
                {
                    ListenerClient listenerClient = PLCChannel as ListenerClient;
                    if (listenerClient.Disposed)
                    {
                        throw new ComDriveExceptions(
                            "PLC Object is disposed due to Connection close of Listener Client",
                            ComDriveExceptions.ComDriveException.ObjectDisposed);
                    }
                }

                try
                {
                    return getRTC(m_unitId, m_channel);
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
            set
            {
                if (PLCChannel is ListenerClient)
                {
                    ListenerClient listenerClient = PLCChannel as ListenerClient;
                    if (listenerClient.Disposed)
                    {
                        throw new ComDriveExceptions(
                            "PLC Object is disposed due to Connection close of Listener Client",
                            ComDriveExceptions.ComDriveException.ObjectDisposed);
                    }
                }

                try
                {
                    setRTC(m_unitId, m_channel, value);
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
        }

        public Channel PLCChannel
        {
            get { return m_channel; }
            internal set { m_channel = value; }
        }

        public string PlcName
        {
            get
            {
                if (PLCChannel is ListenerClient)
                {
                    ListenerClient listenerClient = PLCChannel as ListenerClient;
                    if (listenerClient.Disposed)
                    {
                        throw new ComDriveExceptions(
                            "PLC Object is disposed due to Connection close of Listener Client",
                            ComDriveExceptions.ComDriveException.ObjectDisposed);
                    }
                }

                try
                {
                    if (m_version.SuppressEthernetHeader)
                    {
                        return getJazzPlcName(m_unitId);
                    }
                    else
                    {
                        return getPlcName(m_unitId, m_channel);
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
        }

        public SD SD
        {
            get { return m_SD; }
        }

        internal bool? SuppressEthernetHeader
        {
            get
            {
                if (m_version != null)
                {
                    return m_version.SuppressEthernetHeader;
                }

                return null;
            }
        }

        #endregion

        #region Private

        private void waitForAbortToComplete()
        {
            System.Diagnostics.Debug.Print("Aborting");
            System.Diagnostics.Debug.Print("Aborting Read Write. Count: " +
                                           m_ExecutersContainer.BreakFlagCount.ToString());

            while (m_ExecutersContainer.BreakFlagCount >= 0 || m_SD.BreakFlagCount >= 0)
            {
                System.Diagnostics.Debug.Print("Still Aborting");
                if (m_ExecutersContainer.BreakFlagCount <= 0 && m_SD.BreakFlagCount <= 0)
                {
                    m_ExecutersContainer.BreakFlagCount = 0;
                    m_SD.BreakFlagCount = 0;
                    System.Diagnostics.Debug.Print("Reseting Flag Count");
                    if (EventAbortCompleted != null)
                    {
                        EventAbortCompleted();
                    }

                    return;
                }

                System.Threading.Thread.Sleep(10);
            }
        }

        private void setExecuters(int unitId, Channel channel)
        {
            // Operands Executer
            if (m_version.SupportedExecuters[ExecutersType.Operands] == true)
            {
                m_ExecutersContainer.OperandsExecuter = getOperandsExecuter(unitId, channel, m_version);
            }
            else
            {
                m_ExecutersContainer.OperandsExecuter = null;
            }

            // Data Tables Executer
            if (m_version.SupportedExecuters[ExecutersType.DataTables] == true)
            {
                m_ExecutersContainer.DataTablesExecuter = new DataTablesExecuter(unitId, channel, m_version, plcGuid);
            }
            else
            {
                m_ExecutersContainer.DataTablesExecuter = null;
            }

            // Basic Binary Executer
            if (m_version.SupportedExecuters[ExecutersType.BasicBinary] == true)
            {
                m_ExecutersContainer.BasicBinaryExecuter = new BinaryExecuter(unitId, channel, m_version, plcGuid);
            }
            else
            {
                m_ExecutersContainer.BasicBinaryExecuter = null;
            }

            // Binary Executer
            if (m_version.SupportedExecuters[ExecutersType.Binary] == true)
            {
                m_ExecutersContainer.BinaryExecuter = new BinaryExecuter(unitId, channel, m_version, plcGuid);
            }
            else
            {
                m_ExecutersContainer.BinaryExecuter = null;
            }
        }

        private void setForcedParams()
        {
            if (m_ForcedParams.BufferSize > 0)
            {
                if (m_ForcedParams.CalcBinaryExecuterForcedBufferSizeOnDataOnly)
                    m_version.SetBufferSize(m_ForcedParams.BufferSize +
                                            (ushort) Utils.Lengths.LENGTH_HEADER_AND_FOOTER);
                else
                    m_version.SetBufferSize(m_ForcedParams.BufferSize);

                if (m_ExecutersContainer != null)
                {
                    m_ExecutersContainer.OperandsExecuter = getOperandsExecuter(m_unitId, m_channel, m_version);
                    if (m_ExecutersContainer.BasicBinaryExecuter != null)
                        m_ExecutersContainer.BasicBinaryExecuter =
                            new BinaryExecuter(m_unitId, m_channel, m_version, plcGuid);

                    if (m_ExecutersContainer.BinaryExecuter != null)
                        m_ExecutersContainer.BinaryExecuter =
                            new BinaryExecuter(m_unitId, m_channel, m_version, plcGuid);

                    if (m_ExecutersContainer.DataTablesExecuter != null)
                        m_ExecutersContainer.DataTablesExecuter =
                            new DataTablesExecuter(m_unitId, m_channel, m_version, plcGuid);
                }
            }

            // None keeps it unchanged
            if (m_ForcedParams.ExecuterType != OperandsExecuterType.None)
            {
                SetExecuter(m_ForcedParams.ExecuterType);
            }
        }

        private bool validateExecuter()
        {
            if (m_ExecutersContainer == null)
            {
                if (EventRiseError != null)
                {
                    EventRiseError("PLC is in PreBoot state! The request cannot be performed!");
                    return false;
                }
                else
                    throw new Exception("PLC is in PreBoot state! The request cannot be performed!");
            }

            return true;
        }

        private Executer getOperandsExecuter(int unitId, Channel channel, PlcVersion plcVersion)
        {
            switch (plcVersion.z_OperandsExecuterType)
            {
                case OperandsExecuterType.ExecuterAscii:
                    return new AsciiExecuter(unitId, channel, plcVersion, plcGuid);
                case OperandsExecuterType.ExecuterPartialBinaryMix:
                    return new PartialBinaryMixExecuter(unitId, channel, plcVersion, plcGuid);
                case OperandsExecuterType.ExecuterFullBinaryMix:
                    return new FullBinaryMixExecuter(unitId, channel, plcVersion, plcGuid);
            }

            return null;
        }

        private PlcVersion getVersion(int unitId, Channel channel, bool suppressEthernetHeader)
        {
            PlcResponseMessage plcResponseMessage;

            if (!m_channel.Connected)
                m_channel.Connect();
            string idCommand = Utils.GetIDCommand(unitId);
            GuidClass guid = new GuidClass();

            lock (guid)
            {
                if (this.SuppressEthernetHeader.HasValue)
                {
                    channel.Send(idCommand, receiveString, guid, guid.ToString(), "ASCII Protocol - Get Version",
                        plcGuid, true, this.SuppressEthernetHeader.Value);
                }
                else
                {
                    channel.Send(idCommand, receiveString, guid, guid.ToString(), "ASCII Protocol - Get Version",
                        plcGuid, true, suppressEthernetHeader);
                }

                Monitor.Wait(guid);
            }

            lock (_lockObj)
            {
                plcResponseMessage = m_responseMessageQueue[guid];
                m_responseMessageQueue.Remove(guid);
            }

            if (plcResponseMessage.comException == CommunicationException.Timeout)
            {
                throw new ComDriveExceptions("Cannot communicate with the PLC with the specified UnitID!",
                    ComDriveExceptions.ComDriveException.CommunicationTimeout);
            }

            return new PlcVersion(plcResponseMessage.responseStringMessage);
        }

        private DateTime getRTC(int unitId, Channel channel)
        {
            PlcResponseMessage plcResponseMessage;

            if (!m_channel.Connected)
                m_channel.Connect();

            string getRtcCommand = Utils.GetRtcCommand(unitId);
            GuidClass guid = new GuidClass();

            lock (guid)
            {
                bool suppressEthernetHeader = false;
                if (SuppressEthernetHeader.HasValue)
                {
                    suppressEthernetHeader = SuppressEthernetHeader.Value;
                }

                channel.Send(getRtcCommand, receiveString, guid, guid.ToString(), "ASCII Protocol - Get RTC", plcGuid,
                    suppressEthernetHeader: suppressEthernetHeader);
                Monitor.Wait(guid);
            }

            lock (_lockObj)
            {
                plcResponseMessage = m_responseMessageQueue[guid];
                m_responseMessageQueue.Remove(guid);
            }

            if (plcResponseMessage.comException == CommunicationException.Timeout)
            {
                throw new ComDriveExceptions("Cannot communicate with the PLC with the specified UnitID!",
                    ComDriveExceptions.ComDriveException.CommunicationTimeout);
            }

            return Utils.getDateTime(plcResponseMessage.responseStringMessage);
        }

        private void setRTC(int unitId, Channel channel, DateTime dateTime)
        {
            PlcResponseMessage plcResponseMessage;

            if (!m_channel.Connected)
                m_channel.Connect();
            string setRtcCommand = Utils.SetRtcCommand(unitId, dateTime);
            GuidClass guid = new GuidClass();

            lock (guid)
            {
                bool suppressEthernetHeader = false;
                if (SuppressEthernetHeader.HasValue)
                {
                    suppressEthernetHeader = SuppressEthernetHeader.Value;
                }

                channel.Send(setRtcCommand, receiveString, guid, guid.ToString(), "ASCII Protocol - Set RTC", plcGuid,
                    suppressEthernetHeader: suppressEthernetHeader);
                Monitor.Wait(guid);
            }

            lock (_lockObj)
            {
                plcResponseMessage = m_responseMessageQueue[guid];
                m_responseMessageQueue.Remove(guid);
            }

            if (plcResponseMessage.comException == CommunicationException.Timeout)
            {
                throw new ComDriveExceptions("Cannot communicate with the PLC with the specified UnitID!",
                    ComDriveExceptions.ComDriveException.CommunicationTimeout);
            }
        }

        private string getPlcName(int unitId, Channel channel)
        {
            PlcResponseMessage plcResponseMessage;

            if (!m_channel.Connected)
                m_channel.Connect();

            Command.PComB pComB = Utils.getPlcName(unitId);

            GuidClass guid = new GuidClass();

            lock (guid)
            {
                channel.Send(pComB.MessageToPLC as byte[], receiveBytes, guid, guid.ToString(),
                    "Binary Protocol - Get PLC Name", plcGuid);
                Monitor.Wait(guid);
            }

            lock (_lockObj)
            {
                plcResponseMessage = m_responseMessageQueue[guid];
                m_responseMessageQueue.Remove(guid);
            }

            if (plcResponseMessage.comException == CommunicationException.Timeout)
            {
                throw new ComDriveExceptions("Cannot communicate with the PLC with the specified UnitID!",
                    ComDriveExceptions.ComDriveException.CommunicationTimeout);
            }


            byte[] incomingBuffer = new byte[plcResponseMessage.responseBytesMessage.Length -
                                             Utils.Lengths.LENGTH_HEADER_AND_FOOTER];
            if (incomingBuffer.Length > 0)
            {
                Array.Copy(plcResponseMessage.responseBytesMessage, Utils.Lengths.LENGTH_HEADER, incomingBuffer, 0,
                    incomingBuffer.Length);
            }

            return ASCIIEncoding.UTF7.GetString(incomingBuffer);
            //return ASCIIEncoding.ASCII.GetString(incomingBuffer);
        }

        private string getJazzPlcName(int unitId)
        {
            string result = string.Empty;
            var plcName = SendString("NMR", unitId);
            if (plcName.Length > 2)
            {
                result = plcName.Substring(2);
            }

            return result;
        }

        private void receiveString(string responseString, CommunicationException communicationException,
            GuidClass messageGuid)
        {
            PlcResponseMessage plcResponseMessage = new PlcResponseMessage
            {
                comException = communicationException,
                responseStringMessage = responseString
            };

            lock (_lockObj)
            {
                m_responseMessageQueue.Add(messageGuid, plcResponseMessage);
            }

            lock (messageGuid)
            {
                Monitor.PulseAll(messageGuid);
            }
        }

        private void receiveBytes(byte[] responseBytes, CommunicationException communicationException,
            GuidClass messageGuid)
        {
            PlcResponseMessage plcResponseMessage = new PlcResponseMessage
            {
                comException = communicationException,
                responseBytesMessage = responseBytes
            };

            lock (_lockObj)
            {
                m_responseMessageQueue.Add(messageGuid, plcResponseMessage);
            }

            lock (messageGuid)
            {
                Monitor.PulseAll(messageGuid);
            }
        }

        private void setNewUnitIdForPLC(int oldUnitId, int newUnitId)
        {
            GuidClass guid = new GuidClass();

            lock (guid)
            {
                bool suppressEthernetHeader = false;
                if (SuppressEthernetHeader.HasValue)
                {
                    suppressEthernetHeader = SuppressEthernetHeader.Value;
                }

                m_channel.Send(Utils.GetSetNewUnitIdCommand(oldUnitId, newUnitId), receiveString, guid, guid.ToString(),
                    "ASCII Protocol - Set Unit ID", plcGuid, suppressEthernetHeader: suppressEthernetHeader);
                Monitor.Wait(guid);
            }

            PlcResponseMessage plcResponseMessage;

            lock (_lockObj)
            {
                plcResponseMessage = m_responseMessageQueue[guid];
                m_responseMessageQueue.Remove(guid);
            }

            if (plcResponseMessage.comException == CommunicationException.Timeout)
            {
                throw new ComDriveExceptions("Cannot communicate with the PLC with the specified UnitID!",
                    ComDriveExceptions.ComDriveException.CommunicationTimeout);
            }
        }

        private int getUnitIdForPlcInDirectConnection()
        {
            PlcResponseMessage plcResponseMessage;

            GuidClass guid = new GuidClass();

            lock (guid)
            {
                bool suppressEthernetHeader = false;
                if (SuppressEthernetHeader.HasValue)
                {
                    suppressEthernetHeader = SuppressEthernetHeader.Value;
                }

                m_channel.Send(Utils.GetUnitIdCommand(0), receiveString, guid, guid.ToString(),
                    "ASCII Protocol - Get Unit ID", plcGuid, suppressEthernetHeader: suppressEthernetHeader);
                Monitor.Wait(guid);
            }

            lock (_lockObj)
            {
                plcResponseMessage = m_responseMessageQueue[guid];
                m_responseMessageQueue.Remove(guid);
            }

            if (plcResponseMessage.comException == CommunicationException.Timeout)
            {
                throw new ComDriveExceptions("Cannot communicate with the PLC with the specified UnitID!",
                    ComDriveExceptions.ComDriveException.CommunicationTimeout);
            }

            return Utils.getInitID(plcResponseMessage.responseStringMessage);
        }

        private void sendBreakCommand(bool isU90)
        {
            Serial serial = m_channel as Serial;
            int originalUnitId;
            DataBits dataBits = serial.DataBits;
            System.IO.Ports.Parity parity = serial.Parity;
            System.IO.Ports.StopBits stopBits = serial.StopBits;
            serial.BreakState = true;
            Thread.Sleep(500);
            serial.BreakState = false;
            Thread.Sleep(1000);
            BaudRate originalBaudRate = serial.BaudRate; // Saving original baudrate
            originalUnitId = m_unitId; // Saving original unit id
            m_unitId = 0; // Unit id must be 0
            string breakCommand = "";
            PlcResponseMessage plcResponseMessage;

            if (isU90)
            {
                serial.DataBits = DataBits.DB7;
                serial.Parity = System.IO.Ports.Parity.Even;
                serial.StopBits = System.IO.Ports.StopBits.One;
                breakCommand = Utils.GetGenericCommand(m_unitId,
                    serial.SetPlcConnetionParamsU90((int) dataBits, parity, stopBits));
            }
            else
            {
                breakCommand = Utils.GetGenericCommand(m_unitId, serial.BreakCommand());
            }

            serial.Disconnect();
            serial.BaudRate = BaudRate.BR9600;
            serial.Connect();
            Thread.Sleep(1000);
            try
            {
                GuidClass guid = new GuidClass();

                lock (guid)
                {
                    bool suppressEthernetHeader = false;
                    if (SuppressEthernetHeader.HasValue)
                    {
                        suppressEthernetHeader = SuppressEthernetHeader.Value;
                    }

                    m_channel.Send(breakCommand, receiveString, guid, guid.ToString(),
                        "ASCII Protocol - Synch Communication Params", plcGuid,
                        suppressEthernetHeader: suppressEthernetHeader);
                    Monitor.Wait(guid);
                }

                lock (_lockObj)
                {
                    plcResponseMessage = m_responseMessageQueue[guid];
                    m_responseMessageQueue.Remove(guid);
                }
            }
            catch
            {
                throw new ComDriveExceptions("Cannot communicate with the PLC with the specified UnitID!",
                    ComDriveExceptions.ComDriveException.CommunicationTimeout);
            }
            finally
            {
                m_unitId = originalUnitId;
                serial.Disconnect();
                serial.BaudRate = originalBaudRate;
                if (isU90)
                {
                    serial.DataBits = dataBits;
                    serial.Parity = parity;
                    serial.StopBits = stopBits;
                }

                if (serial.Connected)
                {
                    Thread.Sleep(100);
                }

                serial.Connect();
                Thread.Sleep(1000);
            }

            if (plcResponseMessage.comException == CommunicationException.Timeout)
            {
                throw new ComDriveExceptions("Cannot communicate with the PLC with the specified UnitID!",
                    ComDriveExceptions.ComDriveException.CommunicationTimeout);
            }
        }

        private string getClearRespone(string response)
        {
            string result;

            result = response.Substring(Utils.Lengths.LENGTH_STX1 + Utils.Lengths.LENGTH_UNIT_ID,
                response.Length - Utils.Lengths.LENGTH_STX1 - Utils.Lengths.LENGTH_UNIT_ID - Utils.Lengths.LENGTH_CRC -
                Utils.Lengths.LENGTH_ETX);

            return result;
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            if (PLCChannel is ListenerClient)
            {
                ListenerClient listenerClient = PLCChannel as ListenerClient;
                listenerClient.Dispose();
            }
        }

        #endregion
    }
}