using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLFS
{
    public class Settings
    {
        public string MountLocation { get; set; }
        public string DatabaseType { get; set; } = "TEXT";
    }
}
