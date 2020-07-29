using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Unitronics.ComDriver
{
    public static class ListenerClientsInfo
    {
        private static Dictionary<int, Details> details = new Dictionary<int, Details>();
        private static object lockObject = new object();

        public static List<Details> Details
        {
            get
            {
                lock (lockObject)
                {
                    return details.Values.ToList();
                }
            }
        }

        internal static void IncrementCount(int port)
        {
            lock (lockObject)
            {
                if (details.ContainsKey(port))
                {
                    details[port].Count++;
                }
                else
                {
                    details.Add(port, new Details());
                    details[port].Count++;
                }
            }
        }

        internal static void DecrementCount(int port)
        {
            lock (lockObject)
            {
                if (details.ContainsKey(port))
                {
                    details[port].Count--;
                }
                else
                {
                    details.Add(port, new Details());
                    details[port].Count--;
                }
            }
        }

        public static Details GetDetails(int port)
        {
            lock (lockObject)
            {
                if (details.ContainsKey(port))
                    return details[port];
                else
                    return null;
            }
        }
    }


    public class Details
    {
        private int count = 0;

        public int Count
        {
            get { return count; }
            internal set { count = value; }
        }
    }
}