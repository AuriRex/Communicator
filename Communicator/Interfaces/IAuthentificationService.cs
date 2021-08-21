namespace Communicator.Interfaces
{
    public interface IAuthentificationService
    {
        public bool AuthenticateGameserver(string passwordHash, string serverID, string gameIdentification);
    }
}
