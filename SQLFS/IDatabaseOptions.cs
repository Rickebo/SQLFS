using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLFS
{
    public interface IDatabaseOptions
    {
        public string Hostname { get; }
        public string Database { get; }
        public string Table { get; }
        public string Username { get; }
        public string Password { get; }
    }
}
