using Communicator.Net;
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
            GameserverClient cl = new GameserverClient("localhost", 11000, "aPersistentServerId", "terraria", (s) => { Console.WriteLine($"Log: {s}"); } );
            Console.WriteLine("Test");
            Console.ReadKey();
        }
    }
}
