using Communicator.Interfaces;
using Communicator.Net;
using Communicator.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
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
            return ToHexString(GetSHA256Hash(inputString));
        }

        public static byte[] GenerateSalt(int length = 8)
        {
            byte[] salt = new byte[length];
            using (RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider())
            {
                rngCsp.GetBytes(salt);
            }
            return salt;
        }

        public static byte[] HashPassword(string password, byte[] salt)
        {
            using (SHA512 shaM = new SHA512Managed())
            {
                byte[] data = Encoding.Unicode.GetBytes(password);

                data = data.Concat(salt).ToArray();

                return shaM.ComputeHash(data);
            }
        }

        public static string ToHexString(byte[] data)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in data)
                sb.Append(b.ToString("X2"));

            return sb.ToString().ToUpper();
        }

        private static PacketSerializer _packetSerializer;
        public static string HashPacket(IPacket packet)
        {
            if (_packetSerializer == null)
                _packetSerializer = new PacketSerializer();

            return GetSHA256HashString(_packetSerializer.SerializePacket(packet, packet.GetType()));
        }

        // https://stackoverflow.com/a/1841276
        public static void Split<T>(T[] array, int index, out T[] first, out T[] second)
        {
            first = array.Take(index).ToArray();
            second = array.Skip(index).ToArray();
        }

        public static void SplitMidPoint<T>(T[] array, out T[] first, out T[] second)
        {
            Split(array, array.Length / 2, out first, out second);
        }

    }
}
