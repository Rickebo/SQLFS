using System;
using System.Collections.Generic;
using System.Text;

namespace SQLFS.Database
{
    public class DatabaseTable
    {
        private List<string> _options;

        public IReadOnlyDictionary<string, DatabaseColumn> Columns { get; }
        public IEnumerable<string> Options => _options;

        public DatabaseTable(Dictionary<string, DatabaseColumn> columns)
        {
            Columns = columns;
            _options = new List<string>();
        }

        public bool HasOptions() => _options.Count > 0;

        public DatabaseTable WithPrimaryKey(params string[] columns)
        {
            _options.Add("PRIMARY KEY (" + string.Join(",", columns) + ")");
            return this;
        }

        public DatabaseTable WithUnique(params string[] columns)
        {
            _options.Add("UNIQUE (" + string.Join(",", columns) + ")");
            return this;
        }
    }
}
