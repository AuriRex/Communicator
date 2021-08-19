using System;
using System.Collections.Generic;
using System.Text;

namespace Communicator.Packets
{
    public class IdentificationPacket : BasePacket<IdentificationData>
    {
        public override IdentificationData PacketData { get; set; } = new IdentificationData() { ServerID = "ServerID", GameName = "UnsetGameName" };
    }

    public struct IdentificationData
    {
        // Should be generated on the Game server once and used to identificate
        public string ServerID { get; set; }
        // eg. Terraria, Minecraft
        public string GameName { get; set; }
    }
}
