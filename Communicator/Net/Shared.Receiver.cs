using Communicator.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Communicator.Net
{
    internal partial class Shared
    {
        //https://stackoverflow.com/a/20698153
        internal sealed class Receiver
        {
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

            internal event EventHandler<IPacket> PacketReceived;
            internal event Action ThreadFinished;

            private NetworkStream _stream;
            private Thread _thread;
            private byte[] _data;
            private byte[] _messageLengthData;
            private ManualResetEvent _shutdownEvent;
            private PacketSerializer _packetSerializer;
            private Action<string> _logAction;

            internal Receiver(NetworkStream stream, ManualResetEvent shutdownEvent, PacketSerializer packetSerializer, Action<string> logAction = null)
            {
                _stream = stream;
                _logAction = logAction;
                _shutdownEvent = shutdownEvent;
                _packetSerializer = packetSerializer;

                _thread = new Thread(Run);
                _thread.Start();
            }

            private void Run()
            {
                // main thread loop for receiving data...
                try
                {
                    
                    while (!_shutdownEvent.WaitOne(0))
                    {
                        _messageLengthData = new byte[4];
                        try
                        {
                            //_data.Clear();
                            if (!_stream.DataAvailable)
                            {
                                // Give up the remaining time slice.
                                Thread.Sleep(1);
                            }
                            else if (_stream.Read(_messageLengthData, 0, 4) > 0)
                            {
                                var length = BitConverter.ToInt32(_messageLengthData, 0);
                                _data = new byte[length];
                                _stream.Read(_data, 0, length);
                                // Raise the DataReceived event w/ data...

                                try
                                {
                                    _data = EncryptionProvider.Decrypt(_data, KeyBytes, IVBytes);
                                }
                                catch(Exception)
                                {
                                    ErrorAction?.Invoke($"Decryption with '{EncryptionProvider.GetType()}' failed, falling back to no encryption!");
                                    _data = Encryption.EncryptionProvider.NONE.Decrypt(_data, KeyBytes, IVBytes);
                                }
                                

                                string jsonPacket = ASCIIEncoding.UTF8.GetString(_data);

                                var packet = _packetSerializer.DeserializePacket(jsonPacket);

                                PacketReceived?.Invoke(this, packet);
                            }
                            else
                            {
                                // The connection has closed gracefully, so stop the thread.
                                _shutdownEvent.Set();
                            }
                        }
                        catch (IOException ex)
                        {
                            // Handle the exception...
                            ErrorAction?.Invoke($"An error occured (A): {ex.Message}\n{ex.StackTrace}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorAction?.Invoke($"An error occured (B): {ex.Message}\n{ex.StackTrace}");
                }
                finally
                {
                    _logAction?.Invoke("Receiver closing ...");
                    _shutdownEvent.Set();
                    //_stream.Close();
                    HasExited = true;
                    ThreadFinished?.Invoke();
                }
            }
        }
    }
}
