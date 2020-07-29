using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver.Messages.DataRequest;

namespace Unitronics.ComDriver
{
    class ExecutersContainer
    {
        public Dictionary<string, Executer> Executers;
        private Guid m_PlcGuid;
        private bool m_BreakFlag;
        private int m_BreakFlagCount;
        private Object objectLocker = new Object();
        private readonly Channel m_channel;
        private int m_unitId;
        private readonly PlcVersion m_plcVersion;

        public ExecutersContainer(Guid plcGuid, int unitId, Channel channel, PlcVersion plcVersion)
        {
            m_PlcGuid = plcGuid;
            m_channel = channel;
            m_unitId = unitId;
            m_plcVersion = plcVersion;
            m_BreakFlag = false;
            m_BreakFlagCount = 0;

            Executers = new Dictionary<string, Executer>();
        }

        #region Properties

        public Executer OperandsExecuter
        {
            get
            {
                bool isKeyExist = Executers.ContainsKey("Operands");
                if (isKeyExist)
                {
                    return Executers["Operands"];
                }
                else
                {
                    return null;
                }
            }

            set
            {
                bool isKeyExist = Executers.ContainsKey("Operands");

                if (isKeyExist)
                {
                    Executers["Operands"] = value;
                }
                else
                {
                    Executers.Add("Operands", value);
                }

                if (Executers["Operands"] != null)
                {
                    Executers["Operands"].PlcGuid = m_PlcGuid;
                }
            }
        }


        public Executer DataTablesExecuter
        {
            get
            {
                bool isKeyExist = Executers.ContainsKey("DataTables");
                if (isKeyExist)
                {
                    return Executers["DataTables"];
                }
                else
                {
                    return null;
                }
            }

            set
            {
                bool isKeyExist = Executers.ContainsKey("DataTables");

                if (isKeyExist)
                {
                    Executers["DataTables"] = value;
                }
                else
                {
                    Executers.Add("DataTables", value);
                }

                if (Executers["DataTables"] != null)
                {
                    Executers["DataTables"].PlcGuid = m_PlcGuid;
                }
            }
        }


        public Executer BasicBinaryExecuter
        {
            get
            {
                bool isKeyExist = Executers.ContainsKey("BasicBinary");
                if (isKeyExist)
                {
                    return Executers["BasicBinary"];
                }
                else
                {
                    return null;
                }
            }

            set
            {
                bool isKeyExist = Executers.ContainsKey("BasicBinary");

                if (isKeyExist)
                {
                    Executers["BasicBinary"] = value;
                }
                else
                {
                    Executers.Add("BasicBinary", value);
                }

                if (Executers["BasicBinary"] != null)
                {
                    Executers["BasicBinary"].PlcGuid = m_PlcGuid;
                }
            }
        }

        public Executer BinaryExecuter
        {
            get
            {
                bool isKeyExist = Executers.ContainsKey("Binary");
                if (isKeyExist)
                {
                    return Executers["Binary"];
                }
                else
                {
                    return null;
                }
            }

            set
            {
                bool isKeyExist = Executers.ContainsKey("Binary");

                if (isKeyExist)
                {
                    Executers["Binary"] = value;
                }
                else
                {
                    Executers.Add("Binary", value);
                }

                if (Executers["Binary"] != null)
                {
                    Executers["Binary"].PlcGuid = m_PlcGuid;
                }
            }
        }

        protected Channel Channel
        {
            get { return m_channel; }
        }

        protected int UnitId
        {
            get { return m_unitId; }
        }

        protected PlcVersion PLCVersion
        {
            get { return m_plcVersion; }
        }

        public bool BreakFlag
        {
            get { return m_BreakFlag; }
            set
            {
                if (value == true)
                    m_channel.AbortSend(m_PlcGuid);

                m_BreakFlag = value;

                foreach (string executerKey in Executers.Keys)
                {
                    if (Executers[executerKey] != null)
                    {
                        Executers[executerKey].BreakFlag = value;
                    }
                }
            }
        }

        internal int BreakFlagCount
        {
            get { return m_BreakFlagCount; }
            set
            {
                m_BreakFlagCount = value;
                if (m_BreakFlagCount <= 0)
                {
                    m_BreakFlagCount = 0;
                    BreakFlag = false;
                }
            }
        }

        #endregion

        internal void ReadWrite(ref ReadWriteRequest[] values, bool suppressEthernetHeader)
        {
            lock (objectLocker)
            {
                m_BreakFlagCount++;
            }

            System.Diagnostics.Debug.Print("Entering Read Write. Count: " + m_BreakFlagCount.ToString());
            Guid parentID = Guid.NewGuid();

            List<ReadWriteRequest> requestsList = new List<ReadWriteRequest>();

            try
            {
                ComDriverLogger.LogReadWriteRequest(DateTime.Now, m_channel.GetLoggerChannelText(), values,
                    MessageDirection.Sent, parentID.ToString());

                CheckReadWriteRequests(values);

                for (int i = 0; i < values.Length; i++)
                {
                    ReadWriteRequest rw = values[i];

                    if ((rw is ReadOperands) || (rw is WriteOperands))
                    {
                        requestsList.Add(rw);

                        if (i == values.Length - 1)
                        {
                            if (requestsList.Count > 0)
                            {
                                ReadWriteRequest[] requestsArray = requestsList.ToArray();
                                if (OperandsExecuter != null)
                                {
                                    OperandsExecuter.PerformReadWrite(ref requestsArray, parentID.ToString(),
                                        suppressEthernetHeader);
                                }
                                else
                                {
                                    throw new ComDriveExceptions(
                                        "The PLC or the state the PLC is in does not support Read/Write Operands",
                                        ComDriveExceptions.ComDriveException.UnsupportedCommand);
                                }

                                requestsList.Clear();
                            }
                        }
                    }

                    else if ((rw is ReadDataTables) || (rw is WriteDataTables))
                    {
                        ReadWriteRequest[] requestsArray;

                        if (requestsList.Count > 0)
                        {
                            requestsArray = requestsList.ToArray();
                            if (OperandsExecuter != null)
                            {
                                OperandsExecuter.PerformReadWrite(ref requestsArray, parentID.ToString(),
                                    suppressEthernetHeader);
                            }
                            else
                            {
                                throw new ComDriveExceptions(
                                    "The PLC or the state the PLC is in does not support Read/Write Operands",
                                    ComDriveExceptions.ComDriveException.UnsupportedCommand);
                            }

                            requestsList.Clear();
                        }

                        requestsArray = new ReadWriteRequest[] {rw};

                        if (DataTablesExecuter != null)
                        {
                            DataTablesExecuter.PerformReadWrite(ref requestsArray, parentID.ToString(),
                                suppressEthernetHeader);
                        }
                        else
                        {
                            throw new ComDriveExceptions(
                                "The PLC or the state the PLC is in does not support Read/Write Data Tables",
                                ComDriveExceptions.ComDriveException.UnsupportedCommand);
                        }
                    }

                    else if (rw is BinaryRequest)
                    {
                        ReadWriteRequest[] requestsArray;
                        if (requestsList.Count > 0)
                        {
                            requestsArray = requestsList.ToArray();
                            if (OperandsExecuter != null)
                            {
                                OperandsExecuter.PerformReadWrite(ref requestsArray, parentID.ToString(),
                                    suppressEthernetHeader);
                            }
                            else
                            {
                                throw new ComDriveExceptions(
                                    "The PLC or the state the PLC is in does not support Read/Write Operands",
                                    ComDriveExceptions.ComDriveException.UnsupportedCommand);
                            }

                            requestsList.Clear();
                        }

                        requestsArray = new ReadWriteRequest[] {rw};

                        BinaryRequest br = rw as BinaryRequest;

                        switch (br.CommandCode)
                        {
                            case 0x1: //Read Ram/Flash
                            case 0x41: //Write Ram/Flash
                            case 62: //Set Password
                            case 0x2: //verify password
                            case 0x9: //Download Start
                            case 0x45: //Download End
                            case 0xA: //Erase Flash
                            case 0x7: //Wait for flash idle
                            case 0xB: //Blind mode
                            case 0x13: //UnBlind mode
                            case 0xF: //Put PLC in state (Preebot, boot, OS Stop, OS Run)
                            case 0xC: //Get PLC Name

                                if (BasicBinaryExecuter != null)
                                {
                                    BasicBinaryExecuter.PerformReadWrite(ref requestsArray, parentID.ToString(),
                                        suppressEthernetHeader);
                                }
                                else
                                {
                                    throw new ComDriveExceptions(
                                        "The PLC or the state the PLC is in does not support Basic Binary commands",
                                        ComDriveExceptions.ComDriveException.UnsupportedCommand);
                                }

                                break;
                            default:

                                if (BinaryExecuter != null)
                                {
                                    BinaryExecuter.PerformReadWrite(ref requestsArray, parentID.ToString(),
                                        suppressEthernetHeader);
                                }
                                else
                                {
                                    throw new ComDriveExceptions(
                                        "The PLC or the state the PLC is in does not support Binary Commands",
                                        ComDriveExceptions.ComDriveException.UnsupportedCommand);
                                }

                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ComDriveExceptions comDriveException = ex as ComDriveExceptions;
                if (comDriveException != null)
                {
                    if ((comDriveException.ErrorCode == ComDriveExceptions.ComDriveException.AbortedByUser)
                        || m_BreakFlag)
                    {
                        throw;
                    }
                    else if (m_BreakFlag)
                    {
                        throw new ComDriveExceptions("Request aborted by user",
                            ComDriveExceptions.ComDriveException.AbortedByUser);
                    }
                    else
                    {
                        throw;
                    }
                }
                else if (m_BreakFlag)
                {
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                lock (objectLocker)
                {
                    m_BreakFlagCount--;
                }

                System.Diagnostics.Debug.Print("Exiting Read Write. Count: " + m_BreakFlagCount.ToString());
                ComDriverLogger.LogReadWriteRequest(DateTime.Now, m_channel.GetLoggerChannelText(), values,
                    MessageDirection.Received, parentID.ToString());
            }
        }

        internal void CheckReadWriteRequests(ReadWriteRequest[] values)
        {
            foreach (ReadWriteRequest rw in values)
            {
                if (rw is ReadOperands)
                {
                    ReadOperands ro = rw as ReadOperands;
                    if (ro == null)
                    {
                        throw new ComDriveExceptions("Read Operand Request cannot be Null",
                            ComDriveExceptions.ComDriveException.UserInputException);
                    }

                    if (ro.NumberOfOperands <= 0)
                    {
                        throw new ComDriveExceptions("The number of Operands to read cannot be less than 1",
                            ComDriveExceptions.ComDriveException.UserInputException);
                    }

                    if ((ro.StartAddress < 0) ||
                        (ro.StartAddress + ro.NumberOfOperands > m_plcVersion.OperandCount(ro.OperandType)))
                    {
                        throw new ComDriveExceptions(
                            "Operand start address and end must be non-negative and less than the operand count on the PLC",
                            ComDriveExceptions.ComDriveException.OperandAddressOutOfRange);
                    }
                }
                else if (rw is WriteOperands)
                {
                    WriteOperands wo = rw as WriteOperands;
                    if (wo == null)
                    {
                        throw new ComDriveExceptions("Write Operand Request cannot be Null",
                            ComDriveExceptions.ComDriveException.UserInputException);
                    }

                    if (wo.NumberOfOperands <= 0)
                    {
                        throw new ComDriveExceptions("The number of Operands to write cannot be less than 1",
                            ComDriveExceptions.ComDriveException.UserInputException);
                    }

                    if (wo.Values == null)
                    {
                        throw new ComDriveExceptions("Values cannot be Null in a Write Operands request",
                            ComDriveExceptions.ComDriveException.UserInputException);
                    }

                    if (wo.NumberOfOperands > wo.Values.Length)
                    {
                        throw new ComDriveExceptions(
                            "The number of Operands to write is larger than the number of values that were entered",
                            ComDriveExceptions.ComDriveException.UserInputException);
                    }

                    if ((wo.StartAddress < 0) ||
                        (wo.StartAddress + wo.NumberOfOperands > m_plcVersion.OperandCount(wo.OperandType)))
                    {
                        throw new ComDriveExceptions(
                            "Operand start address and end must be non-negative and less than the operand count on the PLC",
                            ComDriveExceptions.ComDriveException.OperandAddressOutOfRange);
                    }
                }
                else if (rw is ReadDataTables)
                {
                    ReadDataTables rdt = rw as ReadDataTables;
                    if (rdt == null)
                    {
                        throw new ComDriveExceptions("Read DataTables Request cannot be Null",
                            ComDriveExceptions.ComDriveException.UserInputException);
                    }

                    if ((rdt.NumberOfBytesToReadInRow <= 0) && (rdt.NumberOfRowsToRead <= 0) &&
                        (rdt.RowSizeInBytes <= 0) && (rdt.StartAddress < 0))
                    {
                        throw new ComDriveExceptions("Invalid request parameters in Read DataTables Request",
                            ComDriveExceptions.ComDriveException.UserInputException);
                    }
                }
                else if (rw is WriteDataTables)
                {
                    WriteDataTables wdt = rw as WriteDataTables;
                    if (wdt == null)
                    {
                        throw new ComDriveExceptions("Write DataTables Request cannot be Null",
                            ComDriveExceptions.ComDriveException.UserInputException);
                    }

                    if ((wdt.NumberOfBytesToWriteInRow <= 0) && (wdt.NumberOfRowsToWrite <= 0) &&
                        (wdt.RowSizeInBytes <= 0) && (wdt.StartAddress < 0))
                    {
                        throw new ComDriveExceptions("Invalid request parameters in Read DataTables Request",
                            ComDriveExceptions.ComDriveException.UserInputException);
                    }
                }
                else if (rw == null)
                {
                    throw new ComDriveExceptions("One or more ReadWriteRequest object are Null",
                        ComDriveExceptions.ComDriveException.UserInputException);
                }
                else if (rw is BinaryRequest)
                {
                    // Nothing specific to check
                }
                else
                {
                    throw new ComDriveExceptions("Unsupported request",
                        ComDriveExceptions.ComDriveException.UnsupportedCommand);
                }
            }
        }

        internal void SetNewUnitId(int unitId)
        {
            this.m_unitId = unitId;
        }
    }
}