using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communicator.Interfaces
{
    public interface IAuthentificationService
    {
        public bool AuthenticateGameserver(string passwordHash, string serverID, string gameIdentification);
    }
}
