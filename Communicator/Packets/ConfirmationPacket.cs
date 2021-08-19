namespace Communicator.Packets
{
    public class ConfirmationPacket : BasePacket<ConfirmationData>
    {
        public override ConfirmationData PacketData { get; set; } = new ConfirmationData() { Hash = "HashNotSet" };
    }

    public struct ConfirmationData
    {
        public string Hash { get; set; }
    }
}
