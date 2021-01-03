using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLFS
{
    public interface IFileFactory<out T> where T : FileBase
    {
        T Create(string filename, byte[] content);
        T Create(string filename);
        T Create(DbDataReader reader);
    }
}
