namespace Communicator.Packets
{
    public class GenericEventPacket : BasePacket<GenericEventPacket.EventData>
    {
        public override EventData PacketData { get; set; } = new EventData() {
            Type = "UnsetGenericEventType",
            Data = "UnsetGenericEventData"
        };

        public struct EventData
        {
            public string Type { get; set; }
            public string Data { get; set; }
        }
    }
}
