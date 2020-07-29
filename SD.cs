using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver.Messages.DataRequest;
using System.Text.RegularExpressions;
using System.IO;

namespace Unitronics.ComDriver
{
    public class SD
    {
        #region public/internal Classes and Strucs

        internal struct SdParams
        {
            public en_FileAccessMode FileAccessMode;
            public ushort SeekOrigin;
            public int SeekOffset;
            public ushort BufferSize;
            public bool LastReadWriteChunk;
            public string Guid;
            public string FileName;
        }

        public enum SdFolder
        {
            RootFolder = -1,
            ALARMS = 0,
            DT = 1,
            DT_DT1 = 100,
            DT_DT2 = 101,
            DT_DT3 = 102,
            DT_DT4 = 103,
            LOG = 3,
            SYSTEM = 4,
            USER_APP = 5,
            TRENDS = 6,
            TRENDS_TRENDS1 = 600,
            TRENDS_TRENDS2 = 601,
            TRENDS_TRENDS3 = 602,
            TRENDS_TRENDS4 = 603,
            SDBLOCKS = 9,
            EXCEL = 10,
            EXCEL_EXCEL1 = 1000,
            EXCEL_EXCEL2 = 1001,
            EXCEL_EXCEL3 = 1002,
            EXCEL_EXCEL4 = 1003,
            WEB = 11,
        }

        internal enum NameType
        {
            FileNameNoExtension,
            FileNameWithExtension,
            ExtensionOnly,
            Path
        }

        public class Folder : File
        {
            private List<Folder> m_Folders;
            private List<File> m_Files;

            public Folder()
            {
                m_Folders = new List<Folder>();
                m_Files = new List<File>();
            }

            public List<Folder> Folders
            {
                get { return m_Folders; }
                internal set { m_Folders = value; }
            }

            public List<File> Files
            {
                get { return m_Files; }
                internal set { m_Files = value; }
            }
        }

        public class File
        {
            private string m_Name;
            private uint m_Size;
            private DateTime m_CreationDate;
            private DateTime m_LastModifiedDate;
            private bool m_ReadOnly;

            public File()
            {
                m_Name = "";
                m_Size = 0;
                m_CreationDate = new DateTime(1980, 1, 1);
                m_LastModifiedDate = new DateTime(1980, 1, 1);
                m_ReadOnly = false;
            }

            public DateTime DateCreated
            {
                get { return m_CreationDate; }
                internal set { m_CreationDate = value; }
            }

            public DateTime DateModified
            {
                get { return m_LastModifiedDate; }
                internal set { m_LastModifiedDate = value; }
            }

            public uint Size
            {
                get { return m_Size; }
                internal set { m_Size = value; }
            }

            public string Name
            {
                get { return m_Name; }
                internal set { m_Name = value; }
            }

            public bool ReadOnly
            {
                get { return m_ReadOnly; }
                internal set { m_ReadOnly = value; }
            }
        }

        #endregion

        #region locals

        private PLC m_Plc;
        private PlcVersion m_Version;

        bool m_BreakFlag = false;
        private int m_BreakFlagCount;
        private Object objectLocker = new Object();

        private static char[] forbiddenChars = new char[]
        {
            '"', '*', '/', ':', '<', '>',
            '?', '\\', '|', ' ', '\x0000', '\x0001', '\x0002', '\x0003', '\x0004', '\x0005', '\x0006', '\x0007',
            '\x0008', '\x0009', '\x000a', '\x000b', '\x000c', '\x000d', '\x000e', '\x000f', '\x0010', '\x0011',
            '\x0012', '\x0013', '\x0014', '\x0015', '\x0016', '\x0017', '\x0018', '\x0019', '\x001a', '\x001b',
            '\x001c', '\x001e', '\x001e', '\x001f', '\x007f', '.'
        };

        internal bool BreakFlag
        {
            get { return m_BreakFlag; }
            set { m_BreakFlag = value; }
        }

        internal int BreakFlagCount
        {
            get { return m_BreakFlagCount; }
            set
            {
                m_BreakFlagCount = value;
                if (m_BreakFlagCount <= 0)
                {
                    m_BreakFlagCount = 0;
                    m_BreakFlag = false;
                }
            }
        }

        #endregion

        #region enums

        /// <summary>
        /// "r"     Open a file for reading. The file must exist.
        /// "r+"    Open a file for reading and writing. The file must exist.
        /// "w"     Create an empty file for writing. If a file with the same name already exists its content is erased.
        /// "w+"    Create an empty file for writing and reading. If a file with the same name already exists its content is erased before it is opened.
        /// "a"     Append to a file. Writing operations append data at the end of the file. The file is created if it doesn't exist.
        /// "a+"    Open a file for reading and appending. All writing operations are done at the end of the file protecting the previous
        ///             content from being overwritten.  You can reposition (fseek) the pointer to anywhere in the file for reading, but
        ///             writing operations will move back to the end of file.  The file is created if it doesn't exist.
        /// 
        /// </summary>
        internal enum en_FileAccessMode
        {
            R = 0,
            R_Plus = 1,
            W = 2,
            W_Plus = 3,
            A = 4,
            A_Plus = 5,
        }

        internal const int ACCESS_SD_COMMANDCODE = 0x2A;

        internal enum en_PComSubCommand
        {
            DirInit = 0x87,
            Dir = 0x1,
            ReadFileInit = 0x82,
            ReadFile = 0x2,
            WriteFileInit = 0x83,
            WriteFile = 0x3,
            WriteFileStatus = 0x4,
            CloseSdChannel = 0x5,
            DeleteFileInit = 0x86,
            DeleteFile = 0x6,
            DirGetStatus = 0x7,
            DirGetChunk = 0x8,
        }

        internal enum en_SdStatus
        {
            Error = 0xFF,
            Ack = 0x80,
            Busy = 0x40
        }

        internal enum en_SdErrorCode
        {
            UnknownError = 0,
            TriggerError = 1,
            SdDriverError = 2,
            BufferOverflow = 3,
            MsgKeyError = 4,
            SdLocked = 5,
            Conflict = 6,
            VersionMismatch = 7,
            PathLength = 8,
            FileOpenedByOtherClient = 9,

            NoError = 0xff,
        }

        #endregion

        internal PLC Plc
        {
            get { return m_Plc; }
        }

        internal SD(PLC plc, PlcVersion version)
        {
            // By making the constructor Internal, I prevent the user from creating an SD class
            m_Plc = plc;
            m_Version = version;
        }

        #region public methods

        /// <summary>
        /// Returns a Dir of all Folders and files in the SD (Dirs the Root)
        /// </summary>
        public Folder Dir(ProgressStatusChangedDelegate del)
        {
            bool sdChannelLockInitiated = false;
            string guid = "";
            lock (objectLocker)
            {
                m_BreakFlagCount++;
            }

            try
            {
                Folder folder = new Folder();
                folder.Name = "";
                dirAllSubFolders("", "*.*", folder, ref sdChannelLockInitiated, out guid, del);

                return folder;
            }
            finally
            {
                try
                {
                    if (sdChannelLockInitiated)
                    {
                        closeSdChannel(guid);
                    }
                }
                finally
                {
                    lock (objectLocker)
                    {
                        m_BreakFlagCount--;
                    }
                }
            }
        }

        /// <summary>
        /// Requests Dir Syncrhronously
        /// </summary>
        public Folder Dir(SdFolder sdFolder, string filesExtension, bool scanSubFolders,
            ProgressStatusChangedDelegate del)
        {
            string path = GetSdFolderAsString(sdFolder);
            bool sdChannelLockInitiated = false;
            string guid = "";
            lock (objectLocker)
            {
                m_BreakFlagCount++;
            }

            try
            {
                if (!scanSubFolders)
                {
                    return dir(path, filesExtension, ref sdChannelLockInitiated, out guid, del);
                }
                else
                {
                    string[] dirs = path.Split('\\');
                    Folder folder = new Folder();
                    folder.Name = dirs[dirs.Length - 1];
                    dirAllSubFolders(path, filesExtension, folder, ref sdChannelLockInitiated, out guid, del);

                    return folder;
                }
            }
            finally
            {
                try
                {
                    if (sdChannelLockInitiated)
                    {
                        closeSdChannel(guid);
                    }
                }
                finally
                {
                    lock (objectLocker)
                    {
                        m_BreakFlagCount--;
                    }
                }
            }
        }

        /// <summary>
        /// Requests Dir Syncrhronously
        /// </summary>
        public Folder Dir(string folderName, string filesExtension, bool scanSubFolders,
            ProgressStatusChangedDelegate del)
        {
            bool sdChannelLockInitiated = false;
            string guid = "";
            lock (objectLocker)
            {
                m_BreakFlagCount++;
            }

            try
            {
                if (!scanSubFolders)
                {
                    return dir(folderName, filesExtension, ref sdChannelLockInitiated, out guid, del);
                }
                else
                {
                    string[] dirs = folderName.Split('\\');
                    Folder folder = new Folder();
                    folder.Name = dirs[dirs.Length - 1];
                    dirAllSubFolders(folderName, filesExtension, folder, ref sdChannelLockInitiated, out guid, del);

                    return folder;
                }
            }
            finally
            {
                try
                {
                    if (sdChannelLockInitiated)
                    {
                        closeSdChannel(guid);
                    }
                }
                finally
                {
                    lock (objectLocker)
                    {
                        m_BreakFlagCount--;
                    }
                }
            }
        }

        /// <summary>
        /// Read File Syncrhronously
        /// </summary>
        public byte[] ReadFile(string folderName, string fileName, ProgressStatusChangedDelegate del)
        {
            bool sdChannelLockInitiated = false;
            string guid = "";
            lock (objectLocker)
            {
                m_BreakFlagCount++;
            }

            try
            {
                return readFile(folderName, fileName, ref sdChannelLockInitiated, out guid, del);
            }
            finally
            {
                try
                {
                    if (sdChannelLockInitiated)
                    {
                        closeSdChannel(guid);
                    }
                }
                finally
                {
                    lock (objectLocker)
                    {
                        m_BreakFlagCount--;
                    }
                }
            }
        }

        public void ReadFile(string sourceFolderName, string sourceFileName, string targetFolder, bool resumeRead,
            ProgressStatusChangedDelegate del)
        {
            bool sdChannelLockInitiated = false;
            string guid = "";
            lock (objectLocker)
            {
                m_BreakFlagCount++;
            }

            try
            {
                readFile(sourceFolderName, sourceFileName, targetFolder, resumeRead, ref sdChannelLockInitiated,
                    out guid, del);
            }
            finally
            {
                try
                {
                    if (sdChannelLockInitiated)
                    {
                        closeSdChannel(guid);
                    }
                }
                finally
                {
                    lock (objectLocker)
                    {
                        m_BreakFlagCount--;
                    }
                }
            }
        }

        /// <summary>
        /// Read File Syncrhronously
        /// </summary>
        public byte[] ReadFile(SdFolder sdFolder, string fileName, ProgressStatusChangedDelegate del)
        {
            string folderName = GetSdFolderAsString(sdFolder);
            bool sdChannelLockInitiated = false;
            string guid = "";
            lock (objectLocker)
            {
                m_BreakFlagCount++;
            }

            try
            {
                return readFile(folderName, fileName, ref sdChannelLockInitiated, out guid, del);
            }
            finally
            {
                try
                {
                    if (sdChannelLockInitiated)
                    {
                        closeSdChannel(guid);
                    }
                }
                finally
                {
                    lock (objectLocker)
                    {
                        m_BreakFlagCount--;
                    }
                }
            }
        }

        /// <summary>
        /// Write File Syncrhronously
        /// </summary>
        public void WriteFile(SdFolder sdFolder, string fileName, byte[] fileContent, ProgressStatusChangedDelegate del)
        {
            string folderName = GetSdFolderAsString(sdFolder);
            bool sdChannelLockInitiated = false;
            string guid = "";
            lock (objectLocker)
            {
                m_BreakFlagCount++;
            }

            try
            {
                writeFile(folderName, fileName, fileContent, ref sdChannelLockInitiated, out guid, del);
            }
            finally
            {
                lock (objectLocker)
                {
                    m_BreakFlagCount--;
                }

                if (sdChannelLockInitiated)
                {
                    closeSdChannel(guid);
                }
            }
        }

        /// <summary>
        /// Delete File Syncrhronously
        /// </summary>
        public void DeleteFile(string folderName, string fileName, ProgressStatusChangedDelegate del)
        {
            bool sdChannelLockInitiated = false;
            string guid = "";
            lock (objectLocker)
            {
                m_BreakFlagCount++;
            }

            try
            {
                deleteFile(folderName, fileName, ref sdChannelLockInitiated, out guid, del);
            }
            finally
            {
                try
                {
                    if (sdChannelLockInitiated)
                    {
                        closeSdChannel(guid);
                    }
                }
                finally
                {
                    lock (objectLocker)
                    {
                        m_BreakFlagCount--;
                    }
                }
            }
        }

        public void DeleteFile(SdFolder sdFolder, string fileName, ProgressStatusChangedDelegate del)
        {
            string folderName = GetSdFolderAsString(sdFolder);
            bool sdChannelLockInitiated = false;
            string guid = "";
            lock (objectLocker)
            {
                m_BreakFlagCount++;
            }

            try
            {
                deleteFile(folderName, fileName, ref sdChannelLockInitiated, out guid, del);
            }
            finally
            {
                try
                {
                    if (sdChannelLockInitiated)
                    {
                        closeSdChannel(guid);
                    }
                }
                finally
                {
                    lock (objectLocker)
                    {
                        m_BreakFlagCount--;
                    }
                }
            }
        }

        public static string GetSdFolderAsString(SdFolder sdFolder)
        {
            switch (sdFolder)
            {
                case SdFolder.ALARMS:
                    return "ALARMS";

                case SdFolder.DT:
                    return "DT";

                case SdFolder.DT_DT1:
                    return @"DT\DT1";

                case SdFolder.DT_DT2:
                    return @"DT\DT2";

                case SdFolder.DT_DT3:
                    return @"DT\DT3";

                case SdFolder.DT_DT4:
                    return @"DT\DT4";

                case SdFolder.LOG:
                    return "LOG";

                case SdFolder.EXCEL:
                    return "EXCEL";

                case SdFolder.EXCEL_EXCEL1:
                    return @"EXCEL\EXCEL1";

                case SdFolder.EXCEL_EXCEL2:
                    return @"EXCEL\EXCEL2";

                case SdFolder.EXCEL_EXCEL3:
                    return @"EXCEL\EXCEL3";

                case SdFolder.EXCEL_EXCEL4:
                    return @"EXCEL\EXCEL4";

                case SdFolder.SDBLOCKS:
                    return "SDBLOCKS";

                case SdFolder.SYSTEM:
                    return "SYSTEM";

                case SdFolder.TRENDS:
                    return "TRENDS";

                case SdFolder.TRENDS_TRENDS1:
                    return @"TRENDS\TRENDS1";

                case SdFolder.TRENDS_TRENDS2:
                    return @"TRENDS\TRENDS2";

                case SdFolder.TRENDS_TRENDS3:
                    return @"TRENDS\TRENDS3";

                case SdFolder.TRENDS_TRENDS4:
                    return @"TRENDS\TRENDS4";

                case SdFolder.USER_APP:
                    return "USER_APP";

                case SdFolder.WEB:
                    return "WEB";

                default:
                    return "";
            }
        }

        public static SdFolder GetSdFolderFromString(string path)
        {
            switch (path.ToUpper())
            {
                case "ALARMS":
                    return SdFolder.ALARMS;

                case "DT":
                    return SdFolder.DT;

                case @"DT\DT1":
                    return SdFolder.DT_DT1;

                case @"DT\DT2":
                    return SdFolder.DT_DT2;

                case @"DT\DT3":
                    return SdFolder.DT_DT3;

                case @"DT\DT4":
                    return SdFolder.DT_DT4;

                case "LOG":
                    return SdFolder.LOG;

                case "EXCEL":
                    return SdFolder.EXCEL;

                case @"EXCEL\EXCEL1":
                    return SdFolder.EXCEL_EXCEL1;

                case @"EXCEL\EXCEL2":
                    return SdFolder.EXCEL_EXCEL2;

                case @"EXCEL\EXCEL3":
                    return SdFolder.EXCEL_EXCEL3;

                case @"EXCEL\EXCEL4":
                    return SdFolder.EXCEL_EXCEL4;

                case @"SDBLOCKS":
                    return SdFolder.SDBLOCKS;

                case @"SYSTEM":
                    return SdFolder.SYSTEM;

                case @"TRENDS":
                    return SdFolder.TRENDS;

                case @"TRENDS\TRENDS1":
                    return SdFolder.TRENDS_TRENDS1;

                case @"TRENDS\TRENDS2":
                    return SdFolder.TRENDS_TRENDS2;

                case @"TRENDS\TRENDS3":
                    return SdFolder.TRENDS_TRENDS3;

                case @"TRENDS\TRENDS4":
                    return SdFolder.TRENDS_TRENDS4;

                case @"USER_APP":
                    return SdFolder.USER_APP;

                case @"WEB":
                    return SdFolder.WEB;

                default:
                    return SdFolder.RootFolder;
            }
        }

        #endregion


        #region private methods

        private void closeSdChannel(string guid)
        {
            en_SdStatus sdStatus;
            en_SdErrorCode sdErrorCode;
            PLC plc = PLCFactory.GetPLC(m_Plc.PLCChannel, m_Plc.UnitId);

            SdParams sdParams = getInitializedSdParamsObject();
            sdParams.Guid = guid;

            BinaryRequest br = new BinaryRequest()
            {
                CommandCode = ACCESS_SD_COMMANDCODE,
                SubCommand = (int) en_PComSubCommand.CloseSdChannel,
                Address = 0,
                ElementsCount = 0,
                MessageKey = 0,
                OutgoingBuffer = getSdParamsAsByteArray(sdParams),
                IsInternal = true,
            };

            ReadWriteRequest[] rw = new ReadWriteRequest[] {br};

            plc.ReadWrite(ref rw);
            br.MessageKey = (br.MessageKey + 1) % 256;

            sdStatus = (en_SdStatus) br.IncomingBuffer[0];
            sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];

            while ((sdStatus == en_SdStatus.Error) || (sdStatus == en_SdStatus.Busy))
            {
                if (sdStatus == en_SdStatus.Error)
                {
                    if (sdErrorCode != en_SdErrorCode.NoError)
                    {
                        throwSdException(sdErrorCode);
                    }
                }

                br.SubCommand = (int) en_PComSubCommand.CloseSdChannel;

                plc.ReadWrite(ref rw);
                br.MessageKey = (br.MessageKey + 1) % 256;

                sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];
            }
        }

        private void dirAllSubFolders(string path, string filesExtension, Folder folder,
            ref bool sdChannelLockInitiated, out string guid, ProgressStatusChangedDelegate del)
        {
            Folder folderContent = dir(path, filesExtension, ref sdChannelLockInitiated, out guid, del);

            foreach (File file in folderContent.Files)
            {
                folder.Files.Add(file);
            }

            foreach (Folder subFolder in folderContent.Folders)
            {
                folder.Folders.Add(subFolder);
                if (path != "")
                {
                    dirAllSubFolders(path + "\\" + subFolder.Name, filesExtension, subFolder,
                        ref sdChannelLockInitiated, out guid, del);
                }
                else
                {
                    dirAllSubFolders(subFolder.Name, filesExtension, subFolder, ref sdChannelLockInitiated, out guid,
                        del);
                }
            }
        }

        private Folder dir(string path, string filesExtension, ref bool sdChannelLockInitiated, out string guid,
            ProgressStatusChangedDelegate del)
        {
            ushort chunkSize = (ushort) m_Version.PlcBuffer;
            chunkSize = (ushort) (chunkSize - 20); // (Reduced size to prevent messages from not fitting into buffer)

            Folder result = new Folder();

            int messageKey = 0;

            // We don't care about chunk size bigger than 512 bytes since we read from Ram and not from SD.

            chunkSize -= (ushort) (chunkSize % 2); // Make the chunk size an even number

            string extension = filesExtension.Replace("*.", "");
            extension = extension.Replace(".", "");

            if (path.Length > 0)
            {
                if (path.Substring(path.Length - 1) == "\\")
                    path = path.Substring(0, path.Length - 1);
            }

            checkNameLegality(path, NameType.Path);
            checkNameLegality(extension, NameType.ExtensionOnly);

            SdParams sdParams = getInitializedSdParamsObject();
            guid = sdParams.Guid;

            if (path != "")
            {
                sdParams.FileName = path + @"\" + "*." + extension;
            }
            else
            {
                sdParams.FileName = "*." + extension;
            }

            sdParams.FileAccessMode = en_FileAccessMode.R;
            sdParams.BufferSize = 2048; // 2KB for 64 files max

            en_SdStatus sdStatus;
            en_SdErrorCode sdErrorCode;

            BinaryRequest br = new BinaryRequest()
            {
                CommandCode = ACCESS_SD_COMMANDCODE,
                SubCommand = (int) en_PComSubCommand.DirInit,
                MessageKey = messageKey,
                OutgoingBuffer = getSdParamsAsByteArray(sdParams),
                IsInternal = true,
            };

            ReadWriteRequest[] rw = new ReadWriteRequest[] {br};

            if (this.BreakFlag)
                throw new ComDriveExceptions("Request aborted by user",
                    ComDriveExceptions.ComDriveException.AbortedByUser);

            m_Plc.ReadWrite(ref rw);

            sdStatus = (en_SdStatus) br.IncomingBuffer[0];
            sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];

            RequestProgress requestProgress = new RequestProgress();
            requestProgress.Minimum = 0;
            requestProgress.Maximum = 100;
            requestProgress.NotificationType = RequestProgress.en_NotificationType.SetMinMax;
            requestProgress.Text = "";
            requestProgress.Value = 0;

            while ((sdStatus == en_SdStatus.Error) && (sdErrorCode == en_SdErrorCode.SdLocked))
            {
                // if Sd channel is locked then message key is not incremented
                requestProgress.NotificationType = RequestProgress.en_NotificationType.ProgressChanged;
                requestProgress.Text = "SD Card is Locked by another process... Please wait.";
                requestProgress.Value = 0;

                if (del != null)
                    del(requestProgress);

                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                m_Plc.ReadWrite(ref rw);

                sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];
            }

            br.MessageKey = (br.MessageKey + 1) % 256;
            messageKey = br.MessageKey;

            sdChannelLockInitiated = true;

            br.SubCommand = (int) en_PComSubCommand.DirGetStatus;

            if (this.BreakFlag)
                throw new ComDriveExceptions("Request aborted by user",
                    ComDriveExceptions.ComDriveException.AbortedByUser);

            m_Plc.ReadWrite(ref rw);
            br.MessageKey = (br.MessageKey + 1) % 256;
            messageKey = br.MessageKey;

            sdStatus = (en_SdStatus) br.IncomingBuffer[0];
            sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];

            while ((sdStatus == en_SdStatus.Error) || (sdStatus == en_SdStatus.Busy))
            {
                if (sdStatus == en_SdStatus.Error)
                {
                    if (sdErrorCode != en_SdErrorCode.NoError)
                    {
                        throwSdException(sdErrorCode);
                    }
                }

                br.SubCommand = (int) en_PComSubCommand.DirGetStatus;

                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                m_Plc.ReadWrite(ref rw);
                br.MessageKey = (br.MessageKey + 1) % 256;
                messageKey = br.MessageKey;

                sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];
            }

            int relDataSize = BitConverter.ToInt32(br.IncomingBuffer, 2);
            byte[] binData = new byte[relDataSize];
            int totalTransfered = 0;

            requestProgress.Minimum = 0;
            requestProgress.Maximum = relDataSize;
            requestProgress.NotificationType = RequestProgress.en_NotificationType.SetMinMax;
            requestProgress.Text = "Listing Files in Directory: " + sdParams.FileName;
            requestProgress.Value = 0;

            if (del != null)
                del(requestProgress);


            requestProgress.Value = 0;
            requestProgress.NotificationType = RequestProgress.en_NotificationType.ProgressChanged;

            if (del != null)
                del(requestProgress);

            while (totalTransfered < relDataSize)
            {
                sdParams.BufferSize = chunkSize;
                br.SubCommand = (int) en_PComSubCommand.DirGetChunk;

                if (sdParams.BufferSize + totalTransfered > relDataSize)
                {
                    sdParams.BufferSize = (ushort) (relDataSize - totalTransfered);
                }

                br.Address = totalTransfered;
                br.ChunkSizeAlignment = 2;
                br.ElementsCount = sdParams.BufferSize;

                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                m_Plc.ReadWrite(ref rw);
                br.MessageKey = (br.MessageKey + 1) % 256;
                messageKey = br.MessageKey;

                sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];

                Array.Copy(br.IncomingBuffer, 2, binData, totalTransfered, br.IncomingBuffer.Length - 2);

                totalTransfered += br.IncomingBuffer.Length - 2;

                requestProgress.Value = totalTransfered;
                if (del != null)
                    del(requestProgress);
            }

            br.SubCommand = (int) en_PComSubCommand.CloseSdChannel;
            br.Address = 0;
            br.ChunkSizeAlignment = 2;
            br.ElementsCount = 0;

            if (this.BreakFlag)
                throw new ComDriveExceptions("Request aborted by user",
                    ComDriveExceptions.ComDriveException.AbortedByUser);

            m_Plc.ReadWrite(ref rw);

            br.MessageKey = (br.MessageKey + 1) % 256;
            messageKey = br.MessageKey;

            sdStatus = (en_SdStatus) br.IncomingBuffer[0];
            sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];

            while ((sdStatus == en_SdStatus.Error) || (sdStatus == en_SdStatus.Busy))
            {
                if (sdStatus == en_SdStatus.Error)
                {
                    if (sdErrorCode != en_SdErrorCode.NoError)
                    {
                        throwSdException(sdErrorCode);
                    }
                }

                br.SubCommand = (int) en_PComSubCommand.CloseSdChannel;

                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                m_Plc.ReadWrite(ref rw);
                br.MessageKey = (br.MessageKey + 1) % 256;
                messageKey = br.MessageKey;

                sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];
            }

            sdChannelLockInitiated = false;

            result = parseFat32ByteArrayIntoFoldersAndFilesList(binData);

            requestProgress.Value = totalTransfered;
            requestProgress.NotificationType = RequestProgress.en_NotificationType.Completed;

            if (del != null)
                del(requestProgress);

            return result;
        }

        private Folder parseFat32ByteArrayIntoFoldersAndFilesList(byte[] binData)
        {
            Folder result = new Folder();
            if ((binData.Length % 32) != 0)
            {
                throw new SdExceptions("Invalid Data from SD FAT", SdExceptions.SdException.UnexpectedError);
            }

            for (int i = 0; i < binData.Length; i += 32)
            {
                byte[] tmp = new byte[32];
                Array.Copy(binData, i, tmp, 0, 32);
                byte[] fileNameBytes = new byte[8];
                Array.Copy(tmp, 0, fileNameBytes, 0, fileNameBytes.Length);
                string fileName = Encoding.Default.GetString(fileNameBytes);
                fileName = fileName.Trim(new char[] {' '});

                byte[] extensionBytes = new byte[3];
                Array.Copy(tmp, 8, extensionBytes, 0, extensionBytes.Length);
                string extension = Encoding.Default.GetString(extensionBytes);
                extension = extension.Trim(new char[] {' '});

                bool isFolder;

                File file;

                byte[] dateTimeBytes = new byte[5];
                Array.Copy(tmp, 0x0d, dateTimeBytes, 0, 5);
                DateTime dateCreated = new DateTime();
                try
                {
                    dateCreated = getDateFromBytes(dateTimeBytes);
                }
                catch
                {
                }

                dateTimeBytes = new byte[5];
                Array.Copy(tmp, 0x16, dateTimeBytes, 1, 4);

                DateTime dateModified = new DateTime();
                try
                {
                    dateModified = getDateFromBytes(dateTimeBytes);
                }
                catch
                {
                }

                byte attributes = tmp[0x0b];

                if ((attributes & 0x10) != 0)
                {
                    // Directory
                    file = new Folder();
                    isFolder = true;
                }
                else
                {
                    file = new File();
                    isFolder = false;

                    if (extension != "")
                        fileName += "." + extension;
                }

                if ((attributes & 0x1) != 0)
                    file.ReadOnly = true;

                file.Name = fileName;
                file.DateCreated = dateCreated;
                file.DateModified = dateModified;
                file.Size = BitConverter.ToUInt32(tmp, 0x1c);

                if (isFolder)
                {
                    result.Folders.Add((Folder) file);
                }
                else
                {
                    result.Files.Add(file);
                }
            }

            return result;
        }


        private DateTime getDateFromBytes(byte[] dateTimeBytes)
        {
            // Should be 5 bytes
            int milliSeconds = dateTimeBytes[0] * 10;
            int creationTime = BitConverter.ToUInt16(dateTimeBytes, 1);
            int seconds = (creationTime & 0x1f) * 2; // Bits 0 to 4
            creationTime = creationTime >> 5;
            int minutes = (creationTime & 0x3f); // Bits 0 to 5 (After bytes shift)
            creationTime = creationTime >> 6;
            int hours = (creationTime & 0x1f); // Bits 0 to 4 (After bytes shift)

            int creationDate = BitConverter.ToUInt16(dateTimeBytes, 3);
            int day = (creationDate & 0x1f); // Bits 0 to 4
            creationDate = creationDate >> 5;
            int month = (creationDate & 0x0f); // Bits 0 to 3 (After bytes shift)
            creationDate = creationDate >> 4;
            int year = 1980 + (creationDate & 0x7f); // Bits 0 to 6 (After bytes shift)

            return new DateTime(year, month, day, hours, minutes, seconds, milliSeconds);
        }

        private void parseFileListBytes(byte[] binData, string fileExtension, List<string> filesList)
        {
            byte[] fileNameBytes = new byte[8];

            for (int i = 0; i < binData.Length; i += 8)
            {
                Array.Copy(binData, i, fileNameBytes, 0, fileNameBytes.Length);
                string fileName = ASCIIEncoding.ASCII.GetString(fileNameBytes);
                if (fileName.IndexOf('\x0000', 0) >= 0)
                    break;

                fileName = fileName.Trim(new char[] {' '});
                if (fileName != "")
                {
                    fileName += "." + fileExtension;
                    filesList.Add(fileName.Trim());
                }
            }
        }

        private byte[] readFile(string folderName, string fileName, ref bool sdChannelLockInitiated, out string guid,
            ProgressStatusChangedDelegate del)
        {
            ushort chunkSize = (ushort) m_Version.PlcBuffer;
            DateTime startTime;
            int messageKey = 0;

            if (chunkSize > 512)
                chunkSize = 512;

            chunkSize -= (ushort) (chunkSize % 2); // Make the chunk size an even number

            if (folderName.Substring(folderName.Length - 1) == "\\")
                folderName = folderName.Substring(0, folderName.Length - 1);


            checkNameLegality(folderName, NameType.Path);
            checkNameLegality(fileName, NameType.FileNameWithExtension);

            SdParams sdParams = getInitializedSdParamsObject();
            guid = sdParams.Guid;
            sdParams.FileName = folderName + @"\" + fileName;

            sdParams.FileAccessMode = en_FileAccessMode.R;
            sdParams.BufferSize = chunkSize;

            en_SdStatus sdStatus;
            en_SdErrorCode sdErrorCode;

            BinaryRequest br = new BinaryRequest()
            {
                CommandCode = ACCESS_SD_COMMANDCODE,
                SubCommand = (int) en_PComSubCommand.ReadFileInit,
                MessageKey = messageKey,
                OutgoingBuffer = getSdParamsAsByteArray(sdParams),
                IsInternal = true,
            };

            ReadWriteRequest[] rw = new ReadWriteRequest[] {br};

            if (this.BreakFlag)
                throw new ComDriveExceptions("Request aborted by user",
                    ComDriveExceptions.ComDriveException.AbortedByUser);

            m_Plc.ReadWrite(ref rw);

            sdStatus = (en_SdStatus) br.IncomingBuffer[0];
            sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];

            RequestProgress requestProgress = new RequestProgress();
            requestProgress.Minimum = 0;
            requestProgress.Maximum = 100;
            requestProgress.NotificationType = RequestProgress.en_NotificationType.SetMinMax;
            requestProgress.Text = "";
            requestProgress.Value = 0;

            while ((sdStatus == en_SdStatus.Error) && (sdErrorCode == en_SdErrorCode.SdLocked))
            {
                // if Sd channel is locked then message key is not incremented
                requestProgress.NotificationType = RequestProgress.en_NotificationType.ProgressChanged;
                requestProgress.Text = "SD Card is Locked by another process... Please wait.";
                requestProgress.Value = 0;

                if (del != null)
                    del(requestProgress);

                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                m_Plc.ReadWrite(ref rw);

                sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];
            }

            sdChannelLockInitiated = true;

            br.MessageKey = (br.MessageKey + 1) % 256;
            messageKey = br.MessageKey;

            while ((sdStatus == en_SdStatus.Error) || (sdStatus == en_SdStatus.Busy))
            {
                if (sdStatus == en_SdStatus.Error)
                {
                    if ((sdErrorCode != en_SdErrorCode.NoError) && (sdErrorCode != en_SdErrorCode.SdLocked))
                    {
                        throwSdException(sdErrorCode);
                    }
                }

                br.SubCommand = (int) en_PComSubCommand.ReadFile;

                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                m_Plc.ReadWrite(ref rw);
                br.MessageKey = (br.MessageKey + 1) % 256;
                messageKey = br.MessageKey;

                sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];
            }

            int fileLength = 0;
            List<byte> fileBytes = new List<byte>();

            fileLength = BitConverter.ToInt32(br.IncomingBuffer, 2);
            int totalTransfered = 0;

            requestProgress.Minimum = 0;
            requestProgress.Maximum = fileLength;
            requestProgress.NotificationType = RequestProgress.en_NotificationType.SetMinMax;
            requestProgress.Text = "Reading File: " + folderName + "\\" + fileName;
            requestProgress.Value = 0;

            if (del != null)
                del(requestProgress);

            requestProgress.NotificationType = RequestProgress.en_NotificationType.ProgressChanged;
            requestProgress.Value = 0;

            if (del != null)
                del(requestProgress);


            byte[] dataReceived = new byte[br.IncomingBuffer.Length - 6];

            // If the size of file is smaller than the chunk size
            if (fileLength < dataReceived.Length)
            {
                dataReceived = new byte[fileLength];
            }

            Array.Copy(br.IncomingBuffer, 6, dataReceived, 0, dataReceived.Length);

            fileBytes.AddRange(dataReceived);
            totalTransfered += dataReceived.Length;

            sdParams.SeekOrigin = 1;
            br.OutgoingBuffer = getSdParamsAsByteArray(sdParams);

            startTime = DateTime.Now;

            while (totalTransfered < fileLength)
            {
                sdParams.BufferSize = chunkSize;

                if (sdParams.BufferSize + totalTransfered >= fileLength)
                {
                    sdParams.BufferSize = (ushort) (fileLength - totalTransfered);
                    sdParams.LastReadWriteChunk = true;
                }

                if (totalTransfered / 512 != (totalTransfered + sdParams.BufferSize) / 512)
                {
                    sdParams.BufferSize = (ushort) (((totalTransfered + chunkSize) / 512) * 512 - totalTransfered);
                }

                br.OutgoingBuffer = getSdParamsAsByteArray(sdParams);

                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                m_Plc.ReadWrite(ref rw);
                br.MessageKey = (br.MessageKey + 1) % 256;
                messageKey = br.MessageKey;
                sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];

                while ((sdStatus == en_SdStatus.Error) || (sdStatus == en_SdStatus.Busy))
                {
                    if (sdStatus == en_SdStatus.Error)
                    {
                        if ((sdErrorCode != en_SdErrorCode.NoError) && (sdErrorCode != en_SdErrorCode.SdLocked))
                        {
                            throwSdException(sdErrorCode);
                        }
                    }

                    if (this.BreakFlag)
                        throw new ComDriveExceptions("Request aborted by user",
                            ComDriveExceptions.ComDriveException.AbortedByUser);

                    m_Plc.ReadWrite(ref rw);
                    br.MessageKey = (br.MessageKey + 1) % 256;
                    messageKey = br.MessageKey;

                    sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                    sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];
                }

                dataReceived = new byte[br.IncomingBuffer.Length - 6];
                Array.Copy(br.IncomingBuffer, 6, dataReceived, 0, dataReceived.Length);

                fileBytes.AddRange(dataReceived);
                totalTransfered += sdParams.BufferSize;

                TimeSpan ts = DateTime.Now - startTime;

                ts = TimeSpan.FromTicks((long) (ts.Ticks * (fileLength - totalTransfered)) / (long) totalTransfered);

                requestProgress.Text = "Reading File: " + folderName + "\\" + fileName + " - Estimated time left: " +
                                       ts.Hours.ToString().PadLeft(2, '0') + ":" +
                                       ts.Minutes.ToString().PadLeft(2, '0') + ":" +
                                       ts.Seconds.ToString().PadLeft(2, '0');

                requestProgress.Value = totalTransfered;

                if (del != null)
                    del(requestProgress);
            }

            br.SubCommand = (int) en_PComSubCommand.CloseSdChannel;
            br.OutgoingBuffer = getSdParamsAsByteArray(sdParams);

            if (this.BreakFlag)
                throw new ComDriveExceptions("Request aborted by user",
                    ComDriveExceptions.ComDriveException.AbortedByUser);

            m_Plc.ReadWrite(ref rw);
            br.MessageKey = (br.MessageKey + 1) % 256;
            messageKey = br.MessageKey;

            sdStatus = (en_SdStatus) br.IncomingBuffer[0];
            sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];

            while ((sdStatus == en_SdStatus.Error) || (sdStatus == en_SdStatus.Busy))
            {
                if (sdStatus == en_SdStatus.Error)
                {
                    if (sdErrorCode != en_SdErrorCode.NoError)
                    {
                        throwSdException(sdErrorCode);
                    }
                }

                br.SubCommand = (int) en_PComSubCommand.CloseSdChannel;
                br.OutgoingBuffer = getSdParamsAsByteArray(sdParams);

                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                m_Plc.ReadWrite(ref rw);
                br.MessageKey = (br.MessageKey + 1) % 256;
                messageKey = br.MessageKey;

                sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];
            }

            sdChannelLockInitiated = false;

            if (fileBytes.Count() != fileLength)
            {
                throw new SdExceptions("Read file size does not match actual file size on SD",
                    SdExceptions.SdException.UnexpectedError);
            }

            requestProgress.NotificationType = RequestProgress.en_NotificationType.Completed;

            if (del != null)
                del(requestProgress);

            return fileBytes.ToArray();
        }


        private void readFile(string sourceFolderName, string sourceFileName, string targetFolder, bool resumeRead,
            ref bool sdChannelLockInitiated, out string guid, ProgressStatusChangedDelegate del)
        {
            FileStream fs = null;
            try
            {
                ushort chunkSize = (ushort) m_Version.PlcBuffer;
                DateTime startTime;
                int messageKey = 0;
                int offsentInBuffer = 0;

                if (chunkSize > 512)
                    chunkSize = 512;

                chunkSize -= (ushort) (chunkSize % 2); // Make the chunk size an even number


                if (sourceFolderName.Substring(sourceFolderName.Length - 1) == "\\")
                    sourceFolderName = sourceFolderName.Substring(0, sourceFolderName.Length - 1);

                if (targetFolder.Substring(targetFolder.Length - 1) == "\\")
                    targetFolder = targetFolder.Substring(0, targetFolder.Length - 1);

                checkNameLegality(sourceFolderName, NameType.Path);
                checkNameLegality(sourceFileName, NameType.FileNameWithExtension);

                SdParams sdParams = getInitializedSdParamsObject();
                guid = sdParams.Guid;
                sdParams.FileName = sourceFolderName + @"\" + sourceFileName;

                sdParams.FileAccessMode = en_FileAccessMode.R;
                sdParams.BufferSize = chunkSize;

                if (System.IO.File.Exists(targetFolder + "\\" + sourceFileName))
                {
                    // remove attributes that may cause exceptions, for example read-only.
                    System.IO.File.SetAttributes(targetFolder + "\\" + sourceFileName, FileAttributes.Normal);
                }

                if (System.IO.File.Exists(targetFolder + "\\" + sourceFileName) && !resumeRead)
                {
                    System.IO.File.Delete(targetFolder + "\\" + sourceFileName);
                    fs = new FileStream(targetFolder + "\\" + sourceFileName, FileMode.Create);
                    fs.Close();
                }
                else if (!System.IO.File.Exists(targetFolder + "\\" + sourceFileName))
                {
                    fs = new FileStream(targetFolder + "\\" + sourceFileName, FileMode.Create);
                    fs.Close();
                }

                fs = new FileStream(targetFolder + "\\" + sourceFileName, FileMode.Open);
                long fileSize = fs.Length;
                fileSize = fileSize - (fileSize % 4096); // truncate the file so it will be 4KB aligned
                fs.SetLength(fileSize);
                fs.Seek(0, System.IO.SeekOrigin.End);

                sdParams.SeekOffset = (int) fileSize;

                en_SdStatus sdStatus;
                en_SdErrorCode sdErrorCode;

                BinaryRequest br = new BinaryRequest()
                {
                    CommandCode = ACCESS_SD_COMMANDCODE,
                    SubCommand = (int) en_PComSubCommand.ReadFileInit,
                    MessageKey = messageKey,
                    OutgoingBuffer = getSdParamsAsByteArray(sdParams),
                    IsInternal = true,
                };

                ReadWriteRequest[] rw = new ReadWriteRequest[] {br};

                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                m_Plc.ReadWrite(ref rw);

                sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];

                RequestProgress requestProgress = new RequestProgress();
                requestProgress.Minimum = 0;
                requestProgress.Maximum = 100;
                requestProgress.NotificationType = RequestProgress.en_NotificationType.SetMinMax;
                requestProgress.Text = "";
                requestProgress.Value = 0;

                while ((sdStatus == en_SdStatus.Error) && (sdErrorCode == en_SdErrorCode.SdLocked))
                {
                    // if Sd channel is locked then message key is not incremented
                    requestProgress.NotificationType = RequestProgress.en_NotificationType.ProgressChanged;
                    requestProgress.Text = "SD Card is Locked by another process... Please wait.";
                    requestProgress.Value = 0;

                    if (del != null)
                        del(requestProgress);

                    if (this.BreakFlag)
                        throw new ComDriveExceptions("Request aborted by user",
                            ComDriveExceptions.ComDriveException.AbortedByUser);

                    m_Plc.ReadWrite(ref rw);

                    sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                    sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];
                }

                sdChannelLockInitiated = true;

                br.MessageKey = (br.MessageKey + 1) % 256;
                messageKey = br.MessageKey;

                while ((sdStatus == en_SdStatus.Error) || (sdStatus == en_SdStatus.Busy))
                {
                    if (sdStatus == en_SdStatus.Error)
                    {
                        if ((sdErrorCode != en_SdErrorCode.NoError) && (sdErrorCode != en_SdErrorCode.SdLocked))
                        {
                            throwSdException(sdErrorCode);
                        }
                    }

                    br.SubCommand = (int) en_PComSubCommand.ReadFile;

                    if (this.BreakFlag)
                        throw new ComDriveExceptions("Request aborted by user",
                            ComDriveExceptions.ComDriveException.AbortedByUser);

                    m_Plc.ReadWrite(ref rw);
                    br.MessageKey = (br.MessageKey + 1) % 256;
                    messageKey = br.MessageKey;

                    sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                    sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];
                }

                int fileLength = 0;
                List<byte> fileBytes = new List<byte>();

                fileLength = BitConverter.ToInt32(br.IncomingBuffer, 2);
                int totalTransfered = (int) fileSize;

                requestProgress.Minimum = 0;
                requestProgress.Maximum = fileLength;
                requestProgress.NotificationType = RequestProgress.en_NotificationType.SetMinMax;
                requestProgress.Text = "Reading File: " + sourceFolderName + "\\" + sourceFileName;
                requestProgress.Value = 0;

                if (del != null)
                    del(requestProgress);

                requestProgress.NotificationType = RequestProgress.en_NotificationType.ProgressChanged;
                requestProgress.Value = 0;

                if (del != null)
                    del(requestProgress);


                byte[] dataReceived = new byte[br.IncomingBuffer.Length - 6];

                // If the size of file is smaller than the chunk size
                if (fileLength < dataReceived.Length)
                {
                    dataReceived = new byte[fileLength];
                }

                Array.Copy(br.IncomingBuffer, 6, dataReceived, 0, dataReceived.Length);

                fileBytes.AddRange(dataReceived);
                totalTransfered += dataReceived.Length;
                offsentInBuffer += dataReceived.Length;

                sdParams.SeekOffset = 0;
                sdParams.SeekOrigin = 1;
                br.OutgoingBuffer = getSdParamsAsByteArray(sdParams);

                startTime = DateTime.Now;

                while (totalTransfered < fileLength)
                {
                    sdParams.BufferSize = chunkSize;

                    if (sdParams.BufferSize + totalTransfered >= fileLength)
                    {
                        sdParams.BufferSize = (ushort) (fileLength - totalTransfered);
                        sdParams.LastReadWriteChunk = true;
                    }

                    if (totalTransfered / 512 != (totalTransfered + sdParams.BufferSize) / 512)
                    {
                        sdParams.BufferSize = (ushort) (((totalTransfered + chunkSize) / 512) * 512 - totalTransfered);
                    }

                    br.OutgoingBuffer = getSdParamsAsByteArray(sdParams);

                    if (this.BreakFlag)
                        throw new ComDriveExceptions("Request aborted by user",
                            ComDriveExceptions.ComDriveException.AbortedByUser);

                    m_Plc.ReadWrite(ref rw);
                    br.MessageKey = (br.MessageKey + 1) % 256;
                    messageKey = br.MessageKey;
                    sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                    sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];

                    while ((sdStatus == en_SdStatus.Error) || (sdStatus == en_SdStatus.Busy))
                    {
                        if (sdStatus == en_SdStatus.Error)
                        {
                            if ((sdErrorCode != en_SdErrorCode.NoError) && (sdErrorCode != en_SdErrorCode.SdLocked))
                            {
                                throwSdException(sdErrorCode);
                            }
                        }

                        if (this.BreakFlag)
                            throw new ComDriveExceptions("Request aborted by user",
                                ComDriveExceptions.ComDriveException.AbortedByUser);

                        m_Plc.ReadWrite(ref rw);

                        br.MessageKey = (br.MessageKey + 1) % 256;
                        messageKey = br.MessageKey;

                        sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                        sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];
                    }

                    dataReceived = new byte[br.IncomingBuffer.Length - 6];
                    Array.Copy(br.IncomingBuffer, 6, dataReceived, 0, dataReceived.Length);

                    fileBytes.AddRange(dataReceived);
                    totalTransfered += sdParams.BufferSize;
                    offsentInBuffer += sdParams.BufferSize;

                    if ((offsentInBuffer % 4096) == 0)
                    {
                        fs.Write(fileBytes.ToArray(), 0, fileBytes.Count());
                        fileBytes.Clear();
                        offsentInBuffer = 0;
                    }

                    TimeSpan ts = DateTime.Now - startTime;
                    ts = TimeSpan.FromTicks((long) (ts.Ticks * (fileLength - totalTransfered)) /
                                            ((long) totalTransfered - fileSize));
                    requestProgress.Text = "Reading File: " + sourceFolderName + "\\" + sourceFileName +
                                           " - Estimated time left: " + ts.Hours.ToString().PadLeft(2, '0') + ":" +
                                           ts.Minutes.ToString().PadLeft(2, '0') + ":" +
                                           ts.Seconds.ToString().PadLeft(2, '0');
                    requestProgress.Value = totalTransfered;

                    if (del != null)
                        del(requestProgress);
                }

                if (fileBytes.Count() != 0)
                {
                    fs.Write(fileBytes.ToArray(), 0, fileBytes.Count());
                    fileBytes.Clear();
                    offsentInBuffer = 0;
                }

                br.SubCommand = (int) en_PComSubCommand.CloseSdChannel;
                br.OutgoingBuffer = getSdParamsAsByteArray(sdParams);

                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                m_Plc.ReadWrite(ref rw);
                br.MessageKey = (br.MessageKey + 1) % 256;
                messageKey = br.MessageKey;

                sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];

                while ((sdStatus == en_SdStatus.Error) || (sdStatus == en_SdStatus.Busy))
                {
                    if (sdStatus == en_SdStatus.Error)
                    {
                        if (sdErrorCode != en_SdErrorCode.NoError)
                        {
                            throwSdException(sdErrorCode);
                        }
                    }

                    br.SubCommand = (int) en_PComSubCommand.CloseSdChannel;
                    br.OutgoingBuffer = getSdParamsAsByteArray(sdParams);

                    if (this.BreakFlag)
                        throw new ComDriveExceptions("Request aborted by user",
                            ComDriveExceptions.ComDriveException.AbortedByUser);

                    m_Plc.ReadWrite(ref rw);
                    br.MessageKey = (br.MessageKey + 1) % 256;
                    messageKey = br.MessageKey;

                    sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                    sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];
                }

                sdChannelLockInitiated = false;

                if (totalTransfered != fileLength)
                {
                    throw new SdExceptions("Read file size does not match actual file size on SD",
                        SdExceptions.SdException.UnexpectedError);
                }

                requestProgress.NotificationType = RequestProgress.en_NotificationType.Completed;

                if (del != null)
                    del(requestProgress);
            }
            finally
            {
                if (fs != null)
                {
                    fs.Close();
                }
            }
        }

        private void writeFile(string folderName, string fileName, byte[] fileContent, ref bool sdChannelLockInitiated,
            out string guid, ProgressStatusChangedDelegate del)
        {
            ushort chunkSize = (ushort) m_Version.PlcBuffer;
            DateTime startTime;
            int messageKey = 0;

            if (chunkSize > 512)
                chunkSize = 512;

            chunkSize -= (ushort) (chunkSize % 2); // Make the chunk size an even number

            if (folderName.Substring(folderName.Length - 1) == "\\")
                folderName = folderName.Substring(0, folderName.Length - 1);

            checkNameLegality(folderName, NameType.Path);
            checkNameLegality(fileName, NameType.FileNameWithExtension);

            SdParams sdParams = getInitializedSdParamsObject();
            guid = sdParams.Guid;
            sdParams.FileName = folderName + @"\" + fileName;
            sdParams.FileAccessMode = en_FileAccessMode.W; // Create file. If file exist, overwrite.
            sdParams.BufferSize = chunkSize;

            en_SdStatus sdStatus;
            en_SdErrorCode sdErrorCode;
            BinaryRequest br = new BinaryRequest();
            br.IsInternal = true;
            ReadWriteRequest[] rw = new ReadWriteRequest[] {br};

            bool init = true;
            int totalTransfered = 0;

            RequestProgress requestProgress = new RequestProgress();
            requestProgress.Minimum = 0;
            requestProgress.Maximum = fileContent.Length;
            requestProgress.NotificationType = RequestProgress.en_NotificationType.SetMinMax;
            requestProgress.Text = "Writing File: " + folderName + "\\" + fileName;
            requestProgress.Value = 0;

            if (del != null)
                del(requestProgress);

            requestProgress.NotificationType = RequestProgress.en_NotificationType.ProgressChanged;
            if (del != null)
                del(requestProgress);

            startTime = DateTime.Now;

            // We also enter this loop on Init since 0 bytes file will not follow the rule: totalTransfered < fileContent.Length
            while ((totalTransfered < fileContent.Length) || (init))
            {
                sdParams.BufferSize = chunkSize;

                if (sdParams.BufferSize + totalTransfered >= fileContent.Length)
                {
                    sdParams.BufferSize = (ushort) (fileContent.Length - totalTransfered);
                    // Last chunk of data. Since this chunk might not create a 512 bytes alignment
                    // Then we need to force the PLC to write the data into the SD card.
                    sdParams.LastReadWriteChunk = true;
                }

                if ((totalTransfered + sdParams.BufferSize) / 512 != totalTransfered / 512)
                {
                    sdParams.BufferSize = (ushort) (((totalTransfered + chunkSize) / 512) * 512 - totalTransfered);
                }

                byte[] binData = new byte[sdParams.BufferSize];
                Array.Copy(fileContent, totalTransfered, binData, 0, binData.Length);

                List<byte> dataToSend = new List<byte>();

                dataToSend.AddRange(getSdParamsAsByteArray(sdParams));
                dataToSend.AddRange(binData);

                if (init)
                {
                    br.CommandCode = ACCESS_SD_COMMANDCODE;
                    br.SubCommand = (int) en_PComSubCommand.WriteFileInit;
                    br.MessageKey = messageKey;
                    br.OutgoingBuffer = dataToSend.ToArray();

                    if (this.BreakFlag)
                        throw new ComDriveExceptions("Request aborted by user",
                            ComDriveExceptions.ComDriveException.AbortedByUser);

                    m_Plc.ReadWrite(ref rw);

                    sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                    sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];

                    while ((sdStatus == en_SdStatus.Error) && (sdErrorCode == en_SdErrorCode.SdLocked))
                    {
                        // if Sd channel is locked then message key is not incremented

                        requestProgress.NotificationType = RequestProgress.en_NotificationType.ProgressChanged;
                        requestProgress.Text = "SD Card is Locked by another process... Please wait.";
                        requestProgress.Value = 0;

                        if (del != null)
                            del(requestProgress);

                        if (this.BreakFlag)
                            throw new ComDriveExceptions("Request aborted by user",
                                ComDriveExceptions.ComDriveException.AbortedByUser);

                        m_Plc.ReadWrite(ref rw);

                        sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                        sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];
                    }

                    sdChannelLockInitiated = true;
                    br.MessageKey = (br.MessageKey + 1) % 256;
                    messageKey = br.MessageKey;
                    init = false;
                }
                else
                {
                    br.SubCommand = (int) en_PComSubCommand.WriteFile;
                    br.OutgoingBuffer = dataToSend.ToArray();

                    if (this.BreakFlag)
                        throw new ComDriveExceptions("Request aborted by user",
                            ComDriveExceptions.ComDriveException.AbortedByUser);

                    m_Plc.ReadWrite(ref rw);
                    br.MessageKey = (br.MessageKey + 1) % 256;
                    messageKey = br.MessageKey;

                    sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                    sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];
                }

                while ((sdStatus == en_SdStatus.Error) || (sdStatus == en_SdStatus.Busy))
                {
                    if (sdStatus == en_SdStatus.Error)
                    {
                        if ((sdErrorCode != en_SdErrorCode.NoError) && (sdErrorCode != en_SdErrorCode.SdLocked))
                        {
                            throwSdException(sdErrorCode);
                        }
                    }

                    br.SubCommand = (int) en_PComSubCommand.WriteFileStatus;
                    br.OutgoingBuffer = getSdParamsAsByteArray(sdParams);

                    if (this.BreakFlag)
                        throw new ComDriveExceptions("Request aborted by user",
                            ComDriveExceptions.ComDriveException.AbortedByUser);

                    m_Plc.ReadWrite(ref rw);
                    br.MessageKey = (br.MessageKey + 1) % 256;
                    messageKey = br.MessageKey;

                    sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                    sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];
                }

                sdParams.SeekOrigin = 1;

                totalTransfered += sdParams.BufferSize;

                TimeSpan ts = DateTime.Now - startTime;
                if (totalTransfered > 0)
                {
                    ts = TimeSpan.FromTicks((long) (ts.Ticks * (fileContent.Length - totalTransfered)) /
                                            (long) totalTransfered);
                }
                else
                {
                    ts = new TimeSpan(0, 0, 0);
                }

                requestProgress.Text = "Writing File: " + folderName + "\\" + fileName + " - Estimated time left: " +
                                       ts.Hours.ToString().PadLeft(2, '0') + ":" +
                                       ts.Minutes.ToString().PadLeft(2, '0') + ":" +
                                       ts.Seconds.ToString().PadLeft(2, '0');


                requestProgress.Value = totalTransfered;

                if (del != null)
                    del(requestProgress);
            }

            br.SubCommand = (int) en_PComSubCommand.CloseSdChannel;
            br.OutgoingBuffer = getSdParamsAsByteArray(sdParams);

            if (this.BreakFlag)
                throw new ComDriveExceptions("Request aborted by user",
                    ComDriveExceptions.ComDriveException.AbortedByUser);

            m_Plc.ReadWrite(ref rw);
            br.MessageKey = (br.MessageKey + 1) % 256;
            messageKey = br.MessageKey;

            sdStatus = (en_SdStatus) br.IncomingBuffer[0];
            sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];

            while ((sdStatus == en_SdStatus.Error) || (sdStatus == en_SdStatus.Busy))
            {
                if (sdStatus == en_SdStatus.Error)
                {
                    if (sdErrorCode != en_SdErrorCode.NoError)
                    {
                        throwSdException(sdErrorCode);
                    }
                }

                br.SubCommand = (int) en_PComSubCommand.CloseSdChannel;
                br.OutgoingBuffer = getSdParamsAsByteArray(sdParams);

                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                m_Plc.ReadWrite(ref rw);
                br.MessageKey = (br.MessageKey + 1) % 256;
                messageKey = br.MessageKey;

                sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];
            }

            sdChannelLockInitiated = false;

            requestProgress.NotificationType = RequestProgress.en_NotificationType.Completed;

            if (del != null)
                del(requestProgress);
        }

        private void deleteFile(string folderName, string fileName, ref bool sdChannelLockInitiated, out string guid,
            ProgressStatusChangedDelegate del)
        {
            ushort chunkSize = (ushort) m_Version.PlcBuffer;

            List<string> filesList = new List<string>();
            int messageKey = 0;

            if (folderName.Substring(folderName.Length - 1) == "\\")
                folderName = folderName.Substring(0, folderName.Length - 1);

            checkNameLegality(folderName, NameType.Path);
            checkNameLegality(fileName, NameType.FileNameWithExtension);

            SdParams sdParams = getInitializedSdParamsObject();
            guid = sdParams.Guid;
            sdParams.FileName = folderName + @"\" + fileName;

            sdParams.FileAccessMode = en_FileAccessMode.R;
            sdParams.BufferSize = 0;

            en_SdStatus sdStatus;
            en_SdErrorCode sdErrorCode;

            RequestProgress requestProgress = new RequestProgress();
            requestProgress.Minimum = 0;
            requestProgress.Maximum = 100;
            requestProgress.NotificationType = RequestProgress.en_NotificationType.SetMinMax;
            requestProgress.Text = "Deleting File: " + folderName + "\\" + fileName;
            requestProgress.Value = 0;

            if (del != null)
                del(requestProgress);

            requestProgress.NotificationType = RequestProgress.en_NotificationType.ProgressChanged;
            requestProgress.Value = 0;

            if (del != null)
                del(requestProgress);

            BinaryRequest br = new BinaryRequest()
            {
                CommandCode = ACCESS_SD_COMMANDCODE,
                SubCommand = (int) en_PComSubCommand.DeleteFileInit,
                MessageKey = messageKey,
                OutgoingBuffer = getSdParamsAsByteArray(sdParams),
                IsInternal = true,
            };

            ReadWriteRequest[] rw = new ReadWriteRequest[] {br};

            if (this.BreakFlag)
                throw new ComDriveExceptions("Request aborted by user",
                    ComDriveExceptions.ComDriveException.AbortedByUser);

            m_Plc.ReadWrite(ref rw);

            sdStatus = (en_SdStatus) br.IncomingBuffer[0];
            sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];

            while ((sdStatus == en_SdStatus.Error) && (sdErrorCode == en_SdErrorCode.SdLocked))
            {
                // if Sd channel is locked then message key is not incremented
                requestProgress.NotificationType = RequestProgress.en_NotificationType.ProgressChanged;
                requestProgress.Text = "SD Card is Locked by another process... Please wait.";
                requestProgress.Value = 0;

                if (del != null)
                    del(requestProgress);

                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                m_Plc.ReadWrite(ref rw);

                sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];
            }

            br.MessageKey = (br.MessageKey + 1) % 256;
            messageKey = br.MessageKey;

            sdChannelLockInitiated = true;

            while ((sdStatus == en_SdStatus.Error) || (sdStatus == en_SdStatus.Busy))
            {
                if (sdStatus == en_SdStatus.Error)
                {
                    if (sdErrorCode != en_SdErrorCode.NoError)
                    {
                        throwSdException(sdErrorCode);
                    }
                }

                br.SubCommand = (int) en_PComSubCommand.DeleteFile;

                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                m_Plc.ReadWrite(ref rw);
                br.MessageKey = (br.MessageKey + 1) % 256;
                messageKey = br.MessageKey;

                sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];
            }

            br.SubCommand = (int) en_PComSubCommand.CloseSdChannel;
            br.Address = 0;
            br.ChunkSizeAlignment = 2;
            br.ElementsCount = 0;

            if (this.BreakFlag)
                throw new ComDriveExceptions("Request aborted by user",
                    ComDriveExceptions.ComDriveException.AbortedByUser);

            m_Plc.ReadWrite(ref rw);

            br.MessageKey = (br.MessageKey + 1) % 256;
            messageKey = br.MessageKey;

            sdStatus = (en_SdStatus) br.IncomingBuffer[0];
            sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];

            while ((sdStatus == en_SdStatus.Error) || (sdStatus == en_SdStatus.Busy))
            {
                if (sdStatus == en_SdStatus.Error)
                {
                    if (sdErrorCode != en_SdErrorCode.NoError)
                    {
                        throwSdException(sdErrorCode);
                    }
                }

                br.SubCommand = (int) en_PComSubCommand.CloseSdChannel;

                if (this.BreakFlag)
                    throw new ComDriveExceptions("Request aborted by user",
                        ComDriveExceptions.ComDriveException.AbortedByUser);

                m_Plc.ReadWrite(ref rw);
                br.MessageKey = (br.MessageKey + 1) % 256;
                messageKey = br.MessageKey;

                sdStatus = (en_SdStatus) br.IncomingBuffer[0];
                sdErrorCode = (en_SdErrorCode) br.IncomingBuffer[1];
            }

            sdChannelLockInitiated = false;

            requestProgress.NotificationType = RequestProgress.en_NotificationType.Completed;
            requestProgress.Value = 100;

            if (del != null)
                del(requestProgress);
        }

        private SdParams getInitializedSdParamsObject()
        {
            string guid = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 8);
            SdParams sdParams = new SdParams()
            {
                BufferSize = 0,
                FileAccessMode = en_FileAccessMode.R,
                FileName = "",
                LastReadWriteChunk = false,
                SeekOffset = 0,
                SeekOrigin = 0,
                Guid = guid,
            };
            return sdParams;
        }

        private bool checkNameLegality(string name, int length)
        {
            try
            {
                if ((name.Length > length) || (name.Length == 0))
                {
                    return false;
                }
                else
                {
                    for (int i = 0; i < forbiddenChars.Length; i++)
                    {
                        if (name.Contains(forbiddenChars[i]))
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }


        private void checkNameLegality(string name, NameType nameType)
        {
            switch (nameType)
            {
                case NameType.ExtensionOnly:
                    if (name == "*")
                        return;

                    if (!checkNameLegality(name, 3))
                        throw new SdExceptions("Extention not in 8.3 Dos format",
                            SdExceptions.SdException.IllegalFileName);
                    break;
                case NameType.FileNameNoExtension:
                    if (!checkNameLegality(name, 8))
                        throw new SdExceptions("File name not in 8.3 Dos format",
                            SdExceptions.SdException.IllegalFileName);
                    break;
                case NameType.FileNameWithExtension:
                    try
                    {
                        string[] filename = name.Split('.');

                        if (!checkNameLegality(filename[0], 8))
                            throw new SdExceptions("File name not in 8.3 Dos format",
                                SdExceptions.SdException.IllegalFileName);

                        if (filename.Length > 2)
                        {
                            throw new SdExceptions("File name not in 8.3 Dos format",
                                SdExceptions.SdException.IllegalFileName);
                        }
                        else if (filename.Length == 2)
                        {
                            if (!checkNameLegality(filename[1], 3))
                                throw new SdExceptions("File name not in 8.3 Dos format",
                                    SdExceptions.SdException.IllegalFileName);
                        }
                    }
                    catch
                    {
                        throw new SdExceptions("File name not in 8.3 Dos format",
                            SdExceptions.SdException.IllegalFileName);
                    }

                    break;
                case NameType.Path:
                    if (name == "")
                        return;

                    try
                    {
                        string[] directories = name.Split('\\');
                        foreach (string directory in directories)
                        {
                            if (!checkNameLegality(directory, 8))
                                throw new SdExceptions("File name not in 8.3 Dos format",
                                    SdExceptions.SdException.IllegalFileName);
                        }
                    }
                    catch
                    {
                        throw new SdExceptions("Directory not in 8.3 Dos format",
                            SdExceptions.SdException.IllegalFileName);
                    }

                    break;
            }
        }

        private byte[] getSdParamsAsByteArray(SdParams sdParams)
        {
            List<byte> bytesList = new List<byte>();
            bytesList.Add(100);
            bytesList.Add(0);
            bytesList.AddRange(BitConverter.GetBytes((ushort) sdParams.FileAccessMode));
            bytesList.AddRange(BitConverter.GetBytes(sdParams.SeekOrigin));
            bytesList.AddRange(BitConverter.GetBytes(sdParams.SeekOffset));
            bytesList.AddRange(BitConverter.GetBytes(sdParams.BufferSize));
            if (sdParams.LastReadWriteChunk)
            {
                bytesList.Add(1);
            }
            else
            {
                bytesList.Add(0);
            }

            bytesList.Add(0);
            bytesList.AddRange(ASCIIEncoding.ASCII.GetBytes(sdParams.Guid));
            bytesList.AddRange(new byte[8]);
            bytesList.AddRange(BitConverter.GetBytes((ushort) sdParams.FileName.Length));
            bytesList.AddRange(ASCIIEncoding.ASCII.GetBytes(sdParams.FileName));
            bytesList.Add((byte) 0); // Null at the end of string

            if ((bytesList.Count() % 2) != 0)
                bytesList.Add((byte) 0); // padding for even num of bytes

            return bytesList.ToArray();
        }

        private void throwSdException(en_SdErrorCode sdErrorCode)
        {
            switch (sdErrorCode)
            {
                case en_SdErrorCode.BufferOverflow:
                    throw new SdExceptions("Buffer Overflow.", SdExceptions.SdException.BufferOverflow);
                case en_SdErrorCode.MsgKeyError:
                    throw new SdExceptions("Message Key Error.", SdExceptions.SdException.MsgKeyError);
                case en_SdErrorCode.SdDriverError:
                    throw new SdExceptions("SD Driver Error. File or directory might not exist.",
                        SdExceptions.SdException.SdDriverError);
                case en_SdErrorCode.SdLocked:
                    throw new SdExceptions("SD channel is locked by another process.",
                        SdExceptions.SdException.SdChannelLocked);
                case en_SdErrorCode.TriggerError:
                    throw new SdExceptions(
                        "Trigger Error. Please make sure that the SD Card is present inside the PLC.",
                        SdExceptions.SdException.TriggerError);
                case en_SdErrorCode.UnknownError:
                    throw new SdExceptions(
                        "Unknown Error. Please make sure that the SD Card is present inside the PLC.",
                        SdExceptions.SdException.TriggerError);
                case en_SdErrorCode.Conflict:
                    throw new SdExceptions(
                        "Conflict Error (unexpected error). The request sent to the PLC is conflicting with the expected request.",
                        SdExceptions.SdException.TriggerError);
                case en_SdErrorCode.VersionMismatch:
                    throw new SdExceptions(
                        "Version Error. The SD request version is not supported by the PLC. Either the PLC OS version is too old or too new to be compatible with the version of the request.",
                        SdExceptions.SdException.TriggerError);
                case en_SdErrorCode.PathLength:
                    throw new SdExceptions(
                        "Path Lenght Error. The path including the file name exceeds the maximum path length allowed by the PLC.",
                        SdExceptions.SdException.TriggerError);
                case en_SdErrorCode.FileOpenedByOtherClient:
                    throw new SdExceptions(
                        "File Access Error. The file is already opened by another client and therefore cannot be accessed.",
                        SdExceptions.SdException.FileOpenedByOtherClient);
                default:
                    throw new SdExceptions("Undefined Error Number.", SdExceptions.SdException.UnknownError);
            }
        }

        #endregion
    }
}