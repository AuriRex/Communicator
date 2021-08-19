using Communicator.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Communicator.Net
{
    public class ValidationEntry
    {
        public ValidationEntry(IPacket packet)
        {
            TimeSent = DateTimeOffset.UtcNow;
            Hash = Utils.Utils.HashPacket(packet);
            Packet = packet;
        }

        public string Hash { get; private set; }
        public IPacket Packet { get; private set; }
        public DateTimeOffset TimeSent { get; private set; }
    }
}
