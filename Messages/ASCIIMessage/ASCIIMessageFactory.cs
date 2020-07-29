using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver.Messages;
using Unitronics.ComDriver.Messages.ASCIIMessage;
using Unitronics.ComDriver;

namespace Unitronics.ComDriver.Messages.ASCIIMessage
{
    public static class ASCIIMessageFactory
    {
        /// <summary>
        /// Return's the type of specified command code. 
        /// </summary>
        /// <param name="unitId"></param>
        /// <param name="commandCode"></param>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public static AbstractASCIIMessage GetMessageType(int unitId, string commandCode, int? address, int? length,
            object[] values)
        {
            switch (commandCode)
            {
                case CommandCode.ReadBits.GS:
                case CommandCode.ReadBits.RA:
                case CommandCode.ReadBits.RB:
                case CommandCode.ReadBits.RE:
                case CommandCode.ReadBits.RM:
                case CommandCode.ReadBits.RT:
                case CommandCode.ReadBits.RZB:
                    return new ReadBitsMessage(unitId, commandCode, address, length);
                case CommandCode.SetBits.SA:
                case CommandCode.SetBits.SB:
                case CommandCode.SetBits.SS:
                case CommandCode.SetBits.SZB:
                case CommandCode.SetBits.SD:
                case CommandCode.SetBits.SE:
                    return new SetBitsMessage(unitId, commandCode, address, length, values);
                case CommandCode.ReadIntegers.GF:
                    return new ReadIntegersMessage(unitId, commandCode, address, length,
                        Utils.OperandTypesLength.LENGTH_SI);
                case CommandCode.ReadIntegers.GP:
                    return new ReadIntegersMessage(unitId, commandCode, address, length,
                        Utils.OperandTypesLength.LENGTH_TIMER_PRESET);
                case CommandCode.ReadIntegers.GT:
                    return new ReadIntegersMessage(unitId, commandCode, address, length,
                        Utils.OperandTypesLength.LENGTH_TIMER_CURRENT);
                case CommandCode.ReadIntegers.GX:
                    return new ReadIntegersMessage(unitId, commandCode, address, length,
                        Utils.OperandTypesLength.LENGTH_COUNTER_CURRENT);
                case CommandCode.ReadIntegers.GY:
                    return new ReadIntegersMessage(unitId, commandCode, address, length,
                        Utils.OperandTypesLength.LENGTH_COUNTER_PRESET);
                case CommandCode.ReadIntegers.RND:
                    return new ReadIntegersMessage(unitId, commandCode, address, length,
                        Utils.OperandTypesLength.LENGTH_DW);
                case CommandCode.ReadIntegers.RNF:
                    return new ReadIntegersMessage(unitId, commandCode, address, length,
                        Utils.OperandTypesLength.LENGTH_MF);
                case CommandCode.ReadIntegers.RNH:
                    return new ReadIntegersMessage(unitId, commandCode, address, length,
                        Utils.OperandTypesLength.LENGTH_SL);
                case CommandCode.ReadIntegers.RNJ:
                    return new ReadIntegersMessage(unitId, commandCode, address, length,
                        Utils.OperandTypesLength.LENGTH_SDW);
                case CommandCode.ReadIntegers.RZD:
                    return new ReadIntegersMessage(unitId, commandCode, address, length,
                        Utils.OperandTypesLength.LENGTH_XDW);
                case CommandCode.ReadIntegers.RNL:
                case CommandCode.ReadIntegers.RZL:
                    return new ReadIntegersMessage(unitId, commandCode, address, length,
                        Utils.OperandTypesLength.LENGTH_ML);
                case CommandCode.ReadIntegers.RW:
                    return new ReadIntegersMessage(unitId, commandCode, address, length,
                        Utils.OperandTypesLength.LENGTH_MI);
                case CommandCode.ReadIntegers.RZI:
                    return new ReadIntegersMessage(unitId, commandCode, address, length,
                        Utils.OperandTypesLength.LENGTH_XI);
                case CommandCode.WriteIntegers.SF:
                    return new WriteIntegersMessage(unitId, commandCode, address, length, values,
                        Utils.OperandTypesLength.LENGTH_SI);
                case CommandCode.WriteIntegers.SND:
                    return new WriteIntegersMessage(unitId, commandCode, address, length, values,
                        Utils.OperandTypesLength.LENGTH_DW);
                case CommandCode.WriteIntegers.SNF:
                    return new WriteIntegersMessage(unitId, commandCode, address, length, values,
                        Utils.OperandTypesLength.LENGTH_MF);
                case CommandCode.WriteIntegers.SNH:
                    return new WriteIntegersMessage(unitId, commandCode, address, length, values,
                        Utils.OperandTypesLength.LENGTH_SL);
                case CommandCode.WriteIntegers.SNJ:
                    return new WriteIntegersMessage(unitId, commandCode, address, length, values,
                        Utils.OperandTypesLength.LENGTH_SDW);
                case CommandCode.WriteIntegers.SNL:
                case CommandCode.WriteIntegers.SZL:
                    return new WriteIntegersMessage(unitId, commandCode, address, length, values,
                        Utils.OperandTypesLength.LENGTH_ML);
                case CommandCode.WriteIntegers.SW:
                    return new WriteIntegersMessage(unitId, commandCode, address, length, values,
                        Utils.OperandTypesLength.LENGTH_MI);
                case CommandCode.GetUnitId.UG:
                    return new ReadUnitIdMessage(unitId, commandCode);
                case CommandCode.SetUnitId.US:
                    return new SetUnitIdMessage(unitId, commandCode, values);
                case CommandCode.WriteIntegers.SJ:
                    return new WriteIntegersMessage(unitId, commandCode, address, length, values,
                        Utils.OperandTypesLength.LENGTH_COUNTER_PRESET);
                case CommandCode.WriteIntegers.SK:
                    return new WriteIntegersMessage(unitId, commandCode, address, length, values,
                        Utils.OperandTypesLength.LENGTH_COUNTER_CURRENT);
                case CommandCode.WriteIntegers.SNK:
                    return new WriteIntegersMessage(unitId, commandCode, address, length, values,
                        Utils.OperandTypesLength.LENGTH_TIMER_CURRENT);
                case CommandCode.WriteIntegers.SNT:
                    return new WriteIntegersMessage(unitId, commandCode, address, length, values,
                        Utils.OperandTypesLength.LENGTH_TIMER_PRESET);
                case CommandCode.WriteIntegers.SZI:
                    return new WriteIntegersMessage(unitId, commandCode, address, length, values,
                        Utils.OperandTypesLength.LENGTH_XI);
                case CommandCode.WriteIntegers.SZD:
                    return new WriteIntegersMessage(unitId, commandCode, address, length, values,
                        Utils.OperandTypesLength.LENGTH_XDW);
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}