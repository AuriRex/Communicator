using Communicator.Interfaces;
using Communicator.Net;
using System;
using System.Security.Cryptography;
using System.Text;

namespace Communicator.Utils
{
    public class Utils
    {
        // https://stackoverflow.com/a/6839784
        public static byte[] GetSHA256Hash(string inputString)
        {
            using (HashAlgorithm algorithm = SHA256.Create())
                return algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));
        }

        public static string GetSHA256HashString(string inputString)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in GetSHA256Hash(inputString))
                sb.Append(b.ToString("X2"));

            return sb.ToString().ToUpper();
        }

        public static string GenerateServerID()
        {
            return GetSHA256HashString($"{DateTimeOffset.UtcNow}{System.Net.Dns.GetHostName()}");
        }

        private static PacketSerializer _packetSerializer;
        public static string HashPacket(IPacket packet)
        {
            if(_packetSerializer == null)
                _packetSerializer = new PacketSerializer();

            return GetSHA256HashString(_packetSerializer.SerializePacket(packet, packet.GetType()));
        }
    }
}
