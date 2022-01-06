using Communicator.Interfaces;
using Communicator.Net.EventArgs;
using Communicator.Packets;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
            internal IEncryptionProvider EncryptionProvider { get; set; } = Encryption.EncryptionProvider.NONE;
            internal byte[] KeyBytes { get; set; } = new byte[0];
            internal byte[] IVBytes { get; set; } = new byte[0];
            internal int ValidationTimeoutSeconds { get; set; } = 5;
            public int HeartbeatPacketInterval { get; internal set; } = 5;

            private NetworkStream _stream;
            private Task _task;
            private ConcurrentQueue<IPacket> _packetQueue = new ConcurrentQueue<IPacket>();
            private ConcurrentQueue<IPacket> _unsafePacketQueue = new ConcurrentQueue<IPacket>();
            private ConcurrentQueue<ValidationEntry> _validationQueue = new ConcurrentQueue<ValidationEntry>();
            private ConcurrentBag<string> _unusedValidationHashes = new ConcurrentBag<string>();
            private ManualResetEvent _shutdownEvent;
            private PacketSerializer _packetSerializer;
            private Action<string> _logAction;
            private DateTimeOffset _lastPacketSent = DateTimeOffset.UtcNow.AddSeconds(5);

            internal void QueuePacket(IPacket packet)
            {
               /* if (packet.GetType() != typeof(ConfirmationPacket))
                {
                    // Add to validation queue
                    _validationQueue.Enqueue(new ValidationEntry(packet));
                }*/

                _packetQueue.Enqueue(packet);
            }

            internal void QueuePacketUnsafe(IPacket packet)
            {
                _unsafePacketQueue.Enqueue(packet);
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

                _task = Task.Run(Run);
            }

            private void SendPacket(IPacket packet, bool sendUnsafe = false)
            {
                var encryptionProvider = sendUnsafe ? Encryption.EncryptionProvider.NONE : EncryptionProvider;

                string jsonPacket = _packetSerializer.SerializePacket(packet, packet.GetType());
                if (packet.GetType() != typeof(HeartbeatPacket))
                    _logAction?.Invoke($"Sending Packet: '{jsonPacket}' with encryption '{encryptionProvider.GetType()}'");
                byte[] data = ASCIIEncoding.UTF8.GetBytes(jsonPacket);

                data = encryptionProvider.Encrypt(data, KeyBytes, IVBytes);

                byte[] messageLength = BitConverter.GetBytes((Int32) data.Length);
                Console.WriteLine($"Sending \"{packet.GetType().FullName}\" with ENC \"{encryptionProvider.GetType().FullName}\", length = {data.Length}");
                _stream.Write(messageLength, 0, 4);

                _stream.Write(data, 0, data.Length);
                _lastPacketSent = DateTimeOffset.UtcNow;
            }

            private void Run()
            {
                try
                {
                    IPacket packet = null;
                    while (!_shutdownEvent.WaitOne(0))
                    {
                        try
                        {
                            if(_packetQueue.IsEmpty)
                            {
                                Thread.Sleep(1);
                            }
                            

                            if(_lastPacketSent.AddSeconds(HeartbeatPacketInterval) < DateTimeOffset.UtcNow)
                            {
                                SendPacket(new HeartbeatPacket());
                            }

                            while(_unsafePacketQueue.TryDequeue(out packet))
                            {
                                Thread.Sleep(1);
                                SendPacket(packet, sendUnsafe: true);
                            }

                            while (_packetQueue.TryDequeue(out packet))
                            {
                                Thread.Sleep(1);
                                SendPacket(packet);
                            }
                        }
                        catch(IOException)
                        {
                            ErrorAction?.Invoke($"An IOException has occured, client has most likely disconnected.");
                            _shutdownEvent.Set();
                            DisconnectedEvent?.Invoke(this, new ClientDisconnectedEventArgs()
                            {
                                IsIntentional = false,
                                Packet = packet
                            });
                        }
                        catch(Exception ex)
                        {
                            ErrorAction?.Invoke($"An error has occured (C): {ex.Message}\n{ex.StackTrace}");
                            _shutdownEvent.Set();
                            DisconnectedEvent?.Invoke(this, new ClientDisconnectedEventArgs()
                            {
                                IsIntentional = false,
                                Packet = packet
                            });
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

            internal void WaitDispose()
            {
                _shutdownEvent.Set();
                _task.Wait();
            }
        }

    }
}
