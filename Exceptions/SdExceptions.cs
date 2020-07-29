using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Unitronics.ComDriver
{
    public class SdExceptions : Exception
    {
        public enum SdException
        {
            UnknownError,
            TriggerError,
            SdDriverError,
            BufferOverflow,
            MsgKeyError,
            SdChannelLocked,
            InvalidPathOrFilename,
            UnexpectedError,
            IllegalFileName,
            FileOpenedByOtherClient,
        }

        private SdException _errCode = SdException.UnknownError;

        public SdExceptions(string pExceptionMsg, SdException pErrCode)
            : base(pExceptionMsg)
        {
            _errCode = pErrCode;

            string exceptionText = pErrCode.ToString() + " - " + pExceptionMsg;
            ComDriverLogger.LogExceptions(DateTime.Now, exceptionText);
        }

        public SdException ErrorCode
        {
            get { return _errCode; }
        }
    }
}