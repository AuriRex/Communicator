using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communicator.Packets
{
    public class IdentificationPacket : BasePacket<IdentificationData>
    {
        public override IdentificationData PacketData { get; set; } = new IdentificationData() { ServerID = "ServerID", GameIdentification = "UnsetGameName" };
    }

    public struct IdentificationData
    {
        // Should be generated on the Game server once and used to identificate
        public string ServerID { get; set; }
        // eg. Terraria, Minecraft
        public string GameIdentification { get; set; }
        // Key and Salt for the symmetrical encryption
        public string Base64Key { get; set; }
        public string Base64IV { get; set; }
        // For authentification
        public string Base64PasswordHashFirst { get; set; }
        public string Base64PasswordHashSecond { get; set; }

        public static byte[] GetKey(IdentificationData keyData)
        {
            return keyData.GetKey();
        }

        public static byte[] GetIV(IdentificationData keyData)
        {
            return keyData.GetIV();
        }

        public byte[] GetKey()
        {
            return Convert.FromBase64String(Base64Key);
        }

        public byte[] GetIV()
        {
            return Convert.FromBase64String(Base64IV);
        }

        public static IdentificationData CreateKeyData(string serverID, string gameIdentification, byte[] keyBytes, byte[] ivBytes, byte[] passowrdHashBytesFirst, byte[] passwordHashBytesSecond)
        {
            
            return new IdentificationData
            {
                ServerID = serverID,
                GameIdentification = gameIdentification,
                Base64Key = Convert.ToBase64String(keyBytes),
                Base64IV = Convert.ToBase64String(ivBytes),
                Base64PasswordHashFirst = Convert.ToBase64String(passowrdHashBytesFirst),
                Base64PasswordHashSecond = Convert.ToBase64String(passwordHashBytesSecond)
            };
        }

        
    }
}
