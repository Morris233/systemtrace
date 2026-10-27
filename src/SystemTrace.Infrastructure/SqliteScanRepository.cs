using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SystemTrace.Core;

namespace SystemTrace.Infrastructure;

public sealed partial class SqliteScanRepository : IScanRepository, IHistoryReader
{
    private readonly string databasePath;
    private readonly bool isDemo;
    private readonly DiffEngine diff = new();

    public SqliteScanRepository(string databasePath, bool isDemo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        this.databasePath = Path.GetFullPath(databasePath);
        this.isDemo = isDemo;
    }

    // SQLite ADO async methods are synchronous. Dispatch the entire operation away from the UI.
    public Task<ScanCommit> CommitAsync(ScanRun run, CancellationToken cancellationToken = default) =>
        Task.Run(() => Commit(run, cancellationToken), cancellationToken);

    public Task<InventoryHistory> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Load(cancellationToken), cancellationToken);

    private ScanCommit Commit(ScanRun run, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(run);
        token.ThrowIfCancellationRequested();
        if (run.IsDemo != isDemo) throw new InvalidOperationException("Inventory origin mismatch");
        var json = JsonSerializer.Serialize(run);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        using var connection = Open(token);
        using var transaction = connection.BeginTransaction(deferred: false);
        using (var existing = Command(connection, transaction, "SELECT input_hash FROM scan_runs WHERE id=$id", ("$id", run.Id.ToString())))
        {
            var previousHash = existing.ExecuteScalar() as string;
            if (previousHash is not null)
            {
                if (previousHash != hash) throw new InvalidOperationException("Scan ID reused with different input");
                return new ScanCommit(true, 0);
            }
        }
        Execute(connection, transaction,
            "INSERT INTO scan_runs(id,input_hash,started_utc,completed_utc,is_demo,evidence_json) VALUES($id,$hash,$start,$end,$demo,$json)",
            ("$id", run.Id.ToString()), ("$hash", hash), ("$start", Utc(run.StartedAtUtc)),
            ("$end", Utc(run.CompletedAtUtc)), ("$demo", isDemo ? 1 : 0), ("$json", json));
        var totalChanges = 0;
        foreach (var result in run.Results)
        {
            token.ThrowIfCancellationRequested();
            Execute(connection, transaction,
                "INSERT INTO collection_results(scan_id,category,status,evidence_json) VALUES($run,$category,$status,$json)",
                ("$run", run.Id.ToString()), ("$category", (int)result.Category),
                ("$status", (int)result.Status), ("$json", JsonSerializer.Serialize(result)));
            if (result.Status != CollectionStatus.Succeeded) continue;
            Snapshot? previous = null;
            using (var baseline = Command(connection, transaction,
                "SELECT s.evidence_json FROM baselines b JOIN snapshots s ON s.id=b.snapshot_id WHERE b.category=$category",
                ("$category", (int)result.Category)))
            {
                if (baseline.ExecuteScalar() is string evidence) previous = Deserialize<Snapshot>(evidence);
            }
            var current = new Snapshot(Guid.NewGuid(), run.Id, result.Category, result.CompletedAtUtc, result.Items);
            var changes = diff.Compare(previous, current, token);
            Execute(connection, transaction,
                "INSERT INTO snapshots(id,scan_id,category,observed_utc,evidence_json,item_count) VALUES($id,$run,$category,$observed,$json,$count)",
                ("$id", current.Id.ToString()), ("$run", run.Id.ToString()), ("$category", (int)result.Category),
                ("$observed", Utc(current.ObservedAtUtc)), ("$json", JsonSerializer.Serialize(current)), ("$count", current.Items.Count));
            foreach (var change in changes)
            {
                token.ThrowIfCancellationRequested();
                Execute(connection, transaction,
                    "INSERT INTO changes(id,snapshot_id,category,stable_key,kind,observed_from_utc,observed_to_utc,evidence_json,search_text,summary_json) VALUES($id,$snapshot,$category,$key,$kind,$from,$to,$json,$search,$summary)",
                    ("$id", change.Id), ("$snapshot", current.Id.ToString()), ("$category", (int)change.Category),
                    ("$key", change.StableKey), ("$kind", (int)change.Kind), ("$from", Utc(change.ObservedFromUtc)),
                    ("$to", Utc(change.ObservedToUtc)), ("$json", JsonSerializer.Serialize(change)),
                    ("$search", SearchText(change)), ("$summary", JsonSerializer.Serialize(ChangePreviews.Create(change))));
            }
            totalChanges += changes.Count;
            Execute(connection, transaction,
                "INSERT INTO baselines(category,snapshot_id) VALUES($category,$id) ON CONFLICT(category) DO UPDATE SET snapshot_id=excluded.snapshot_id",
                ("$category", (int)result.Category), ("$id", current.Id.ToString()));
        }
        token.ThrowIfCancellationRequested();
        transaction.Commit();
        return new ScanCommit(false, totalChanges);
    }

    private InventoryHistory Load(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!File.Exists(databasePath)) return InventoryHistory.Empty;
        using var connection = Open(token);
        using var transaction = connection.BeginTransaction(deferred: true);
        using var count = Command(connection, transaction, "SELECT COUNT(*) FROM scan_runs");
        var total = Convert.ToInt32(count.ExecuteScalar(), CultureInfo.InvariantCulture);
        using var last = Command(connection, transaction, "SELECT evidence_json FROM scan_runs ORDER BY completed_utc DESC,id DESC LIMIT 1");
        var lastRun = last.ExecuteScalar() is string json ? Deserialize<ScanRun>(json) : null;
        var baselines = ReadEvidence<Snapshot>(connection, transaction,
            "SELECT s.evidence_json FROM baselines b JOIN snapshots s ON b.snapshot_id=s.id ORDER BY b.category", token);
        var changes = ReadEvidence<Change>(connection, transaction,
            "SELECT evidence_json FROM changes ORDER BY observed_to_utc DESC,id DESC LIMIT 200", token);
        var counts = Enum.GetValues<InventoryCategory>().ToDictionary(c => c, _ => 0);
        using (var query = Command(connection, transaction, "SELECT category,COUNT(*) FROM changes GROUP BY category"))
        using (var reader = query.ExecuteReader())
            while (reader.Read()) counts[(InventoryCategory)reader.GetInt32(0)] = reader.GetInt32(1);
        token.ThrowIfCancellationRequested();
        transaction.Commit();
        return new InventoryHistory(total, lastRun, baselines, changes,
            new ReadOnlyDictionary<InventoryCategory, int>(counts));
    }

    private SqliteConnection Open(CancellationToken token)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false,
            DefaultTimeout = 5
        }.ToString());
        try
        {
            connection.Open();
            Execute(connection, null, "PRAGMA foreign_keys=ON");
            using var versionQuery = Command(connection, null, "PRAGMA user_version");
            var version = Convert.ToInt32(versionQuery.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (version > 2) throw new InvalidOperationException("Database schema is newer than this application");
            if (version == 0)
            {
                using var migration = connection.BeginTransaction(deferred: false);
                using var check = Command(connection, migration, "PRAGMA user_version");
                if (Convert.ToInt32(check.ExecuteScalar(), CultureInfo.InvariantCulture) == 0)
                {
                    using var tables = Command(connection, migration,
                        "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT GLOB 'sqlite_*'");
                    if (Convert.ToInt32(tables.ExecuteScalar(), CultureInfo.InvariantCulture) != 0)
                        throw new InvalidOperationException("Unrecognized unversioned database");
                    Execute(connection, migration, Schema);
                    Execute(connection, migration, "INSERT INTO store_info(id,is_demo) VALUES(1,$demo)", ("$demo", isDemo ? 1 : 0));
                }
                migration.Commit();
            }
            using var origin = Command(connection, null, "SELECT is_demo FROM store_info WHERE id=1");
            if (origin.ExecuteScalar() is not long storedOrigin || storedOrigin != (isDemo ? 1 : 0))
                throw new InvalidOperationException("Database origin mismatch");
            UpgradeQueries(connection, token);
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    private static string Utc(DateTimeOffset time) => time.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json)
        ?? throw new InvalidDataException("Missing persisted evidence");

    private static IReadOnlyList<T> ReadEvidence<T>(SqliteConnection connection, SqliteTransaction transaction,
        string sql, CancellationToken token)
    {
        using var command = Command(connection, transaction, sql);
        using var reader = command.ExecuteReader();
        var result = new List<T>();
        while (reader.Read())
        {
            token.ThrowIfCancellationRequested();
            result.Add(Deserialize<T>(reader.GetString(0)));
        }
        return result.AsReadOnly();
    }

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? transaction,
        string sql, params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return command;
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction? transaction,
        string sql, params (string Name, object Value)[] parameters)
    {
        using var command = Command(connection, transaction, sql, parameters);
        command.ExecuteNonQuery();
    }

    private const string Schema = """
        CREATE TABLE store_info(id INTEGER PRIMARY KEY CHECK(id=1),is_demo INTEGER NOT NULL CHECK(is_demo IN(0,1)));
        CREATE TABLE scan_runs(id TEXT PRIMARY KEY,input_hash TEXT NOT NULL,started_utc TEXT NOT NULL,completed_utc TEXT NOT NULL,is_demo INTEGER NOT NULL,evidence_json TEXT NOT NULL);
        CREATE TABLE collection_results(scan_id TEXT NOT NULL REFERENCES scan_runs(id),category INTEGER NOT NULL,status INTEGER NOT NULL,evidence_json TEXT NOT NULL,PRIMARY KEY(scan_id,category));
        CREATE TABLE snapshots(id TEXT PRIMARY KEY,scan_id TEXT NOT NULL REFERENCES scan_runs(id),category INTEGER NOT NULL,observed_utc TEXT NOT NULL,evidence_json TEXT NOT NULL,UNIQUE(scan_id,category),UNIQUE(id,category));
        CREATE TABLE baselines(category INTEGER PRIMARY KEY,snapshot_id TEXT NOT NULL,FOREIGN KEY(snapshot_id,category) REFERENCES snapshots(id,category));
        CREATE TABLE changes(id TEXT PRIMARY KEY,snapshot_id TEXT NOT NULL,category INTEGER NOT NULL,stable_key TEXT NOT NULL,kind INTEGER NOT NULL,observed_from_utc TEXT NOT NULL,observed_to_utc TEXT NOT NULL,evidence_json TEXT NOT NULL,UNIQUE(snapshot_id,stable_key),FOREIGN KEY(snapshot_id,category) REFERENCES snapshots(id,category));
        CREATE INDEX changes_observed ON changes(observed_to_utc DESC,id DESC);
        CREATE INDEX runs_completed ON scan_runs(completed_utc DESC,id DESC);
        PRAGMA user_version=1;
        """;
}
