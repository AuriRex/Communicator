using System;
using System.Collections.Generic;
using System.Text;

namespace Communicator.Packets
{
    public class HeartbeatPacket : BasePacket<char>
    {
        public override char PacketData { get; set; } = 'h';
    }
}
