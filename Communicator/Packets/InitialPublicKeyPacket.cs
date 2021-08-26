using Communicator.Attributes;
using System;

namespace Communicator.Packets
{
    [Unignorable]
    public class InitialPublicKeyPacket : BasePacket<InitialPublicKeyPacket.KeyData>
    {
        public override KeyData PacketData { get; set; }

        public struct KeyData
        {
            public string Base64Key { get; set; }

            public static byte[] GetKey(KeyData keyData)
            {
                return keyData.GetKey();
            }

            public byte[] GetKey()
            {
                return Convert.FromBase64String(Base64Key);
            }

            public static KeyData CreateKeyData(byte[] keyBytes)
            {
                return new KeyData
                {
                    Base64Key = Convert.ToBase64String(keyBytes)
                };
            }
        }
    }
}
