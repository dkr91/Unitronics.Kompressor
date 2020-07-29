using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Threading;
using Unitronics.ComDriver.Messages.DataRequest;


namespace Unitronics.ComDriver
{
    class Main
    {
        public static void main()
        {
            
            //Haussteuerung
            {
                Channel ch = new Ethernet("192.168.47.12", 20257, EthProtocol.TCP);
                PLC plc = PLCFactory.GetPLC(ch, 0);
                Console.WriteLine(plc.PlcName);
                plc.SetExecuter(OperandsExecuterType.ExecuterPartialBinaryMix);

                Console.WriteLine(plc.Version + "\t");
                Console.WriteLine(plc.RTC + "\tPLC Timer \n" + System.DateTime.Now + "\tUTC Time");

                plc_Kompressor plcKompressor = new plc_Kompressor(ch);
                ReadWriteRequest[] rw = plcKompressor.init();


                int j = 0;
                while (j<30)
                {
                    plcKompressor.readData();
                    Thread.Sleep(1000);
                    j++;
                }
                
            }
            
            
        }
    }
}