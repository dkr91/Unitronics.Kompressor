using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Unitronics.ComDriver
{
    public class ComDriveExceptions : Exception
    {
        public enum ComDriveException
        {
            GeneralException,
            MissingFile,
            CannotCreateFile,
            CommunicationTimeout,
            ChecksumError,
            UnsupportedCommand,
            InvalidUnitID,
            CommunicationParamsException,
            ETXMissing,
            GeneralCommunicationError,
            UserInputException,
            UnauthorisedCommand,
            UnexpectedError,
            AbortedByUser,
            PlcNameMismatch,
            PortInUse,
            UnknownPlcModel,
            OperandAddressOutOfRange,
            TransactionIdMismatch,
            ObjectDisposed,
        }

        private ComDriveException _errCode = ComDriveException.CommunicationTimeout;

        public ComDriveExceptions(string pExceptionMsg, ComDriveException pErrCode)
            : base(pExceptionMsg)
        {
            _errCode = pErrCode;

            string exceptionText = pErrCode.ToString() + " - " + pExceptionMsg;
            ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
        }

        public ComDriveException ErrorCode
        {
            get { return _errCode; }
        }
    }
}