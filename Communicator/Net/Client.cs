using Communicator.Interfaces;
using Communicator.Net.EventArgs;
using Communicator.Packets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Communicator.Attributes;

namespace Communicator.Net
{
    public class Client : IDisposable
    {
        // Consumers register to receive data.
        public virtual event EventHandler<IPacket> PacketReceivedEvent;
        public virtual event EventHandler<IPacket> IgnoredPacketReceivedEvent;
        public virtual event EventHandler<ClientDisconnectedEventArgs> DisconnectedEvent;
        public int BufferSize
        {
            get => _receiver.BufferSize;
            set => _receiver.BufferSize = value;
        }
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
        protected PacketSerializer packetSerializer;

        private Action<string> _logAction;
        private Action<string> _errorLogAction;
        private TcpClient _client;
        private NetworkStream _stream;
        private Shared.Receiver _receiver;
        private Shared.Sender _sender;
        private ManualResetEvent _shutdownEvent = new ManualResetEvent(false);
        private Type _packetTypeToWaitFor = null;
        private bool _disconnectEventRaised = false;

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

        public Client(TcpClient client = null, PacketSerializer packetSerializer = null, Action<string> logAction = null)
        {
            this.packetSerializer = packetSerializer ?? new PacketSerializer();
            _logAction = logAction;

            _client = client ?? new TcpClient("localhost", 11000);
            _stream = _client.GetStream();

            _logAction?.Invoke($"Client created.");

            _receiver = new Shared.Receiver(_stream, _shutdownEvent, this.packetSerializer, logAction);
            _sender = new Shared.Sender(_stream, _shutdownEvent, this.packetSerializer, logAction);

            _receiver.PacketReceived += OnPacketReceived;
            _receiver.ThreadFinished += OnSubThreadsExited;
            _sender.ThreadFinished += OnSubThreadsExited;
            _sender.DisconnectedEvent += RaiseDisconnectEvent;
        }

        private void RaiseDisconnectEvent(object sender, ClientDisconnectedEventArgs e)
        {
            if (_disconnectEventRaised) return;
            _disconnectEventRaised = true;
            DisconnectedEvent?.Invoke(this, e);
        }

        internal void StartDisconnect()
        {
            _shutdownEvent.Set();
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
            }
        }

        protected virtual void OnPacketReceived(object sender, IPacket packet)
        {
            switch(packet)
            {
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
        }
    }
}
