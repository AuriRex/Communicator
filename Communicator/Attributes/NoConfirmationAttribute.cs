using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communicator.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class NoConfirmationAttribute : Attribute
    {
    }
}
