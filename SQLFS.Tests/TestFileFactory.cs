using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLFS.Tests
{
    public class TestFileFactory : IFileFactory<FileBase>
    {
        public FileBase Create(string filename, byte[] fileContent)
        {
            var file = FileBase.Create(filename);
            file.SetData(fileContent);
            return file;
        }

        public FileBase Create(DbDataReader reader) => 
            new FileBase(reader);

        public FileBase Create(string filename) =>
                FileBase.Create(filename);
    }
}
