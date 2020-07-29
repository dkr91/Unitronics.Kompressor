using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Unitronics.ComDriver.Messages
{
    public class OperandType
    {
        public string OperandName { get; set; }
        public byte OperandSize { get; set; }
        public string CommandCodeForRead { get; set; }
        public string CommandCodeForWrite { get; set; }

        public byte VectorialValue
        {
            get { return (byte) (ByteValue + 128); }
        }

        public byte ByteValue { get; set; }
    }
}