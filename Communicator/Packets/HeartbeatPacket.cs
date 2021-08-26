using Communicator.Attributes;

namespace Communicator.Packets
{
    [NoConfirmation]
    public class HeartbeatPacket : BasePacket<char>
    {
        public override char PacketData { get; set; } = 'h';
    }
}
