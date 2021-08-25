using Communicator.Attributes;
using Communicator.Interfaces;
using Communicator.Net.Encryption;
using Communicator.Packets;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Communicator.Net
{
    public class GameserverClient : Client 
    {
        public bool Connected { get; private set; } = false;
        public static PacketSerializer PacketSerializer { get; set; } = new PacketSerializer();

        private string _serverId;
        private string _serviceIdentification;
        private string _passwordHash;
        private IdentificationPacket _identificationPacket;
        private EncryptionProvider.S_AES _aesEncryptionInstance = new EncryptionProvider.S_AES();

        public GameserverClient(string hostname, int port, string serverId, string serviceIdentification, string password, string base64salt, Action<string> logAction = null) : base(new TcpClient(hostname, port), PacketSerializer, logAction)
        {            
            if(string.IsNullOrEmpty(serverId) || string.IsNullOrEmpty(serviceIdentification) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(base64salt))
            {
                throw new ArgumentException("Arguments may not be null or empty!");
            }

            _serverId = serverId;
            _serviceIdentification = serviceIdentification;
            
            _passwordHash = Utils.Utils.ToHexString(Utils.Utils.HashPassword(password, Convert.FromBase64String(base64salt)));

            //Console.WriteLine(_passwordHash);

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
                        Utils.Utils.SplitMidPoint<byte>(Encoding.UTF8.GetBytes(_passwordHash), out byte[] pwBytesFirst, out byte[] pwBytesSecond);

                        //Console.WriteLine($"{_aesEncryptionInstance.GetIV().Length} {pwBytesFirst.Length}");

                        var encryptedPWHashFirst = AsymmetricEncryptionProvider.Encrypt(pwBytesFirst, publicKey, new byte[0]);
                        var encryptedPWHashSecond = AsymmetricEncryptionProvider.Encrypt(pwBytesSecond, publicKey, new byte[0]);
                        _identificationPacket = new IdentificationPacket()
                        {
                            PacketData = IdentificationData.CreateKeyData(_serverId, _serviceIdentification, encryptedKey, encryptedIV, encryptedPWHashFirst, encryptedPWHashSecond)
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
