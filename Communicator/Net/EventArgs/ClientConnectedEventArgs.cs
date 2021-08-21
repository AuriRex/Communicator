namespace Communicator.Net.EventArgs
{
    public class ClientConnectedEventArgs
    {
        public string ServerID { get; set; }
        public Client Client { get; set; }
        public string GameName { get; set; }
    }
}
