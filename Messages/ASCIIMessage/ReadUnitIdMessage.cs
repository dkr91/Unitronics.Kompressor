using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver.Messages.ASCIIMessage;

namespace Unitronics.ComDriver.Messages.ASCIIMessage
{
    public class ReadUnitIdMessage : AbstractASCIIMessage
    {
        #region Constructors

        public ReadUnitIdMessage() : base()
        {
        }

        public ReadUnitIdMessage(int unitId, string commandCode)
            : base(unitId, commandCode)
        {
        }

        #endregion

        #region Public

        public override AbstractASCIIMessage GetMessage(string message)
        {
            int index = 0;

            index += Utils.Lengths.LENGTH_STX1;
            int unitId = Convert.ToInt32(message.Substring(index, Utils.Lengths.LENGTH_UNIT_ID), 16);

            index += Utils.Lengths.LENGTH_UNIT_ID;
            string commandCode = message.Substring(index, Utils.Lengths.LENGTH_COMMAND_CODE);

            index += Utils.Lengths.LENGTH_UNIT_ID;
            char[] charBits = message.Substring(index, Utils.Lengths.LENGTH_UNIT_ID).ToCharArray();
            ArrayValues = charBits.Select(c => c as object);

            index += Utils.Lengths.LENGTH_CRC;
            string crc = message.Substring(index, Utils.Lengths.LENGTH_CRC);

            return this;
        }

        #endregion
    }
}