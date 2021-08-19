using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communicator.Interfaces
{
    public interface IEncryptionProvider
    {
        public byte[] Encrypt(byte[] plainText, byte[] key, byte[] iv);
        public byte[] Decrypt(byte[] cipherText, byte[] key, byte[] iv);

        public byte[] GetKey(bool includePrivateParametersIfAvailable);
        public byte[] GetIV();
    }
}
