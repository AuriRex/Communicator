using Communicator.Attributes;
using Communicator.Interfaces;
using Communicator.Net.Encryption;
using Communicator.Packets;
using Communicator.Utils.Extensions;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Communicator.Net
{
    public class GameserverClient : Client 
    {
        public bool Connected { get; private set; } = false;

        private string _serverId;
        private string _serviceIdentification;
        private string _passwordHash;
        private IdentificationPacket _identificationPacket;
        private EncryptionProvider.S_AES _aesEncryptionInstance = new EncryptionProvider.S_AES();

        public static GameserverClient Create(GSConfig config, string serviceIdentification, PacketSerializer packetSerializer, Action<string> logAction = null)
        {
            if(config.IsIncomplete)
            {
                logAction?.Invoke("Incomplete config file. Please setup your password, serverId, etc. and try again!");
                return null;
            }

            var client = new GameserverClient(config.Hostname, config.Port, config.ServerId, serviceIdentification, config.Password, config.Base64Salt, packetSerializer, logAction);
            client.OnlyAcceptPacketsOfType(typeof(ConfirmationPacket));
            client.SetAsymmetricalEncryptionProvider(new EncryptionProvider.A_RSA());


            return client;
        }

        public void Disconnect()
        {
            StartDisconnect();
        }

        public GameserverClient(string hostname, int port, string serverId, string serviceIdentification, string password, string base64salt, PacketSerializer packetSerializerPrefab = null, Action<string> logAction = null) : base(new TcpClient(hostname, port), packetSerializerPrefab, logAction)
        {            
            if(string.IsNullOrEmpty(serverId) || string.IsNullOrEmpty(serviceIdentification) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(base64salt))
            {
                throw new ArgumentException("Arguments may not be null or empty!");
            }

            _serverId = serverId;
            _serviceIdentification = serviceIdentification;
            
            _passwordHash = Utils.Utils.ToHexString(Utils.Utils.HashPassword(password, Convert.FromBase64String(base64salt)));

            //Console.WriteLine(_passwordHash);

            //this.OnlyAcceptPacketsOfType(typeof(ConfirmationPacket));

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
                        if (Connected) return;

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

                        this.SetEncryption(_aesEncryptionInstance);
                        this.SetEncryptionData(_aesEncryptionInstance.GetKey(true), _aesEncryptionInstance.GetIV());

                        this.SendPacketUnencrypted(_identificationPacket);

                        return;
                    case ConfirmationPacket confirmationPacket:
                        if (Connected) return;

                        string identificationPacketHash = Utils.Utils.HashPacket(_identificationPacket);

                        if (confirmationPacket.PacketData.Hash != identificationPacketHash)
                        {
                            LogAction?.Invoke($"Confirmation Hash doesn't match, dropping connection!");
                            this.StartDisconnect();
                            return;
                        }
                       
                        this.AcceptAllPackets();

                        confirmationPacket.ConfirmPacket(this);

                        Connected = true;
                        return;
                    /*case SwitchEncryptionPacket encryptionPacket:
                        if (Connected) return;
                        string identificationPacketHash = Utils.Utils.HashPacket(_identificationPacket);

                        if (encryptionPacket.PacketData.ConfirmationHash != identificationPacketHash)
                        {
                            LogAction?.Invoke($"Confirmation Hash doesn't match, dropping connection!");
                            this.StartDisconnect();
                            return;
                        }

                        this.SetEncryption(_aesEncryptionInstance);
                        this.SetEncryptionData(_aesEncryptionInstance.GetKey(true), _aesEncryptionInstance.GetIV());
                        this.AcceptAllPackets();

                        Connected = true;
                        break;*/
                    default:
                        return;
                }
            }

            if (!incomingPacket.GetType().CustomAttributes.Any(x => x.AttributeType == typeof(NoConfirmationAttribute)))
            {
                incomingPacket.ConfirmPacket(this);
            }

            base.OnPacketReceived(sender, incomingPacket);
        }

        public class GSConfig
        {
            [JsonIgnore]
            public bool IsIncomplete
            {
                get
                {
                    if (string.IsNullOrEmpty(Hostname)) return true;
                    if (string.IsNullOrEmpty(ServerId)) return true;
                    if (string.IsNullOrEmpty(Password)) return true;

                    return false;
                }
            }

            public string Hostname { get; set; } = string.Empty;
            public int Port { get; set; } = 0;
            public string ServerId { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string Base64Salt { get; set; } = string.Empty;

            internal GSConfig()
            {

            }

            public GSConfig(string hostname, int port, string serverId, string password, string base64salt)
            {
                if (string.IsNullOrEmpty(base64salt))
                {
                    // Generate new Salt
                    base64salt = Convert.ToBase64String(Utils.Utils.GenerateSalt());
                }

                if (string.IsNullOrEmpty(serverId) || string.IsNullOrEmpty(hostname) || string.IsNullOrEmpty(base64salt))
                {
                    throw new ArgumentException("Arguments may not be null or empty!");
                }

                Hostname = hostname;
                Port = port;
                ServerId = serverId;
                Password = password;
                Base64Salt = base64salt;
            }

            public byte[] GetSalt()
            {
                return Convert.FromBase64String(Base64Salt);
            }

            public string GetPasswordHash()
            {
                return Utils.Utils.ToHexString(Utils.Utils.HashPassword(Password, GetSalt()));
            }


            private static JsonSerializerSettings _jsonSettings { get; set; } = new JsonSerializerSettings { Formatting = Formatting.Indented };

            public static GSConfig LoadFromJson(string json)
            {
                return JsonConvert.DeserializeObject<GSConfig>(json, _jsonSettings);
            }

            public static GSConfig LoadFromFile(string path)
            {
                try
                {
                    var json = File.ReadAllText(Path.GetFullPath(path));
                    return LoadFromJson(json);
                }
                catch (Exception)
                {
                    throw new ArgumentException($"Error loading config from path '{path}'");
                }
            }

            public static string SaveAsJson(GSConfig config)
            {
                return JsonConvert.SerializeObject(config, _jsonSettings);
            }

            public static void SaveToFile(string path, GSConfig config)
            {
                var dir = new FileInfo(path).Directory.FullName;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(Path.GetFullPath(path), SaveAsJson(config));
            }
        }

    }
}
