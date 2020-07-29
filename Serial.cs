using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Runtime.Serialization;
using System.Threading;
using Unitronics.ComDriver.Messages;
using System.IO;

namespace Unitronics.ComDriver
{
    public class Serial : Channel
    {
        #region Locals

        private SerialPort m_serialPort = new SerialPort();
        private const String STX_STRING = "/_OPLC";
        private SerialPortNames m_SerialPortNames;
        private bool m_AutoDetectComParams = true;
        private bool messageReceived = false;
        private byte[] resultBytes = new byte[0];

        #endregion

        #region Constructors

        public Serial()
        {
            m_serialPort.PortName = SerialPortNames.COM1.ToString();
        }

        public Serial(SerialPortNames portName, BaudRate baudRate, int retry, int timeOut, DataBits dataBits,
            Parity parity, StopBits stopBit)
            : base(retry, timeOut)
        {
            m_SerialPortNames = portName;
            m_serialPort.BaudRate = (int) baudRate;
            m_serialPort.DataBits = (int) dataBits;
            m_serialPort.Parity = parity;
            m_serialPort.StopBits = stopBit;
        }

        public Serial(SerialPortNames portName, BaudRate baudRate, int retry, int timeOut, DataBits dataBits,
            Parity parity, StopBits stopBit, bool autoDetectComParams)
            : base(retry, timeOut)
        {
            m_SerialPortNames = portName;
            m_serialPort.BaudRate = (int) baudRate;
            m_serialPort.DataBits = (int) dataBits;
            m_serialPort.Parity = parity;
            m_serialPort.StopBits = stopBit;
            m_AutoDetectComParams = autoDetectComParams;
        }

        public Serial(SerialPortNames portName, BaudRate baudRate)
            : base(3, 3000)
        {
            m_SerialPortNames = portName;
            m_serialPort.DataBits = 8;
            m_serialPort.Parity = Parity.None;
            m_serialPort.StopBits = StopBits.One;
            m_serialPort.BaudRate = (int) baudRate;
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
            if (!m_serialPort.IsOpen)
                m_serialPort.Open();

            try
            {
                //m_serialPort.DiscardInBuffer();
                //m_serialPort.DiscardOutBuffer();
                m_serialPort.Write(text);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (ArgumentOutOfRangeException)
            {
                throw;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (TimeoutException)
            {
                throw;
            }

            //finally
            //{
            //    System.Threading.Thread.Sleep(10);
            //}
        }

        internal override void SendBytes(byte[] bytes, byte messageEnumerator)
        {
            if (!m_serialPort.IsOpen)
                m_serialPort.Open();

            try
            {
                //m_serialPort.DiscardInBuffer();
                //m_serialPort.DiscardOutBuffer();
                m_serialPort.Write(bytes, 0, bytes.Length);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (ArgumentOutOfRangeException)
            {
                throw;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (TimeoutException)
            {
                throw;
            }

            //finally
            //{
            //    System.Threading.Thread.Sleep(10);
            //}
        }

        internal override string ReceiveString()
        {
            messageReceived = false;
            resultBytes = new byte[0];

            m_serialPort.ReadTimeout = TimeOut;

            while (!messageReceived)
            {
                byte[] receivedData = new byte[1024];
                int received = m_serialPort.Read(receivedData, 0, receivedData.Length);
                serialPortReceiveString(receivedData, received);
            }

            if (!messageReceived)
            {
                throw new TimeoutException();
            }

            return ASCIIEncoding.ASCII.GetString(resultBytes);
        }

        private void serialPortReceiveString(byte[] incomingBytes, int count)
        {
            bool bStxFound = false;
            bool bEtxFound = false;
            int index = 0;
            int checksum = 0;
            string resultString;

            if (count > 0)
            {
                byte[] temp = new byte[count];
                Array.Copy(incomingBytes, 0, temp, 0, count);

                ComDriverLogger.LogReceivedMessageChunk(DateTime.Now,
                    Utils.HelperComDriverLogger.GetLoggerChannel(this), temp);

                byte[] tmpBuffer = new byte[resultBytes.Length + count];
                Array.Copy(resultBytes, 0, tmpBuffer, 0, resultBytes.Length);
                Array.Copy(temp, 0, tmpBuffer, resultBytes.Length, count);
                resultBytes = tmpBuffer;
            }

            resultString = ASCIIEncoding.ASCII.GetString(resultBytes);

            if (resultString.Length > 0)
            {
                index = resultString.IndexOf("/"); // find the STX
                if (index >= 0)
                {
                    resultString = resultString.Substring(index, resultString.Length - index);
                    byte[] tempBuffer = new byte[resultBytes.Length - index];
                    Array.Copy(resultBytes, index, tempBuffer, 0, resultBytes.Length - index);
                    resultBytes = tempBuffer;
                    bStxFound = true;
                }
                else
                {
                    if (resultString.Length > 100)
                    {
                        throw new ComDriveExceptions("STX is missing",
                            ComDriveExceptions.ComDriveException.CommunicationTimeout);
                    }
                }

                index = resultString.IndexOf("\r"); // find the ETX
                if (index >= 0)
                {
                    resultString = resultString.Substring(0, index + 1);
                    bEtxFound = true;
                }
            }
            else
            {
                return;
            }

            if (!bStxFound || !bEtxFound)
                return;

            for (int i = 2; i < resultString.Length - 3; i++)
            {
                checksum += resultBytes[i];
            }

            string CRC = Utils.DecimalToHex(checksum % 256);

            if (CRC != resultString.Substring(resultString.Length - 3, 2))
            {
                throw new ComDriveExceptions("Wrong Data Checksum", ComDriveExceptions.ComDriveException.ChecksumError);
            }

            messageReceived = true;
        }

        internal override byte[] ReceiveBytes()
        {
            messageReceived = false;
            resultBytes = new byte[0];

            m_serialPort.ReadTimeout = TimeOut;

            while (!messageReceived)
            {
                byte[] receivedData = new byte[1024];
                int received = m_serialPort.Read(receivedData, 0, receivedData.Length);
                serialPortReceiveBytes(receivedData, received);
            }

            if (!messageReceived)
            {
                throw new TimeoutException();
            }

            return resultBytes;
        }

        private void serialPortReceiveBytes(byte[] incomingBytes, int count)
        {
            bool bStxFound = false;
            byte[] tempBuffer;
            string bufferAsString;
            int index = 0;
            UInt16 pcCheckSum;
            int totalLength = 0;

            const int HEADER_LENGTH = 24;

            if (count > 0)
            {
                byte[] temp = new byte[count];
                Array.Copy(incomingBytes, 0, temp, 0, count);

                ComDriverLogger.LogReceivedMessageChunk(DateTime.Now,
                    Utils.HelperComDriverLogger.GetLoggerChannel(this), temp);

                byte[] tmpBuffer = new byte[resultBytes.Length + count];
                Array.Copy(resultBytes, 0, tmpBuffer, 0, resultBytes.Length);
                Array.Copy(temp, 0, tmpBuffer, resultBytes.Length, count);
                resultBytes = tmpBuffer;
            }

            if (resultBytes.Length > 0)
            {
                bufferAsString = ASCIIEncoding.ASCII.GetString(resultBytes);
                index = bufferAsString.IndexOf(STX_STRING);
                if (index >= 0)
                {
                    tempBuffer = new byte[resultBytes.Length - index];
                    Array.Copy(resultBytes, index, tempBuffer, 0, resultBytes.Length - index);
                    resultBytes = tempBuffer;
                    bStxFound = true;
                }
                else
                {
                    if (resultBytes.Length > 100)
                    {
                        throw new ComDriveExceptions("STX is missing",
                            ComDriveExceptions.ComDriveException.CommunicationTimeout);
                    }
                }
            }
            else
            {
                return;
            }

            if (!bStxFound)
                return;

            if (resultBytes.Length < HEADER_LENGTH)
                return;

            pcCheckSum = Utils.calcCheckSum(ref resultBytes, 0, 21);

            if (pcCheckSum != BitConverter.ToUInt16(resultBytes, 22))
            {
                throw new ComDriveExceptions("Wrong Header Checksum",
                    ComDriveExceptions.ComDriveException.ChecksumError);
            }

            totalLength = BitConverter.ToUInt16(resultBytes, 20) +
                          HEADER_LENGTH + 3; // 3 for data checksum + ETX


            if (resultBytes.Length < totalLength)
                return;

            tempBuffer = new byte[totalLength];
            Array.Copy(resultBytes, 0, tempBuffer, 0, totalLength);
            resultBytes = tempBuffer;

            tempBuffer = null;

            pcCheckSum = Utils.calcCheckSum(ref resultBytes, 24, totalLength - 4);
            if (pcCheckSum != BitConverter.ToUInt16(resultBytes, totalLength - 3))
            {
                throw new ComDriveExceptions("Wrong Data Checksum", ComDriveExceptions.ComDriveException.ChecksumError);
            }

            if (resultBytes[totalLength - 1] != 92) // 92 is '\' which is the ETX
            {
                throw new ComDriveExceptions("ETX is missing", ComDriveExceptions.ComDriveException.ETXMissing);
            }

            messageReceived = true;
        }

        internal override void Connect()
        {
            if (!m_serialPort.IsOpen)
            {
                CheckPortName(m_SerialPortNames);
                m_serialPort.PortName = m_SerialPortNames.ToString();

                try
                {
                    m_serialPort.DtrEnable = true;
                    m_serialPort.RtsEnable = true;
                    m_serialPort.Open();
                }
                catch (Exception ex)
                {
                    throw new ComDriveExceptions(ex.Message, ComDriveExceptions.ComDriveException.PortInUse);
                }

                string text = ConnectionStatus.ConnectionOpened + " with BR" + m_serialPort.BaudRate.ToString() +
                              "(" + m_serialPort.DataBits.ToString() + ", " + m_serialPort.Parity.ToString() + ", " +
                              m_serialPort.StopBits.ToString() + ")";
                ComDriverLogger.LogConnectionState(DateTime.Now,
                    Utils.HelperComDriverLogger.GetLoggerChannel(this), text);
            }
        }

        public override void Disconnect()
        {
            if (base.QueueCount != 0)
                return;

            if (m_serialPort.IsOpen)
            {
                m_serialPort.Close();
                AlreadyInitialized = false;

                try
                {
                    ComDriverLogger.LogConnectionState(DateTime.Now,
                        Utils.HelperComDriverLogger.GetLoggerChannel(this),
                        ConnectionStatus.ConnectionClosed.ToString());
                }
                catch
                {
                }

                m_serialPort.Dispose();
            }
        }

        public void DtrSwitch()
        {
            m_serialPort.DtrEnable = !m_serialPort.DtrEnable;
        }

        public bool z_SendBreak()
        {
            try
            {
                bool isOpened = m_serialPort.IsOpen;
                if (!m_serialPort.IsOpen)
                    m_serialPort.Open();

                m_serialPort.BreakState = true;
                Thread.Sleep(500);
                m_serialPort.BreakState = false;
                Thread.Sleep(1000);

                if (!isOpened)
                    m_serialPort.Close();

                return true;
            }
            catch
            {
                return false;
            }
        }

        internal bool BreakState
        {
            get { return m_serialPort.BreakState; }
            set { m_serialPort.BreakState = value; }
        }

        internal string SetPlcConnetionParamsU90(int dataBits, System.IO.Ports.Parity parity,
            System.IO.Ports.StopBits stopBits)
        {
            // The same as the break command, but for U90 PLCs
            string SetPlcConnetionParamsU90 = "CPSR";
            int nibble = 0;
            SetPlcConnetionParamsU90 += "A"; // Flow control + Timeout 60 seconds
            SetPlcConnetionParamsU90 += "7"; // CANBus baudrate = 10Kb
            nibble += dataBits - 7;

            switch (parity)
            {
                case System.IO.Ports.Parity.Even:
                    nibble += 0;
                    break;
                case System.IO.Ports.Parity.Odd:
                    nibble += 2;
                    break;
                case System.IO.Ports.Parity.None:
                    nibble += 4;
                    break;
                default:
                    throw new ComDriveExceptions("Invalid Parity for U90 PLC Connection!",
                        ComDriveExceptions.ComDriveException.CommunicationParamsException);
            }

            switch (stopBits)
            {
                case System.IO.Ports.StopBits.One:
                    nibble += 0;
                    break;
                case System.IO.Ports.StopBits.Two:
                    nibble += 8;
                    break;
                default:
                    throw new ComDriveExceptions("Invalid Stop Bits for U90 PLC Connection!",
                        ComDriveExceptions.ComDriveException.CommunicationParamsException);
            }

            SetPlcConnetionParamsU90 += nibble.ToString("X");


            switch (BaudRate)
            {
                case BaudRate.BR110:
                    SetPlcConnetionParamsU90 += "1";
                    break;
                case BaudRate.BR300:
                    SetPlcConnetionParamsU90 += "2";
                    break;
                case BaudRate.BR600:
                    SetPlcConnetionParamsU90 += "3";
                    break;
                case BaudRate.BR1200:
                    SetPlcConnetionParamsU90 += "4";
                    break;
                case BaudRate.BR2400:
                    SetPlcConnetionParamsU90 += "5";
                    break;
                case BaudRate.BR4800:
                    SetPlcConnetionParamsU90 += "6";
                    break;
                case BaudRate.BR9600:
                    SetPlcConnetionParamsU90 += "7";
                    break;
                case BaudRate.BR19200:
                    SetPlcConnetionParamsU90 += "8";
                    break;
                case BaudRate.BR38400:
                    SetPlcConnetionParamsU90 += "9";
                    break;
                case BaudRate.BR57600:
                    SetPlcConnetionParamsU90 += "A";
                    break;
                case BaudRate.BR115200:
                    // Don't throw an exception... since if it's not a U90 then this exception is wrong
                    SetPlcConnetionParamsU90 += "A";
                    break;
                default:
                    throw new ComDriveExceptions("Invalid Baudrate for U90 PLC Connection!",
                        ComDriveExceptions.ComDriveException.CommunicationParamsException);
            }

            return SetPlcConnetionParamsU90;
        }

        internal string BreakCommand()
        {
            string breakCommand = "CPC";
            breakCommand += "1"; // plc port
            breakCommand += "T"; // Temp change
            switch (BaudRate)
            {
                case BaudRate.BR110:
                    breakCommand += "01";
                    break;
                case BaudRate.BR300:
                    breakCommand += "02";
                    break;
                case BaudRate.BR600:
                    breakCommand += "03";
                    break;
                case BaudRate.BR1200:
                    breakCommand += "04";
                    break;
                case BaudRate.BR2400:
                    breakCommand += "05";
                    break;
                case BaudRate.BR4800:
                    breakCommand += "06";
                    break;
                case BaudRate.BR9600:
                    breakCommand += "07";
                    break;
                case BaudRate.BR19200:
                    breakCommand += "08";
                    break;
                case BaudRate.BR38400:
                    breakCommand += "09";
                    break;
                case BaudRate.BR57600:
                    breakCommand += "0A";
                    break;
                case BaudRate.BR115200:
                    breakCommand += "0B";
                    break;
            }

            breakCommand += "FF"; // Timeout - no change
            breakCommand += "FF"; // Flow Control - no change
            return breakCommand;
        }

        internal override bool IsEquivalentChannel(Channel anotherChanel)
        {
            Serial serial = anotherChanel as Serial;
            if (serial.PortName == PortName)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

        #region Properties

        public BaudRate BaudRate
        {
            get { return (BaudRate) m_serialPort.BaudRate; }
            set { m_serialPort.BaudRate = (int) value; }
        }

        public DataBits DataBits
        {
            get { return (DataBits) m_serialPort.DataBits; }
            set { m_serialPort.DataBits = (int) value; }
        }

        public Parity Parity
        {
            get { return m_serialPort.Parity; }
            set { m_serialPort.Parity = value; }
        }

        public StopBits StopBits
        {
            get { return m_serialPort.StopBits; }
            set { m_serialPort.StopBits = value; }
        }

        public SerialPortNames PortName
        {
            get { return m_SerialPortNames; }
            set
            {
                if (m_SerialPortNames != value)
                {
                    if (PLCFactory.ValidateChannelPropertyChange(this, value))
                    {
                        m_SerialPortNames = value;
                    }
                    else
                    {
                        throw new ComDriveExceptions(
                            "Cannot change Serial port name since it will result a duplicated channel with the same Port name",
                            ComDriveExceptions.ComDriveException.CommunicationParamsException);
                    }
                }
            }
        }

        public bool AutoDetectComParams
        {
            get { return m_AutoDetectComParams; }
            set { m_AutoDetectComParams = value; }
        }

        public override bool Connected
        {
            get { return m_serialPort.IsOpen; }
        }

        #endregion

        #region Private

        private void CheckPortName(SerialPortNames portName)
        {
            string[] portNames = SerialPort.GetPortNames();
            if (!portNames.Contains(portName.ToString()))
            {
                throw new ComDriveExceptions("Specified Serial Port doesn't exist!",
                    ComDriveExceptions.ComDriveException.CommunicationParamsException);
            }
        }

        private void appendReceivedDataToArray(ref byte[] incomingData)
        {
            byte[] tempBuffer;
            byte[] copyOfBuffer;
            int bytesToRead;
            bytesToRead = m_serialPort.BytesToRead;
            if (bytesToRead > 0)
            {
                tempBuffer = new byte[bytesToRead];
                m_serialPort.Read(tempBuffer, 0, bytesToRead);
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

            bytesToRead = m_serialPort.BytesToRead;
            if (bytesToRead > 0)
            {
                tempBuffer = new byte[bytesToRead];
                m_serialPort.Read(tempBuffer, 0, bytesToRead);
                bufferAsString = ASCIIEncoding.ASCII.GetString(tempBuffer);
                incomingData += bufferAsString;

                ComDriverLogger.LogReceivedMessageChunk(DateTime.Now,
                    Utils.HelperComDriverLogger.GetLoggerChannel(this), tempBuffer);
            }
        }

        #endregion
    }
}