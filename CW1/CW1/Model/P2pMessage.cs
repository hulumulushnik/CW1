using System;
using System.Collections.Generic;
using System.Text;

namespace CW1.Models
{
    public enum MessageType
    {
        SyncChain,
        NewBlock
    }

    public class P2pMessage
    {
        public MessageType Type { get; set; }
        public string Data { get; set; }

        public P2pMessage() { }

        public P2pMessage(MessageType type, string data)
        {
            Type = type;
            Data = data;
        }
    }
}