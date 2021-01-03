using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace SQLFS.Database
{
    public sealed class DatabaseConnection : IDisposable, IAsyncDisposable
    {
        public MySqlConnection Connection { get; }

        private readonly HashSet<MySqlCommand> _commands = new HashSet<MySqlCommand>();

        public DatabaseConnection(MySqlConnection connection)
        {
            Connection = connection;
        }

        public void Connect()
        {
            Connection.Open();
        }

        public async Task ConnectAsync()
        {
            await Connection.OpenAsync();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities")]
        public MySqlCommand Command(string sql, params MySqlParameter[] parameters)
        {
            var cmd = new MySqlCommand(sql, Connection);

            if (parameters?.Any() ?? false)
                cmd.Parameters.AddRange(parameters);

            _commands.Add(cmd);

            return cmd;
        }
        
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities")]
        public MySqlCommand Command(string sql, ISavable savable, params string[] fields)
        {
            var cmd = new MySqlCommand(sql, Connection);
            savable.Save(cmd, fields);

            return cmd;
        }
        
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities")]
        public MySqlCommand Command(string sql, ISavable savable, IEnumerable<string> fields)
        {
            var cmd = new MySqlCommand(sql, Connection);
            savable.Save(cmd, fields.ToArray());

            return cmd;
        }

        public void Dispose()
        {
            foreach (var command in _commands)
                command.Dispose();

            Connection.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var command in _commands)
                await command.DisposeAsync();

            await Connection.DisposeAsync();
        }
    }
}
