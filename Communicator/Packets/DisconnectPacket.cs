using Communicator.Attributes;

namespace Communicator.Packets
{
    [NoConfirmation, Unignorable]
    public class DisconnectPacket : BasePacket<bool>
    {
        public override bool PacketData { get; set; } = true;
    }
}
