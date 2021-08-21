using Communicator.Interfaces;
using Communicator.Packets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Communicator.Net
{
    public class PacketSerializer
    {
        internal JsonSerializerSettings JsonSettings { get; set; } = new JsonSerializerSettings { Formatting = Formatting.None };

        private Dictionary<string, Type> _registeredPacketTypes = new Dictionary<string, Type>();

        public PacketSerializer()
        {
            this.RegisterPacket<ConfirmationPacket>();
            this.RegisterPacket<GenericEventPacket>();
            this.RegisterPacket<IdentificationPacket>();
            this.RegisterPacket<DisconnectPacket>();
            this.RegisterPacket<ReconnectPacket>();
            this.RegisterPacket<HeartbeatPacket>();
            this.RegisterPacket<InitialPublicKeyPacket>();
        }

        /// <summary>
        /// Register a custom packet type.
        /// </summary>
        /// <typeparam name="T">The Packet type to register</typeparam>
        /// <exception cref="ArgumentException"></exception>
        public void RegisterPacket<T>() where T : IPacket
        {
            if(_registeredPacketTypes.ContainsKey(typeof(T).Name))
            {
                throw new ArgumentException($"A custom packet with name '{typeof(T).Name}' is already registered!");
            }

            _registeredPacketTypes.Add(typeof(T).Name, typeof(T));
        }

        public string SerializePacket<T>(T packet) where T : IPacket
        {
            packet.PacketType = typeof(T).Name;

            return JsonConvert.SerializeObject(packet);
        }

        public string SerializePacket(IPacket packet, Type type)
        {
            packet.PacketType = type.Name;

            return SerializePacketInternal(packet, type);
        }

        internal string SerializePacketInternal(IPacket packet, Type type)
        {
            return JsonConvert.SerializeObject(packet, type, JsonSettings);
        }

        /// <summary>
        /// Deserialize an incoming packet using all registered packet types.
        /// </summary>
        /// <param name="jsonPacket">Incoming packet</param>
        /// <exception cref="ArgumentException"></exception>
        public IPacket DeserializePacket(string jsonPacket)
        {
            try
            {
                var info = (IPacket) JsonConvert.DeserializeObject(jsonPacket, typeof(DummyPacket), JsonSettings);

                if (_registeredPacketTypes.TryGetValue(info.PacketType, out Type type))
                {
                    return (IPacket) DeserializePacket(jsonPacket, type);
                }

                throw new ArgumentException($"Unknown packet type '{info.PacketType}' received!");
            }
            catch(Exception ex)
            {
                throw new Exception($"Error while decoding json '{jsonPacket}'", ex);
            }
        }

        public T DeserializePacket<T>(string jsonPacket) where T : IPacket
        {
            return JsonConvert.DeserializeObject<T>(jsonPacket, JsonSettings);
        }

        public object DeserializePacket(string jsonPacket, Type type)
        {
            return JsonConvert.DeserializeObject(jsonPacket, type, JsonSettings);
        }
    }
}
