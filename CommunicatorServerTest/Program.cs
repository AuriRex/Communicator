using Communicator.Net;
using Communicator.Packets;
using System;
using System.Threading.Tasks;
using static CommunicatorServerTest.Program.MyCoolCustomEventPacket;

namespace CommunicatorServerTest
{
    class Program
    {

        public class MyCoolCustomEventPacket : BasePacket<CustomEventData>
        {
            public override CustomEventData PacketData { get; set; }

            public class CustomEventData
            {
                public string Message { get; set; }
            }
        }

        static void Main(string[] args)
        {
            AsyncMain().GetAwaiter().GetResult();
        }

        static async Task AsyncMain()
        {
            Console.WriteLine("Starting Test Server ...");
            Server server = new Server();

            server.RegisterCustomPacket<MyCoolCustomEventPacket>();


            server.ErrorLogAction = (msg) => Console.WriteLine($"Error {msg}");
            server.LogAction = (msg) => Console.WriteLine($"Info  {msg}");

            server.StartServer();

            await Task.Delay(-1);
        }
    }
}
