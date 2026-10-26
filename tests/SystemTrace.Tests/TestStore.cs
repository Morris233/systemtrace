using System.IO;
using Microsoft.Data.Sqlite;
using SystemTrace.Infrastructure;

namespace SystemTrace.Tests;

internal sealed class TestStore : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "SystemTrace.Tests", Guid.NewGuid().ToString("N"));
    public string DatabasePath { get; }
    public SqliteScanRepository Repository { get; }

    public TestStore(bool isDemo = true)
    {
        DatabasePath = Path.Combine(directory, "demo.db");
        Repository = new SqliteScanRepository(DatabasePath, isDemo);
    }

    public void Execute(string sql)
    {
        Directory.CreateDirectory(directory);
        using var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public long Scalar(string sql)
    {
        using var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
