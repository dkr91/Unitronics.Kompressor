using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Text.RegularExpressions;
using Unitronics.ComDriver.Messages;
using Unitronics.ComDriver.Messages.DataRequest;
using Unitronics.ComDriver;

namespace Unitronics.ComDriver.Messages.BinMessage
{
    public class Details
    {
        #region Locals

        private List<object> m_responseValues;
        private byte[] m_detailsValues;
        private List<byte> m_detailsBytes;
        private List<ReadWriteRequest> m_dataRequests;

        #endregion

        #region Constructor

        public Details()
        {
            m_detailsBytes = new List<byte>();
            m_dataRequests = new List<ReadWriteRequest>();
        }

        public Details(byte[] message, BinaryMessage requestedMessage, List<List<byte>> dataRequestsBytes)
            : this()
        {
            //20 is location in message where I have details length
            UInt16 length = BitConverter.ToUInt16(message, 20);
            m_detailsValues = new byte[length];

            Array.Copy(message, 24, m_detailsValues, 0, (int) length);

            m_dataRequests = requestedMessage.Details.m_dataRequests;
            SetValues(requestedMessage, dataRequestsBytes);
        }

        public Details(List<ReadWriteRequest> dataRequests, List<List<byte>> dataRequestsBytes)
            : this()
        {
            if ((dataRequestsBytes[0])[1] == 0 && (dataRequestsBytes[0][3] == 0) &&
                !(dataRequests[0].GetType().Equals(typeof(ReadDataTables))) &&
                !(dataRequests[0].GetType().Equals(typeof(WriteDataTables))))
            {
                UpdateDetailsForFullBinaryMix(dataRequests, dataRequestsBytes);
            }
            else
            {
                m_dataRequests.AddRange(dataRequests);
                foreach (List<byte> iterList in dataRequestsBytes)
                {
                    m_detailsBytes.AddRange(iterList);
                }
            }
        }

        #endregion

        #region Public

        public void AddDataRequest(ReadWriteRequest dataRequest)
        {
            m_dataRequests.Add(dataRequest);
        }

        public void AddDataRequests(List<ReadWriteRequest> dataRequests)
        {
            m_dataRequests.AddRange(dataRequests);
        }

        #endregion

        #region Private

        private void SetValues(BinaryMessage requestedMessage, List<List<byte>> dataRequestsBytes)
        {
            if (requestedMessage.Header.CommandNumber == (byte) BinaryCommand.ReadWrite)
            {
                // Full Binary Mix
                SetValuesFullBinaryMix(requestedMessage, dataRequestsBytes);
                //SetValuesEx(requestedMessage, dataRequestsBytes);
            }
            else if (requestedMessage.Header.CommandNumber == (byte) BinaryCommand.ReadOperands)
            {
                // Partial Binart Mix
                SetValuesPartialBinaryMix(requestedMessage, dataRequestsBytes);
            }
            else if (requestedMessage.Header.CommandNumber == (byte) BinaryCommand.ReadDataTables)
            {
                // Read DT
                SetValuesReadDT(requestedMessage, dataRequestsBytes);
            }
            else if (requestedMessage.Header.CommandNumber == (byte) BinaryCommand.ReadPartOfProjectDataTables)
            {
                // Read DT
                SetValuesReadDT(requestedMessage, dataRequestsBytes);
            }
            else if (requestedMessage.Header.CommandNumber == (byte) BinaryCommand.WriteDataTables)
            {
                // Write DT
                System.Diagnostics.Debug.Assert(false);
            }
        }

        private void SetValuesFullBinaryMix(BinaryMessage requestedMessage, List<List<byte>> dataRequestsBytes)
        {
            m_responseValues = new List<object>();
            int index = 8;

            if (m_detailsBytes.Count > 0 || requestedMessage.Details.DetailsBytes.Length > 0)
            {
                for (int i = 0; i < m_dataRequests.Count; i++)
                {
                    string operatorName = String.Empty;
                    UInt16 length = 0;
                    int size = 0;

                    operatorName = (dataRequestsBytes[i])[4].GetOperandNameByValueForFullBinary();
                    size = Utils.FullBinaryOperandTypes[operatorName].OperandSize;
                    length = (dataRequestsBytes[i])[5];

                    if ((dataRequestsBytes[i])[0] == 1)
                    {
                        // This is a read request
                        switch (size)
                        {
                            case 1:
                                for (int iter = 0; iter < length; iter++)
                                {
                                    m_responseValues.Add(
                                        (short) (m_detailsValues[index] & 1)); // BitWise And for checking LSB value
                                    index++;
                                }

                                index +=
                                    4; // jumping over Operand Type, Num Of Operands, Address (2 bytes)... Index will point the next value
                                index += index % 2; // Odd Addresses are not allowed
                                break;
                            case 16:
                                for (int j = 0; j < length; j++)
                                {
                                    try
                                    {
                                        m_responseValues.Add(BitConverter.ToInt16(
                                            new byte[2] {m_detailsValues[index], m_detailsValues[index + 1]}, 0));
                                    }
                                    catch
                                    {
                                    }
                                    finally
                                    {
                                        index += 2;
                                    }
                                }

                                index +=
                                    4; // jumping over Operand Type, Num Of Operands, Address (2 bytes)... Index will point the next value
                                break;
                            case 32:
                                for (int j = 0; j < length; j++)
                                {
                                    switch (operatorName)
                                    {
                                        case Utils.Operands.OP_MF:
                                            m_responseValues.Add(Utils.HexEncoding.ConvertBytesToSingle(new byte[4]
                                            {
                                                m_detailsValues[index + 2],
                                                m_detailsValues[index + 3],
                                                m_detailsValues[index + 0],
                                                m_detailsValues[index + 1]
                                            }));
                                            break;

                                        case Utils.Operands.OP_TIMER_CURRENT:
                                        case Utils.Operands.OP_TIMER_PRESET:
                                            UInt32 uint_Value = BitConverter.ToUInt32(m_detailsValues, index);
                                            if ((requestedMessage.Details.m_dataRequests[i] as ReadOperands)
                                                .TimerValueFormat.Equals(TimerValueFormat.TimeFormat))
                                            {
                                                m_responseValues.Add(Utils.z_GetTimeUnits(Convert.ToInt32(uint_Value)));
                                            }
                                            else
                                            {
                                                m_responseValues.Add(uint_Value);
                                            }

                                            break;

                                        case Utils.Operands.OP_ML:
                                        case Utils.Operands.OP_XL:
                                        case Utils.Operands.OP_SL:
                                            Int32 value = BitConverter.ToInt32(m_detailsValues, index);
                                            m_responseValues.Add(value);
                                            break;

                                        case Utils.Operands.OP_DW:
                                        case Utils.Operands.OP_XDW:
                                        case Utils.Operands.OP_SDW:
                                            uint_Value = BitConverter.ToUInt32(m_detailsValues, index);
                                            m_responseValues.Add(uint_Value);
                                            break;
                                    }

                                    index += 4;
                                }

                                index +=
                                    4; // jumping over Operand Type, Num Of Operands, Address (2 bytes)... Index will point the next value
                                break;
                        } //end swich(size)
                    }
                    else
                    {
                        //Write request No data arrives from PLC
                    }

                    object[] objects = new object[m_responseValues.Count];
                    Array.Copy(m_responseValues.ToArray(), objects, m_responseValues.Count);
                    m_dataRequests[i].ResponseValues = objects;
                    m_responseValues.Clear();
                }
            }
        }

        private void SetValuesPartialBinaryMix(BinaryMessage requestedMessage, List<List<byte>> dataRequestsBytes)
        {
            m_responseValues = new List<object>();
            int index = 0;
            int bitIndex = 0;


            if (m_detailsBytes.Count > 0 || requestedMessage.Details.DetailsBytes.Length > 0)
            {
                for (int i = 0; i < m_dataRequests.Count; i++)
                {
                    string operatorName = String.Empty;
                    UInt16 length = 0;
                    int size = 0;

                    operatorName = (dataRequestsBytes[i])[2].GetOperandNameByValue();
                    size = Utils.OperandTypesDictionary[operatorName].OperandSize;
                    length = BitConverter.ToUInt16(dataRequestsBytes[i].ToArray(), 0);

                    switch (size)
                    {
                        case 1:
                            int NumOfValues =
                                BitConverter.ToInt32(
                                    new byte[] {dataRequestsBytes[i][0], dataRequestsBytes[i][1], 0, 0}, 0);
                            ByteBits bits;
                            bits = new ByteBits(m_detailsValues[index]);
                            for (int iter = 0; iter < NumOfValues; iter++)
                            {
                                if (bits[bitIndex])
                                    m_responseValues.Add((short) 1);
                                else
                                    m_responseValues.Add((short) 0);

                                bitIndex++;
                                if (bitIndex >= 8)
                                {
                                    bitIndex = 0;
                                    index++;
                                    bits = new ByteBits(m_detailsValues[index]);
                                }
                            }

                            break;
                        case 16:
                            if (bitIndex != 0)
                            {
                                bitIndex = 0;
                                index++;
                                index += (index % 2);
                            }
                            else
                            {
                                index += (index % 2);
                            }

                            for (int j = 0; j < length; j++)
                            {
                                try
                                {
                                    m_responseValues.Add(BitConverter.ToInt16(
                                        new byte[2] {m_detailsValues[index], m_detailsValues[index + 1]}, 0));
                                }
                                catch
                                {
                                }
                                finally
                                {
                                    index += 2;
                                }
                            }

                            break;
                        case 32:
                            if (bitIndex != 0)
                            {
                                bitIndex = 0;
                                index++;
                                index += (index % 2);
                            }
                            else
                            {
                                index += (index % 2);
                            }

                            for (int j = 0; j < length; j++)
                            {
                                switch (operatorName)
                                {
                                    case Utils.Operands.OP_MF:
                                        m_responseValues.Add(Utils.HexEncoding.ConvertBytesToSingle(new byte[4]
                                        {
                                            m_detailsValues[index + 2],
                                            m_detailsValues[index + 3],
                                            m_detailsValues[index + 0],
                                            m_detailsValues[index + 1]
                                        }));
                                        break;

                                    case Utils.Operands.OP_TIMER_CURRENT:
                                    case Utils.Operands.OP_TIMER_PRESET:
                                        UInt32 uint_Value = BitConverter.ToUInt32(m_detailsValues, index);
                                        if ((requestedMessage.Details.m_dataRequests[i] as ReadOperands)
                                            .TimerValueFormat.Equals(TimerValueFormat.TimeFormat))
                                        {
                                            m_responseValues.Add(Utils.z_GetTimeUnits(Convert.ToInt32(uint_Value)));
                                        }
                                        else
                                        {
                                            m_responseValues.Add(uint_Value);
                                        }

                                        break;

                                    case Utils.Operands.OP_ML:
                                    case Utils.Operands.OP_XL:
                                    case Utils.Operands.OP_SL:
                                        Int32 value = BitConverter.ToInt32(m_detailsValues, index);
                                        m_responseValues.Add(value);
                                        break;

                                    case Utils.Operands.OP_DW:
                                    case Utils.Operands.OP_XDW:
                                    case Utils.Operands.OP_SDW:
                                        uint_Value = BitConverter.ToUInt32(m_detailsValues, index);
                                        m_responseValues.Add(uint_Value);
                                        break;
                                }

                                index += 4;
                            }

                            break;
                        default:
                            System.Diagnostics.Debug.Assert(false);
                            break;
                    }

                    object[] objects = new object[m_responseValues.Count];
                    Array.Copy(m_responseValues.ToArray(), objects, m_responseValues.Count);
                    m_dataRequests[i].ResponseValues = objects;
                    m_responseValues.Clear();
                }
            }
        }

        private void SetValuesReadDT(BinaryMessage requestedMessage, List<List<byte>> dataRequestsBytes)
        {
            m_responseValues = new List<object>();

            List<object> dtValues = new List<object>(m_detailsValues.Length);
            for (int iter = 0; iter < m_detailsValues.Length; iter++)
            {
                dtValues.Add(m_detailsValues[iter] as object);
            }

            m_responseValues.AddRange(dtValues);

            m_dataRequests[0].ResponseValues = m_responseValues.ToArray();
        }

        private void UpdateDetailsForFullBinaryMix(List<ReadWriteRequest> dataRequests,
            List<List<byte>> dataRequestsBytes)
        {
            UInt16 nrOfWrites = 0;
            UInt16 nrOfReads = 0;
            int startPos = 4;

            for (int i = 0; i < dataRequests.Count; i++)
            {
                if ((dataRequestsBytes[i])[0] == 1)
                    nrOfReads++;
                if ((dataRequestsBytes[i])[2] == 1)
                    nrOfWrites++;
            }

            m_dataRequests.AddRange(dataRequests);
            m_detailsBytes.AddRange(BitConverter.GetBytes(nrOfReads));
            m_detailsBytes.AddRange(BitConverter.GetBytes(nrOfWrites));

            if (nrOfReads > 0)
            {
                for (int i = 0; i < dataRequests.Count; i++)
                {
                    if ((dataRequestsBytes[i])[0] > 0)
                        m_detailsBytes.AddRange(dataRequestsBytes[i].GetRange(startPos, 4));
                }
            }

            if (nrOfWrites > 0)
            {
                for (int i = 0; i < dataRequests.Count; i++)
                {
                    startPos = 4;
                    if ((dataRequestsBytes[i])[2] > 0)
                    {
                        if (dataRequestsBytes[i][0] > 0)
                            startPos += 4;

                        byte operandId = dataRequestsBytes[i][startPos];
                        byte noOfOperands = dataRequestsBytes[i][startPos + 1];
                        byte operandBitsSize = operandId.GetOperandSizeByValueForFullBinary();
                        byte operanBytesSize = (Convert.ToInt16(operandBitsSize) == 1)
                            ? (byte) 1
                            : (byte) (operandBitsSize / 8);
                        int count = 4;
                        if (dataRequests.Count == 1)
                            count += (noOfOperands * operanBytesSize);
                        else
                            count += (operanBytesSize == 1) ? (noOfOperands) : (noOfOperands * operanBytesSize);


                        m_detailsBytes.AddRange(dataRequestsBytes[i].GetRange(startPos, count));
                        if (operanBytesSize == 1 && noOfOperands % 2 == 1)
                            m_detailsBytes.Add((byte) 0);
                    }
                }
            }
        }

        #endregion

        #region Properties

        public byte[] DetailsBytes
        {
            get { return m_detailsBytes.ToArray(); }
        }

        public UInt16 CheckSum
        {
            get
            {
                int sum = m_detailsBytes.Sum(x => (int) x);
                int checkSum = ~(sum % 0x10000) + 1;
                return (UInt16) checkSum;
            }
        }

        public List<object> ResponseValues
        {
            get { return m_responseValues; }
        }

        #endregion
    }
}