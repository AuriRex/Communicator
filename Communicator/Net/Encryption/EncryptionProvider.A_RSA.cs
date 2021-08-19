using Communicator.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

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
                    //return cipherText;
                }
                return rsa.Decrypt(cipherText, true);
            }
            public byte[] Encrypt(byte[] plainText, byte[] publicKey, byte[] iv)
            {
                string s = Encoding.Unicode.GetString(publicKey);
                //Console.WriteLine($"XML: {s}");
                rsa.FromXmlString(s);
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
