using System;
using System.Collections.Generic;
using System.Text;

namespace Communicator.Interfaces
{
    public interface IPacket
    {
        public DateTimeOffset EventTime { get; set; }
        public string PacketType { get; set; }
        public bool IsLate { get; set; }
    }
}
