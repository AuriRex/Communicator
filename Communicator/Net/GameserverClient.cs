using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using Communicator.Interfaces;
using Communicator.Packets;
using System.Linq;
using Communicator.Attributes;
using Communicator.Net.Encryption;

namespace Communicator.Net
{
    public class GameserverClient : Client 
    {
        public bool Connected { get; private set; } = false;
        public static PacketSerializer PacketSerializer { get; set; } = new PacketSerializer();

        private string _serverId;
        private string _gameName;
        private IdentificationPacket _identificationPacket;
        private EncryptionProvider.S_AES _aesEncryptionInstance = new EncryptionProvider.S_AES();

        public GameserverClient(string hostname, int port, string serverId, string gameName, Action<string> logAction = null) : base(new TcpClient(hostname, port), PacketSerializer, logAction)
        {
            _serverId = serverId;
            _gameName = gameName;
            this.OnlyAcceptPacketsOfType(typeof(ConfirmationPacket));

            SetAsymmetricalEncryptionProvider(new EncryptionProvider.A_RSA());
        }

        public void RegisterPacket<T>() where T : IPacket
        {
            PacketSerializer.RegisterPacket<T>();
        }

        protected override void OnPacketReceived(object sender, IPacket incomingPacket)
        {
            if (!Connected)
            {
                switch(incomingPacket)
                {
                    case InitialPublicKeyPacket ipkp:

                        byte[] publicKey = ipkp.PacketData.GetKey();

                        var encryptedKey = AsymmetricEncryptionProvider.Encrypt(_aesEncryptionInstance.GetKey(false), publicKey, new byte[0]);
                        var encryptedIV = AsymmetricEncryptionProvider.Encrypt(_aesEncryptionInstance.GetIV(), publicKey, new byte[0]);
                        _identificationPacket = new IdentificationPacket()
                        {
                            PacketData = IdentificationData.CreateKeyData(_serverId, _gameName, encryptedKey, encryptedIV)
                        };

                        this.SendPacket(_identificationPacket);

                        break;
                    case ConfirmationPacket confirmationPacket:
                        string identificationPacketHash = Utils.Utils.HashPacket(_identificationPacket);

                        if (confirmationPacket.PacketData.Hash != identificationPacketHash)
                        {
                            LogAction?.Invoke($"Confirmation Hash doesn't match, dropping connection!");
                            this.StartDisconnect();
                            return;
                        }

                        this.SetEncryption(_aesEncryptionInstance);
                        this.SetEncryptionData(_aesEncryptionInstance.GetKey(true), _aesEncryptionInstance.GetIV());
                        this.AcceptAllPackets();
                        Connected = true;
                        return;
                    default:
                        return;
                }
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
