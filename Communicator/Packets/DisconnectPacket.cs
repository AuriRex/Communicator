using Communicator.Attributes;

namespace Communicator.Packets
{
    [NoConfirmation, Unignorable]
    public class DisconnectPacket : BasePacket<string>
    {
        public override string PacketData { get; set; } = "UnknownReason";
    }
}
