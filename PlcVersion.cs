using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Unitronics.ComDriver.Messages;
using System.Resources;
using System.Runtime.Serialization;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Configuration;
using Unitronics.ComDriver.Resources;

namespace Unitronics.ComDriver
{
    public class PlcVersion
    {
        #region Locals

        string m_oplcModel;
        string m_hwVersion;
        string m_osVersion;
        string m_osBuildNum;
        string m_Boot;
        string m_FactoryBoot;
        string m_BinLib;
        int m_BinLibType1 = 0;
        int m_BinLibType2 = 0;
        bool _suppressEthernetHeader = false;

        OperandsExecuterType m_OperandsExecuterType;

        string plcReply;
        int m_plcBuffer;
        private int m_sendBufferSize;
        private int m_receiveBufferSize;
        private Dictionary<OperandTypes, int> m_OperandsCount;
        internal SupportedExecuterTypes SupportedExecuters;

        #endregion

        #region Constructors

        public PlcVersion()
        {
        }

        public PlcVersion(string versionString)
        {
            InitializeMembers();
            SupportedExecuters = new SupportedExecuterTypes(0);
            ParseVersion(versionString);
            m_receiveBufferSize = m_plcBuffer;
            m_sendBufferSize = m_plcBuffer;
        }

        #endregion

        #region Properties

        public OperandsExecuterType z_OperandsExecuterType
        {
            get { return m_OperandsExecuterType; }
            set { m_OperandsExecuterType = value; }
        }

        public int PlcBuffer
        {
            get { return m_plcBuffer; }
        }

        public string OPLCModel
        {
            get { return m_oplcModel; }
            set { m_oplcModel = value; }
        }

        public string HWVersion
        {
            get { return m_hwVersion; }
            set { m_hwVersion = value; }
        }

        public string OSVersion
        {
            get { return m_osVersion; }
            set { m_osVersion = value; }
        }

        public string OSBuildNum
        {
            get { return m_osBuildNum; }
            set { m_osBuildNum = value; }
        }

        public bool SupportsBinaryProtocol
        {
            get { return SupportedExecuters[ExecutersType.Binary]; }
        }


        // DO NOT USE
        internal int SendBufferSize
        {
            get { return m_sendBufferSize; }
            set
            {
                if (PlcBuffer >= value)
                {
                    m_sendBufferSize = value;
                }
                else
                {
                    throw new ComDriveExceptions("BufferSizeForSend cannot be greater than the plc buffer!",
                        ComDriveExceptions.ComDriveException.UnexpectedError);
                }
            }
        }

        // DO NOT USE
        internal int ReceiveBufferSize
        {
            get { return m_receiveBufferSize; }
            set
            {
                if (PlcBuffer >= value)
                {
                    m_receiveBufferSize = value;
                }
                else
                {
                    throw new ComDriveExceptions("BufferSizeForReceive cannot be greater than the plc buffer!",
                        ComDriveExceptions.ComDriveException.UnexpectedError);
                }
            }
        }

        internal void SetBufferSize(int bufferSize)
        {
            m_plcBuffer = bufferSize;
            m_receiveBufferSize = bufferSize;
            m_sendBufferSize = bufferSize;
        }

        public string Boot
        {
            get { return m_Boot; }
            set { m_Boot = value; }
        }

        public string FactoryBoot
        {
            get { return m_FactoryBoot; }
            set { m_FactoryBoot = value; }
        }

        public string BinLib
        {
            get { return m_BinLib; }
            set { m_BinLib = value; }
        }

        public int BinLibType1
        {
            get { return m_BinLibType1; }
            set { m_BinLibType1 = value; }
        }

        public int BinLibType2
        {
            get { return m_BinLibType2; }
            set { m_BinLibType2 = value; }
        }

        public bool SuppressEthernetHeader
        {
            get { return _suppressEthernetHeader; }
            internal set { _suppressEthernetHeader = value; }
        }

        // Returns the number of operans from a specific operand type.
        // The max openrad address is obviously Operand Count - 1 (Since the address starts with 0)
        internal int OperandCount(OperandTypes operandType)
        {
            return m_OperandsCount[operandType];
        }


        internal struct SupportedExecuterTypes
        {
            public SupportedExecuterTypes(int initialBitValue)
            {
                bits = initialBitValue;
            }

            public bool this[ExecutersType index]
            {
                get { return (bits & (1 << (int) index)) != 0; }
                set
                {
                    if (value) // turn the bit on if value is true; otherwise, turn it off
                        bits |= (byte) (1 << (int) index);
                    else
                        bits &= (byte) (~(1 << (int) index));
                }
            }

            private int bits;
        }

        #endregion

        #region Private

        private void ParseVersion(string versionString)
        {
            if (versionString == String.Empty || versionString == null)
                return;

            string response = versionString.Substring(4, versionString.Length - 7);
            if (!Regex.IsMatch(response, Utils.HelperPlcVersion.OldOrNewPLCPattern) &&
                (!Regex.IsMatch(response, Utils.HelperPlcVersion.OldPLCPattern)) &&
                (!Regex.IsMatch(response, Utils.HelperPlcVersion.NewPLCPattern)))
            {
                response = response.Replace('?', '0');
            }

            //Old or NewOS PLCs
            if (Regex.IsMatch(response, Utils.HelperPlcVersion.OldOrNewPLCPattern))
            {
                string[] splits = Regex.Split(response, Utils.HelperPlcVersion.OldOrNewPLCPattern);
                SetPLCVersions(splits);
                return;
            }

            //OLD OS PLCs
            if (Regex.IsMatch(response, Utils.HelperPlcVersion.OldPLCPattern))
            {
                string[] splits = Regex.Split(response, Utils.HelperPlcVersion.OldPLCPattern);
                SetPLCVersions(splits);
                return;
            }

            //NEW OS PLCs
            if (Regex.IsMatch(response, Utils.HelperPlcVersion.NewPLCPattern))
            {
                string[] splits = Regex.Split(response, Utils.HelperPlcVersion.NewPLCPattern);
                SetPLCVersions(splits);
                return;
            }
        }

        private void SetPLCVersions(string[] splits)
        {
            plcReply = splits[1];
            m_hwVersion = splits[2];
            if (splits.Length >= 15)
            {
                m_osVersion = RemoveZerosFromString(splits[3]) + "." + RemoveZerosFromString(splits[4]);
                m_osBuildNum = splits[5];
                SetPlcType();
                SetBootFactoryBootBinLib(splits);
                if (splits.Length >= 19)
                {
                    try
                    {
                        m_BinLibType1 = int.Parse(splits[17]);
                    }
                    catch
                    {
                    }

                    try
                    {
                        m_BinLibType1 = int.Parse(splits[18]);
                    }
                    catch
                    {
                    }
                }
            }
            else
            {
                m_osVersion = splits[3] + "." + splits[4];
                m_osBuildNum = splits[5];
                SetPlcType();
            }
        }

        private void SetBootFactoryBootBinLib(string[] splits)
        {
            m_Boot = RemoveZerosFromString(splits[6]) + "." + RemoveZerosFromString(splits[7]) + " (" + splits[8] + ")";
            m_FactoryBoot = RemoveZerosFromString(splits[9]) + "." + RemoveZerosFromString(splits[10]) + " (" +
                            splits[11] + ")";
            m_BinLib = RemoveZerosFromString(splits[12]) + "-" + RemoveZerosFromString(splits[13]) + "." +
                       RemoveZerosFromString(splits[14]) + " (" + splits[15] + ")";
        }

        private void SetPlcType()
        {
            string path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase
                .Replace(@"file:///", ""));
            string dirSeparator = Path.DirectorySeparatorChar.ToString();
            if (path.Substring(path.Length - 1) == dirSeparator)
                path = path.Substring(0, path.Length - 1);
            path += dirSeparator + "PLCModels.xml";

            XmlDocument xmlDoc = new XmlDocument();


            if (!PLCFactory.WorkWithXmlInternally)
            {
                // If the PLCModels.xml file doesn't exist 
                // create it from resource.
                if (!File.Exists(path))
                {
                    try
                    {
                        File.WriteAllText(path, ComDriverResource.PLCModels);
                    }
                    catch
                    {
                        throw new ComDriveExceptions("Error creating file 'PLCModels.xml'",
                            ComDriveExceptions.ComDriveException.CannotCreateFile);
                    }
                }
                else
                {
                    // Check if the existing XML version should be replaced with a new one.
                    int versionFromExistingFile = getXmlVersionFromFile(path);
                    int versionInComDrive = getXmlVersionForFileInComDrive();

                    if (versionInComDrive > versionFromExistingFile)
                    {
                        // If the XML Version in the Com Drive is newer than overwrite the XML file
                        try
                        {
                            File.WriteAllText(path, ComDriverResource.PLCModels);
                        }
                        catch
                        {
                            throw new ComDriveExceptions("Error creating file 'PLCModels.xml'",
                                ComDriveExceptions.ComDriveException.CannotCreateFile);
                        }
                    }
                }

                // Load the PLCModels.xml from local drive.
                xmlDoc.Load(path);
            }
            else
            {
                MemoryStream ms = new MemoryStream(ASCIIEncoding.ASCII.GetBytes(ComDriverResource.PLCModels));
                xmlDoc.Load(ms);
            }

            XmlNodeList nodeList = xmlDoc.GetElementsByTagName("PLC");
            IEnumerator enumerator = nodeList.GetEnumerator();
            while (enumerator.MoveNext())
            {
                XmlElement xmlElem = (XmlElement) enumerator.Current;
                if (xmlElem.HasAttribute("Reply") && xmlElem.HasAttribute("Model"))
                {
                    bool isRegex = false;
                    if (xmlElem.HasAttribute("ExpressionType") && xmlElem.Attributes["ExpressionType"].Value == "Regex")
                    {
                        isRegex = true;
                    }

                    if (xmlElem.Attributes["Reply"].Value.ToString() == plcReply ||
                        (isRegex && Regex.IsMatch(plcReply, xmlElem.Attributes["Reply"].Value)))
                    {
                        if (isRegex)
                        {
                            var match = Regex.Match(plcReply, xmlElem.Attributes["Reply"].Value).Groups[1].Value.Trim();
                            m_oplcModel = xmlElem.Attributes["Model"].Value.Replace("*", match);
                        }
                        else
                        {
                            m_oplcModel = xmlElem.Attributes["Model"].Value.ToString();
                        }

                        if (xmlElem.HasAttribute("SuppressEthernetHeader"))
                        {
                            bool.TryParse(xmlElem.Attributes["SuppressEthernetHeader"].Value,
                                out _suppressEthernetHeader);
                        }

                        if (xmlElem.HasChildNodes)
                        {
                            XmlNodeList childs = xmlElem.ChildNodes;
                            IEnumerator childsEnumerator = childs.GetEnumerator();

                            while (childsEnumerator.MoveNext())
                            {
                                XmlElement childElem = (XmlElement) childsEnumerator.Current;
                                if (childElem.Name.Equals("OS") && ValidateOSXMLElement(childElem))
                                {
                                    SetPLCDetailsFromOSXMLElement(childElem);
                                }
                                else
                                {
                                    if (childElem.Name.Equals("PreBoot") || childElem.Name.Equals("Boot"))
                                    {
                                        SetPLCExecuterType(Convert.ToInt32(childElem
                                            .Attributes[Utils.HelperPlcVersion.xmlExecuter].Value));
                                        if (childElem.HasAttribute(Utils.HelperPlcVersion.xmlOpMemoryMapID))
                                        {
                                            setOperandsCount(Convert.ToInt32(childElem
                                                .Attributes[Utils.HelperPlcVersion.xmlOpMemoryMapID].Value));
                                        }

                                        m_plcBuffer = Convert.ToInt32(childElem
                                            .Attributes[Utils.HelperPlcVersion.xmlBufferSize].Value);
                                        SupportedExecuters = new SupportedExecuterTypes(
                                            Convert.ToInt32(childElem
                                                .Attributes[Utils.HelperPlcVersion.xmlSupportedExecuters].Value));
                                    }
                                }
                            }
                        }

                        return;
                    }
                }
            }
        }

        private int getXmlVersionFromFile(string path)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(path);
            return getXmlVersionFromXmlDocument(xmlDoc);
        }

        private int getXmlVersionForFileInComDrive()
        {
            XmlDocument xmlDoc = new XmlDocument();
            MemoryStream ms = new MemoryStream(ASCIIEncoding.ASCII.GetBytes(ComDriverResource.PLCModels));
            xmlDoc.Load(ms);
            return getXmlVersionFromXmlDocument(xmlDoc);
        }

        private int getXmlVersionFromXmlDocument(XmlDocument xmlDoc)
        {
            XmlNodeList nodeList = xmlDoc.GetElementsByTagName("PLCs");
            IEnumerator enumerator = nodeList.GetEnumerator();

            if (enumerator.MoveNext())
            {
                XmlElement xmlElem = (XmlElement) enumerator.Current;
                if (xmlElem.HasAttribute("Version"))
                {
                    string versionAsString = xmlElem.Attributes["Version"].Value.ToString();
                    if (Regex.IsMatch(versionAsString, "^(\\d+)\\.(\\d+)\\.(\\d+)\\.(\\d+)$"))
                    {
                        string[] splits = Regex.Split(versionAsString, "^(\\d+)\\.(\\d+)\\.(\\d+)\\.(\\d+)$");
                        int version = int.Parse(splits[4]) + int.Parse(splits[3]) * 100 +
                                      int.Parse(splits[2]) * 10000 + int.Parse(splits[1]) * 100000000;
                        return version;
                    }
                    else
                    {
                        return 0;
                    }
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                return 0;
            }
        }

        private void SetPLCDetailsFromOSXMLElement(XmlElement childElem)
        {
            string[] xmlVersionString;
            string[] responseOSVersion;
            int xmlWeightedOSFromVersion;
            int xmlWeightedOSToVersion;
            int plcWeightedOSVersion;
            string fromVersion = "";
            string toVersion = "";

            // The code had the From='Value' To='Value' missing
            // I'm just offering a code which shorter by assuming that there cannot be a version before 0.0
            // and that there cannot be a version after 999.999
            // Weighted Version is calculated as Major*1000 + Minor

            fromVersion = childElem.Attributes[Utils.HelperPlcVersion.xmlFromVersion].Value.ToString();
            toVersion = childElem.Attributes[Utils.HelperPlcVersion.xmlToVersion].Value.ToString();

            responseOSVersion = m_osVersion.Replace('?', '0').Split('.');
            plcWeightedOSVersion = Convert.ToInt32(responseOSVersion[0]) * 1000 +
                                   Convert.ToInt32(responseOSVersion[1]);

            if (fromVersion == "*")
            {
                xmlWeightedOSFromVersion = 0;
            }
            else
            {
                xmlVersionString = fromVersion.Split('.');
                xmlWeightedOSFromVersion = Convert.ToInt32(xmlVersionString[0]) * 1000 +
                                           Convert.ToInt32(xmlVersionString[1]);
            }

            if (toVersion == "*")
            {
                xmlWeightedOSToVersion = 999999;
            }
            else
            {
                xmlVersionString = toVersion.Split('.');
                xmlWeightedOSToVersion = Convert.ToInt32(xmlVersionString[0]) * 1000 +
                                         Convert.ToInt32(xmlVersionString[1]);
            }

            if ((plcWeightedOSVersion >= xmlWeightedOSFromVersion) &&
                (plcWeightedOSVersion <= xmlWeightedOSToVersion))
            {
                SetPLCExecuterType(Convert.ToInt32(childElem.Attributes[Utils.HelperPlcVersion.xmlExecuter].Value));
                setOperandsCount(Convert.ToInt32(childElem.Attributes[Utils.HelperPlcVersion.xmlOpMemoryMapID].Value));
                m_plcBuffer = Convert.ToInt32(childElem.Attributes[Utils.HelperPlcVersion.xmlBufferSize].Value);
                SupportedExecuters = new SupportedExecuterTypes(
                    Convert.ToInt32(childElem.Attributes[Utils.HelperPlcVersion.xmlSupportedExecuters].Value));
            }
        }

        private bool ValidateOSXMLElement(XmlElement childElem)
        {
            if (childElem.HasAttribute(Utils.HelperPlcVersion.xmlFromVersion) &&
                childElem.HasAttribute(Utils.HelperPlcVersion.xmlToVersion) &&
                childElem.HasAttribute(Utils.HelperPlcVersion.xmlExecuter) &&
                childElem.HasAttribute(Utils.HelperPlcVersion.xmlBufferSize) &&
                childElem.HasAttribute(Utils.HelperPlcVersion.xmlSupportedExecuters))
                return true;
            else
                return false;
        }

        private void SetPLCExecuterType(int executerNo)
        {
            switch (executerNo)
            {
                case 1:
                    m_OperandsExecuterType = OperandsExecuterType.ExecuterAscii;
                    break;
                case 3:
                    m_OperandsExecuterType = OperandsExecuterType.ExecuterFullBinaryMix;
                    break;
                case 2:
                    m_OperandsExecuterType = OperandsExecuterType.ExecuterPartialBinaryMix;
                    break;
                default:
                    throw new ComDriveExceptions("Invalid PLC Executer No!",
                        ComDriveExceptions.ComDriveException.UnexpectedError);
            }
        }

        private void setOperandsCount(int opMemoryMapID)
        {
            string path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase
                .Replace(@"file:///", ""));
            string dirSeparator = Path.DirectorySeparatorChar.ToString();
            if (path.Substring(path.Length - 1) == dirSeparator)
                path = path.Substring(0, path.Length - 1);
            path += dirSeparator + "PLCModels.xml";

            XmlDocument xmlDoc = new XmlDocument();

            if (!PLCFactory.WorkWithXmlInternally)
            {
                xmlDoc.Load(path);
            }
            else
            {
                MemoryStream ms = new MemoryStream(ASCIIEncoding.ASCII.GetBytes(ComDriverResource.PLCModels));
                xmlDoc.Load(ms);
            }

            XmlNodeList operandsMemoryMap = xmlDoc.GetElementsByTagName(Utils.HelperPlcVersion.xmlOperandsMemoryMap);

            foreach (XmlElement memoryMap in operandsMemoryMap)
            {
                if (Convert.ToInt16(memoryMap.Attributes["MapID"].Value) == opMemoryMapID)
                {
                    XmlNodeList operands = memoryMap.GetElementsByTagName("Operand");
                    foreach (XmlElement operand in operands)
                    {
                        string operandType = operand.Attributes["Type"].Value;
                        int count = Convert.ToInt32(operand.Attributes["Count"].Value);

                        switch (operandType)
                        {
                            case "MB":
                                m_OperandsCount[OperandTypes.MB] = count;
                                break;
                            case "SB":
                                m_OperandsCount[OperandTypes.SB] = count;
                                break;
                            case "MI":
                                m_OperandsCount[OperandTypes.MI] = count;
                                break;
                            case "SI":
                                m_OperandsCount[OperandTypes.SI] = count;
                                break;
                            case "ML":
                                m_OperandsCount[OperandTypes.ML] = count;
                                break;
                            case "SL":
                                m_OperandsCount[OperandTypes.SL] = count;
                                break;
                            case "MF":
                                m_OperandsCount[OperandTypes.MF] = count;
                                break;
                            case "Input":
                                m_OperandsCount[OperandTypes.Input] = count;
                                break;
                            case "Output":
                                m_OperandsCount[OperandTypes.Output] = count;
                                break;
                            case "Timer":
                                m_OperandsCount[OperandTypes.TimerCurrent] = count;
                                m_OperandsCount[OperandTypes.TimerPreset] = count;
                                m_OperandsCount[OperandTypes.TimerRunBit] = count;
                                break;
                            case "Counter":
                                m_OperandsCount[OperandTypes.CounterCurrent] = count;
                                m_OperandsCount[OperandTypes.CounterPreset] = count;
                                m_OperandsCount[OperandTypes.CounterRunBit] = count;
                                break;
                            case "DW":
                                m_OperandsCount[OperandTypes.DW] = count;
                                break;
                            case "SDW":
                                m_OperandsCount[OperandTypes.SDW] = count;
                                break;
                            case "XB":
                                m_OperandsCount[OperandTypes.XB] = count;
                                break;
                            case "XI":
                                m_OperandsCount[OperandTypes.XI] = count;
                                break;
                            case "XL":
                                m_OperandsCount[OperandTypes.XL] = count;
                                break;
                            case "XDW":
                                m_OperandsCount[OperandTypes.XDW] = count;
                                break;
                            default:
                                System.Diagnostics.Debug.Assert(false);
                                break;
                        }
                    }

                    return;
                }
            }

            throw new ComDriveExceptions(
                "Could not find a matching operands memory map for the PLC. Possibly an outdated XML file",
                ComDriveExceptions.ComDriveException.UnexpectedError);
        }

        private string RemoveZerosFromString(string str)
        {
            bool noZerosFlag = false;
            do
            {
                noZerosFlag = false;
                if (str.IndexOf("0") == 0 && str.Length > 1)
                {
                    str = str.Remove(0, 1);
                    noZerosFlag = true;
                }
            } while (noZerosFlag);

            return str;
        }

        private void InitializeMembers()
        {
            plcReply = String.Empty;
            m_Boot = String.Empty;
            m_FactoryBoot = String.Empty;
            m_BinLib = String.Empty;
            m_hwVersion = String.Empty;
            m_OperandsExecuterType = OperandsExecuterType.ExecuterAscii;
            initOperandsCount();
        }

        private void initOperandsCount()
        {
            m_OperandsCount = new Dictionary<OperandTypes, int>();
            m_OperandsCount.Add(OperandTypes.CounterCurrent, 0);
            m_OperandsCount.Add(OperandTypes.CounterPreset, 0);
            m_OperandsCount.Add(OperandTypes.CounterRunBit, 0);
            m_OperandsCount.Add(OperandTypes.DW, 0);
            m_OperandsCount.Add(OperandTypes.Input, 0);
            m_OperandsCount.Add(OperandTypes.MB, 0);
            m_OperandsCount.Add(OperandTypes.MF, 0);
            m_OperandsCount.Add(OperandTypes.MI, 0);
            m_OperandsCount.Add(OperandTypes.ML, 0);
            m_OperandsCount.Add(OperandTypes.Output, 0);
            m_OperandsCount.Add(OperandTypes.SB, 0);
            m_OperandsCount.Add(OperandTypes.SDW, 0);
            m_OperandsCount.Add(OperandTypes.SI, 0);
            m_OperandsCount.Add(OperandTypes.SL, 0);
            m_OperandsCount.Add(OperandTypes.TimerCurrent, 0);
            m_OperandsCount.Add(OperandTypes.TimerPreset, 0);
            m_OperandsCount.Add(OperandTypes.TimerRunBit, 0);
            m_OperandsCount.Add(OperandTypes.XB, 0);
            m_OperandsCount.Add(OperandTypes.XDW, 0);
            m_OperandsCount.Add(OperandTypes.XI, 0);
            m_OperandsCount.Add(OperandTypes.XL, 0);
        }

        #endregion
    }
}