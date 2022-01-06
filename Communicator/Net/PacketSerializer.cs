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
            RegisterBasePackets();
        }

        public PacketSerializer(PacketSerializer prefab)
        {

            if (prefab == null)
            {
                RegisterBasePackets();
                return;
            }

            foreach(var pType in prefab.GetRegisteredPacketTypes())
            {
                this.RegisterPacket(pType);
            }

        }

        private void RegisterBasePackets()
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
            RegisterPacket(typeof(T));
        }

        protected void RegisterPacket(Type pType)
        {
            if (!typeof(IPacket).IsAssignableFrom(pType)) throw new ArgumentException($"{pType.FullName} does inherit {nameof(IPacket)}!");

            if (_registeredPacketTypes.ContainsKey(pType.Name))
            {
                throw new ArgumentException($"A custom packet with name '{pType.Name}' is already registered!");
            }

            _registeredPacketTypes.Add(pType.Name, pType);
        }

        /// <summary>
        /// Get all packet types that have been registered.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Type> GetRegisteredPacketTypes()
        {
            return _registeredPacketTypes.Values;
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
            IPacket info;
            try
            {
                info = (IPacket) JsonConvert.DeserializeObject(jsonPacket, typeof(DummyPacket), JsonSettings);
            
                if (_registeredPacketTypes.TryGetValue(info.PacketType, out Type type))
                {
                    return (IPacket) DeserializePacket(jsonPacket, type);
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Error while decoding packet json!", ex);
            }

            throw new ArgumentException($"Unknown packet type '{info.PacketType}' received!");
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
