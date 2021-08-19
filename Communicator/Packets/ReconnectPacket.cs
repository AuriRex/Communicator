using System;
using System.Collections.Generic;
using System.Text;

namespace Communicator.Packets
{
    public class ReconnectPacket : BasePacket<bool>
    {
        public override bool PacketData { get; set; } = true;
    }
}
