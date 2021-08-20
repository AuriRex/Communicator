using Communicator.Attributes;
using Communicator.Interfaces;
using Communicator.Net.Encryption;
using Communicator.Net.EventArgs;
using Communicator.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Communicator.Net
{
    public partial class Server : IDisposable
    {
        public delegate void ClientConnectedEventHandler(ClientConnectedEventArgs args);
        public event ClientConnectedEventHandler ClientConnectedEvent;

        public PacketSerializer PacketSerializer { get; set; } = new PacketSerializer();
        //public event EventHandler<ClientConnectedEventArgs> ClientConnectedEvent;
        public Action<string> LogAction { get; set; }
        public Action<string> ErrorLogAction { get; set; }

        private Thread _thread;
        private ManualResetEvent _shutdownEvent = new ManualResetEvent(false);
        private Dictionary<string, Client> _clients = new Dictionary<string, Client>();
        private List<Client> _connectingClients = new List<Client>();

        /// <summary>
        /// Tries to send a packet to a specific client
        /// </summary>
        /// <param name="serverID"></param>
        /// <param name="packet"></param>
        /// <returns>returns <paramref name="true"/> if successful</returns>
        public bool TrySendPacket(string serverID, IPacket packet)
        {
            if (_clients.TryGetValue(serverID, out Client client))
            {
                client.SendPacket(packet);
                return true;
            }

            return false;
        } 

        public void StopServer()
        {
            _shutdownEvent.Set();
            Dispose();
        }

        public Server()
        {
            _thread = new Thread(Run);
            _thread.Start();
        }

        //EncryptionProvider.A_RSA _rsaEncryptionInstance = new EncryptionProvider.A_RSA();

        public virtual void Run()
        {
            TcpListener server = new TcpListener(IPAddress.Any, 11000);
            server.Start();

            LogAction?.Invoke("Server thread started!");

            while (!_shutdownEvent.WaitOne(0))
            {
                Client client = new Client(server.AcceptTcpClient(), PacketSerializer, LogAction);

                client.ErrorAction = ErrorLogAction;

                LogAction?.Invoke($"Client connecting ... ({client.GetRemoteEndpoint().Address}:{client.GetRemoteEndpoint().Port})");

                client.OnlyAcceptPacketsOfType(typeof(IdentificationPacket));

                client.PacketReceivedEvent += OnPacketReceived;
                client.DisconnectedEvent += OnClientDisconnect;

                client.SendPacket(new InitialPublicKeyPacket() {
                    PacketData = InitialPublicKeyPacket.KeyData.CreateKeyData(client.AsymmetricEncryptionProvider.GetKey(false))
                });

                //client.SetAsymmetricalEncryptionProvider(_rsaEncryptionInstance);
                /*client.SetEncryption(_rsaEncryptionInstance);
                client.SetEncryptionData(_rsaEncryptionInstance.GetKey(true), _rsaEncryptionInstance.GetIV());*/

                //client.PacketReceivedEvent += Client_PacketReceivedEvent;

                //_clients.Add(client);
                _connectingClients.Add(client);
            }
        }

        private void OnClientDisconnect(object sender, ClientDisconnectedEventArgs e)
        {
            Client client = (Client) sender;

            if (!_clients.Any(x => x.Value == client)) return;

            string serverId = _clients.First(x => x.Value == client).Key;

            _clients.Remove(serverId);

            client.PacketReceivedEvent -= OnPacketReceived;
            client.DisconnectedEvent -= OnClientDisconnect;
        }

        public void OnPacketReceived(object sender, IPacket incomingPacket)
        {
            Client client = (Client) sender;
            IdentificationPacket identificationPacket;
            if (_connectingClients.Contains(client))
            {
                switch(incomingPacket)
                {
                    case IdentificationPacket ip:
                        // Fully connect or drop client
                        identificationPacket = (IdentificationPacket) incomingPacket;
                        if (_clients.ContainsKey(identificationPacket.PacketData.ServerID))
                        {
                            ErrorLogAction?.Invoke($"Duplicate connection with ID '{identificationPacket.PacketData.ServerID}', dropping connection!");
                            DisconnectClient(client);
                            return;
                        }
                        break;
                    case ConfirmationPacket cp:
                        identificationPacket = client.InitialIdentificationPacket;
                        _clients.Add(client.InitialIdentificationPacket.PacketData.ServerID, client);
                        _connectingClients.Remove(client);
                        client.AcceptAllPackets();
                        ClientConnectedEvent?.Invoke(new ClientConnectedEventArgs()
                        {
                            ServerID = identificationPacket.PacketData.ServerID,
                            Client = client,
                            GameName = identificationPacket.PacketData.GameIdentification
                        });

                        var clientEncryption = client.AsymmetricEncryptionProvider;

                        var symmetricalKey = clientEncryption.Decrypt(identificationPacket.PacketData.GetKey(), clientEncryption.GetKey(true), new byte[0]);
                        var symmetricalIV = clientEncryption.Decrypt(identificationPacket.PacketData.GetIV(), clientEncryption.GetKey(true), new byte[0]);
                        var passwordHashFirst = clientEncryption.Decrypt(Convert.FromBase64String(identificationPacket.PacketData.Base64PasswordHashFirst), clientEncryption.GetKey(true), new byte[0]);
                        var passwordHashSecond = clientEncryption.Decrypt(Convert.FromBase64String(identificationPacket.PacketData.Base64PasswordHashSecond), clientEncryption.GetKey(true), new byte[0]);
                        var passwordHash = Encoding.UTF8.GetString(passwordHashFirst.Concat(passwordHashSecond).ToArray());

                        if (!TryAuthenticateGameserver(passwordHash, identificationPacket.PacketData.ServerID, identificationPacket.PacketData.GameIdentification))
                        {
                            ErrorLogAction?.Invoke($"Invalid password for server with ID '{identificationPacket.PacketData.ServerID}', dropping connection!");
                            DisconnectClient(client);
                            return;
                        }

                        client.SetEncryption(new Encryption.EncryptionProvider.S_AES());
                        client.SetEncryptionData(symmetricalKey, symmetricalIV);
                        LogAction?.Invoke($"Client with ID '{identificationPacket.PacketData.ServerID}' '{identificationPacket.PacketData.GameIdentification}' connected!");
                        return;
                }
                
            }

            if (!incomingPacket.GetType().CustomAttributes.Any(x => x.AttributeType == typeof(NoConfirmationAttribute)))
            {
                client.SendPacket(new ConfirmationPacket()
                {
                    PacketData = new ConfirmationData()
                    {
                        Hash = Utils.Utils.HashPacket(incomingPacket)
                    }
                });
            }
        }

        private void DisconnectClient(Client client)
        {
            client.SetEncryption(Encryption.EncryptionProvider.NONE);
            client.SendPacket(new DisconnectPacket());
            client.StartDisconnect();
            client.Dispose();
        }

        private bool TryAuthenticateGameserver(string passwordHash, string serverID, string gameIdentification)
        {
            // TODO


            ErrorLogAction?.Invoke($"TODO, IMPLEMENT THIS: {nameof(TryAuthenticateGameserver)}");
            return true;
        }

        public void Dispose()
        {
            foreach(KeyValuePair<string, Client> kvp in _clients)
            {
                var client = kvp.Value;
                client.StartDisconnect();
                //client.Dispose();
                LogAction?.Invoke($"Closing connection with client '{kvp.Key}'");
            }
        }
    }
}
