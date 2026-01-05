using Microsoft.Data.Sqlite;

namespace Database;
internal class SQLiteConnection : IDisposable
{
    private SQLiteConnection()
    {

    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public string DataSource { get; set; }

    public SqliteConnection? Connection { get; set; }

    private static SQLiteConnection? instance = null;
    public static SQLiteConnection Instance()
    {
        instance ??= new SQLiteConnection();
        return instance;
    }

    /*/
     * ==============================
     * ===== Connection Methods =====
     * ==============================
     */

    public void Connect()
    {
        if (IsConnected())
            return;

        if (string.IsNullOrEmpty(DataSource))
            throw new InvalidOperationException("Database source is not set.");

        string connectionString = $"Data Source={DataSource};";
        Connection = new SqliteConnection(connectionString);
        Connection.Open();
    }

    public void Disconnect()
    {
        if (IsConnected())
        {
            Connection?.Close();
            Connection = null;
        }
    }

    public bool IsConnected() =>
        Connection != null && Connection.State == System.Data.ConnectionState.Open;

    /*/
     * ==============================
     * ======= Utility Methods ======
     * ==============================
     */

    public SqliteCommand CreateCommand(string query)
    {
        if (!IsConnected())
            throw new InvalidOperationException("Database is not connected.");

        SqliteCommand command = Connection!.CreateCommand();
        command.CommandText = query;
        return command;
    }

    public Dictionary<string, object> ExecuteReaderQuery(string query)
    {
        using SqliteCommand command = CreateCommand(query);
        using SqliteDataReader reader = command.ExecuteReader();
        if (reader.Read())
        {
            Dictionary<string, object> result = new();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                result[reader.GetName(i)] = reader.GetValue(i);
            }
            return result;
        }
        else
        {
            throw new InvalidOperationException("No rows returned from query.");
        }
    }

    public int ExecuteNonQuery(string query, Dictionary<string, object?>? parameters = null)
    {
        using SqliteCommand command = CreateCommand(query);

        if (parameters != null)
            foreach (var param in parameters)
                command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);

        return command.ExecuteNonQuery();
    }
}
