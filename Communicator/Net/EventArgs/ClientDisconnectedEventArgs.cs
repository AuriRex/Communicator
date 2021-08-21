using Communicator.Interfaces;

namespace Communicator.Net.EventArgs
{
    public class ClientDisconnectedEventArgs
    {
        public bool IsIntentional { get; set; } = false;
        public IPacket Packet { get; set; } = null;
        public Client Client { get; set; }
    }
}
