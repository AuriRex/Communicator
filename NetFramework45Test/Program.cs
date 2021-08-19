using Communicator.Net;
using Communicator.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetFramework45Test
{
    class Program
    {
        static void Main(string[] args)
        {
            GameserverClient cl = new GameserverClient("localhost", 11000, "aPersistentServerId", "terraria", (s) => { Console.WriteLine($"[Info ] {s}"); } );
            cl.ErrorAction = (s) => { Console.WriteLine($"[Error] {s}"); };
            cl.PacketReceivedEvent += Cl_PacketReceivedEvent;
            Console.WriteLine("Test");
            while(true)
            {
                string message = Console.ReadLine();

                cl.SendPacket(new GenericEventPacket() {
                    PacketData = new GenericEventPacket.EventData
                    {
                        Type = "Chat",
                        Data = message
                    }
                });
            }
        }

        private static void Cl_PacketReceivedEvent(object sender, Communicator.Interfaces.IPacket e)
        {
            Console.WriteLine($"Packet received from server: {e.GetType()}, {e.EventTime}");
        }
    }
}
