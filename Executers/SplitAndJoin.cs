using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver.Messages.DataRequest;

namespace Unitronics.ComDriver.Executers
{
    internal class SplitAndJoin
    {
        #region Locals

        internal List<ReadWriteRequest> requestsList;
        internal List<List<ReadWriteRequest>> allRequestsList;
        internal List<SplitDetails> splitDetailsList;
        internal int bytesNoToSend;
        internal int bytesNoToReceive;
        internal ushort requestBytesNoToSend;
        internal ushort requestBytesNoToReceive;
        internal int plcBuffer;
        private int reqPosition;
        private ushort operandSize;

        #endregion

        #region Constructor

        internal SplitAndJoin(int plcBuffer)
        {
            this.plcBuffer = plcBuffer;
            requestsList = new List<ReadWriteRequest>();
            splitDetailsList = new List<SplitDetails>();
            allRequestsList = new List<List<ReadWriteRequest>>();
            _resetRequestsSize();
        }

        #endregion

        #region SplitAndJoinMethods

        internal void AddNewRequest(ReadWriteRequest readWriteRequest, int reqPosition, bool splitFlag)
        {
            this.reqPosition = reqPosition;
            _getRequestBytesSize(readWriteRequest);

            if (_requestsSizeFitsPLCBuffer())
            {
                _addToRequests(readWriteRequest, splitFlag);
            }
            else
            {
                _sizeExceedPLCBuffer(readWriteRequest, splitFlag);
            }
        }

        internal bool FinishSpliting()
        {
            if (requestsList.Count > 0)
            {
                ReadWriteRequest[] tmpReq = requestsList.ToArray();
                allRequestsList.Add(tmpReq.ToList());
                requestsList.Clear();
                return true;
            }

            if (allRequestsList.Count > 0)
                return true;

            return false;
        }

        internal void ClearLists()
        {
            allRequestsList.Clear();
            splitDetailsList.Clear();
            requestsList.Clear();
            _resetRequestsSize();
        }

        #endregion

        #region Private

        private void _getRequestBytesSize(ReadWriteRequest readWriteRequest)
        {
            ushort noOfOperands;
            int noOf255Splits, noOfSplits;

            if (readWriteRequest is WriteOperands)
            {
                WriteOperands wo = readWriteRequest as WriteOperands;
                noOfOperands = wo.NumberOfOperands;
                operandSize = wo.OperandType.GetOperandSizeByOperandTypeForFullBinarry();

                noOf255Splits = noOfOperands / 255;
                noOfSplits = noOfOperands % 255;

                if (noOfOperands > 255)
                {
                    requestBytesNoToSend =
                        (ushort) (noOf255Splits * Utils.Lengths.LENGTH_WR_DETAILS + noOf255Splits * 255 * operandSize);
                    requestBytesNoToSend += (ushort) ((operandSize % 2) * (noOf255Splits + noOfSplits % 2));

                    if (noOfSplits > 0)
                    {
                        requestBytesNoToSend += (ushort) (Utils.Lengths.LENGTH_WR_DETAILS + noOfSplits * operandSize);
                    }
                }
                else
                {
                    requestBytesNoToSend = (ushort) (operandSize * noOfOperands + Utils.Lengths.LENGTH_WR_DETAILS);
                    requestBytesNoToSend += (ushort) (requestBytesNoToSend % 2);
                }

                requestBytesNoToReceive = 0;
            }
            else
            {
                ReadOperands ro = readWriteRequest as ReadOperands;
                noOfOperands = ro.NumberOfOperands;
                operandSize = ro.OperandType.GetOperandSizeByOperandTypeForFullBinarry();

                if (noOfOperands > 255)
                {
                    noOf255Splits = (noOfOperands / 255);
                    noOfSplits = (noOfOperands % 255);

                    requestBytesNoToSend = (ushort) (noOf255Splits * Utils.Lengths.LENGTH_WR_DETAILS);
                    requestBytesNoToReceive =
                        (ushort) (noOf255Splits * Utils.Lengths.LENGTH_WR_DETAILS + noOf255Splits * 255 * operandSize);
                    requestBytesNoToReceive += (ushort) ((operandSize % 2) * (noOf255Splits + noOfSplits % 2));

                    if (noOfSplits != 0)
                    {
                        requestBytesNoToSend += Utils.Lengths.LENGTH_WR_DETAILS;
                        requestBytesNoToReceive +=
                            (ushort) (Utils.Lengths.LENGTH_WR_DETAILS + noOfSplits * operandSize);
                    }
                }
                else
                {
                    requestBytesNoToReceive = (ushort) (operandSize * noOfOperands + Utils.Lengths.LENGTH_WR_DETAILS);
                    requestBytesNoToReceive += (ushort) (requestBytesNoToReceive % 2);
                    requestBytesNoToSend = Utils.Lengths.LENGTH_WR_DETAILS;
                }
            }
        }

        private void _updateRequestsListSize()
        {
            bytesNoToSend += requestBytesNoToSend;
            bytesNoToReceive += requestBytesNoToReceive;
        }

        private void _addToRequests(ReadWriteRequest readWriteRequest, bool splitFlag)
        {
            ushort noOfOperands = (readWriteRequest is ReadOperands)
                ? (readWriteRequest as ReadOperands).NumberOfOperands
                : (readWriteRequest as WriteOperands).NumberOfOperands;

            if (noOfOperands > 255)
            {
                _addRequestsWhenNoOfOperandsExceed255(readWriteRequest);
            }
            else
            {
                requestsList.Add(readWriteRequest);

                if (splitFlag)
                    _updateSplitDetailsList();
            }

            _updateRequestsListSize();
        }

        private void _addRequestsWhenNoOfOperandsExceed255(ReadWriteRequest readWriteRequest)
        {
            ushort startAddress;
            ushort noOfOperands;
            ushort remainingNoOfOperands;

            if (readWriteRequest is ReadOperands)
            {
                ReadOperands ro = readWriteRequest as ReadOperands;
                remainingNoOfOperands = ro.NumberOfOperands;
                startAddress = ro.StartAddress;
                noOfOperands = 255;

                while (remainingNoOfOperands > 0)
                {
                    requestsList.Add(new ReadOperands(noOfOperands, ro.OperandType, startAddress, ro.TimerValueFormat));
                    startAddress += noOfOperands;
                    remainingNoOfOperands -= noOfOperands;

                    if (remainingNoOfOperands <= 255)
                        noOfOperands = remainingNoOfOperands;

                    _updateSplitDetailsList();
                }
            }
            else
            {
                WriteOperands wo = readWriteRequest as WriteOperands;
                remainingNoOfOperands = wo.NumberOfOperands;
                startAddress = wo.StartAddress;
                noOfOperands = 255;

                while (remainingNoOfOperands > 0)
                {
                    object[] values = new object[noOfOperands];
                    Array.Copy(wo.Values, startAddress - wo.StartAddress, values, 0, noOfOperands);

                    requestsList.Add(new WriteOperands(noOfOperands, wo.OperandType, startAddress, values,
                        wo.TimerValueFormat));
                    startAddress += noOfOperands;
                    remainingNoOfOperands -= noOfOperands;

                    if (remainingNoOfOperands <= 255)
                        noOfOperands = remainingNoOfOperands;

                    _updateSplitDetailsList();
                }
            }
        }

        private void _sizeExceedPLCBuffer(ReadWriteRequest readWriteRequest, bool splitFlag)
        {
            if (_canWriteAtLeast1Operand() && _canReadAtLeast1Operand())
            {
                if (readWriteRequest is ReadOperands)
                {
                    _receiveSizeExceedPLCBUffer(readWriteRequest);
                }
                else
                {
                    _sendSizeExceedPLCBuffer(readWriteRequest);
                }
            }
            else
            {
                ReadWriteRequest[] tmpReq = requestsList.ToArray();
                allRequestsList.Add(tmpReq.ToList());

                _resetRequestsList();
                AddNewRequest(readWriteRequest, reqPosition, splitFlag);
            }
        }

        private void _sendSizeExceedPLCBuffer(ReadWriteRequest readWriteRequest)
        {
            WriteOperands wo = readWriteRequest as WriteOperands;
            ushort availableSendSize = (ushort) (plcBuffer - bytesNoToSend - Utils.Lengths.LENGTH_WR_DETAILS);
            ushort availableNoOfOperands = (ushort) (availableSendSize / operandSize);
            if (availableNoOfOperands > 255)
            {
                availableSendSize -= (ushort) ((availableNoOfOperands / 255) * (Utils.Lengths.LENGTH_WR_DETAILS + 1));
                availableSendSize -= 1;
                availableSendSize -= (ushort) ((availableNoOfOperands % 255) % 2);
                availableNoOfOperands = (ushort) (availableSendSize / operandSize);
            }

            ushort noOfOperands = 0;
            ushort startAddress = wo.StartAddress;
            int requestNo = 0;

            WriteOperands remainingWo = new WriteOperands
            {
                NumberOfOperands = (ushort) (wo.NumberOfOperands - availableNoOfOperands),
                OperandType = wo.OperandType,
                StartAddress = (ushort) (wo.StartAddress + availableNoOfOperands),
                TimerValueFormat = wo.TimerValueFormat
            };

            object[] remainingValues = new object[remainingWo.NumberOfOperands];
            Array.Copy(wo.Values, availableNoOfOperands, remainingValues, 0, remainingWo.NumberOfOperands);
            remainingWo.Values = remainingValues;

            if (_canWriteAtLeast1Operand())
            {
                object[] values = null;

                while (availableNoOfOperands > 0)
                {
                    requestNo++;

                    if (availableNoOfOperands > 255)
                    {
                        noOfOperands = 255;
                        availableNoOfOperands -= noOfOperands;
                        values = new object[noOfOperands];
                    }
                    else
                    {
                        noOfOperands = availableNoOfOperands;
                        availableNoOfOperands = 0;
                        values = new object[noOfOperands];
                    }

                    Array.Copy(wo.Values, (requestNo - 1) * 255, values, 0, noOfOperands);
                    WriteOperands tmpWO = new WriteOperands(noOfOperands, wo.OperandType, startAddress, values,
                        wo.TimerValueFormat);
                    requestsList.Add(tmpWO);
                    startAddress += noOfOperands;
                    _updateSplitDetailsList();
                }

                ReadWriteRequest[] tmpReq = requestsList.ToArray();
                allRequestsList.Add(tmpReq.ToList());

                _resetRequestsList();
                AddNewRequest(remainingWo, reqPosition, true);
            }
        }

        private void _receiveSizeExceedPLCBUffer(ReadWriteRequest readWriteRequest)
        {
            ReadOperands ro = readWriteRequest as ReadOperands;
            ushort availableReceiveSize = (ushort) (plcBuffer - bytesNoToReceive - (Utils.Lengths.LENGTH_WR_DETAILS));
            ushort availableNoOfOperands = (ushort) (availableReceiveSize / operandSize);
            if (availableNoOfOperands > 255)
            {
                availableReceiveSize -=
                    (ushort) ((availableNoOfOperands / 255) * (Utils.Lengths.LENGTH_WR_DETAILS + 1));
                availableReceiveSize -= 1;
                availableReceiveSize -= (ushort) ((availableNoOfOperands % 255) % 2);
                availableNoOfOperands = (ushort) (availableReceiveSize / operandSize);
            }

            ushort noOfOperands = 0;
            ushort startAddress = ro.StartAddress;
            int requestNo = 0;

            ReadOperands remainingRo = new ReadOperands
            {
                NumberOfOperands = (ushort) (ro.NumberOfOperands - availableNoOfOperands),
                OperandType = ro.OperandType,
                StartAddress = (ushort) (ro.StartAddress + availableNoOfOperands),
                TimerValueFormat = ro.TimerValueFormat
            };


            while (availableNoOfOperands > 0)
            {
                if (availableNoOfOperands > 255)
                {
                    noOfOperands = 255;
                    availableNoOfOperands -= noOfOperands;
                }
                else
                {
                    noOfOperands = availableNoOfOperands;
                    availableNoOfOperands = 0;
                }

                requestNo++;
                ReadOperands tmpRO = new ReadOperands(noOfOperands, ro.OperandType, startAddress, ro.TimerValueFormat);
                requestsList.Add(tmpRO);
                startAddress += noOfOperands;
                _updateSplitDetailsList();
            }

            ReadWriteRequest[] tmpReq = requestsList.ToArray();
            allRequestsList.Add(tmpReq.ToList());

            _resetRequestsList();
            AddNewRequest(remainingRo, reqPosition, true);
        }

        private void _updateSplitDetailsList()
        {
            SplitDetails sd = new SplitDetails();
            sd.userRequestPosition = reqPosition;
            sd.splitRequestPosition = requestsList.Count - 1;
            sd.allRequestsPosition = allRequestsList.Count;

            splitDetailsList.Add(sd);
        }

        private bool _canReadAtLeast1Operand()
        {
            return plcBuffer - bytesNoToReceive > Utils.Lengths.LENGTH_WR_DETAILS + operandSize;
        }

        private bool _canWriteAtLeast1Operand()
        {
            return plcBuffer - bytesNoToSend > Utils.Lengths.LENGTH_WR_DETAILS + operandSize;
        }

        private bool _requestsSizeFitsPLCBuffer()
        {
            return (bytesNoToSend + requestBytesNoToSend <= plcBuffer) &&
                   (bytesNoToReceive + requestBytesNoToReceive <= plcBuffer);
        }

        private void _resetRequestsSize()
        {
            bytesNoToReceive = Utils.Lengths.LENGTH_HEADER_AND_FOOTER + Utils.Lengths.LENGTH_DETAIL_AREA_HEADER;
            bytesNoToSend = Utils.Lengths.LENGTH_HEADER_AND_FOOTER + Utils.Lengths.LENGTH_DETAIL_AREA_HEADER;
        }

        private void _resetRequestsList()
        {
            requestsList.Clear();
            _resetRequestsSize();
        }

        #endregion
    }
}