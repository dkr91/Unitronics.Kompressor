using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unitronics.ComDriver.Messages.DataRequest;
using Unitronics.ComDriver.Messages;

namespace Unitronics.ComDriver
{
    class MessageRecord
    {
        public Guid PlcGuid { get; set; }
        public string ParentID { get; set; }
        public GuidClass MessageGuid { get; set; }
        public int UnitId { get; set; }
        public object MessageRequest { get; set; }
        public object MessageResponse { get; set; }
        public DateTime LastSendTime { get; set; }
        public int RemainingRetries { get; set; }
        public string Description { get; set; }
        public bool IsSent { get; set; }
        public byte MessageEnumerator { get; set; }
        public ReceiveStringDelegate ReceiveStringDelegate { get; set; }
        public ReceiveBytesDelegate ReceiveBytesDelegate { get; set; }
        public bool IsIdMessage { get; set; }
        public bool SuppressEthernetHeader { get; set; }
    }
}