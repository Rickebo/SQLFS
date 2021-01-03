using System;
using System.Collections.Generic;
using System.Text;
using MySql.Data.MySqlClient;

namespace SQLFS.Database
{
    public interface ISavable
    {
        void Save(MySqlCommand command, params string[] parameters);
        void SaveParameter(MySqlCommand param, string column);
        MySqlDbType? GetColumnType(string column);
        IEnumerable<string> GetColumns(Func<DatabaseColumn, bool> condition);
        IEnumerable<string> GetColumns();
    }
}
