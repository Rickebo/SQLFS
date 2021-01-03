using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLFS
{
    public interface ISqlFileSystemOptions<out T> where T : FileBase
    {
        string VolumeName { get; }
        long Space { get; }
        long FreeSpace { get; }
        public string SecurityTemplate { get; }
        T FileTemplate { get; }
        IFileFactory<T> FileFactory { get; } 
    }
}
