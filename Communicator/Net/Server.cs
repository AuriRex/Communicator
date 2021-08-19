using Communicator.Attributes;
using Communicator.Interfaces;
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
        public PacketSerializer PacketSerializer { get; set; } = new PacketSerializer();
        public event EventHandler<ClientConnectedEventArgs> ClientConnectedEvent;
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

            if(_connectingClients.Contains(client) && incomingPacket.GetType() == typeof(IdentificationPacket))
            {
                // Fully connect or drop client
                var identification = (IdentificationPacket) incomingPacket;
                bool disconnect = false;
                if(_clients.ContainsKey(identification.PacketData.ServerID))
                {
                    ErrorLogAction?.Invoke($"Duplicate connection with ID '{identification.PacketData.ServerID}', dropping connection!");
                    disconnect = true;
                }
                else
                {
                    LogAction?.Invoke($"Client with ID '{identification.PacketData.ServerID}' connected!");
                    _clients.Add(identification.PacketData.ServerID, client);
                    client.AcceptAllPackets();
                    ClientConnectedEvent?.Invoke(this, new ClientConnectedEventArgs() {
                        ServerID = identification.PacketData.ServerID,
                        Client = client,
                        GameName = identification.PacketData.GameName
                    });
                }

                _connectingClients.Remove(client);

                if(disconnect)
                {
                    client.SendPacket(new DisconnectPacket());
                    client.StartDisconnect();
                    client.Dispose();
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

    public class TestServer : Server 
    {
        

        public TestServer()
        {
            Client testClient = new Client(packetSerializer: PacketSerializer, logAction: (string s) => { Console.WriteLine($"[Client] {s}"); });

            testClient.PacketReceivedEvent += (object sender, Interfaces.IPacket e) => {
                Console.WriteLine($"[CLIENT] Received Packet '{e.GetType()}'");
                switch (e)
                {
                    case GenericEventPacket ge:
                        Console.WriteLine($"{ge.PacketData.Type} -> {ge.PacketData.Data}");
                        break;
                    case ConfirmationPacket ge:
                        Console.WriteLine($"{ge.PacketData.Hash}");
                        break;
                }
            };

            //Thread.Sleep(250);
            while (true)
            {
                Console.WriteLine("[Client] Sending packet ...");
                testClient.SendPacket(new ConfirmationPacket() {
                    PacketData = new ConfirmationData
                    {
                        Hash = "MyCoolHashFromTheClient"
                    }
                });
                Thread.Sleep(1000);
            }
        }

        public override void Run()
        {
            TcpListener server = new TcpListener(IPAddress.Any, 11000);
            server.Start();

            int count = 0;
            LogAction = (string s) => { Console.WriteLine($"[Server] {s}"); };

            while (true)
            {
                Client client = new Client(server.AcceptTcpClient(), PacketSerializer, LogAction);

                Console.WriteLine("[Server] Client connected!");

                client.PacketReceivedEvent += Client_PacketReceivedEvent;

                while (true)
                {
                    Console.WriteLine("[Server] sending packet ...");
                    client.SendPacket(new GenericEventPacket()
                    {
                        PacketData = new GenericEventPacket.EventData()
                        {
                            Type = "Chat",
                            Data = $"Num: {count}"
                        }
                    });
                    count++;
                    Thread.Sleep(1000);
                }
            }
        }

        private void Client_PacketReceivedEvent(object sender, Interfaces.IPacket e)
        {
            Console.WriteLine($"[SERVER] Received Packet '{e.GetType()}'");
            switch(e)
            {
                case GenericEventPacket ge:
                    Console.WriteLine($"{ge.PacketData.Type} -> {ge.PacketData.Data}");
                    break;
                case ConfirmationPacket ge:
                    Console.WriteLine($"{ge.PacketData.Hash}");
                    break;
            }
        }
    }
}
