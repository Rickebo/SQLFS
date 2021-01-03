using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using SQLFS.Database;

namespace SQLFS
{
    public class FileBase
    {
        public const string NameColumn = "name";
        public const string CreationTimeColumn = "creation_time";
        public const string LastModifyTimeColumn = "last_modify_time";
        public const string AccessTimeColumn = "access_time";
        public const string FlagsColumn = "flags";
        public const string DataColumn = "data";
        public const string LengthColumn = "length";

        public string NameColumnName => NameColumn;
        public string CreationTimeColumnName => CreationTimeColumn;
        public string LastModifyTimeColumnName => LastModifyTimeColumn;
        public string AccessTimeColumnName => AccessTimeColumn;
        public string FlagsColumnName => FlagsColumn;
        public string DataColumnName => DataColumn;
        public string LengthColumnName => LengthColumn;

        [DatabaseColumn(NameColumn, "VARCHAR(256) PRIMARY KEY", MySqlDbType.Text, IsKeyColumn = true)]
        public string Name { get; set; }
        
        [DatabaseColumn(CreationTimeColumn, "TIMESTAMP DEFAULT NOW()", MySqlDbType.Timestamp)]
        public DateTime CreationTime { get; set; }
        
        [DatabaseColumn(LastModifyTimeColumn, "TIMESTAMP DEFAULT NOW()", MySqlDbType.Timestamp)]
        public DateTime LastModifyTime { get; set; }
        
        [DatabaseColumn(AccessTimeColumn, "TIMESTAMP DEFAULT NOW()", MySqlDbType.Timestamp)]
        public DateTime AccessTime { get; set; }
        
        [DatabaseColumn(FlagsColumn, "TINYINT", MySqlDbType.UByte)]
        public byte Flags { get; set; }
        
        [DatabaseColumn(DataColumn, "LONGBLOB", MySqlDbType.LongBlob)]
        public byte[] Data { get; set; }

        [DatabaseColumn(LengthColumn, "INT", MySqlDbType.Int32, IsSaved = false, IsCreated = false)]
        public int Length { get; set; }

        public FileFlags FileFlags
        {
            get => (FileFlags)Flags;
            set => Flags = (byte) value;
        }

        public bool IsDirectory
        {
            get => (FileFlags & FileFlags.Directory) != 0;
            set
            {
                if (value)
                    FileFlags |= FileFlags.Directory;
                else
                    FileFlags &= ~FileFlags.Directory;
            }
        }

        public bool IsLocked
        {
            get => (FileFlags & FileFlags.Locked) != 0;
            set
            {
                if (value)
                    FileFlags |= FileFlags.Locked;
                else
                    FileFlags &= ~FileFlags.Locked;
            }
        }

        #region Columns
        public static Dictionary<string, DatabaseColumn> StaticColumns = new Dictionary<string, DatabaseColumn>();
        public virtual Dictionary<string, DatabaseColumn> Columns { get; protected set; } = StaticColumns;

        static FileBase()
        {
            var properties = typeof(FileBase).GetProperties();

            foreach (var property in properties)
            {
                var attributes = property.GetCustomAttributes(typeof(DatabaseColumn), false);
                if (attributes.Length == 0)
                    continue;

                if (!(attributes.First() is DatabaseColumn attribute))
                    continue;

                StaticColumns.Add(attribute.Name, attribute);
            }
        }
        #endregion

        public FileBase(DbDataReader reader)
        {
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);

                switch (name)
                {
                    case NameColumn:
                        Name = reader.GetString(i);
                        break;

                    case CreationTimeColumn:
                        CreationTime = reader.GetDateTime(i);
                        break;

                    case LastModifyTimeColumn:
                        LastModifyTime = reader.GetDateTime(i);
                        break;

                    case AccessTimeColumn:
                        AccessTime = reader.GetDateTime(i);
                        break;

                    case FlagsColumn:
                        Flags = reader.GetByte(i);
                        break;

                    case LengthColumn:
                        Length = reader[i] is DBNull ? 0 : reader.GetInt32(i);
                        break;

                    case DataColumn:
                        var entry = reader[i];

                        if (entry is DBNull)
                            Data = new byte[0];
                        else
                            Data = (byte[]) entry;

                        break;
                }
            }
        }

        protected FileBase()
        {

        }

        public static FileBase Create(string filename, bool isDirectory = false)
        {
            var now = DateTime.Now;

            return new FileBase()
            {
                Name = filename,
                CreationTime = now,
                LastModifyTime = now,
                AccessTime = now,
                Data = new byte[0],
                IsDirectory = isDirectory
            };
        }

        public virtual void SetData(byte[] data)
        {
            Data = data;
        }

        public virtual void Save(MySqlCommand command, params string[] columns)
        {
            Save(command, (IEnumerable<string>) columns);
        }

        public virtual void Save(MySqlCommand command, IEnumerable<string> columns)
        {
            var commandParameters = command.Parameters;

            foreach (var column in columns)
            {
                if (!Columns.TryGetValue(column, out var dbColumn))
                    throw new ArgumentException("Specified column is not valid for this object.");

                var param = new MySqlParameter(column, dbColumn.MySqlType);
                SaveParameter(param, column);

                commandParameters.Add(param);
            }
        }

        public virtual void SaveParameter(MySqlParameter param, string column) =>
            param.Value = column switch
            {
                NameColumn => Name,
                CreationTimeColumn => CreationTime,
                LastModifyTimeColumn => LastModifyTime,
                AccessTimeColumn => AccessTime,
                FlagsColumn => Flags,
                DataColumn => Data,
                _ => param.Value
            };

        public MySqlDbType? GetColumnType(string column) =>
            Columns.TryGetValue(column, out var dbColumn)
                ? dbColumn.MySqlType
                : (MySqlDbType?) null;

        public IEnumerable<string> GetColumns(Func<string, DatabaseColumn, bool> condition) =>
            Columns.Where(entry => condition(entry.Key, entry.Value))
                .Select(col => col.Key);

        public IEnumerable<string> GetColumns() => Columns.Keys;
    }
}
