using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLFS.Tests
{
    public class Settings<T> : ISqlFileSystemOptions<T>, IDatabaseOptions where T : FileBase
    {
        public string VolumeName { get; set; }

        public long Space { get; set; }

        public long FreeSpace { get; set; }

        public string SecurityTemplate { get; set; }

        public T FileTemplate { get; set; }

        public IFileFactory<T> FileFactory { get; set; }

        public string Hostname { get; set; }

        public string Database { get; set; }

        public string Table { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }
    }
}
