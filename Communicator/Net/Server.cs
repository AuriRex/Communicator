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
using static Communicator.Net.Client;

namespace Communicator.Net
{
    public partial class Server : IDisposable
    {
        /// <summary>
        /// Called every time a client has authenticated and is ready to communicate over the network.
        /// </summary>
        public event ClientConnectedEventHandler ClientConnectedEvent;
        /// <summary>
        /// Called every time a client disconnects.
        /// </summary>
        public event ClientDisconnectedEventHandler ClientDisconnectedEvent;

        /// <summary>
        /// Used to authentificate connecting clients.
        /// </summary>
        public IAuthentificationService AuthentificationService { get; set; } = new Auth.AuthService.None();
        // TODO: Use a clients GameIdentification to load the right PacketSerializer
        public PacketSerializer PacketSerializer { get; set; } = new PacketSerializer();

        public Action<string> LogAction { get; set; }
        public Action<string> ErrorLogAction { get; set; }

        private Thread _thread;
        private ManualResetEvent _shutdownEvent = new ManualResetEvent(false);
        private Dictionary<string, Client> _clients = new Dictionary<string, Client>();
        private List<Client> _connectingClients = new List<Client>();

        public Server()
        {
            _thread = new Thread(Run);
            _thread.Start();
        }

        public void StopServer()
        {
            _shutdownEvent.Set();
        }

        /// <summary>
        /// Tries to send a packet to a client with a specific <paramref name="serverID"/>
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

        public void RegisterCustomPacket<T>(/* string gameIdentification */) where T : IPacket
        {
            PacketSerializer.RegisterPacket<T>();
        }

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

                _connectingClients.Add(client);
            }

            Dispose();
        }

        private void OnClientDisconnect(ClientDisconnectedEventArgs e)
        {
            Client client = e.Client;

            if (!_clients.Any(x => x.Value == client)) return;

            string serverId = _clients.First(x => x.Value == client).Key;

            _clients.Remove(serverId);

            client.PacketReceivedEvent -= OnPacketReceived;
            client.DisconnectedEvent -= OnClientDisconnect;

            ClientDisconnectedEvent?.Invoke(e);
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
                        if (_clients.ContainsKey(identificationPacket.PacketData.ServerID) || _connectingClients.Any(cl => cl.InitialIdentificationPacket == null ? false : _clients.ContainsKey(cl.InitialIdentificationPacket?.PacketData.ServerID)))
                        {
                            ErrorLogAction?.Invoke($"Duplicate connection with ID '{identificationPacket.PacketData.ServerID}', dropping connection!");
                            DisconnectClient(client);
                            return;
                        }
                        break;
                    case ConfirmationPacket cp:
                        identificationPacket = client.InitialIdentificationPacket;

                        var clientEncryption = client.AsymmetricEncryptionProvider;

                        var symmetricalKey = clientEncryption.Decrypt(identificationPacket.PacketData.GetKey(), clientEncryption.GetKey(true), new byte[0]);
                        var symmetricalIV = clientEncryption.Decrypt(identificationPacket.PacketData.GetIV(), clientEncryption.GetKey(true), new byte[0]);
                        var passwordHashFirst = clientEncryption.Decrypt(Convert.FromBase64String(identificationPacket.PacketData.Base64PasswordHashFirst), clientEncryption.GetKey(true), new byte[0]);
                        var passwordHashSecond = clientEncryption.Decrypt(Convert.FromBase64String(identificationPacket.PacketData.Base64PasswordHashSecond), clientEncryption.GetKey(true), new byte[0]);
                        var passwordHash = Encoding.UTF8.GetString(passwordHashFirst.Concat(passwordHashSecond).ToArray());

                        if (!TryAuthenticateGameserver(passwordHash, identificationPacket.PacketData.ServerID, identificationPacket.PacketData.ServiceIdentification))
                        {
                            ErrorLogAction?.Invoke($"Invalid password for server with ID '{identificationPacket.PacketData.ServerID}', dropping connection!");
                            DisconnectClient(client);
                            return;
                        }

                        client.SetEncryption(new EncryptionProvider.S_AES());
                        client.SetEncryptionData(symmetricalKey, symmetricalIV);
                        client.AcceptAllPackets();

                        _clients.Add(client.InitialIdentificationPacket.PacketData.ServerID, client);
                        _connectingClients.Remove(client);

                        ClientConnectedEvent?.Invoke(new ClientConnectedEventArgs()
                        {
                            ServerID = identificationPacket.PacketData.ServerID,
                            Client = client,
                            GameName = identificationPacket.PacketData.ServiceIdentification
                        });

                        LogAction?.Invoke($"Client with ID '{identificationPacket.PacketData.ServerID}' '{identificationPacket.PacketData.ServiceIdentification}' connected!");
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
            client.SendPacket(new DisconnectPacket {
                PacketData = "Disconnected by Server."
            });
            client.StartDisconnect();
        }

        private bool TryAuthenticateGameserver(string passwordHash, string serverID, string gameIdentification)
        {
            try
            {
                return AuthentificationService.AuthenticateGameserver(passwordHash, serverID, gameIdentification);
            }
            catch(Exception ex)
            {
                ErrorLogAction?.Invoke($"An error occured while trying to authicate client with id '{serverID}': {ex.Message}\n{ex.StackTrace}");
            }
            return false;
        }

        public void Dispose()
        {
            foreach(KeyValuePair<string, Client> kvp in _clients)
            {
                var client = kvp.Value;
                client.StartDisconnect();
                LogAction?.Invoke($"Closing connection with client '{kvp.Key}'");
            }
        }
    }
}
