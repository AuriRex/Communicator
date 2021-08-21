using Communicator.Interfaces;
using System;
using System.Security.Cryptography;
using System.Text;

namespace Communicator.Net.Encryption
{
    public partial class EncryptionProvider
    {
        public class A_RSA : IEncryptionProvider
        {
            RSACryptoServiceProvider rsa;

            public A_RSA()
            {
                rsa = new RSACryptoServiceProvider();
            }

            public byte[] Decrypt(byte[] cipherText, byte[] privateKey, byte[] iv)
            {
                rsa.FromXmlString(Encoding.Unicode.GetString(privateKey));
                if (rsa.PublicOnly)
                {
                    throw new ArgumentException("Tried to decrypt a message without a private key!");
                }
                return rsa.Decrypt(cipherText, true);
            }

            public byte[] Encrypt(byte[] plainText, byte[] publicKey, byte[] iv)
            {
                rsa.FromXmlString(Encoding.Unicode.GetString(publicKey));
                return rsa.Encrypt(plainText, true);
            }

            public byte[] GetIV()
            {
                return new byte[0];
            }

            public byte[] GetKey(bool includePrivateParametersIfAvailable)
            {
                return Encoding.Unicode.GetBytes(rsa.ToXmlString(includePrivateParametersIfAvailable));
            }
        }
    }
}
