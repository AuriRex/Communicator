using Communicator.Net;
using Communicator.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NetFramework45Test.Program.MyCoolCustomEventPacket;

namespace NetFramework45Test
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
            /*var t = new Communicator.Net.Encryption.EncryptionProvider.S_AES();

            var key = t.GetKey(true);
            var iv = t.GetIV();

            var data = Encoding.Unicode.GetBytes("My Secret message :)");

            data = t.Encrypt(data, key, iv);
            data = t.Decrypt(data, key, iv);

            Console.WriteLine($"{Encoding.Unicode.GetString(data)}");

            var u = new Communicator.Net.Encryption.EncryptionProvider.A_RSA();

            var privateKey = u.GetKey(true);
            var publicKey = u.GetKey(false);

            data = u.Encrypt(data, publicKey, new byte[0]);
            data = u.Decrypt(data, privateKey, new byte[0]);

            Console.WriteLine($"{Encoding.Unicode.GetString(data)}");

            Console.WriteLine("Press any key to continue ...");
            Console.ReadKey();*/

            byte[] saltBytes = new byte[] { 0x0,0x1,0x2,0x3,0x4,0x5,0x6,0x8 };


            var salt = Convert.ToBase64String(saltBytes/*Communicator.Utils.Utils.GenerateSalt()*/);


            var comPlugin = new TerrariaCommunicator_NetworkPlugin.ComPlugin();

            var ps = new PacketSerializer();

            comPlugin.Register(ps);

            Console.WriteLine($"Base64 salt: {salt}");

            GameserverClient cl = new GameserverClient("localhost", 11000, "aPersistentServerId", "terraria", "password123", salt, ps, (s) => { Console.WriteLine($"[Info ] {s}"); } );
            cl.RegisterPacket<MyCoolCustomEventPacket>();
            cl.ErrorAction = (s) => { Console.WriteLine($"[Error] {s}"); };
            cl.PacketReceivedEvent += Cl_PacketReceivedEvent;
            Console.WriteLine("Test");
            while (true)
            {
                string message = Console.ReadLine();

                cl.SendPacket(new MyCoolCustomEventPacket() {
                    PacketData = new MyCoolCustomEventPacket.CustomEventData
                    {
                        Message = message
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
