using Communicator.Interfaces;
using System.IO;
using System.Security.Cryptography;

namespace Communicator.Net.Encryption
{
    public partial class EncryptionProvider
    {
        public class S_AES : IEncryptionProvider
        {
            private Aes aes;

            public S_AES()
            {
                aes = Aes.Create();
            }

            public byte[] Decrypt(byte[] cipherText, byte[] key, byte[] iv)
            {
                aes.Key = key;
                aes.IV = iv;
                var decryptor = aes.CreateDecryptor();

                byte[] result;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Write))
                    {
                        using (BinaryWriter binaryWriter = new BinaryWriter(cryptoStream))
                        {
                            binaryWriter.Write(cipherText);
                        }

                        result = memoryStream.ToArray();
                    }
                }
                return result;
            }

            public byte[] Encrypt(byte[] plainText, byte[] key, byte[] iv)
            {
                aes.Key = key;
                aes.IV = iv;
                var encryptor = aes.CreateEncryptor();

                byte[] result;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (BinaryWriter binaryWriter = new BinaryWriter(cryptoStream))
                        {
                            binaryWriter.Write(plainText);
                        }

                        result = memoryStream.ToArray();
                    }
                }
                return result;
            }

            public byte[] GetIV()
            {
                return aes.IV;
            }

            public byte[] GetKey(bool includePrivateParametersIfAvailable)
            {
                return aes.Key;
            }
        }
    }
}
