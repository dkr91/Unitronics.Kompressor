using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Unitronics.ComDriver
{
    public class PlcComConfig
    {
        public Channel Channel { get; set; }
        public int UnitID { get; set; }
        public string PlcName { get; set; }
        public bool RequirePlcName { get; set; }
        public bool ForceJazz { get; set; }

        public PlcComConfig()
        {
            UnitID = 0;
            PlcName = "";
            RequirePlcName = false;
            Serial serial = new Serial(SerialPortNames.COM1, BaudRate.BR57600, 3, 1000, DataBits.DB8,
                System.IO.Ports.Parity.None, System.IO.Ports.StopBits.One);
            Channel = serial;
        }

        public PlcComConfig(Channel channel)
        {
            UnitID = 0;
            PlcName = "";
            RequirePlcName = false;
            Channel = channel;
        }

        public override string ToString()
        {
            if (Channel is Serial)
            {
                return "Serial";
            }
            else if (Channel is Ethernet)
            {
                return "Ethernet (Call)";
            }
            else if (Channel is EthernetListener)
            {
                return "Ethernet (Listen)";
            }
            else
            {
                return "";
            }
        }
    }
}