using System;
using System.Collections;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using Unitronics.ComDriver.Messages.DataRequest;

namespace Unitronics.ComDriver
{
    public class plc_Kompressor
    {
        private Channel ch;
        private PLC plc;
        public static ReadWriteRequest[] rw;
        private IEnumerable _enumerable;

        public plc_Kompressor(Channel ch)
        {
            init();
            this.ch = ch;
            plc = PLCFactory.GetPLC(ch, 0);
            plc.SetExecuter(OperandsExecuterType.ExecuterPartialBinaryMix);
            rw = init();
        }

        public ReadWriteRequest[] init()
        {
            //
            ReadWriteRequest r1 = new ReadOperands
            {
                NumberOfOperands = 8,
                OperandType = OperandTypes.MI,
                StartAddress = 0,
            };
            return new ReadWriteRequest[] {r1};
        }

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
            int[] iar = new int[8];
            DateTime RTC = plc.RTC;
            Console.Write(RTC + "\t");
            for (int i = 0; i < rw.Length; i++)
            {
                ar = rw[i].ResponseValues as IEnumerable;
                foreach (var VARIABLE in ar)
                {
                    int d = Convert.ToInt32(VARIABLE);

                    if (j < iar.Length)
                    {
                        iar[j] = d;
                    }

                    j++;

                }
            }

            Console.WriteLine(String.Join("\t", iar.Select(dd => dd.ToString()).ToArray()));
            
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

                var dar = iar;
                String query =
                    //"INSERT INTO T2020_07_24_Kühlanlage (date) VALUES ('24.07.2020')";
                    "INSERT INTO Table_1 (date,timestamp,[Luftverbrauch/h SP2],[Luftverbrauch/h SP1],[Luftverbrauch/h MO],[Luftverbrauch/h FB],[Luftverbrauch/h Versand],[Luftverbrauch/h SC],[Luftverbrauch/h HRL],[Luftverbrauch/h F&E]) VALUES ('"+
                    DateTime.Today.ToShortDateString()+"','"+
                    RTC.ToLongTimeString()+"',"+
                    iar[0]+","+
                    iar[1]+","+
                    iar[2]+","+
                    iar[3]+","+
                    iar[4]+","+
                    iar[5]+","+
                    iar[6]+","+
                    iar[7]+");";
                
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