using Communicator.Interfaces;
using Communicator.Net;
using Communicator.Packets;

namespace Communicator.Utils.Extensions
{
    public static class PacketExtensions
    {

        public static void ConfirmPacket(this IPacket packet, Client client)
        {
            client.SendPacket(new ConfirmationPacket() {
                PacketData = new ConfirmationData
                {
                    Hash = Utils.HashPacket(packet)
                }
            });
        }

        public static void ConfirmPacketUnencrypted(this IPacket packet, Client client)
        {
            client.SendPacketUnencrypted(new ConfirmationPacket()
            {
                PacketData = new ConfirmationData
                {
                    Hash = Utils.HashPacket(packet)
                }
            });
        }

    }
}
