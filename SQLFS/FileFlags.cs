using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLFS
{
    [Flags]
    public enum FileFlags : byte
    {
        Locked = 1,
        Directory = 2
    }
}
