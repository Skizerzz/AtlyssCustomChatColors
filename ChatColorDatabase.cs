using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;

namespace CustomChatColors {
    internal class ChatColorDatabase {

        private string _connectionStr;
        private string _tableName { get; } = "ChatColors";

        public ChatColorDatabase(string dbPath) {
            Console.WriteLine($"Database path provided: {dbPath}");
            _connectionStr = $"Data Source={dbPath}";
            Init();
        }

        private void Init() {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionStr)) {
                connection.Open();
                string createTable = $@"
                    CREATE TABLE IF NOT EXISTS {_tableName} (
                        SteamID TEXT PRIMARY KEY,
                        Color TEXT
                    );
                ";
                using (var command = new SQLiteCommand(createTable, connection)) {
                    command.ExecuteNonQuery();
                }
            }
        }

        public void RemoveUserColor(string steamID) {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionStr)) {
                connection.Open();
                string query = $@"
                    DELETE FROM {_tableName} WHERE SteamID = @SteamID
                ";

                using (SQLiteCommand command = new SQLiteCommand(query, connection)) {
                    command.Parameters.AddWithValue("@SteamID", steamID);
                    command.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Can return null on failure.
        /// </summary>
        /// <param name="steamID"></param>
        /// <returns></returns>
        public string GetUserColor(string steamID) {
            string color = null;
            using (SQLiteConnection connection = new SQLiteConnection(_connectionStr)) {
                connection.Open();
                string query = $@"
                    SELECT * FROM {_tableName} WHERE SteamID = @SteamID;
                ";

                using (SQLiteCommand command = new SQLiteCommand(query, connection)) {
                    command.Parameters.AddWithValue("@SteamID", steamID);
                    using (SQLiteDataReader reader = command.ExecuteReader()) {
                        while(reader.Read()) {
                            color = reader["Color"].ToString();
                            break;
                        }
                    }
                }
            }

            return color;
        }

        /// <summary>
        /// Can return null on failure.
        /// </summary>
        /// <param name="steamID"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetAllColors() {
            Dictionary<string, string> dictionary = null;
            using (SQLiteConnection connection = new SQLiteConnection(_connectionStr)) {
                connection.Open();
                string query = $@"
                    SELECT * FROM {_tableName};
                ";
                using (SQLiteCommand command = new SQLiteCommand(query, connection)) {
                    using (SQLiteDataReader reader = command.ExecuteReader()) {
                        dictionary = new Dictionary<string, string>();
                        while(reader.Read()) {
                            string steamID = reader["SteamID"].ToString();
                            string color = reader["Color"].ToString();
                            dictionary[steamID] = color;
                        }
                    }
                }
            }

            return dictionary;
        }

        public void SetUserColor(string steamID, string color) {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionStr)) {
                connection.Open();
                string insert = $@"
                    INSERT INTO {_tableName} (SteamID, Color)
                    VALUES (@SteamID, @Color)
                    ON CONFLICT(SteamID) DO UPDATE SET Color = @Color;
                ";
                using (SQLiteCommand command = new SQLiteCommand(insert, connection)) {
                    command.Parameters.AddWithValue("@SteamID", steamID);
                    command.Parameters.AddWithValue("@Color", color);
                    command.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
