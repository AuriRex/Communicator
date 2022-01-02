using Communicator.Attributes;
using Communicator.Interfaces;
using Communicator.Net.Encryption;
using Communicator.Net.EventArgs;
using Communicator.Packets;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Communicator.Net
{
    public class Client : IDisposable
    {
        public delegate void ClientConnectedEventHandler(ClientConnectedEventArgs args);
        public delegate void ClientDisconnectedEventHandler(ClientDisconnectedEventArgs args);

        // Consumers register to receive data.
        public virtual event EventHandler<IPacket> PacketReceivedEvent;
        public virtual event EventHandler<IPacket> IgnoredPacketReceivedEvent;
        public virtual event ClientDisconnectedEventHandler DisconnectedEvent;
        public Action<string> LogAction
        {
            get => _logAction;
            set
            {
                _logAction = value;
                _receiver.LogAction = value;
                _sender.LogAction = value;
            }
        }
        public Action<string> ErrorAction
        {
            get => _errorLogAction;
            set
            {
                _errorLogAction = value;
                _receiver.ErrorAction = value;
                _sender.ErrorAction = value;
            }
        }

        internal IdentificationPacket InitialIdentificationPacket { get; set; } = null;

        public PacketSerializer PacketSerializer { get; protected set; }
        protected IEncryptionProvider EncryptionProvider
        {
            get
            {
                return _encryptionProvider;
            }
            set
            {
                _encryptionProvider = value;
                _sender.EncryptionProvider = value;
                _receiver.EncryptionProvider = value;
            }
        } 

        // Every client gets their own
        internal IEncryptionProvider AsymmetricEncryptionProvider { get; private set; } = new EncryptionProvider.A_RSA();

        private IEncryptionProvider _encryptionProvider = new Encryption.EncryptionProvider.None();
        private Action<string> _logAction;
        private Action<string> _errorLogAction;
        private TcpClient _client;
        private NetworkStream _stream;
        private Shared.Receiver _receiver;
        private Shared.Sender _sender;
        private ManualResetEvent _shutdownEvent = new ManualResetEvent(false);
        private Type _packetTypeToWaitFor = null;
        private bool _disconnectEventRaised = false;
        private bool _intentionalDisconnect = false;

        public void SendPacket(IPacket packet)
        {
            _sender.QueuePacket(packet);
        }

        public void Connect(string hostname, int port)
        {
            _client.Connect(hostname, port);
        }

        public IPEndPoint GetRemoteEndpoint()
        {
            return (IPEndPoint) _client.Client.RemoteEndPoint;
        }

        internal void SetAsymmetricalEncryptionProvider(IEncryptionProvider encryptionProvider)
        {
            AsymmetricEncryptionProvider = encryptionProvider;
        }

        /// <summary>
        /// Drops all packets except with Type <paramref name="type"/>
        /// <para>Use <see cref="AcceptAllPackets"/> to enable all packets again.</para>
        /// </summary>
        /// <param name="type"></param>
        public void OnlyAcceptPacketsOfType(Type type)
        {
            _packetTypeToWaitFor = type;
        }

        /// <summary>
        /// Enables all packets to be received again.
        /// <para>Use <see cref="OnlyAcceptPacketsOfType(Type)"/> to only allow the packet with that type.</para>
        /// </summary>
        public void AcceptAllPackets()
        {
            _packetTypeToWaitFor = null;
        }

        internal void SetEncryption(IEncryptionProvider provider)
        {
            this.EncryptionProvider = provider;
        }

#nullable enable
        internal void SetEncryptionData(byte[]? key, byte[]? iv = null)
        {
            SetSenderEncryptionData(key, iv);
            SetReceiverEncryptionData(key, iv);
        }
        protected void SetSenderEncryptionData(byte[]? key, byte[]? iv = null)
        {
            _sender.KeyBytes = key ?? new byte[0];
            _sender.IVBytes = iv ?? new byte[0];
        }
        protected void SetReceiverEncryptionData(byte[]? key, byte[]? iv = null)
        {
            _receiver.KeyBytes = key ?? new byte[0];
            _receiver.IVBytes = iv ?? new byte[0];
        }
#nullable restore

        public Client(TcpClient client = null, PacketSerializer packetSerializer = null, Action<string> logAction = null)
        {
            this.PacketSerializer = packetSerializer ?? new PacketSerializer();
            _logAction = logAction;

            _client = client ?? new TcpClient("localhost", 11000);
            _stream = _client.GetStream();

            _logAction?.Invoke($"Client created.");

            _receiver = new Shared.Receiver(_stream, _shutdownEvent, this.PacketSerializer, logAction);
            _sender = new Shared.Sender(_stream, _shutdownEvent, this.PacketSerializer, logAction);

            _receiver.PacketReceived += OnPacketReceived;
            _receiver.ThreadFinished += OnSubThreadsExited;
            _sender.ThreadFinished += OnSubThreadsExited;
            _sender.DisconnectedEvent += RaiseDisconnectEvent;
        }


        private void RaiseDisconnectEvent(object sender, ClientDisconnectedEventArgs e)
        {
            if (_disconnectEventRaised) return;
            _disconnectEventRaised = true;
            e.IsIntentional = _intentionalDisconnect;
            e.Client = this;
            DisconnectedEvent?.Invoke(e);
        }

        internal void StartDisconnect()
        {
            _shutdownEvent.Set();
            _intentionalDisconnect = true;
        }

        internal void OnSubThreadsExited()
        {
            if(_sender.HasExited && _receiver.HasExited)
            {
                _stream.Close();
                _client.Close();
                RaiseDisconnectEvent(this, new ClientDisconnectedEventArgs()
                {
                    IsIntentional = true,
                    Packet = null
                });
                Dispose();
            }
        }

        protected virtual void OnPacketReceived(object sender, IPacket packet)
        {
            switch(packet)
            {
                case IdentificationPacket ip:
                    if(InitialIdentificationPacket == null)
                        InitialIdentificationPacket = ip;
                    break;
                case DisconnectPacket dp:
                    StartDisconnect();
                    return;
                case ReconnectPacket rp:
                    StartDisconnect();
                    // TODO ?
                    return;
                case HeartbeatPacket hp:
                    return;
            }

            if (!packet.GetType().CustomAttributes.Any(x => x.AttributeType == typeof(UnignorableAttribute)) && (_packetTypeToWaitFor != null && packet.GetType() != _packetTypeToWaitFor))
            {
                IgnoredPacketReceivedEvent?.Invoke(this, packet);
                return;
            }
            
            PacketReceivedEvent?.Invoke(this, packet);
        }

        internal void WaitDispose(bool sendDisconnectPacket = true)
        {
            if(sendDisconnectPacket)
            {
                SendPacket(new DisconnectPacket()
                {
                    PacketData = "Shutting Down."
                });

                Task.Delay(100).Wait();
            }

            _sender.WaitDispose();
            _receiver.WaitDispose();
        }

        public void Dispose()
        {
            _shutdownEvent.Set();
            if(_receiver != null)
            {
                _receiver.PacketReceived -= OnPacketReceived;
                _receiver.ThreadFinished -= OnSubThreadsExited;
            }
            if (_sender != null)
            {
                _sender.ThreadFinished -= OnSubThreadsExited;
            }

            this.PacketReceivedEvent = null;
            this.IgnoredPacketReceivedEvent = null;
            this.DisconnectedEvent = null;
        }
    }
}
