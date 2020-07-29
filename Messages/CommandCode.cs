using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Unitronics.ComDriver.Messages
{
    internal static class CommandCode
    {
        /// <summary>
        /// RE, RA, RB, GS, RT, RM - Read bits
        /// </summary>
        internal static class ReadBits
        {
            /// <summary>
            /// Read inputs
            /// </summary>
            internal const String RE = "RE";

            /// <summary>
            /// Read outputs
            /// </summary>
            internal const String RA = "RA";

            /// <summary>
            /// Read memory bits
            /// </summary>
            internal const String RB = "RB";

            /// <summary>
            /// Read system bits
            /// </summary>
            internal const String GS = "GS";

            /// <summary>
            /// Read timer scan bits
            /// </summary>
            internal const String RT = "RT";

            /// <summary>
            /// Read counter scan bits
            /// </summary>
            internal const String RM = "RM";

            /// <summary>
            /// Read Fast bit
            /// </summary>
            internal const String RZB = "RZB";
        }

        /// <summary>
        /// SA, SB, SS - Set bits
        /// </summary>
        internal static class SetBits
        {
            /// <summary>
            /// Set outputs
            /// </summary>
            internal const String SA = "SA";

            /// <summary>
            /// Set memory bits
            /// </summary>
            internal const String SB = "SB";

            /// <summary>
            /// Set system bits
            /// </summary>
            internal const String SS = "SS";

            /// <summary>
            /// Set Fast Bit
            /// </summary>
            internal const String SZB = "SZB";

            /// <summary>
            /// Set outputs Force Bit
            /// </summary>
            internal const String SE = "SE";

            /// <summary>
            /// Set inputs Force Bit
            /// </summary>
            internal const String SD = "SD";
        }

        /// <summary>
        /// RW, RNL/D/H/J/F, GF, GT, GP,GX, GY - Read integers (16 and 32 bits)
        /// </summary>
        internal static class ReadIntegers
        {
            /// <summary>
            /// Read MIs
            /// </summary>
            internal const String RW = "RW";

            /// <summary>
            /// Read MLs
            /// </summary>
            internal const String RNL = "RNL";

            /// <summary>
            /// Read MDWs
            /// </summary>
            internal const String RND = "RND";

            /// <summary>
            /// Read SIs
            /// </summary>
            internal const String GF = "GF";

            /// <summary>
            /// Read SLs
            /// </summary>
            internal const String RNH = "RNH";

            /// <summary>
            /// Read SDWs
            /// </summary>
            internal const String RNJ = "RNJ";

            /// <summary>
            /// Read MFs (memory floats)
            /// </summary>
            internal const String RNF = "RNF";

            /// <summary>
            /// Read Timer's current value
            /// </summary>
            internal const String GT = "GT";

            /// <summary>
            /// Read Timer's preset value
            /// </summary>
            internal const String GP = "GP";

            /// <summary>
            /// Read Counter's current value
            /// </summary>
            internal const String GX = "GX";

            /// <summary>
            /// Read Counter's preset value
            /// </summary>
            internal const String GY = "GY";

            /// <summary>
            /// Read Fast Integer
            /// </summary>
            internal const String RZI = "RZI";

            /// <summary>
            /// Read Fast Double
            /// </summary>
            internal const String RZD = "RZD";

            /// <summary>
            /// Read Fast Long
            /// </summary>
            internal const String RZL = "RZL";
        }

        /// <summary>
        /// SW, SNL/D/H/J, SF - Write integers (16 and 32 bits)
        /// </summary>
        internal static class WriteIntegers
        {
            /// <summary>
            /// Write MIs
            /// </summary>
            internal const String SW = "SW";

            /// <summary>
            /// Write MLs
            /// </summary>
            internal const String SNL = "SNL";

            /// <summary>
            /// Write MDWs
            /// </summary>
            internal const String SND = "SND";

            /// <summary>
            /// write MFs
            /// </summary>
            internal const String SNF = "SNF";

            /// <summary>
            /// Write SIs
            /// </summary>
            public const String SF = "SF";

            /// <summary>
            /// Write SLs
            /// </summary>
            internal const String SNH = "SNH";

            /// <summary>
            /// Write SDWs
            /// </summary>
            internal const String SNJ = "SNJ";

            /// <summary>
            /// Write Timer Preset
            /// </summary>
            internal const String SNT = "SNT";

            /// <summary>
            /// Write Timer Current
            /// </summary>
            internal const String SNK = "SNK";

            /// <summary>
            /// Write Counter Preset
            /// </summary>
            internal const String SJ = "SJ";

            /// <summary>
            /// Write Counter Current
            /// </summary>
            internal const String SK = "SK";

            /// <summary>
            /// Write Fast Integer
            /// </summary>
            internal const String SZI = "SZI";

            /// <summary>
            /// Write Fast Double
            /// </summary>
            internal const String SZD = "SZD";

            /// <summary>
            /// Write Fast Long
            /// </summary>
            internal const String SZL = "SZL";
        }

        /// <summary>
        /// RC - Read RTC
        /// </summary>
        internal static class ReadRTC
        {
            /// <summary>
            /// Read Real Time Clock
            /// </summary>
            internal const String RC = "RC";
        }

        /// <summary>
        /// SC - Set RTC
        /// </summary>
        internal static class SetRTC
        {
            /// <summary>
            /// Set Real Time Clock
            /// </summary>
            internal const String SC = "SC";
        }

        /// <summary>
        /// Set the Unit Id
        /// </summary>
        internal static class SetUnitId
        {
            /// <summary>
            /// Set the Unit Id
            /// </summary>
            internal const string US = "US";
        }

        /// <summary>
        /// Get the Unit Id
        /// </summary>
        internal static class GetUnitId
        {
            /// <summary>
            /// Get the Unit Id
            /// </summary>
            internal const string UG = "UG";
        }
    }
}