using Communicator.Interfaces;
using Communicator.Net.EventArgs;
using Communicator.Packets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Communicator.Net
{
    internal partial class Shared
    {
        //https://stackoverflow.com/a/20698153
        internal sealed class Sender
        {
            public event EventHandler<ClientDisconnectedEventArgs> DisconnectedEvent;

            internal event Action ThreadFinished;
            internal bool HasExited { get; private set; } = false;
            internal Action<string> LogAction
            {
                get => _logAction;
                set => _logAction = value;
            }
            internal Action<string> ErrorAction { get; set; }
            internal int ValidationTimeoutSeconds { get; set; } = 5;

            private NetworkStream _stream;
            private Thread _thread;
            private ConcurrentQueue<IPacket> _packetQueue = new ConcurrentQueue<IPacket>();
            private ConcurrentQueue<ValidationEntry> _validationQueue = new ConcurrentQueue<ValidationEntry>();
            private ConcurrentBag<string> _unusedValidationHashes = new ConcurrentBag<string>();
            private ManualResetEvent _shutdownEvent;
            private PacketSerializer _packetSerializer;
            private Action<string> _logAction;
            private DateTimeOffset _lastHeartbeatPacketSent = DateTimeOffset.UtcNow;

            internal void QueuePacket(IPacket packet)
            {
               /* if (packet.GetType() != typeof(ConfirmationPacket))
                {
                    // Add to validation queue
                    _validationQueue.Enqueue(new ValidationEntry(packet));
                }*/

                _packetQueue.Enqueue(packet);
            }

            internal void ValidationPacketReceived(string hash)
            {
                if (!_unusedValidationHashes.Contains(hash))
                    _unusedValidationHashes.Add(hash);
            }

            internal Sender(NetworkStream stream, ManualResetEvent shutdownEvent, PacketSerializer packetSerializer, Action<string> logAction)
            {
                _stream = stream;
                _shutdownEvent = shutdownEvent;
                _packetSerializer = packetSerializer;
                _logAction = logAction;

                _thread = new Thread(Run);
                _thread.Start();
            }

            private void SendPacket(IPacket packet)
            {
                string jsonPacket = _packetSerializer.SerializePacket(packet, packet.GetType());
                if(packet.GetType() != typeof(HeartbeatPacket))
                    _logAction?.Invoke($"Sending Packet: '{jsonPacket}'");
                byte[] data = ASCIIEncoding.UTF8.GetBytes(jsonPacket);
                _stream.Write(data, 0, data.Length);
            }

            private void Run()
            {
                try
                {
                    while(!_shutdownEvent.WaitOne(0))
                    {
                        try
                        {
                            if(_lastHeartbeatPacketSent.AddSeconds(5) < DateTimeOffset.UtcNow)
                            {
                                SendPacket(new HeartbeatPacket());
                                _lastHeartbeatPacketSent = DateTimeOffset.UtcNow;
                            }

                            IPacket packet;
                            while (_packetQueue.TryDequeue(out packet))
                            {
                                SendPacket(packet);
                            }
                        }
                        catch(IOException)
                        {
                            ErrorAction?.Invoke($"An IOException has occured, client has most likely disconnected.");
                            _shutdownEvent.Set();
                            DisconnectedEvent?.Invoke(this, new ClientDisconnectedEventArgs() {
                                IsIntentional = false,
                                Packet = null
                            });
                        }
                        catch(Exception ex)
                        {
                            ErrorAction?.Invoke($"An error has occured (C): {ex.Message}\n{ex.StackTrace}");
                        }
                    }
                }
                catch(Exception ex)
                {
                    ErrorAction?.Invoke($"An error has occured (D): {ex.Message}\n{ex.StackTrace}");
                }
                finally
                {
                    _logAction?.Invoke("Sender Closing ...");
                    _shutdownEvent.Set();
                    HasExited = true;
                    ThreadFinished?.Invoke();
                }
            }
        }

    }
}
