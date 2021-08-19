using Communicator.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Communicator.Packets
{
    public class BasePacket<T> : IPacket
    {
        public DateTimeOffset EventTime { get; set; } = DateTimeOffset.Now;
        public string PacketType { get; set; }
        public virtual T PacketData { get; set; }
        public bool IsLate { get; set; } = false;
    }

    internal class DummyPacket : BasePacket<dynamic>
    {
        public override dynamic PacketData { get; set; }
    }
}
