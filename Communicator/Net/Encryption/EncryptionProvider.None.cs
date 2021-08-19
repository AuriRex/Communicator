using Communicator.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communicator.Net.Encryption
{
    public partial class EncryptionProvider
    {
        internal static readonly IEncryptionProvider NONE = new None();

        public class None : IEncryptionProvider
        {
            public byte[] Decrypt(byte[] cipherText, byte[] key, byte[] iv)
            {
                return cipherText;
            }
            public byte[] Encrypt(byte[] plainText, byte[] key, byte[] iv)
            {
                return plainText;
            }

            public byte[] GetIV()
            {
                return new byte[0];
            }
            public byte[] GetKey(bool includePrivateParametersIfAvailable)
            {
                return new byte[0];
            }
        }
    }
}
