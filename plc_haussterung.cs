using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Timers;
using Unitronics.ComDriver.Messages.DataRequest;

namespace Unitronics.ComDriver
{
    public class plc_haussterung
    {
        private Channel ch;
        private PLC plc;
        public static ReadWriteRequest[] rw;
        private IEnumerable _enumerable;

        public plc_haussterung(Channel ch)
        {
            init();
            this.ch = ch;
            plc = PLCFactory.GetPLC(ch, 0);
            plc.SetExecuter(OperandsExecuterType.ExecuterPartialBinaryMix);
            rw = init();
        }

        //Staticly type for needed Application
        public ReadWriteRequest[] init()
        {
            //Unusedwl ser
            ReadWriteRequest r1 = new ReadOperands
            {
                NumberOfOperands = 1,
                OperandType = OperandTypes.MI,
                StartAddress = 1501,
            };ReadWriteRequest r2 = new ReadOperands
            {
                NumberOfOperands = 1,
                OperandType = OperandTypes.MI,
                StartAddress = 56,
            };
            //Hydr Vorlauf+Rück % Kom Vor+Rück --> Spliten
            ReadWriteRequest r3 = new ReadOperands
            {
                NumberOfOperands = 4,
                OperandType = OperandTypes.MI,
                StartAddress = 1631,
            };
            //Hydr. --> Spliten
            ReadWriteRequest r4 = new ReadOperands
            {
                NumberOfOperands = 2,
                OperandType = OperandTypes.MI,
                StartAddress = 29,
            };
            //Aussentemp
            ReadWriteRequest r5 = new ReadOperands
            {
                NumberOfOperands = 1,
                OperandType = OperandTypes.MI,
                StartAddress = 1523,
            };
            //Fehler
            ReadWriteRequest r6 = new ReadOperands
            {
                NumberOfOperands = 1,
                OperandType = OperandTypes.MB,
                StartAddress = 2300,
            };
            //Daten Kühlung aktiv
            ReadWriteRequest r7 = new ReadOperands
            {
                NumberOfOperands = 1,
                OperandType = OperandTypes.SB,
                StartAddress = 3,
            };
            //Luft MO
            ReadWriteRequest r8 = new ReadOperands
            {
                NumberOfOperands = 1,
                OperandType = OperandTypes.MB,
                StartAddress = 1807,
            };
            //Luft SP1
            ReadWriteRequest r9 = new ReadOperands
            {
                NumberOfOperands = 1,
                OperandType = OperandTypes.MB,
                StartAddress = 1805,
            };
            //Luft SC
            ReadWriteRequest r10 = new ReadOperands
            {
                NumberOfOperands = 1,
                OperandType = OperandTypes.MB,
                StartAddress = 1801,
            };
            //Luft Versorgung
            ReadWriteRequest r11 = new ReadOperands
            {
                NumberOfOperands = 1,
                OperandType = OperandTypes.MB,
                StartAddress = 1811,
            };
            //Luft SP2
            ReadWriteRequest r12 = new ReadOperands
            {
                NumberOfOperands = 1,
                OperandType = OperandTypes.MB,
                StartAddress = 1803,
            };
            //Luft FB
            ReadWriteRequest r13 = new ReadOperands
            {
                NumberOfOperands = 1,
                OperandType = OperandTypes.MB,
                StartAddress = 1809,
            };
            //Temp Klebeplatz
            ReadWriteRequest r14 = new ReadOperands
            {
                NumberOfOperands = 1,
                OperandType = OperandTypes.MI,
                StartAddress = 1561,
            };
            return new ReadWriteRequest[] {r1, r2, r3, r4, r5, r6, r7, r8, r9, r10, r11, r12, r13, r14};
        }

        //Uses RWQuest and reads data Prints it or Puts it into DB
        public void readData()
        {
            try
            {
                plc.ReadWrite(ref rw);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + "\t" + e.StackTrace);
                throw;
            }

            int j = 0; // J is counter to specifiy decimal values conversion
            IEnumerable ar = null;
            double[] dar = new double[18];
            DateTime RTC = plc.RTC;
            Console.Write(RTC + "\t");
            for (int i = 0; i < rw.Length; i++)
            {
                ar = rw[i].ResponseValues as IEnumerable;
                foreach (var VARIABLE in ar)
                {
                    double d = Convert.ToDouble(VARIABLE);
                    switch (j)
                    {
                        case 0:
                        case 1:
                        case 2:
                        case 3:
                        case 4:
                        case 5:
                        case 6:
                        case 7:
                        case 8:
                        case 17:
                            d /= 10;
                            d = Math.Round(d, 3);
                            break;
                    }

                    if (j < dar.Length)
                    {
                        dar[j] = d;
                    }

                    j++;

                }
            }    
            Console.Write(String.Join("\t",dar.Select(dd=>dd.ToString()).ToArray()));

            
            String s = (@"Data Source=192.168.50.9;Initial Catalog=UnitronicsPLC;User ID=Unitronics;Password=Uni2020#");
            SqlConnection conn;
            SqlDataReader dataReader;
            conn = new SqlConnection(s);
            try
            {
                if (conn.State == ConnectionState.Closed)
                {
                    conn.Open();
                }

                String query =
                    //"INSERT INTO T2020_07_24_Kühlanlage (date) VALUES ('24.07.2020')";
                    "INSERT INTO Table_4 (date,ts,[Luftverbrauch/h SP1],[Luftverbrauch/h MO],[Luftverbrauch/h FB],[Luftverbrauch/h Versand],[Luftverbrauch/h SC],[Luftverbrauch/h HRL],[Luftverbrauch/h F&E]) VALUES ('"+
                    DateTime.Today.ToShortDateString()+"','"+
                    RTC.ToLocalTime().ToLongTimeString()+"',"+
                    dar[0].ToString("F",CultureInfo.InvariantCulture)+","+
                    dar[1].ToString("F",CultureInfo.InvariantCulture)+","+
                    dar[2].ToString("F",CultureInfo.InvariantCulture)+","+
                    dar[3].ToString("F",CultureInfo.InvariantCulture)+","+
                    dar[4].ToString("F",CultureInfo.InvariantCulture)+","+
                    dar[5].ToString("F",CultureInfo.InvariantCulture)+","+
                    dar[6].ToString("F",CultureInfo.InvariantCulture)+","+
                    dar[7].ToString("F",CultureInfo.InvariantCulture)+","+
                    dar[8].ToString("F",CultureInfo.InvariantCulture)+","+
                    dar[9].ToString("F",CultureInfo.InvariantCulture)+","+
                    dar[10].ToString("F",CultureInfo.InvariantCulture)+","+
                    dar[11].ToString("F",CultureInfo.InvariantCulture)+","+
                    dar[12].ToString("F",CultureInfo.InvariantCulture)+","+
                    dar[13].ToString("F",CultureInfo.InvariantCulture)+","+
                    dar[14].ToString("F",CultureInfo.InvariantCulture)+");";
                
                //Console.WriteLine(query);
                SqlCommand command = new SqlCommand(query, conn);
                dataReader = command.ExecuteReader();
                Console.WriteLine("\t\t SQL-Query succesful");
            }
            catch (Exception e)
            {
                //Console.WriteLine("\tError occured");
                Console.WriteLine("Error occured:\t"+e.Message);
            }
            
        }
    }
}