﻿using Communicator.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communicator.Net.EventArgs
{
    public class ClientDisconnectedEventArgs
    {
        public bool IsIntentional { get; set; } = false;
        public IPacket Packet { get; set; } = null;
        public Client Client { get; set; }
    }
}
