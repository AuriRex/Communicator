using Communicator.Interfaces;

namespace Communicator.Net.Auth
{
    public partial class AuthService
    {
        /// <summary>
        /// No authentification checks are done, every client gets accepted.
        /// </summary>
        public class None : IAuthentificationService
        {
            public bool AuthenticateGameserver(string passwordHash, string serverID, string gameIdentification)
            {
                return true;
            }
        }

    }
}
