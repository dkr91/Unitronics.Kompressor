using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver.Messages.DataRequest;
using Unitronics.ComDriver.Messages;
using Unitronics.ComDriver;

namespace Unitronics.ComDriver.Command
{
    public abstract class CommadBuilder
    {
        public abstract void BuildBinaryCommand(byte unitId, BinaryCommand commandNumber, byte[] cmdDetails,
            List<ReadWriteRequest> dataRequests, List<List<byte>> dataRequestsBytes);

        public abstract void BuildBinaryCommand(byte unitId, BinaryCommand commandNumber, byte[] cmdDetails,
            List<ReadWriteRequest> dataRequests, List<List<byte>> dataRequestsBytes, byte subCommand);

        public abstract void BuildBinaryCommand(byte unitId, int messageKey, int commandNumber, int subCommand,
            int address, int elementsCount, UInt16 outgoingDataLength, byte[] binaryData);

        public abstract void DisAssembleBinaryResult(byte[] message, List<List<byte>> dataRequestBytes);

        public abstract void BuildAsciiCommand(int unitId, string commandCode, int? address, int? length,
            object[] values, TimerValueFormat timerValueFormat);

        public abstract void DisAssembleAsciiResult(string message, int unitId, string commandCode, int? address,
            int? length, List<List<byte>> dataRequestBytes, TimerValueFormat timerValueFormat);

        public abstract object MessageToPLC { get; }
        public abstract IMessage MessageFromPLC { get; }
    }
}