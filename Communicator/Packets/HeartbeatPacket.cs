using Communicator.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Communicator.Packets
{
    [NoConfirmation]
    public class HeartbeatPacket : BasePacket<char>
    {
        public override char PacketData { get; set; } = 'h';
    }
}
