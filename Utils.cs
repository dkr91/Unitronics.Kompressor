using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using Unitronics.ComDriver.Messages.DataRequest;
using Unitronics.ComDriver.Messages;
using System.Configuration;
using System.Text.RegularExpressions;
using System.Collections.Specialized;

namespace Unitronics.ComDriver
{
    #region Enums

    internal enum ConnectionStatus
    {
        ConnectionOpened,
        ConnectionClosed,
        Listening,
    }

    public enum LogType
    {
        ConnectionState,
        Exceptions,
        ReceivedMessageChunk,
        FullMessage,
        ReadWriteRequest
    }

    public enum EthProtocol
    {
        TCP,
        UDP
    }

    public enum OperandsExecuterType
    {
        None = 0,
        ExecuterAscii = 1,
        ExecuterFullBinaryMix = 2,
        ExecuterPartialBinaryMix = 3
    }

    public enum ExecutersType
    {
        Operands = 0, // (represents 2^0) = 1
        DataTables = 1, // (represents 2^1) = 2
        BasicBinary = 2, // (represents 2^2) = 4
        Binary = 3, // (represents 2^3) = 8 
    }

    internal struct FlashStatus
    {
        internal FlashStatusRunStop FlashRunStop;
        internal eFlashStatus eFlashStatus;
        internal MemoryStatus MemoryStatus;
        internal int CurrentAddress;
        internal byte SectorNumber;
        internal int SectorSize;
        internal CompilerStatus CompilerStatus;
        internal CompilerError CompilerError;
        internal bool DownloadEnded;
        internal byte[] ValidBitmap;
        internal bool AllFlashValid;
    }

    internal enum FlashStatusRunStop
    {
        Run = 82,
        Stop = 83
    }

    internal enum eFlashStatus
    {
        Idle = 0,
        Erasing = 1,
        Programing = 2,
        Error = 3
    }

    internal enum MemoryStatus
    {
        Idle = 73,
        Writing = 87,
        Erasing = 69,
        Copying = 67,
        NewPassword = 80,
        SubTopic = 83,
        Repair = 82,
    }

    internal enum CompilerStatus
    {
        Ready = 0,
        Burning = 1,
        Compiling = 2,
        Error = -1
    }

    internal enum CompilerError
    {
        NoError = 0,
        TokenUndefined = 3,
        PLC_OverFlow = 4,
        UnknownStep = 5,
        TokenOverFlow = 6,
        FlashError = 7,
        InvalidData = 8,
        TokenUnderFlow = 9
    }

    /// <summary>
    /// Specifies the format of the Timer value
    /// </summary>
    public enum TimerValueFormat
    {
        /// <summary>
        /// The Timer Value has the format: HH:MM:SS:MS
        /// <remarks>
        /// The user must specify a List of 4 UInt16 value representing HH:MM:SS:MS(in this order)
        /// </remarks>
        /// <example>
        /// The Timer Value: 10 hours: 30 minutes: 25 sec: 95 millisecond (10:30:25:95)
        /// is represented by a List of UInt16{10, 30, 25, 95}
        /// </example>
        /// </summary>
        TimeFormat,

        /// <summary>
        /// The Timer Value has the SecondsFormat: The user must specify the value in 1/100 sec units.
        /// <example>
        /// Example: The Timer value for 12 hours: 34 minutes: 56 seconds: 78 milliseconds (12:34:56:78)
        /// is calculated: (12*3600 + 34*60 + 56)*100 + 78 = 4529678 1/100 sec units.
        /// </example>
        /// </summary>
        SecondsFormat,

        /// <summary>
        /// No Timer Format. Set when the request doesn't contains TimerCurrent and TimerPreset operand types.
        /// </summary>
        None
    }

    public enum BinaryCommand
    {
        ReadDataTables = 4,
        ReadPartOfProjectDataTables = 75,
        WriteDataTables = 68,
        ReadOperands = 77,
        ReadWrite = 80
    }

    public enum CommunicationException
    {
        None = 0,
        Timeout = 1
    }

    public enum SerialPortNames
    {
        COM1,
        COM2,
        COM3,
        COM4,
        COM5,
        COM6,
        COM7,
        COM8,
        COM9,
        COM10,
        COM11,
        COM12,
        COM13,
        COM14,
        COM15,
        COM16,
        COM17,
        COM18,
        COM19,
        COM20,
        COM21,
        COM22,
        COM23,
        COM24,
        COM25,
        COM26,
        COM27,
        COM28,
        COM29,
        COM30,
        COM31,
        COM32,
        COM33,
        COM34,
        COM35,
        COM36,
        COM37,
        COM38,
        COM39,
        COM40,
        COM41,
        COM42,
        COM43,
        COM44,
        COM45,
        COM46,
        COM47,
        COM48,
        COM49,
        COM50,
        COM51,
        COM52,
        COM53,
        COM54,
        COM55,
        COM56,
        COM57,
        COM58,
        COM59,
        COM60,
        COM61,
        COM62,
        COM63,
        COM64,
        COM65,
        COM66,
        COM67,
        COM68,
        COM69,
        COM70,
        COM71,
        COM72,
        COM73,
        COM74,
        COM75,
        COM76,
        COM77,
        COM78,
        COM79,
        COM80,
        COM81,
        COM82,
        COM83,
        COM84,
        COM85,
        COM86,
        COM87,
        COM88,
        COM89,
        COM90,
        COM91,
        COM92,
        COM93,
        COM94,
        COM95,
        COM96,
        COM97,
        COM98,
        COM99,
        COM100,
        COM101,
        COM102,
        COM103,
        COM104,
        COM105,
        COM106,
        COM107,
        COM108,
        COM109,
        COM110,
        COM111,
        COM112,
        COM113,
        COM114,
        COM115,
        COM116,
        COM117,
        COM118,
        COM119,
        COM120,
        COM121,
        COM122,
        COM123,
        COM124,
        COM125,
        COM126,
        COM127,
        COM128,
        COM129,
        COM130,
        COM131,
        COM132,
        COM133,
        COM134,
        COM135,
        COM136,
        COM137,
        COM138,
        COM139,
        COM140,
        COM141,
        COM142,
        COM143,
        COM144,
        COM145,
        COM146,
        COM147,
        COM148,
        COM149,
        COM150,
        COM151,
        COM152,
        COM153,
        COM154,
        COM155,
        COM156,
        COM157,
        COM158,
        COM159,
        COM160,
        COM161,
        COM162,
        COM163,
        COM164,
        COM165,
        COM166,
        COM167,
        COM168,
        COM169,
        COM170,
        COM171,
        COM172,
        COM173,
        COM174,
        COM175,
        COM176,
        COM177,
        COM178,
        COM179,
        COM180,
        COM181,
        COM182,
        COM183,
        COM184,
        COM185,
        COM186,
        COM187,
        COM188,
        COM189,
        COM190,
        COM191,
        COM192,
        COM193,
        COM194,
        COM195,
        COM196,
        COM197,
        COM198,
        COM199,
        COM200,
        COM201,
        COM202,
        COM203,
        COM204,
        COM205,
        COM206,
        COM207,
        COM208,
        COM209,
        COM210,
        COM211,
        COM212,
        COM213,
        COM214,
        COM215,
        COM216,
        COM217,
        COM218,
        COM219,
        COM220,
        COM221,
        COM222,
        COM223,
        COM224,
        COM225,
        COM226,
        COM227,
        COM228,
        COM229,
        COM230,
        COM231,
        COM232,
        COM233,
        COM234,
        COM235,
        COM236,
        COM237,
        COM238,
        COM239,
        COM240,
        COM241,
        COM242,
        COM243,
        COM244,
        COM245,
        COM246,
        COM247,
        COM248,
        COM249,
        COM250,
        COM251,
        COM252,
        COM253,
        COM254,
        COM255,
        COM256
    }

    public enum BaudRate
    {
        BR110 = 110,
        BR300 = 300,
        BR600 = 600,
        BR1200 = 1200,
        BR2400 = 2400,
        BR4800 = 4800,
        BR9600 = 9600,
        BR19200 = 19200,
        BR38400 = 38400,
        BR57600 = 57600,
        BR115200 = 115200
    }

    public enum DataBits
    {
        DB7 = 7,
        DB8 = 8,
    }

    public enum OperandTypes
    {
        MB,
        SB,
        MI,
        SI,
        ML,
        SL,
        MF,
        Input,
        Output,
        TimerRunBit,
        CounterRunBit,
        DW,
        SDW,
        CounterCurrent,
        CounterPreset,
        TimerCurrent,
        TimerPreset,
        XB,
        XI,
        XDW,
        XL
    }

    internal enum MessageDirection
    {
        Sent,
        Received,
        Unspecified
    }

    internal enum Channels
    {
        Serial,
        Ethernet,
        EthernetListener,
        ListenerServer,
        ListenerClient,
    }

    #endregion

    #region Structs

    public struct LogTypesConfig
    {
        public bool LogConnectionState;
        public bool LogExceptions;
        public bool LogReceivedMessageChunk;
        public bool LogFullMessage;
        public bool LogReadWriteRequest;

        public LogTypesConfig(bool logConnectionState, bool logExceptions, bool logReceivedMessageChunk,
            bool logFullMessage, bool logReadWriteRequest)
        {
            LogConnectionState = logConnectionState;
            LogExceptions = logExceptions;
            LogReceivedMessageChunk = logReceivedMessageChunk;
            LogFullMessage = logFullMessage;
            LogReadWriteRequest = logReadWriteRequest;
        }
    }

    public struct ForcedParams
    {
        public ushort BufferSize;
        public bool CalcBinaryExecuterForcedBufferSizeOnDataOnly;
        public OperandsExecuterType ExecuterType;
    }

    internal struct SplitDetails
    {
        public int userRequestPosition;
        public int splitRequestPosition;
        public int allRequestsPosition;
    };

    internal struct ByteBits
    {
        public ByteBits(byte initialBitValue)
        {
            bits = initialBitValue;
        }

        public bool this[int index]
        {
            get { return (bits & (1 << index)) != 0; }
            set
            {
                if (value) // turn the bit on if value is true; otherwise, turn it off
                    bits |= (byte) (1 << index);
                else
                    bits &= (byte) (~(1 << index));
            }
        }

        private byte bits;
    }

    public class RequestProgress
    {
        public enum en_NotificationType
        {
            SetMinMax,
            ProgressChanged,
            Completed,
        }

        private en_NotificationType m_NotificationType;
        private int m_Minimum;
        private int m_Maximum;
        private int m_Value;
        private string m_Text;

        public RequestProgress()
        {
        }

        public en_NotificationType NotificationType
        {
            get { return m_NotificationType; }
            set { m_NotificationType = value; }
        }

        public int Minimum
        {
            get { return m_Minimum; }
            set { m_Minimum = value; }
        }

        public int Maximum
        {
            get { return m_Maximum; }
            set { m_Maximum = value; }
        }

        public int Value
        {
            get { return m_Value; }
            set { m_Value = value; }
        }

        public string Text
        {
            get { return m_Text; }
            set { m_Text = value; }
        }

        public float Percentage
        {
            get { return 100 * (float) Math.Round((float) (m_Value - m_Minimum) / (float) (m_Maximum - m_Minimum), 2); }
        }

        public double PercentageUnRounded
        {
            get { return 100.0 * (double) (m_Value - m_Minimum) / (double) (m_Maximum - m_Minimum); }
        }
    }

    internal class GuidClass
    {
        private Guid guid;

        public GuidClass()
        {
            guid = Guid.NewGuid();
        }

        public override string ToString()
        {
            return guid.ToString();
        }
    }

    #endregion

    #region delegatas and events

    public delegate void ProgressStatusChangedDelegate(RequestProgress requestProgress);

    #endregion

    #region Class Utils

    public static class Utils
    {
        internal static string UnspecifiedString = "";

        internal static double
            z_nanosecondsPerTick =
                (double) 1000000000 / System.Diagnostics.Stopwatch.Frequency; //1 tick in nanoseconds}

        internal static String STX_STRING = "/_OPLC";

        #region Collections

        public static Dictionary<string, OperandType> OperandTypesDictionary = new Dictionary<string, OperandType>()
        {
            {
                Utils.Operands.OP_MB,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_MB, ByteValue = 1, OperandSize = 1,
                    CommandCodeForRead = CommandCode.ReadBits.RB, CommandCodeForWrite = CommandCode.SetBits.SB
                }
            },
            {
                Utils.Operands.OP_SB,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_SB, ByteValue = 2, OperandSize = 1,
                    CommandCodeForRead = CommandCode.ReadBits.GS, CommandCodeForWrite = CommandCode.SetBits.SS
                }
            },
            {
                Utils.Operands.OP_MI,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_MI, ByteValue = 3, OperandSize = 16,
                    CommandCodeForRead = CommandCode.ReadIntegers.RW, CommandCodeForWrite = CommandCode.WriteIntegers.SW
                }
            },
            {
                Utils.Operands.OP_SI,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_SI, ByteValue = 4, OperandSize = 16,
                    CommandCodeForRead = CommandCode.ReadIntegers.GF, CommandCodeForWrite = CommandCode.WriteIntegers.SF
                }
            },
            {
                Utils.Operands.OP_ML,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_ML, ByteValue = 5, OperandSize = 32,
                    CommandCodeForRead = CommandCode.ReadIntegers.RNL,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SNL
                }
            },
            {
                Utils.Operands.OP_SL,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_SL, ByteValue = 6, OperandSize = 32,
                    CommandCodeForRead = CommandCode.ReadIntegers.RNH,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SNH
                }
            },
            {
                Utils.Operands.OP_MF,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_MF, ByteValue = 7, OperandSize = 32,
                    CommandCodeForRead = CommandCode.ReadIntegers.RNF,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SNF
                }
            },
            {
                Utils.Operands.OP_INPUT,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_INPUT, ByteValue = 9, OperandSize = 1,
                    CommandCodeForRead = CommandCode.ReadBits.RE
                }
            },
            {
                Utils.Operands.OP_OUTPUT,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_OUTPUT, ByteValue = 10, OperandSize = 1,
                    CommandCodeForRead = CommandCode.ReadBits.RA, CommandCodeForWrite = CommandCode.SetBits.SA
                }
            },
            {
                Utils.Operands.OP_TIMER_RUN_BIT,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_TIMER_RUN_BIT, ByteValue = 11, OperandSize = 1,
                    CommandCodeForRead = CommandCode.ReadBits.RT
                }
            },
            {
                Utils.Operands.OP_COUNTER_RUN_BIT,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_COUNTER_RUN_BIT, ByteValue = 12, OperandSize = 1,
                    CommandCodeForRead = CommandCode.ReadBits.RM
                }
            },
            //{Utils.OP_TIMER_SCAN_BIT, new OperandType {OperandName = Utils.OP_TIMER_SCAN_BIT, OperandSize = 1,CommandCodeForRead = CommandCode.ReadBits.RT}},
            {
                Utils.Operands.OP_DW,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_DW, ByteValue = 16, OperandSize = 32,
                    CommandCodeForRead = CommandCode.ReadIntegers.RND,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SND
                }
            },
            {
                Utils.Operands.OP_SDW,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_SDW, ByteValue = 17, OperandSize = 32,
                    CommandCodeForRead = CommandCode.ReadIntegers.RNJ,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SNJ
                }
            },
            {
                Utils.Operands.OP_COUNTER_CURRENT,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_COUNTER_CURRENT, ByteValue = 18, OperandSize = 16,
                    CommandCodeForRead = CommandCode.ReadIntegers.GX, CommandCodeForWrite = CommandCode.WriteIntegers.SK
                }
            },
            {
                Utils.Operands.OP_COUNTER_PRESET,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_COUNTER_PRESET, ByteValue = 19, OperandSize = 16,
                    CommandCodeForRead = CommandCode.ReadIntegers.GY, CommandCodeForWrite = CommandCode.WriteIntegers.SJ
                }
            },
            {
                Utils.Operands.OP_TIMER_CURRENT,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_TIMER_CURRENT, ByteValue = 20, OperandSize = 32,
                    CommandCodeForRead = CommandCode.ReadIntegers.GT,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SNK
                }
            },
            {
                Utils.Operands.OP_TIMER_PRESET,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_TIMER_PRESET, ByteValue = 21, OperandSize = 32,
                    CommandCodeForRead = CommandCode.ReadIntegers.GP,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SNT
                }
            },
            {
                Utils.Operands.OP_XB,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_XB, ByteValue = 26, OperandSize = 1,
                    CommandCodeForRead = CommandCode.ReadBits.RZB, CommandCodeForWrite = CommandCode.SetBits.SZB
                }
            },
            {
                Utils.Operands.OP_XI,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_XI, ByteValue = 27, OperandSize = 16,
                    CommandCodeForRead = CommandCode.ReadIntegers.RZI,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SZI
                }
            },
            {
                Utils.Operands.OP_XL,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_XL, ByteValue = 28, OperandSize = 32,
                    CommandCodeForRead = CommandCode.ReadIntegers.RZL,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SZL
                }
            },
            {
                Utils.Operands.OP_XDW,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_XDW, ByteValue = 29, OperandSize = 32,
                    CommandCodeForRead = CommandCode.ReadIntegers.RZD,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SZD
                }
            },
        };

        public static Dictionary<string, OperandType> ASCIIOperandTypes = new Dictionary<string, OperandType>()
        {
            {
                Utils.Operands.OP_INPUT,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_INPUT, CommandCodeForRead = CommandCode.ReadBits.RE, OperandSize = 1
                }
            },
            {
                Utils.Operands.OP_OUTPUT,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_OUTPUT, CommandCodeForRead = CommandCode.ReadBits.RA,
                    CommandCodeForWrite = CommandCode.SetBits.SA, OperandSize = 1
                }
            },
            {
                Utils.Operands.OP_MB,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_MB, CommandCodeForRead = CommandCode.ReadBits.RB,
                    CommandCodeForWrite = CommandCode.SetBits.SB, OperandSize = 1
                }
            },
            {
                Utils.Operands.OP_SB,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_SB, CommandCodeForRead = CommandCode.ReadBits.GS,
                    CommandCodeForWrite = CommandCode.SetBits.SS, OperandSize = 1
                }
            },
            {
                Utils.Operands.OP_TIMER_RUN_BIT,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_TIMER_RUN_BIT, CommandCodeForRead = CommandCode.ReadBits.RT,
                    OperandSize = 1
                }
            },
            {
                Utils.Operands.OP_COUNTER_RUN_BIT,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_COUNTER_RUN_BIT, CommandCodeForRead = CommandCode.ReadBits.RM,
                    OperandSize = 1
                }
            },
            {
                Utils.Operands.OP_MI,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_MI, CommandCodeForRead = CommandCode.ReadIntegers.RW,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SW, OperandSize = 4
                }
            },
            {
                Utils.Operands.OP_ML,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_ML, CommandCodeForRead = CommandCode.ReadIntegers.RNL,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SNL, OperandSize = 8
                }
            },
            {
                Utils.Operands.OP_DW,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_DW, CommandCodeForRead = CommandCode.ReadIntegers.RND,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SND, OperandSize = 8
                }
            },
            {
                Utils.Operands.OP_MF,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_MF, CommandCodeForRead = CommandCode.ReadIntegers.RNF,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SNF, OperandSize = 8
                }
            },
            {
                Utils.Operands.OP_SI,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_SI, CommandCodeForRead = CommandCode.ReadIntegers.GF,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SF, OperandSize = 4
                }
            },
            {
                Utils.Operands.OP_SL,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_SL, CommandCodeForRead = CommandCode.ReadIntegers.RNH,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SNH, OperandSize = 8
                }
            },
            {
                Utils.Operands.OP_SDW,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_SDW, CommandCodeForRead = CommandCode.ReadIntegers.RNJ,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SNJ, OperandSize = 8
                }
            },
            {
                Utils.Operands.OP_COUNTER_CURRENT,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_COUNTER_CURRENT, CommandCodeForRead = CommandCode.ReadIntegers.GX,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SK, OperandSize = 4
                }
            },
            {
                Utils.Operands.OP_COUNTER_PRESET,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_COUNTER_PRESET, CommandCodeForRead = CommandCode.ReadIntegers.GY,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SJ, OperandSize = 4
                }
            },
            {
                Utils.Operands.OP_TIMER_CURRENT,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_TIMER_CURRENT, CommandCodeForRead = CommandCode.ReadIntegers.GT,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SNK, OperandSize = 8
                }
            },
            {
                Utils.Operands.OP_TIMER_PRESET,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_TIMER_PRESET, CommandCodeForRead = CommandCode.ReadIntegers.GP,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SNT, OperandSize = 8
                }
            },
            {
                Utils.Operands.OP_XB,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_XB, CommandCodeForRead = CommandCode.ReadBits.RZB,
                    CommandCodeForWrite = CommandCode.SetBits.SZB, OperandSize = 1
                }
            },
            {
                Utils.Operands.OP_XI,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_XI, CommandCodeForRead = CommandCode.ReadIntegers.RZI,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SZI, OperandSize = 4
                }
            },
            {
                Utils.Operands.OP_XDW,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_XDW, CommandCodeForRead = CommandCode.ReadIntegers.RZD,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SZD, OperandSize = 8
                }
            },
            {
                Utils.Operands.OP_XL,
                new OperandType
                {
                    OperandName = Utils.Operands.OP_XL, CommandCodeForRead = CommandCode.ReadIntegers.RZL,
                    CommandCodeForWrite = CommandCode.WriteIntegers.SZL, OperandSize = 8
                }
            }
        };

        public static Dictionary<string, OperandType> FullBinaryOperandTypes = new Dictionary<string, OperandType>()
        {
            {
                Utils.Operands.OP_Undefined,
                new OperandType {OperandName = Utils.Operands.OP_Undefined, ByteValue = 0, OperandSize = 16}
            }, //TO BE FIX
            {
                Utils.Operands.OP_MB,
                new OperandType {OperandName = Utils.Operands.OP_MB, ByteValue = 1, OperandSize = 1}
            },
            {
                Utils.Operands.OP_SB,
                new OperandType {OperandName = Utils.Operands.OP_SB, ByteValue = 2, OperandSize = 1}
            },
            {
                Utils.Operands.OP_MI,
                new OperandType {OperandName = Utils.Operands.OP_MI, ByteValue = 3, OperandSize = 16}
            },
            {
                Utils.Operands.OP_SI,
                new OperandType {OperandName = Utils.Operands.OP_SI, ByteValue = 4, OperandSize = 16}
            },
            {
                Utils.Operands.OP_ML,
                new OperandType {OperandName = Utils.Operands.OP_ML, ByteValue = 5, OperandSize = 32}
            },
            {
                Utils.Operands.OP_SL,
                new OperandType {OperandName = Utils.Operands.OP_SL, ByteValue = 6, OperandSize = 32}
            },
            {
                Utils.Operands.OP_MF,
                new OperandType {OperandName = Utils.Operands.OP_MF, ByteValue = 7, OperandSize = 32}
            },
            {
                Utils.Operands.OP_SF,
                new OperandType {OperandName = Utils.Operands.OP_SF, ByteValue = 8, OperandSize = 32}
            },
            {
                Utils.Operands.OP_INPUT,
                new OperandType {OperandName = Utils.Operands.OP_INPUT, ByteValue = 9, OperandSize = 1}
            },
            {
                Utils.Operands.OP_OUTPUT,
                new OperandType {OperandName = Utils.Operands.OP_OUTPUT, ByteValue = 10, OperandSize = 1}
            },
            {
                Utils.Operands.OP_TIMER_RUN_BIT,
                new OperandType {OperandName = Utils.Operands.OP_TIMER_RUN_BIT, ByteValue = 11, OperandSize = 1}
            },
            {
                Utils.Operands.OP_COUNTER_RUN_BIT,
                new OperandType {OperandName = Utils.Operands.OP_COUNTER_RUN_BIT, ByteValue = 12, OperandSize = 1}
            },
            {
                Utils.Operands.OP_CONST_FLOAT,
                new OperandType {OperandName = Utils.Operands.OP_CONST_FLOAT, ByteValue = 15, OperandSize = 32}
            },
            {
                Utils.Operands.OP_DW,
                new OperandType {OperandName = Utils.Operands.OP_DW, ByteValue = 16, OperandSize = 32}
            },
            {
                Utils.Operands.OP_HOUR,
                new OperandType {OperandName = Utils.Operands.OP_HOUR, ByteValue = 19, OperandSize = 16}
            }, //TO BE FIX
            {
                Utils.Operands.OP_DAY_OF_THE_WEEK,
                new OperandType {OperandName = Utils.Operands.OP_DAY_OF_THE_WEEK, ByteValue = 20, OperandSize = 16}
            }, //TO BE FIX
            {
                Utils.Operands.OP_DAY_OF_THE_MONTH,
                new OperandType {OperandName = Utils.Operands.OP_DAY_OF_THE_MONTH, ByteValue = 21, OperandSize = 16}
            }, //TO BE FIX
            {
                Utils.Operands.OP_MONTH,
                new OperandType {OperandName = Utils.Operands.OP_MONTH, ByteValue = 22, OperandSize = 16}
            }, //TO BE FIX
            {
                Utils.Operands.OP_YEAR,
                new OperandType {OperandName = Utils.Operands.OP_YEAR, ByteValue = 23, OperandSize = 16}
            }, //TO BE FIX
            {
                Utils.Operands.OP_LadderBit,
                new OperandType {OperandName = Utils.Operands.OP_LadderBit, ByteValue = 24, OperandSize = 1}
            },
            {
                Utils.Operands.OP_LadderInt,
                new OperandType {OperandName = Utils.Operands.OP_LadderInt, ByteValue = 25, OperandSize = 16}
            },
            {
                Utils.Operands.OP_NetworkSystemBit,
                new OperandType {OperandName = Utils.Operands.OP_NetworkSystemBit, ByteValue = 26, OperandSize = 1}
            },
            {
                Utils.Operands.OP_NetworkInput,
                new OperandType {OperandName = Utils.Operands.OP_NetworkInput, ByteValue = 27, OperandSize = 1}
            },
            {
                Utils.Operands.OP_NetworkSystemInt,
                new OperandType {OperandName = Utils.Operands.OP_NetworkSystemInt, ByteValue = 28, OperandSize = 16}
            },
            {
                Utils.Operands.OP_JumpToLabel,
                new OperandType {OperandName = Utils.Operands.OP_JumpToLabel, ByteValue = 29, OperandSize = 16}
            }, //TO BE FIX
            {
                Utils.Operands.OP_JumpToHmiPage,
                new OperandType {OperandName = Utils.Operands.OP_JumpToHmiPage, ByteValue = 30, OperandSize = 16}
            }, //TO BE FIX
            {
                Utils.Operands.OP_CallSub,
                new OperandType {OperandName = Utils.Operands.OP_CallSub, ByteValue = 31, OperandSize = 16}
            }, //TO BE FIX
            {
                Utils.Operands.OP_Return,
                new OperandType {OperandName = Utils.Operands.OP_Return, ByteValue = 32, OperandSize = 16}
            }, //TO BE FIX
            {
                Utils.Operands.OP_ConstSigned,
                new OperandType {OperandName = Utils.Operands.OP_ConstSigned, ByteValue = 34, OperandSize = 16}
            }, //TO BE FIX
            {
                Utils.Operands.OP_ConstUnsigned,
                new OperandType {OperandName = Utils.Operands.OP_ConstUnsigned, ByteValue = 35, OperandSize = 16}
            }, //TO BE FIX
            {
                Utils.Operands.OP_SDW,
                new OperandType {OperandName = Utils.Operands.OP_SDW, ByteValue = 36, OperandSize = 32}
            },
            {
                Utils.Operands.OP_DirectSignedConst,
                new OperandType {OperandName = Utils.Operands.OP_DirectSignedConst, ByteValue = 37, OperandSize = 16}
            }, //TO BE FIX
            {
                Utils.Operands.OP_DirectUnsignedConst,
                new OperandType {OperandName = Utils.Operands.OP_DirectUnsignedConst, ByteValue = 38, OperandSize = 16}
            }, //TO BE FIX
            {
                Utils.Operands.OP_TIMER_PRESET,
                new OperandType {OperandName = Utils.Operands.OP_TIMER_PRESET, ByteValue = 128, OperandSize = 32}
            },
            {
                Utils.Operands.OP_UTIL,
                new OperandType {OperandName = Utils.Operands.OP_UTIL, ByteValue = 40, OperandSize = 16}
            },
            {
                Utils.Operands.OP_Function_Block,
                new OperandType {OperandName = Utils.Operands.OP_Function_Block, ByteValue = 41, OperandSize = 16}
            }, //TO BE FIX
            {
                Utils.Operands.OP_Function_Block_Ex,
                new OperandType {OperandName = Utils.Operands.OP_Function_Block_Ex, ByteValue = 42, OperandSize = 16}
            }, //TO BE FIX
            {
                Utils.Operands.OP_UtilExtended,
                new OperandType {OperandName = Utils.Operands.OP_UtilExtended, ByteValue = 43, OperandSize = 16}
            }, //TO BE FIX
            {
                Utils.Operands.OP_TIMER_CURRENT,
                new OperandType {OperandName = Utils.Operands.OP_TIMER_CURRENT, ByteValue = 129, OperandSize = 32}
            },
            {
                Utils.Operands.OP_COUNTER_CURRENT,
                new OperandType {OperandName = Utils.Operands.OP_COUNTER_CURRENT, ByteValue = 145, OperandSize = 16}
            },
            {
                Utils.Operands.OP_COUNTER_PRESET,
                new OperandType {OperandName = Utils.Operands.OP_COUNTER_PRESET, ByteValue = 144, OperandSize = 16}
            },
            {
                Utils.Operands.OP_RloUtil,
                new OperandType {OperandName = Utils.Operands.OP_RloUtil, ByteValue = 60, OperandSize = 16}
            }, //TO BE FIX
            {
                Utils.Operands.OP_XB,
                new OperandType {OperandName = Utils.Operands.OP_XB, ByteValue = 64, OperandSize = 1}
            },
            {
                Utils.Operands.OP_XI,
                new OperandType {OperandName = Utils.Operands.OP_XI, ByteValue = 65, OperandSize = 16}
            },
            {
                Utils.Operands.OP_XL,
                new OperandType {OperandName = Utils.Operands.OP_XL, ByteValue = 66, OperandSize = 32}
            },
            {
                Utils.Operands.OP_XDW,
                new OperandType {OperandName = Utils.Operands.OP_XDW, ByteValue = 67, OperandSize = 32}
            }
        };

        #endregion

        #region Extension Methods

        #region Full Binary Mix

        internal static void GetMesageLengthInBytesForFullBinary(ref List<ReadWriteRequest> values,
            out UInt16 sendMessageSize, out UInt16 receiveMessageSize)
        {
            sendMessageSize = 0;
            receiveMessageSize = 0;

            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] is WriteOperands)
                {
                    sendMessageSize +=
                        Convert.ToUInt16(
                            (values[i] as WriteOperands).OperandType.GetOperandSizeByOperandTypeForFullBinarry() *
                            (values[i] as WriteOperands).NumberOfOperands);
                }
                else
                {
                    receiveMessageSize +=
                        Convert.ToUInt16(
                            (values[i] as ReadOperands).OperandType.GetOperandSizeByOperandTypeForFullBinarry() *
                            (values[i] as ReadOperands).NumberOfOperands);
                    receiveMessageSize += 4;
                }

                sendMessageSize += 4;
            }
        }

        internal static byte GetOperandSizeByValueForFullBinary(this byte byteValue)
        {
            var ret = Utils.FullBinaryOperandTypes.Single(dr => (dr.Value.ByteValue == byteValue));
            return ret.Value.OperandSize;
        }

        internal static byte GetOperandIdByNameForFullBinary(this string name)
        {
            var ret = Utils.FullBinaryOperandTypes.Single(dr => dr.Value.OperandName.Equals(name));
            return ret.Value.ByteValue;
        }

        //internal static byte GetOperandSizeByCommandCodeForFullBinary(this string name)
        //{
        //    var ret = Utils.FullBinaryOperandTypes.Single(dr => (dr.Value.CommandCodeForRead.Equals(name)) ||
        //        (dr.Value.CommandCodeForWrite.Equals(name)));
        //    return ret.Value.OperandSize;
        //}

        internal static byte GetOperandSizeByOperandTypeForFullBinarry(this OperandTypes operandType)
        {
            string name = Enum.GetName(typeof(OperandTypes), operandType);
            var ret = Utils.FullBinaryOperandTypes.Single(dr => dr.Value.OperandName.Equals(name));
            return ret.Value.OperandSize <= 8 ? (byte) 1 : (byte) (ret.Value.OperandSize / 8);
        }

        //internal static OperandType GetOperandTypeByNameForFullBinary(this string name)
        //{
        //    if (Utils.FullBinaryOperandTypes.ContainsKey(name))
        //        return Utils.OperandTypes[name];

        //    return null;
        //}

        public static object z_GetTimeUnits(this int dateInt)
        {
            UInt16 ms = (UInt16) (dateInt % 100);
            dateInt = dateInt / 100;
            UInt16 hours = (UInt16) (dateInt / 3600);
            dateInt = dateInt % 3600;
            UInt16 min = (UInt16) (dateInt / 60);
            UInt16 sec = (UInt16) (dateInt % 60);

            List<UInt16> timeInfo = new List<ushort>();
            timeInfo.AddRange(new UInt16[4] {hours, min, sec, ms});
            return timeInfo as object;
        }

        public static UInt32 z_GetSecondsValue(List<UInt16> timeValue)
        {
            UInt32 result = 0;

            result = (UInt32) (timeValue[0] * 3600);
            result += (UInt32) (timeValue[1] * 60);
            result += (UInt32) (timeValue[2]);
            result *= 100;
            result += (UInt32) (timeValue[3]);

            return result;
        }

        //internal static string GetOperandTypeNameByCommandCodeForFullBinary(this string name)
        //{
        //    KeyValuePair<string, OperandType> ret = new KeyValuePair<string, OperandType>();
        //    try
        //    {
        //        ret = Utils.FullBinaryOperandTypes.Single(dr => (dr.Value.CommandCodeForRead == name) || (dr.Value.CommandCodeForWrite == name));
        //    }
        //    catch (InvalidOperationException)
        //    {
        //        return string.Empty;
        //    }
        //    return ret.Value.OperandName;
        //}

        internal static string GetOperandNameByValueForFullBinary(this byte value)
        {
            var ret = Utils.FullBinaryOperandTypes.Single(dr => dr.Value.ByteValue.Equals(value));
            return ret.Value.OperandName;
        }

        #endregion

        #region Partial Binary Mix

        internal static byte GetOperandSizeByValue(this byte byteValue)
        {
            var ret = Utils.OperandTypesDictionary.Single(dr =>
                (dr.Value.ByteValue == byteValue) || (dr.Value.VectorialValue == byteValue));
            return ret.Value.OperandSize;
        }

        internal static byte GetOperandSizeByCommandCode(this string name)
        {
            var ret = Utils.ASCIIOperandTypes.Single(dr => (dr.Value.OperandName.Equals(name)));
            //(dr.Value.CommandCodeForWrite.Equals(name))
            return ret.Value.OperandSize;
        }

        internal static byte GetOperandIdByName(this string name)
        {
            var ret = Utils.OperandTypesDictionary.Single(dr => dr.Value.OperandName.Equals(name));
            return ret.Value.ByteValue;
        }

        internal static OperandType GetOperandTypeByName(this string name)
        {
            if (Utils.OperandTypesDictionary.ContainsKey(name))
                return Utils.OperandTypesDictionary[name];

            return null;
        }

        internal static string GetOperandTypeNameByCommandCode(this string name)
        {
            KeyValuePair<string, OperandType> ret = new KeyValuePair<string, OperandType>();
            try
            {
                ret = Utils.ASCIIOperandTypes.Single(dr =>
                    (dr.Value.CommandCodeForRead == name) || (dr.Value.CommandCodeForWrite == name));
            }
            catch (InvalidOperationException)
            {
                return string.Empty;
            }

            return ret.Value.OperandName;
        }

        internal static string GetOperandNameByValue(this byte value)
        {
            var ret = Utils.OperandTypesDictionary.Single(dr =>
                dr.Value.ByteValue.Equals(value) || (dr.Value.VectorialValue == value));
            return ret.Value.OperandName;
        }

        #endregion

        #endregion

        internal static class ComDriverExceptionMessages
        {
            internal static string CannotCommuniteWithPLC =
                "Cannot communicate with the PLC with the specified UnitID!";
        }

        public static class Operands
        {
            /// <summary>
            /// Undefined
            /// </summary>
            public const String OP_Undefined = "Undefined";

            /// <summary>
            /// Memory Bits
            /// </summary>
            public const String OP_MB = "MB";

            /// <summary>
            /// Sistem Bits
            /// </summary>
            public const String OP_SB = "SB";

            /// <summary>
            /// Memory Integers
            /// </summary>
            public const String OP_MI = "MI";

            /// <summary>
            /// Sistem Integers
            /// </summary>
            public const String OP_SI = "SI";

            /// <summary>
            /// Memory Longs
            /// </summary>
            public const String OP_ML = "ML";

            /// <summary>
            /// System Longs
            /// </summary>
            public const String OP_SL = "SL";

            /// <summary>
            /// Memory Floats
            /// </summary>
            public const String OP_MF = "MF";

            /// <summary>
            /// System Floats
            /// </summary>
            public const String OP_SF = "SF";

            /// <summary>
            /// Intput
            /// </summary>
            public const String OP_INPUT = "Input";

            /// <summary>
            /// Output
            /// </summary>
            public const String OP_OUTPUT = "Output";

            /// <summary>
            /// TimerRunBit
            /// </summary>
            public const String OP_TIMER_RUN_BIT = "TimerRunBit";

            /// <summary>
            /// CounterRunBit
            /// </summary>
            public const String OP_COUNTER_RUN_BIT = "CounterRunBit";

            ///// <summary>
            ///// Timer Scan Bits
            ///// </summary>
            //public const String OP_TIMER_RUN_BIT = "TimerScanBits";

            ///// <summary>
            ///// Counter Scan Bits
            ///// </summary>
            //public const String OP_COUNTER_RUN_BIT = "CounterScanBits";

            /// <summary>
            /// Memory Double Word
            /// </summary>
            public const String OP_DW = "DW";

            /// <summary>
            /// System Double Word
            /// </summary>
            public const String OP_SDW = "SDW";

            /// <summary>
            /// CounterCurrent
            /// </summary>
            public const String OP_COUNTER_CURRENT = "CounterCurrent";

            /// <summary>
            /// CounterPreset
            /// </summary>
            public const String OP_COUNTER_PRESET = "CounterPreset";

            /// <summary>
            /// TimerCurrent
            /// </summary>
            public const String OP_TIMER_CURRENT = "TimerCurrent";

            /// <summary>
            /// TimePreset
            /// </summary>
            public const String OP_TIMER_PRESET = "TimerPreset";

            /// <summary>
            /// Const Float
            /// </summary>
            public const String OP_CONST_FLOAT = "ConstFloat";

            /// <summary>
            /// Hour
            /// </summary>
            public const String OP_HOUR = "Hour";

            /// <summary>
            /// Day of The Week
            /// </summary>
            public const String OP_DAY_OF_THE_WEEK = "DayOfTheWeek";

            /// <summary>
            /// Day of The Month
            /// </summary>
            public const String OP_DAY_OF_THE_MONTH = "DayOfTheMonth";

            /// <summary>
            /// Month
            /// </summary>
            public const String OP_MONTH = "Month";

            /// <summary>
            /// Year
            /// </summary>
            public const String OP_YEAR = "Year";

            /// <summary>
            /// Ladder Bit
            /// </summary>
            public const String OP_LadderBit = "LadderBit";

            /// <summary>
            /// Ladder Int
            /// </summary>
            public const String OP_LadderInt = "LadderInt";

            /// <summary>
            /// Network System Bit
            /// </summary>
            public const String OP_NetworkSystemBit = "NetworkSystemBit";

            /// <summary>
            /// Network Input
            /// </summary>
            public const String OP_NetworkInput = "NetworkInput";

            /// <summary>
            /// Network System Int
            /// </summary>
            public const String OP_NetworkSystemInt = "NetworkSystemInt";

            /// <summary>
            /// Jump To Label
            /// </summary>
            public const String OP_JumpToLabel = "JumpToLabel";

            /// <summary>
            /// Jump To HMI Page
            /// </summary>
            public const String OP_JumpToHmiPage = "JumpToHmiPage";

            /// <summary>
            /// Call Sub
            /// </summary>
            public const String OP_CallSub = "CallSub";

            /// <summary>
            /// Return
            /// </summary>
            public const String OP_Return = "Return";

            /// <summary>
            /// Const Signed
            /// </summary>
            public const String OP_ConstSigned = "ConstSigned";

            /// <summary>
            /// Const Unsigned
            /// </summary>
            public const String OP_ConstUnsigned = "ConstUnsigned";


            /// <summary>
            /// System DW
            /// </summary>
            public const String OP_SystemDW = "SystemDW";

            /// <summary>
            /// Direct Signed Const
            /// </summary>
            public const String OP_DirectSignedConst = "DirectSignedConst";

            /// <summary>
            /// Direct Unsigned Const
            /// </summary>
            public const String OP_DirectUnsignedConst = "DirectUnsignedConst";

            /// <summary>
            /// Util
            /// </summary>
            public const String OP_UTIL = "Util";

            /// <summary>
            /// Function Block
            /// </summary>
            public const String OP_Function_Block = "FunctionBlock";

            /// <summary>
            /// Function Block Ex
            /// </summary>
            public const String OP_Function_Block_Ex = "FunctionBlockEx";

            /// <summary>
            /// Util Extended
            /// </summary>
            public const String OP_UtilExtended = "UtilExtended";

            /// <summary>
            /// Rlo Util
            /// </summary>
            public const String OP_RloUtil = "RloUtil";

            /// <summary>
            /// Fast Bit
            /// </summary>
            public const String OP_XB = "XB";

            /// <summary>
            /// Fast Int
            /// </summary>
            public const String OP_XI = "XI";

            /// <summary>
            /// Fast Long
            /// </summary>
            public const String OP_XL = "XL";

            /// <summary>
            /// Fast DW
            /// </summary>
            public const String OP_XDW = "XDW";
        }

        #region Lengths

        internal static class Lengths
        {
            internal const int LENGTH_STX = 1;
            internal const int LENGTH_STX1 = 2;
            internal const int LENGTH_COMMAND_CODE = 2;
            internal const int LENGTH_CRC = 2;
            internal const int LENGTH_UNIT_ID = 2;
            internal const int LENGTH_ADDRESS = 4;
            internal const int LENGTH_LENGTH = 2;
            internal const int LENGTH_HEADER = 24;
            internal const int LENGTH_FOOTER = 3;
            internal const int LENGTH_ETX = 1;

            internal const int LENGTH_ASCII_RECEIVE_MESSAGE =
                LENGTH_STX1 + LENGTH_UNIT_ID + LENGTH_COMMAND_CODE + LENGTH_ETX + LENGTH_CRC;

            internal const int LENGTH_ASCII_SEND_MESSAGE =
                LENGTH_STX + LENGTH_UNIT_ID + LENGTH_COMMAND_CODE + LENGTH_ETX + LENGTH_CRC;

            internal const int LENGTH_HEADER_AND_FOOTER = LENGTH_HEADER + LENGTH_FOOTER;
            internal const int LENGTH_WRITE_DATA_TABLE_DETAILS = 32;
            internal const int LENGTH_TCP_HEADER = 6;
            internal const int LENGTH_DETAIL_AREA_HEADER = 4;
            internal const ushort LENGTH_WR_DETAILS = 4;
        }

        internal static class OperandTypesLength
        {
            //Byte value
            internal const int LENGTH_MB = 1; //1
            internal const int LENGTH_SB = 1; //2
            internal const int LENGTH_MI = 16; //3
            internal const int LENGTH_SI = 16; //4
            internal const int LENGTH_XI = 16; //4
            internal const int LENGTH_ML = 32; //5
            internal const int LENGTH_SL = 32; //6
            internal const int LENGTH_MF = 32; //7
            internal const int LENGTH_SF = 32; //8
            internal const int LENGTH_INPUT = 1; //9
            internal const int LENGTH_OUTPUT = 1; //10
            internal const int LENGTH_TIMER_RUN_BIT = 1; //11
            internal const int LENGTH_COUNTER_RUN_BIT = 1; //12
            internal const int LENGTH_DW = 32; //16
            internal const int LENGTH_SDW = 32; //17
            internal const int LENGTH_XDW = 32;
            internal const int LENGTH_COUNTER_CURRENT = 16; //18
            internal const int LENGTH_COUNTER_PRESET = 16; //19
            internal const int LENGTH_TIMER_CURRENT = 32; //20
            internal const int LENGTH_TIMER_PRESET = 32; //21
        }

        #endregion

        internal static class General
        {
            internal const String ID_COMMAND_CODE = "ID";
            internal const String RESET_COMMAND_CODE = "CCE";
            internal const String INIT_COMMAND_CODE = "CCI";
            internal const String STOP_COMMAND_CODE = "CCS";
            internal const String RUN_COMMAND_CODE = "CCR";
            internal const String SET_RTC_CODE = "SC";
            internal const String GET_RTC_CODE = "RC";
            internal const String GET_ID_COMMAND_CODE = "UG";
            internal const String SET_ID_COMMAND_CODE = "US";
            internal const String STX = "/";
            internal const String STX1 = "/A";
            internal const String ETX = "\u000D";
            internal const byte ASCII_PROTOCOL = 101;
            internal const byte BINARY_PROTOCOL = 102;
            internal const int GET_PLC_NAME = 12;
        }

        #region HexEncoding

        public static class HexEncoding
        {
            #region Internal

            internal static Single ConvertBytesToSingle(byte[] bytes)
            {
                string hex = GetHex(bytes);
                return ConvertHexToSingle(hex);
            }

            internal static string GetHexTwoCharsPerByte(byte[] bytes)
            {
                StringBuilder hexString = new StringBuilder();

                for (int i = bytes.Length - 1; i >= 0; i--)
                {
                    hexString.Append(bytes[i].ToString("X").PadLeft(2, '0'));
                }

                return hexString.ToString();
            }

            /// <summary>
            /// Determines if given string is in proper hexadecimal string format
            /// </summary>
            /// <param name="hexString"></param>
            /// <returns></returns>
            //public static bool InHexFormat(string hexString)
            //{
            //    bool hexFormat = true;

            //    foreach (char digit in hexString)
            //    {
            //        if (!IsHexDigit(digit))
            //        {
            //            hexFormat = false;
            //            break;
            //        }
            //    }
            //    return hexFormat;
            //}
            internal static Single ConvertHexToSingle(string hexVal)
            {
                try
                {
                    int i = 0, j = 0;
                    byte[] bArray = new byte[4];

                    for (i = 0; i <= hexVal.Length - 1; i += 2)
                    {
                        bArray[j] = Byte.Parse(hexVal[i].ToString() + hexVal[i + 1].ToString(),
                            System.Globalization.NumberStyles.HexNumber);
                        j += 1;
                    }

                    Array.Reverse(bArray);
                    Single s = BitConverter.ToSingle(bArray, 0);

                    return (s);
                }
                catch (Exception ex)
                {
                    throw new FormatException(
                        "The supplied hex value is either empty or in an incorrect format.  Use the " +
                        "following format: 00000000", ex);
                }
            }

            internal static string ConvertSingleToHex(Single number)
            {
                uint tmpNum = BitConverter.ToUInt32(BitConverter.GetBytes(number), 0);
                byte[] byteArray = BitConverter.GetBytes(tmpNum);
                return GetHex(byteArray);
            }

            /// <summary>
            /// Creates a byte array from the hexadecimal string. Each two characters are combined
            /// to create one byte. First two hexadecimal characters become first byte in returned array.
            /// Non-hexadecimal characters are ignored. 
            /// </summary>
            /// <param name="hexString">string to convert to byte array</param>
            /// <param name="discarded">number of characters in string ignored</param>
            /// <returns>byte array, in the same left-to-right order as the hexString</returns>
            //public static byte[] GetBytes(string hexString, out int discarded)
            //{
            //    discarded = 0;
            //    string newString = "";
            //    char c;
            //    // remove all none A-F, 0-9, characters
            //    for (int i = 0; i < hexString.Length; i++)
            //    {
            //        c = hexString[i];
            //        if (IsHexDigit(c))
            //            newString += c;
            //        else
            //            discarded++;
            //    }
            //    // if odd number of characters, discard last character
            //    if (newString.Length % 2 != 0)
            //    {
            //        discarded++;
            //        newString = newString.Substring(0, newString.Length - 1);
            //    }

            //    int byteLength = newString.Length / 2;
            //    byte[] bytes = new byte[byteLength];
            //    string hex;
            //    int j = 0;
            //    for (int i = 0; i < bytes.Length; i++)
            //    {
            //        hex = new String(new Char[] { newString[j], newString[j + 1] });
            //        bytes[i] = HexToByte(hex);
            //        j = j + 2;
            //    }
            //    return bytes;
            //}

            #endregion

            #region Private

            /// <summary>
            /// Converts 1 or 2 character string into equivalant byte value
            /// </summary>
            /// <param name="hex">1 or 2 character string</param>
            /// <returns>byte</returns>
            private static byte HexToByte(string hex)
            {
                if (hex.Length > 2 || hex.Length <= 0)
                    throw new ArgumentException("hex must be 1 or 2 characters in length");
                byte newByte = byte.Parse(hex, System.Globalization.NumberStyles.HexNumber);
                return newByte;
            }

            /// <summary>
            /// Returns true is c is a hexadecimal digit (A-F, a-f, 0-9)
            /// </summary>
            /// <param name="c">Character to test</param>
            /// <returns>true if hex digit, false if not</returns>
            private static bool IsHexDigit(Char c)
            {
                int numChar;
                int numA = Convert.ToInt32('A');
                int num1 = Convert.ToInt32('0');
                c = Char.ToUpper(c);
                numChar = Convert.ToInt32(c);
                if (numChar >= numA && numChar < (numA + 6))
                    return true;
                if (numChar >= num1 && numChar < (num1 + 10))
                    return true;
                return false;
            }

            private static string GetHex(byte[] bytes)
            {
                StringBuilder hexString = new StringBuilder();

                for (int i = bytes.Length - 1; i >= 0; i--)
                {
                    hexString.Append(bytes[i].ToString("X").PadLeft(2, '0'));
                }

                return hexString.ToString();
            }

            //private static int GetByteCount(string hexString)
            //{
            //    int numHexChars = 0;
            //    char c;
            //    // remove all none A-F, 0-9, characters
            //    for (int i = 0; i < hexString.Length; i++)
            //    {
            //        c = hexString[i];
            //        if (IsHexDigit(c))
            //            numHexChars++;
            //    }
            //    // if odd number of characters, discard last character
            //    if (numHexChars % 2 != 0)
            //    {
            //        numHexChars--;
            //    }
            //    return numHexChars / 2; // 2 characters per byte
            //}

            #endregion
        }

        #endregion

        #region BaseConversionMethods

        public static string DecimalToHex(int iDec)
        {
            string strBin = "";
            strBin = iDec.ToString("X").PadLeft(2, '0');
            return strBin;
        }

        public static string DecimalToBase(int iDec, int numbase)
        {
            // Do not use this function for CRC calculation. 
            // It is long, lots of commands and doen't pad with 0's

            const int base10 = 10;
            char[] cHexa = new char[] {'A', 'B', 'C', 'D', 'E', 'F'};

            string strBin = "";
            int[] result = new int[32];
            int MaxBit = 32;
            for (; iDec > 0; iDec /= numbase)
            {
                int rem = iDec % numbase;
                result[--MaxBit] = rem;
            }

            for (int i = 0; i < result.Length; i++)
                if ((int) result.GetValue(i) >= base10)
                    strBin += cHexa[(int) result.GetValue(i) % base10];
                else
                    strBin += result.GetValue(i);
            strBin = strBin.TrimStart(new char[] {'0'});
            return strBin;
        }

        #endregion

        #region Compute Command with CRC

        private static string GetCommandWithCRCAndETX(string command)
        {
            string cmd = command;

            int sum = 0;
            for (int i = 1; i < cmd.Length; i++)
            {
                sum += Convert.ToInt32(Convert.ToChar(cmd[i]));
            }

            string CRC = DecimalToHex(sum % 256);
            cmd += CRC + General.ETX;

            return cmd;
        }

        internal static string GetIDCommand(int unitId)
        {
            string cmd = General.STX;
            cmd += unitId.ToString("X").PadLeft(2, '0');
            cmd += General.ID_COMMAND_CODE;
            return GetCommandWithCRCAndETX(cmd);
        }

        internal static string ResetCommand(int unitId)
        {
            string cmd = General.STX;
            cmd += unitId.ToString("X").PadLeft(2, '0');
            cmd += General.RESET_COMMAND_CODE;
            return GetCommandWithCRCAndETX(cmd);
        }

        internal static string InitCommand(int unitId)
        {
            string cmd = General.STX;
            cmd += unitId.ToString("X").PadLeft(2, '0');
            cmd += General.INIT_COMMAND_CODE;
            return GetCommandWithCRCAndETX(cmd);
        }

        internal static string StopCommand(int unitId)
        {
            string cmd = General.STX;
            cmd += unitId.ToString("X").PadLeft(2, '0');
            cmd += General.STOP_COMMAND_CODE;
            return GetCommandWithCRCAndETX(cmd);
        }

        internal static string RunCommand(int unitId)
        {
            string cmd = General.STX;
            cmd += unitId.ToString("X").PadLeft(2, '0');
            cmd += General.RUN_COMMAND_CODE;
            return GetCommandWithCRCAndETX(cmd);
        }

        internal static string SetRtcCommand(int unitId, DateTime dateTime)
        {
            string seconds = dateTime.Second.ToString().PadLeft(2, '0');
            string minutes = dateTime.Minute.ToString().PadLeft(2, '0');
            string hours = dateTime.Hour.ToString().PadLeft(2, '0');
            string day = dateTime.Day.ToString().PadLeft(2, '0');
            string month = dateTime.Month.ToString().PadLeft(2, '0');
            string year = dateTime.Year.ToString().PadLeft(2, '0');
            if (year.Length == 4)
                year = year.Substring(2);
            string dayOfWeek = (((int) dateTime.DayOfWeek) + 1).ToString().PadLeft(2, '0');

            string cmd = General.STX;
            cmd += unitId.ToString("X").PadLeft(2, '0');
            cmd += General.SET_RTC_CODE;
            cmd += seconds;
            cmd += minutes;
            cmd += hours;
            cmd += dayOfWeek;
            cmd += day;
            cmd += month;
            cmd += year;

            return GetCommandWithCRCAndETX(cmd);
        }

        internal static string GetRtcCommand(int unitId)
        {
            string cmd = General.STX;
            cmd += unitId.ToString("X").PadLeft(2, '0');
            cmd += General.GET_RTC_CODE;
            return GetCommandWithCRCAndETX(cmd);
        }

        internal static string GetSetNewUnitIdCommand(int oldUnitId, int newUnitId)
        {
            string cmd = General.STX;
            cmd += oldUnitId.ToString("X").PadLeft(2, '0');
            cmd += CommandCode.SetUnitId.US;
            cmd += newUnitId.ToString("X").PadLeft(2, '0');

            return GetCommandWithCRCAndETX(cmd);
        }

        internal static string GetUnitIdCommand(int unitId)
        {
            string cmd = General.STX;
            cmd += unitId.ToString("X").PadLeft(2, '0');
            cmd += CommandCode.GetUnitId.UG;
            return GetCommandWithCRCAndETX(cmd);
        }


        internal static string GetGenericCommand(int unitId, string command)
        {
            string cmd = General.STX;
            cmd += unitId.ToString("X").PadLeft(2, '0');
            cmd += command;
            return GetCommandWithCRCAndETX(cmd);
        }

        internal static UInt16 calcCheckSum(ref byte[] binData, int start, int end)
        {
            int sum = 0;
            for (int i = start; i <= end; i++)
            {
                sum += binData[i];
            }

            UInt16 checkSum = (UInt16) (~(sum % 0x10000) + 1);
            return checkSum;
            //return BitConverter.GetBytes((UInt16)checkSum);
        }

        internal static DateTime getDateTime(string response)
        {
            response = response.Substring(4, 16);
            string rtcPattern = "^RC(\\d{2})(\\d{2})(\\d{2})(\\d{2})(\\d{2})(\\d{2})(\\d{2})$";
            if (Regex.IsMatch(response, rtcPattern))
            {
                string[] splits = Regex.Split(response, rtcPattern);
                int seconds = Convert.ToInt32(splits[1]);
                int minutes = Convert.ToInt32(splits[2]);
                int hours = Convert.ToInt32(splits[3]);
                int dayOfWeek = Convert.ToInt32(splits[4]);
                int day = Convert.ToInt32(splits[5]);
                int month = Convert.ToInt32(splits[6]);
                int year = Convert.ToInt32(splits[7]) + 2000;

                return new DateTime(year, month, day, hours, minutes, seconds);
            }

            return DateTime.Now;
        }

        internal static int getInitID(string response)
        {
            response = response.Substring(4, 4);
            string getUnitIdPattern = "^UG([A-F|a-f|0-9]{2})$";
            if (Regex.IsMatch(response, getUnitIdPattern))
            {
                string[] splits = Regex.Split(response, getUnitIdPattern);
                return Convert.ToInt32(splits[1], 16);
            }

            return 0;
        }

        internal static Command.PComB getPlcName(int unitId)
        {
            Command.PComB pComB = new Command.PComB();
            pComB.BuildBinaryCommand((byte) unitId, 0, General.GET_PLC_NAME,
                0, 0, 0, 0, new byte[0]);

            return pComB;
        }

        #endregion

        internal static class HelperPlcVersion
        {
            internal const string xmlFromVersion = "FromVersion";
            internal const string xmlToVersion = "ToVersion";
            internal const string xmlExecuter = "OperandsExecuter";
            internal const string xmlSupportedExecuters = "Executers";
            internal const string xmlBufferSize = "BufferSize";
            internal const string xmlOpMemoryMapID = "OpMemoryMapID";
            internal const string xmlOperandsMemoryMap = "OperandsMemoryMap";
            internal const string xmlMapID = "MapID";

            internal const string OldOrNewPLCPattern = "^ID(.{4})(.)(\\d)(\\d{2})(\\d{2})$";

            internal const string NewPLCPattern =
                "^ID(.{4})(.)(.{3})(.{3})(.{2})B(.{3})(.{3})(.{2})P(.{3})(.{3})(.{2})F(.)(.)(.{2}).{2}(.{2})(FT(.{5})(.{5}))?$";

            internal const string OldPLCPattern =
                "^ID(.{6})(.)(.{3})(.{3})(.{2})B(.{3})(.{3})(.{2})P(.{3})(.{3})(.{2})F(.)(.)(.{2}).{2}(.{2})(FT(.{5})(.{5}))?$";
        }

        internal static class HelperComDriverLogger
        {
            #region Members

            internal static string ComDriverLogsTableName = "ComDriverLogs";

            internal static string AccessDbConnStringProvider =
                "Provider=Microsoft.Jet.OLEDB.4.0;OLE DB Services=-1;Data Source=";

            internal static string AccessDbConnStringDbPath = "ComDriverLogs.mdb";

            #endregion

            #region Methods

            internal static string GetLoggerCurrentRetry(int currentRetry, int totalRetries)
            {
                return currentRetry.ToString() + " of " + totalRetries.ToString();
            }

            internal static string GetLoggerChannel(Channel channel)
            {
                if (channel is Serial)
                {
                    Serial serial = channel as Serial;
                    return Channels.Serial.ToString() + "-" + serial.PortName.ToString();
                }
                else if (channel is Ethernet)
                {
                    Ethernet ethernet = channel as Ethernet;
                    return Channels.Ethernet + "-" + ethernet.RemoteIP + ":" + ethernet.RemotePort.ToString();
                }
                else if (channel is EthernetListener)
                {
                    EthernetListener listener = channel as EthernetListener;
                    return Channels.EthernetListener + "-" + "Local Port: " + listener.LocalPort.ToString();
                }
                else if (channel is ListenerServer)
                {
                    ListenerServer listenerServer = channel as ListenerServer;
                    return Channels.ListenerServer + "-" + "Local Port: " + listenerServer.LocalPort.ToString();
                }
                else if (channel is ListenerClient)
                {
                    ListenerClient listenerClient = channel as ListenerClient;
                    return Channels.ListenerClient + "-" + "Local Port: " + listenerClient.LocalPort.ToString();
                }
                else
                {
                    return "";
                }
            }

            internal static string GetAccessDateTime(DateTime dateTime)
            {
                return dateTime.Month.ToString() + "/" + dateTime.Day.ToString() + "/" +
                       dateTime.Year.ToString() + " " + dateTime.Hour.ToString() + ":" +
                       dateTime.Minute.ToString() + ":" + dateTime.Second.ToString() + "." +
                       dateTime.Millisecond.ToString();
            }

            #endregion
        }
    }

    #endregion

    #region Delegates

    public delegate void ReadWriteOperandsDelegate(ref ReadWriteRequest[] values, bool suppressEthernetHeader);

    internal delegate void ReceiveBytesDelegate(byte[] responseBytes, CommunicationException communicationException,
        GuidClass messageGuid);

    internal delegate void ReceiveStringDelegate(string responseString, CommunicationException communicationException,
        GuidClass messageGuid);

    // A delegate type for hooking up change notifications.
    public delegate void ChangedEventHandler(object sender, EventArgs e);

    #endregion

    #region Config

    internal static class Config
    {
        internal static int Retry
        {
            get
            {
                int retry;
                retry = Convert.ToInt32(ConfigurationManager.AppSettings["Retry"]);

                // If AppSettings doesn't contain the key 'Retry' then return defaul value.
                if (retry == 0)
                    retry = 3;
                return retry;
            }
        }

        internal static int TimeOut
        {
            get
            {
                int timeOut;
                timeOut = Convert.ToInt32(ConfigurationManager.AppSettings["TimeOut"]);

                // If AppSettings doesn't contain the key 'TimeOut' then return defaul value.
                if (timeOut == 0)
                    timeOut = 1000;
                return timeOut;
            }
        }
    }

    #endregion
}