using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Juice.Domain
{
    [Flags]
    public enum EntityStates
    {
        Created = 1,
        Modified = 2,
        Deleted = 4,
    }
}
