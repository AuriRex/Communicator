using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using Communicator.Interfaces;
using Communicator.Packets;
using System.Linq;
using Communicator.Attributes;

namespace Communicator.Net
{
    public class GameserverClient : Client 
    {
        public bool Connected { get; private set; } = false;
        public static PacketSerializer PacketSerializer { get; set; } = new PacketSerializer();

        private IdentificationPacket _identificationPacket;

        public GameserverClient(string hostname, int port, string serverId, string gameName, Action<string> logAction = null) : base(new TcpClient(hostname, port), PacketSerializer, logAction)
        {
            this.OnlyAcceptPacketsOfType(typeof(ConfirmationPacket));
            _identificationPacket = new IdentificationPacket()
            {
                PacketData = new IdentificationData
                {
                    ServerID = serverId,
                    GameName = gameName
                }
            };
            this.SendPacket(_identificationPacket);
        }

        public void RegisterPacket<T>() where T : IPacket
        {
            PacketSerializer.RegisterPacket<T>();
        }

        protected override void OnPacketReceived(object sender, IPacket incomingPacket)
        {
            if (!Connected)
            {
                if (incomingPacket.GetType() != typeof(ConfirmationPacket)) return;

                var confirmationPacket = (ConfirmationPacket) incomingPacket;

                string outgoingPacketHash = Utils.Utils.HashPacket(_identificationPacket);

                if(confirmationPacket.PacketData.Hash != outgoingPacketHash)
                {
                    LogAction?.Invoke($"Confirmation Hash doesn't match, dropping connection!");
                    this.StartDisconnect();
                    return;
                }

                this.AcceptAllPackets();
                Connected = true;
            }

            if (!incomingPacket.GetType().CustomAttributes.Any(x => x.AttributeType == typeof(NoConfirmationAttribute)))
            {
                this.SendPacket(new ConfirmationPacket()
                {
                    PacketData = new ConfirmationData()
                    {
                        Hash = Utils.Utils.HashPacket(incomingPacket)
                    }
                });
            }

            base.OnPacketReceived(sender, incomingPacket);
        }

    }
}
