using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Data.OleDb;
using System.Configuration;
using System.Data;
using System.IO;
using Unitronics.ComDriver.Messages.DataRequest;
using System.Runtime.Serialization.Formatters.Soap;
using System.Runtime.Serialization;
using System.Security;
using Unitronics.ComDriver.Resources;
using System.Xml;
using System.Collections;
using System.Xml.Linq;

namespace Unitronics.ComDriver
{
    internal static class ComDriverLogger
    {
        #region Locals

        private static OleDbConnection oleDbConnection;
        private static LogTypesConfig logTypesConfig;
        private static int logFileIndex = 0;
        private static object objectLocket = new object();

        #endregion

        #region Constructor

        static ComDriverLogger()
        {
            _updateLogTypesConfig();
        }

        #endregion

        #region Internal

        internal static void Enable()
        {
            //logTypesConfig.LogConnectionState = false;
            //logTypesConfig.LogExceptions = false;
            //logTypesConfig.LogFullMessage = true;
            //logTypesConfig.LogReadWriteRequest = false;
            //logTypesConfig.LogReceivedMessageChunk = false;
            _readNextLogFileIndexFromXml();

            if (logTypesConfig.LogConnectionState || logTypesConfig.LogExceptions || logTypesConfig.LogFullMessage
                || logTypesConfig.LogReadWriteRequest || logTypesConfig.LogReceivedMessageChunk)
            {
                _openOledbConnection();
            }
        }

        internal static void Enable(LogTypesConfig _logTypesConfig)
        {
            _readNextLogFileIndexFromXml();

            _openOledbConnection();
            logTypesConfig = _logTypesConfig;
        }

        internal static void Disable()
        {
            _resetLogTypesConfig();
            _closeOledbConnection();
        }

        internal static void LoadLoggerSettingsFromConfig()
        {
            _updateLogTypesConfig();

            if (logTypesConfig.LogConnectionState || logTypesConfig.LogExceptions || logTypesConfig.LogFullMessage
                || logTypesConfig.LogReadWriteRequest || logTypesConfig.LogReceivedMessageChunk)
            {
                _openOledbConnection();
            }
        }

        internal static void LogConnectionState(DateTime dateTime, string channel, string text)
        {
            if (!logTypesConfig.LogConnectionState)
                return;

            ComDriverLogEntry logEntry = new ComDriverLogEntry();
            logEntry.LogType = LogType.ConnectionState;
            logEntry.DateTime = dateTime;
            logEntry.LoggerChannel = channel;
            logEntry.Text = text;
            logEntry.MessageDirection = MessageDirection.Unspecified;
            logEntry.ParentID = Utils.UnspecifiedString;
            logEntry.RequestGUID = Utils.UnspecifiedString;
            logEntry.CurrentRetry = Utils.UnspecifiedString;
            logEntry.Message = DBNull.Value;
            logEntry.RequestPropertiesId = Utils.UnspecifiedString;

            _write(logEntry);
        }

        internal static void LogExceptions(DateTime dateTime, string exceptionText)
        {
            if (!logTypesConfig.LogExceptions)
                return;

            ComDriverLogEntry logEntry = new ComDriverLogEntry();
            logEntry.LogType = LogType.Exceptions;
            logEntry.DateTime = dateTime;
            logEntry.LoggerChannel = Utils.UnspecifiedString;
            logEntry.Text = exceptionText;
            logEntry.MessageDirection = MessageDirection.Unspecified;
            logEntry.ParentID = Utils.UnspecifiedString;
            logEntry.RequestGUID = Utils.UnspecifiedString;
            logEntry.CurrentRetry = Utils.UnspecifiedString;
            logEntry.Message = DBNull.Value;
            logEntry.RequestPropertiesId = Utils.UnspecifiedString;

            _write(logEntry);
        }

        internal static void LogReceivedMessageChunk(DateTime dateTime, string channel, byte[] tmpBuffer)
        {
            if (!logTypesConfig.LogReceivedMessageChunk)
                return;

            ComDriverLogEntry logEntry = new ComDriverLogEntry();
            logEntry.LogType = LogType.ReceivedMessageChunk;
            logEntry.DateTime = dateTime;
            logEntry.LoggerChannel = channel;
            logEntry.Text = Utils.UnspecifiedString;
            logEntry.MessageDirection = MessageDirection.Received;
            logEntry.ParentID = Utils.UnspecifiedString;
            logEntry.RequestGUID = Utils.UnspecifiedString;
            logEntry.CurrentRetry = Utils.UnspecifiedString;
            logEntry.Message = tmpBuffer;
            logEntry.RequestPropertiesId = Utils.UnspecifiedString;

            _write(logEntry);
        }

        internal static void LogFullMessage(DateTime dateTime, string channel, string requestGUID,
            MessageDirection messageDirection, string currentRetry, object message, string parentID,
            string messageDescription)
        {
            if (!logTypesConfig.LogFullMessage)
                return;

            ComDriverLogEntry logEntry = new ComDriverLogEntry();
            logEntry.LogType = LogType.FullMessage;
            logEntry.DateTime = dateTime;
            logEntry.LoggerChannel = channel;
            logEntry.Text = messageDescription;
            logEntry.MessageDirection = messageDirection;
            logEntry.ParentID = parentID;
            logEntry.RequestGUID = requestGUID;
            logEntry.CurrentRetry = currentRetry;
            logEntry.Message = message;
            logEntry.RequestPropertiesId = Utils.UnspecifiedString;

            _write(logEntry);
        }

        internal static void LogReadWriteRequest(DateTime dateTime, string channel, ReadWriteRequest[] requests,
            MessageDirection messageDirection, string parentID)
        {
            if (!logTypesConfig.LogReadWriteRequest)
                return;

            ComDriverLogEntry logEntry = new ComDriverLogEntry();
            SoapFormatter sf = new SoapFormatter();
            using (MemoryStream ms = new MemoryStream())
            {
                try
                {
                    sf.Serialize(ms, requests);
                }
                catch (SerializationException)
                {
                    throw;
                }
                catch (ArgumentNullException)
                {
                    throw;
                }
                catch (SecurityException)
                {
                    throw;
                }

                logEntry.LogType = LogType.ReadWriteRequest;
                logEntry.DateTime = dateTime;
                logEntry.LoggerChannel = channel;
                logEntry.Text = Utils.UnspecifiedString;
                logEntry.MessageDirection = messageDirection;
                logEntry.ParentID = parentID;
                logEntry.RequestGUID = Utils.UnspecifiedString;
                logEntry.CurrentRetry = Utils.UnspecifiedString;
                logEntry.Message = ms.ToArray();
                logEntry.RequestPropertiesId = _getRequestPropertiesId(requests);
            }

            _write(logEntry);
        }

        #endregion

        #region Properties

        internal static bool Enabled
        {
            get
            {
                if ((logTypesConfig.LogConnectionState) ||
                    (logTypesConfig.LogExceptions) ||
                    (logTypesConfig.LogFullMessage) ||
                    (logTypesConfig.LogReadWriteRequest) ||
                    (logTypesConfig.LogReceivedMessageChunk))
                    return true;
                else
                    return false;
            }
        }

        #endregion

        #region Private

        private static bool _openOledbConnection()
        {
            lock (objectLocket)
            {
                if (oleDbConnection == null)
                    oleDbConnection = new OleDbConnection(_getConnectionString());

                string connectionString = _getConnectionString();

                try
                {
                    switch (oleDbConnection.State)
                    {
                        case ConnectionState.Broken:
                            oleDbConnection.Close();

                            if (oleDbConnection.ConnectionString != connectionString)
                                oleDbConnection.ConnectionString = connectionString;

                            oleDbConnection.Open();
                            return true;

                        case ConnectionState.Closed:

                            if (oleDbConnection.ConnectionString != connectionString)
                                oleDbConnection.ConnectionString = connectionString;

                            oleDbConnection.Open();
                            return true;

                        case ConnectionState.Executing:
                        case ConnectionState.Fetching:
                        case ConnectionState.Open:
                            return true;
                    }
                }
                catch (InvalidOperationException)
                {
                    //return false;
                    throw;
                }
                catch (OleDbException)
                {
                    //return false
                    throw;
                }

                return false;
            }
        }

        private static void _closeOledbConnection()
        {
            if (oleDbConnection != null && oleDbConnection.State != ConnectionState.Closed)
            {
                oleDbConnection.Close();
            }
        }

        private static uint _getDbRowsNo()
        {
            if (!_openOledbConnection())
                return 0;

            uint dbRowsNo = 0;
            OleDbCommand cmd = oleDbConnection.CreateCommand();

            try
            {
                using (cmd)
                {
                    string selectCmd = "SELECT COUNT(*) FROM " + Utils.HelperComDriverLogger.ComDriverLogsTableName;

                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.Connection = oleDbConnection;
                    cmd.CommandText = selectCmd;

                    //If the oledbConnection is closed then open
                    if (_openOledbConnection())
                        dbRowsNo = Convert.ToUInt32(cmd.ExecuteScalar());
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }

            return dbRowsNo;
        }

        private static void _write(ComDriverLogEntry logEntry)
        {
            if (!_openOledbConnection())
                return;

            OleDbCommand cmd = oleDbConnection.CreateCommand();

            try
            {
                lock (objectLocket)
                {
                    using (cmd)
                    {
                        string insertCmd =
                            " INSERT INTO " + Utils.HelperComDriverLogger.ComDriverLogsTableName +
                            " (DateTimeC, Type, Channel, ParentID, GuidC, MessageDirection, CurrentRetry, Message, TextC, RequestPropertiesId) " +
                            " VALUES(@DateTime, @Type, @Channel, @ParentID, @GuidC, @MessageDirection, @CurrentRetry, @Message, @TextC, @RequestPropertiesId)";

                        cmd.CommandType = System.Data.CommandType.Text;
                        cmd.Connection = oleDbConnection;
                        cmd.CommandText = insertCmd;

                        OleDbParameter dateTimeParam = cmd.CreateParameter();
                        dateTimeParam.ParameterName = "@DateTimeC";
                        dateTimeParam.Value = logEntry.DateTime.Ticks;
                        //dateTimeParam.Value = Utils.HelperComDriverLogger.GetAccessDateTime(logEntry.DateTime);

                        OleDbParameter typeParam = cmd.CreateParameter();
                        typeParam.ParameterName = "@Type";
                        typeParam.Value = logEntry.LogType.ToString();

                        OleDbParameter channelParam = cmd.CreateParameter();
                        channelParam.ParameterName = "@Channel";
                        channelParam.Value = logEntry.LoggerChannel;

                        OleDbParameter parentIDParams = cmd.CreateParameter();
                        parentIDParams.ParameterName = "@ParentID";
                        parentIDParams.Value = logEntry.ParentID;

                        OleDbParameter GUIDParam = cmd.CreateParameter();
                        GUIDParam.ParameterName = "@GuidC";
                        GUIDParam.Value = logEntry.RequestGUID;

                        OleDbParameter messageDirectionParam = cmd.CreateParameter();
                        messageDirectionParam.ParameterName = "@MessageDirection";
                        messageDirectionParam.Value = logEntry.MessageDirection.ToString();

                        OleDbParameter currentRetryParam = cmd.CreateParameter();
                        currentRetryParam.ParameterName = "@CurrentRetry";
                        currentRetryParam.Value = logEntry.CurrentRetry;

                        OleDbParameter messageParam = cmd.CreateParameter();
                        messageParam.ParameterName = "@Message";

                        if (logEntry.Message != DBNull.Value)
                        {
                            messageParam.Value = (logEntry.Message.GetType() == typeof(String))
                                ? ASCIIEncoding.ASCII.GetBytes(logEntry.Message as String)
                                : messageParam.Value = logEntry.Message;
                        }
                        else
                            messageParam.Value = DBNull.Value;

                        OleDbParameter textCParam = cmd.CreateParameter();
                        textCParam.ParameterName = "@TextC";
                        textCParam.Value = logEntry.Text;

                        OleDbParameter requestPropertiesIdParam = cmd.CreateParameter();
                        requestPropertiesIdParam.ParameterName = "@RequestPropertiesId";
                        requestPropertiesIdParam.Value = logEntry.RequestPropertiesId;

                        cmd.Parameters.AddRange(new OleDbParameter[]
                        {
                            dateTimeParam, typeParam, channelParam, parentIDParams, GUIDParam,
                            messageDirectionParam, currentRetryParam, messageParam, textCParam,
                            requestPropertiesIdParam
                        });

                        if (_openOledbConnection())
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }

                    uint numOfRows = _getDbRowsNo();
                    if (numOfRows >= 100000)
                    {
                        _closeOledbConnection();
                        logFileIndex++;

                        string path = String.Empty;
                        path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName()
                            .CodeBase.Replace(@"file:///", ""));
                        string dirSeparator = Path.DirectorySeparatorChar.ToString();
                        if (path.Substring(path.Length - 1) == dirSeparator)
                            path = path.Substring(0, path.Length - 1);

                        XAttribute[] xAttributes = new XAttribute[1];
                        xAttributes[0] = new XAttribute("LogFileIndex", logFileIndex);
                        XElement root = new XElement("LogFile", xAttributes);
                        root.Save(path + "\\ComDriverLogs.xml");

                        _openOledbConnection();
                    }
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
        }

        private static string _getConnectionString()
        {
            string path = String.Empty;
            path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase
                .Replace(@"file:///", ""));
            string dirSeparator = Path.DirectorySeparatorChar.ToString();
            if (path.Substring(path.Length - 1) == dirSeparator)
                path = path.Substring(0, path.Length - 1);

            string connString = Utils.HelperComDriverLogger.AccessDbConnStringProvider + path + dirSeparator +
                                "ComDriverLogs_" + logFileIndex.ToString().PadLeft(3, '0') + ".mdb";

            // If mdb file doesn't exist than create it.
            if (!File.Exists(path + dirSeparator + "ComDriverLogs_" + logFileIndex.ToString().PadLeft(3, '0') + ".mdb"))
            {
                try
                {
                    File.WriteAllBytes(
                        path + dirSeparator + "ComDriverLogs_" + logFileIndex.ToString().PadLeft(3, '0') + ".mdb",
                        ComDriverResource.ComDriverLogs);
                }
                catch (Exception ex)
                {
                    throw new ComDriveExceptions("Error occured while creating mdb file! Error: " + ex.Message,
                        ComDriveExceptions.ComDriveException.CannotCreateFile);
                }
            }

            return connString;
        }

        private static void _updateLogTypesConfig()
        {
            string logConnectionStateApp = ConfigurationManager.AppSettings["LogConnectionState"];
            string LogExceptionsApp = ConfigurationManager.AppSettings["LogExceptions"];
            string LogReceivedMessageChunkApp = ConfigurationManager.AppSettings["LogReceivedMessageChunk"];
            string LogFullMessageApp = ConfigurationManager.AppSettings["LogFullMessage"];
            string LogReadWriteRequestApp = ConfigurationManager.AppSettings["LogReadWriteRequest"];

            if (logConnectionStateApp != null || logConnectionStateApp != String.Empty)
            {
                try
                {
                    logTypesConfig.LogConnectionState = Convert.ToBoolean(logConnectionStateApp);
                }
                catch
                {
                    logTypesConfig.LogConnectionState = false;
                }
            }
            else
                logTypesConfig.LogConnectionState = false;

            if (LogExceptionsApp != null || LogExceptionsApp != String.Empty)
            {
                try
                {
                    logTypesConfig.LogExceptions = Convert.ToBoolean(LogExceptionsApp);
                }
                catch
                {
                    logTypesConfig.LogExceptions = false;
                }
            }
            else
                logTypesConfig.LogExceptions = false;

            if (LogReceivedMessageChunkApp != null || LogReceivedMessageChunkApp != String.Empty)
            {
                try
                {
                    logTypesConfig.LogReceivedMessageChunk = Convert.ToBoolean(LogReceivedMessageChunkApp);
                }
                catch
                {
                    logTypesConfig.LogReceivedMessageChunk = false;
                }
            }
            else
                logTypesConfig.LogReceivedMessageChunk = false;

            if (LogFullMessageApp != null || LogFullMessageApp != String.Empty)
            {
                try
                {
                    logTypesConfig.LogFullMessage = Convert.ToBoolean(LogFullMessageApp);
                }
                catch
                {
                    logTypesConfig.LogFullMessage = false;
                }
            }
            else
                logTypesConfig.LogFullMessage = false;

            if (LogReadWriteRequestApp != null || LogReadWriteRequestApp != String.Empty)
            {
                try
                {
                    logTypesConfig.LogReadWriteRequest = Convert.ToBoolean(LogReadWriteRequestApp);
                }
                catch
                {
                    logTypesConfig.LogReadWriteRequest = false;
                }
            }
            else
                logTypesConfig.LogReadWriteRequest = false;
        }

        private static void _resetLogTypesConfig()
        {
            logTypesConfig.LogConnectionState = false;
            logTypesConfig.LogExceptions = false;
            logTypesConfig.LogFullMessage = false;
            logTypesConfig.LogReadWriteRequest = false;
            logTypesConfig.LogReceivedMessageChunk = false;
        }

        private static void _readNextLogFileIndexFromXml()
        {
            string path = String.Empty;
            path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase
                .Replace(@"file:///", ""));
            string dirSeparator = Path.DirectorySeparatorChar.ToString();
            if (path.Substring(path.Length - 1) == dirSeparator)
                path = path.Substring(0, path.Length - 1);

            if (File.Exists(path + "\\ComDriverLogs.xml"))
            {
                try
                {
                    logFileIndex = 0;
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(path + "\\ComDriverLogs.xml");

                    XmlNodeList nodeList = xmlDoc.GetElementsByTagName("LogFile");
                    IEnumerator enumerator = nodeList.GetEnumerator();

                    if (enumerator.MoveNext())
                    {
                        XmlElement xmlElem = (XmlElement) enumerator.Current;
                        if (xmlElem.HasAttribute("LogFileIndex"))
                        {
                            string index = xmlElem.Attributes["LogFileIndex"].Value.ToString();
                            logFileIndex = int.Parse(index);
                        }
                        else
                        {
                            logFileIndex = 0;
                        }
                    }
                    else
                    {
                        logFileIndex = 0;
                    }
                }
                catch
                {
                    logFileIndex = 0;
                }
            }
            else
            {
                logFileIndex = 0;
                XAttribute[] xAttributes = new XAttribute[1];
                xAttributes[0] = new XAttribute("LogFileIndex", logFileIndex);
                XElement root = new XElement("LogFile", xAttributes);
                root.Save(path + "\\ComDriverLogs.xml");
            }
        }

        #region RequestPropertiesId

        private static string _getRequestPropertiesId(ReadWriteRequest[] requests)
        {
            string requestPropertiesString = _getRequestPropertiesString(requests);

            if (requestPropertiesString.Equals(String.Empty))
                return String.Empty;

            return _getHashString(requestPropertiesString);
        }

        private static string _getHashString(string Value)
        {
            System.Security.Cryptography.MD5CryptoServiceProvider x =
                new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] data = System.Text.Encoding.ASCII.GetBytes(Value);
            data = x.ComputeHash(data);
            string ret = "";

            for (int i = 0; i < data.Length; i++)
                ret += data[i].ToString("x2").ToLower();

            return ret;
        }

        private static string _getRequestPropertiesString(ReadWriteRequest[] requests)
        {
            string result = String.Empty;

            for (int reqIter = 0; reqIter < requests.Length; reqIter++)
            {
                if (requests[reqIter] is ReadOperands ||
                    requests[reqIter] is WriteOperands)
                {
                    result += getROWORequestsPropertiesString(requests[reqIter]);
                }
                else if (requests[reqIter] is ReadDataTables ||
                         requests[reqIter] is WriteDataTables)
                {
                    result += getRDTorWDTRequestsPropertiesString(requests[reqIter]);
                }
            }

            return result;
        }

        private static string getRDTorWDTRequestsPropertiesString(ReadWriteRequest readWriteRequest)
        {
            string result = String.Empty;

            // The string results format is: 
            // RequestType:StartAddress-NumberOfRowsToRead/Write-RowSizeInBytes-NumberOfBytesToRead/WriteInRow.
            // ':' - is the delimitator for the requestType
            // '-' - is the delimitator between request properties.
            // '.' - used to mark the end of the request properties.
            // Example: RDT:1-10-20-10.

            if (readWriteRequest is ReadDataTables)
            {
                ReadDataTables rdt = readWriteRequest as ReadDataTables;

                result += "RDT:" + rdt.StartAddress.ToString() +
                          "-" + rdt.NumberOfRowsToRead.ToString() +
                          "-" + rdt.RowSizeInBytes.ToString() +
                          "-" + rdt.NumberOfBytesToReadInRow.ToString() +
                          "-" + rdt.PartOfProject.ToString() +
                          "-" + rdt.SubCommand.ToString() +
                          ".";
            }
            else
            {
                WriteDataTables wdt = readWriteRequest as WriteDataTables;
                result += "WDT" + wdt.StartAddress.ToString() +
                          "-" + wdt.NumberOfRowsToWrite.ToString() +
                          "-" + wdt.RowSizeInBytes.ToString() +
                          "-" + wdt.NumberOfBytesToWriteInRow.ToString() +
                          "-" + wdt.SubCommand.ToString() +
                          ".";
            }

            return result;
        }

        private static string getROWORequestsPropertiesString(ReadWriteRequest readWriteRequest)
        {
            string result = String.Empty;

            // The string results format is: 
            // RequestType:OperandType-StartAddress-NumberOfOperands.  where
            // ':' - is the delimitator for the requestType
            // '-' - is the delimitator between request properties.
            // '.' - used to mark the end of the request properties.
            // Example: RO:MI-1-10. means Read 10 MI from address 1.

            if (readWriteRequest is ReadOperands)
            {
                ReadOperands ro = readWriteRequest as ReadOperands;

                result += "RO:" + ro.OperandType.ToString() +
                          "-" + ro.StartAddress.ToString() +
                          "-" + ro.NumberOfOperands.ToString() +
                          ".";
            }
            else
            {
                WriteOperands wo = readWriteRequest as WriteOperands;
                result += "WO" + wo.OperandType.ToString() +
                          "-" + wo.StartAddress.ToString() +
                          "-" + wo.NumberOfOperands.ToString() +
                          ".";
            }

            return result;
        }

        #endregion

        #endregion
    }
}