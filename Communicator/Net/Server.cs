using Communicator.Attributes;
using Communicator.Interfaces;
using Communicator.Net.Encryption;
using Communicator.Net.EventArgs;
using Communicator.Packets;
using Communicator.Utils.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

        public bool DisallowExternalHosts { get; set; } = false;

        public Action<string> LogAction { get; set; }
        public Action<string> ErrorLogAction { get; set; }

        private Task _serverTask;
        protected TcpListener tcpListener;
        protected ManualResetEvent shutdownEvent = new ManualResetEvent(false);
        protected Dictionary<string, Client> clients = new Dictionary<string, Client>();
        protected List<Client> connectingClients = new List<Client>();

        public Server()
        {

        }

        public void StartServer()
        {
            _serverTask = Task.Run(Run);
        }

        public void StopServer()
        {
            shutdownEvent.Set();
        }

        /// <summary>
        /// Tries to send a packet to a client with a specific <paramref name="serverID"/>
        /// </summary>
        /// <param name="serverID"></param>
        /// <param name="packet"></param>
        /// <returns>returns <paramref name="true"/> if successful</returns>
        public bool TrySendPacket(string serverID, IPacket packet)
        {
            if (clients.TryGetValue(serverID, out Client client))
            {
                client.SendPacket(packet);
                return true;
            }

            return false;
        }

        public void WaitStopServer()
        {
            WaitDispose();
        }

        [Obsolete]
        public void RegisterCustomPacket<T>(/* string gameIdentification */) where T : IPacket
        {
            PacketSerializer.RegisterPacket<T>();
        }

        public virtual void Run()
        {
            try
            {
                TcpListener server = new TcpListener(IPAddress.Any, 11000);
                this.tcpListener = server;
                server.Start();

                LogAction?.Invoke("Server task started!");

                while (!shutdownEvent.WaitOne(0))
                {
                    try
                    {
                        Client client = new Client(server.AcceptTcpClient(), PacketSerializer, LogAction);

                        if (!AllowClientToConnect(client))
                        {
                            LogAction?.Invoke($"Refused client connection: {client.GetRemoteEndpoint().Address}:{client.GetRemoteEndpoint().Port}");
                            client.StartDisconnect();
                            return;
                        }

                        TryConnectClient(client);
                    }
                    catch (Exception ex)
                    {
                        ErrorLogAction?.Invoke($"{ex}: {ex.Message}\n{ex.StackTrace}");
                    }
                    finally
                    {
                        Thread.Sleep(1);
                    }
                }

                Dispose();
            }
            catch (Exception ex)
            {
                ErrorLogAction?.Invoke($"Server task: {ex}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public virtual void TryConnectClient(Client client)
        {
            client.ErrorAction = ErrorLogAction;

            LogAction?.Invoke($"Client connecting ... ({client.GetRemoteEndpoint().Address}:{client.GetRemoteEndpoint().Port})");

            client.OnlyAcceptPacketsOfType(typeof(IdentificationPacket));

            client.PacketReceivedEvent += OnPacketReceived;
            client.DisconnectedEvent += OnClientDisconnect;

            client.SendPacket(new InitialPublicKeyPacket()
            {
                PacketData = InitialPublicKeyPacket.KeyData.CreateKeyData(client.AsymmetricEncryptionProvider.GetKey(false))
            });

            connectingClients.Add(client);
        }

        public virtual bool AllowClientToConnect(Client client)
        {
            if(DisallowExternalHosts && client.GetRemoteEndpoint().Address.ToString() != ((IPEndPoint) tcpListener.LocalEndpoint).Address.ToString())
            {
                return false;
            }

            return true;
        }

        private void OnClientDisconnect(ClientDisconnectedEventArgs e)
        {
            Client client = e.Client;

            if (!clients.Any(x => x.Value == client)) return;

            string serverId = clients.First(x => x.Value == client).Key;

            clients.Remove(serverId);

            client.PacketReceivedEvent -= OnPacketReceived;
            client.DisconnectedEvent -= OnClientDisconnect;

            ClientDisconnectedEvent?.Invoke(e);
        }

        private byte[] _symmetricalKey;
        private byte[] _symmetricalIV;

        public void OnPacketReceived(object sender, IPacket incomingPacket)
        {
            Client client = (Client) sender;
            IdentificationPacket identificationPacket = client.InitialIdentificationPacket;
            if (connectingClients.Contains(client))
            {
                switch (incomingPacket)
                {
                    case IdentificationPacket ip:
                        if (client.Status != ConnectionStatus.PreConnecting) throw new Exception($"Client sent wrong packet \"{nameof(IdentificationPacket)}\" during connection attempt! ({ConnectionStatus.PreConnecting} != {client.Status})");

                        // Fully connect or drop client
                        identificationPacket = ip;
                        if (clients.ContainsKey(identificationPacket.PacketData.ServerID) || connectingClients.Any(cl => cl.InitialIdentificationPacket == null ? false : clients.ContainsKey(cl.InitialIdentificationPacket?.PacketData.ServerID)))
                        {
                            ErrorLogAction?.Invoke($"Duplicate connection with ID '{identificationPacket.PacketData.ServerID}', dropping connection!");
                            DisconnectClient(client);
                            return;
                        }
                        
                        var clientEncryption = client.AsymmetricEncryptionProvider;

                        _symmetricalKey = clientEncryption.Decrypt(identificationPacket.PacketData.GetKey(), clientEncryption.GetKey(true), new byte[0]);
                        _symmetricalIV = clientEncryption.Decrypt(identificationPacket.PacketData.GetIV(), clientEncryption.GetKey(true), new byte[0]);
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
                        client.SetEncryptionData(_symmetricalKey, _symmetricalIV);


                        ip.ConfirmPacket(client);

                        client.Status = ConnectionStatus.Connecting;

                        return;
                    case ConfirmationPacket cp:
                        if (client.Status != ConnectionStatus.Connecting) throw new Exception($"Client sent wrong packet \"{nameof(ConfirmationPacket)}\" during connection attempt! ({ConnectionStatus.Connecting} != {client.Status})");

                        client.AcceptAllPackets();
                        clients.Add(client.InitialIdentificationPacket.PacketData.ServerID, client);
                        connectingClients.Remove(client);

                        ClientConnectedEvent?.Invoke(new ClientConnectedEventArgs()
                        {
                            ServerID = identificationPacket.PacketData.ServerID,
                            Client = client,
                            ServiceName = identificationPacket.PacketData.ServiceIdentification
                        });

                        client.Status = ConnectionStatus.Connected;

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
            foreach(KeyValuePair<string, Client> kvp in clients)
            {
                var client = kvp.Value;
                LogAction?.Invoke($"Closing connection with client '{kvp.Key}'");
                client.StartDisconnect();
            }
        }

        private void WaitDispose()
        {
            foreach (KeyValuePair<string, Client> kvp in clients)
            {
                var client = kvp.Value;
                LogAction?.Invoke($"Closing connection with client '{kvp.Key}'");
                client.WaitDispose();
            }
        }
    }
}
