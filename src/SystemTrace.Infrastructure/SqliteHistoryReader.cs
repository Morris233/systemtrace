using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SystemTrace.Core;

namespace SystemTrace.Infrastructure;

public sealed partial class SqliteScanRepository
{
    public Task<DashboardSummary> GetDashboardAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(DashboardSummary.Empty, (connection, transaction, token) =>
        {
            using var countQuery = Command(connection, transaction, "SELECT COUNT(*) FROM scan_runs");
            var total = Convert.ToInt32(countQuery.ExecuteScalar(), CultureInfo.InvariantCulture);
            string? lastId = null;
            DateTimeOffset? attempted = null;
            using (var query = Command(connection, transaction, "SELECT id,completed_utc FROM scan_runs ORDER BY completed_utc DESC,id DESC LIMIT 1"))
            using (var reader = query.ExecuteReader())
                if (reader.Read()) { lastId = reader.GetString(0); attempted = ParseTime(reader.GetString(1)); }
            using var completeQuery = Command(connection, transaction,
                "SELECT r.completed_utc FROM scan_runs r WHERE (SELECT COUNT(*) FROM collection_results c WHERE c.scan_id=r.id AND c.status=0)=4 ORDER BY r.completed_utc DESC,r.id DESC LIMIT 1");
            var complete = completeQuery.ExecuteScalar() is string time ? ParseTime(time) : (DateTimeOffset?)null;
            var categories = new List<CategoryOverview>();
            foreach (var category in Enum.GetValues<InventoryCategory>())
            {
                token.ThrowIfCancellationRequested();
                CollectionStatus? status = null;
                var skipped = 0;
                string? error = null;
                IReadOnlyList<string> warnings = [];
                using (var query = Command(connection, transaction,
                    "SELECT status,COALESCE(json_extract(evidence_json,'$.SkippedItemCount'),0),COALESCE(json_extract(evidence_json,'$.Warnings'),'[]'),json_extract(evidence_json,'$.ErrorCode') FROM collection_results WHERE scan_id=$id AND category=$category",
                    ("$id", (object?)lastId ?? DBNull.Value), ("$category", (int)category)))
                using (var reader = query.ExecuteReader())
                    if (reader.Read())
                    {
                        status = (CollectionStatus)reader.GetInt32(0);
                        skipped = reader.GetInt32(1);
                        warnings = Array.AsReadOnly(JsonSerializer.Deserialize<string[]>(reader.GetString(2)) ?? []);
                        error = reader.IsDBNull(3) ? null : reader.GetString(3);
                    }
                int? items = null;
                DateTimeOffset? baselineTime = null;
                using (var query = Command(connection, transaction,
                    "SELECT s.item_count,s.observed_utc FROM baselines b JOIN snapshots s ON b.snapshot_id=s.id WHERE b.category=$category", ("$category", (int)category)))
                using (var reader = query.ExecuteReader())
                    if (reader.Read()) { items = reader.GetInt32(0); baselineTime = ParseTime(reader.GetString(1)); }
                using var totalQuery = Command(connection, transaction, "SELECT COUNT(*) FROM changes WHERE category=$category", ("$category", (int)category));
                var changes = (long)totalQuery.ExecuteScalar()!;
                int? current = null;
                if (status == CollectionStatus.Succeeded)
                {
                    using var latest = Command(connection, transaction,
                        "SELECT COUNT(*) FROM changes c JOIN snapshots s ON c.snapshot_id=s.id WHERE s.scan_id=$id AND c.category=$category",
                        ("$id", lastId!), ("$category", (int)category));
                    current = Convert.ToInt32(latest.ExecuteScalar(), CultureInfo.InvariantCulture);
                }
                categories.Add(new CategoryOverview(category, status, items, baselineTime, changes, current, skipped, warnings, error));
            }
            return new DashboardSummary(total, attempted, complete, categories.AsReadOnly(),
                ReadEvidence<ChangePreview>(connection, transaction, "SELECT summary_json FROM changes ORDER BY observed_to_utc DESC,id DESC LIMIT 8", token));
        }, cancellationToken);

    public Task<PageResult<ChangePreview>> QueryChangesAsync(ChangeQuery query, CancellationToken cancellationToken = default)
    {
        query.Validate();
        return ReadAsync(new PageResult<ChangePreview>([], 0, 0, query.PageSize), (connection, transaction, token) =>
        {
            var args = new (string, object)[]
            {
                ("$text", NormalizeSearch(query.Text.Trim())), ("$category", query.Category is { } category ? (int)category : DBNull.Value),
                ("$from", query.FromUtc is { } from ? Utc(from) : DBNull.Value), ("$to", query.ToUtc is { } to ? Utc(to) : DBNull.Value)
            };
            const string filter = " WHERE ($text='' OR instr(search_text,$text)>0) AND ($category IS NULL OR category=$category) AND ($from IS NULL OR observed_to_utc >= $from) AND ($to IS NULL OR observed_from_utc <= $to)";
            using var count = Command(connection, transaction, "SELECT COUNT(*) FROM changes" + filter, args);
            var total = (long)count.ExecuteScalar()!;
            var page = ClampPage(query.Page, query.PageSize, total);
            using var items = Command(connection, transaction, "SELECT summary_json FROM changes" + filter + " ORDER BY observed_to_utc DESC,id DESC LIMIT $limit OFFSET $offset",
                [.. args, ("$limit", query.PageSize), ("$offset", (long)page * query.PageSize)]);
            return new PageResult<ChangePreview>(ReadRows<ChangePreview>(items, token), total, page, query.PageSize);
        }, cancellationToken);
    }

    public Task<Change?> GetChangeAsync(string id, CancellationToken cancellationToken = default) =>
        ReadAsync<Change?>(null, (connection, transaction, token) =>
        {
            using var query = Command(connection, transaction, "SELECT evidence_json FROM changes WHERE id=$id", ("$id", id));
            return query.ExecuteScalar() is string json ? Deserialize<Change>(json) : null;
        }, cancellationToken);

    public Task<PageResult<SnapshotHeader>> QuerySnapshotsAsync(InventoryCategory category, int page = 0, int pageSize = 40, CancellationToken cancellationToken = default)
    {
        new ChangeQuery(Category: category, Page: page, PageSize: pageSize).Validate();
        return ReadAsync(new PageResult<SnapshotHeader>([], 0, 0, pageSize), (connection, transaction, token) =>
        {
            using var count = Command(connection, transaction, "SELECT COUNT(*) FROM snapshots WHERE category=$category", ("$category", (int)category));
            var total = (long)count.ExecuteScalar()!;
            var selectedPage = ClampPage(page, pageSize, total);
            using var query = Command(connection, transaction,
                "SELECT id,scan_id,category,observed_utc,item_count FROM snapshots WHERE category=$category ORDER BY observed_utc DESC,id DESC LIMIT $limit OFFSET $offset",
                ("$category", (int)category), ("$limit", pageSize), ("$offset", (long)selectedPage * pageSize));
            using var reader = query.ExecuteReader();
            var items = new List<SnapshotHeader>();
            while (reader.Read())
            {
                token.ThrowIfCancellationRequested();
                items.Add(new SnapshotHeader(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)),
                    (InventoryCategory)reader.GetInt32(2), ParseTime(reader.GetString(3)), reader.GetInt32(4)));
            }
            return new PageResult<SnapshotHeader>(items.AsReadOnly(), total, selectedPage, pageSize);
        }, cancellationToken);
    }

    public Task<PageResult<Change>> CompareSnapshotsAsync(Guid beforeId, Guid afterId, int page = 0, int pageSize = 40, CancellationToken cancellationToken = default)
    {
        new ChangeQuery(Page: page, PageSize: pageSize).Validate();
        if (beforeId == afterId) throw new ArgumentException("Choose distinct snapshots");
        return ReadAsync(new PageResult<Change>([], 0, 0, pageSize), (connection, transaction, token) =>
        {
            Snapshot Read(Guid id)
            {
                using var query = Command(connection, transaction, "SELECT evidence_json FROM snapshots WHERE id=$id", ("$id", id.ToString()));
                return query.ExecuteScalar() is string json ? Deserialize<Snapshot>(json) : throw new KeyNotFoundException("Snapshot not found");
            }
            var before = Read(beforeId);
            var after = Read(afterId);
            var changes = diff.Compare(before, after, token);
            var selectedPage = ClampPage(page, pageSize, changes.Count);
            return new PageResult<Change>(Array.AsReadOnly(changes.Skip(selectedPage * pageSize).Take(pageSize).ToArray()), changes.Count, selectedPage, pageSize);
        }, cancellationToken, missingIsError: true);
    }

    private Task<T> ReadAsync<T>(T empty, Func<SqliteConnection, SqliteTransaction, CancellationToken, T> read,
        CancellationToken token, bool missingIsError = false) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        if (!File.Exists(databasePath)) return missingIsError ? throw new KeyNotFoundException("Store not found") : empty;
        using var connection = Open(token);
        using var transaction = connection.BeginTransaction(deferred: true);
        var result = read(connection, transaction, token);
        token.ThrowIfCancellationRequested();
        transaction.Commit();
        return result;
    }, token);

    private static IReadOnlyList<T> ReadRows<T>(SqliteCommand command, CancellationToken token)
    {
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read()) { token.ThrowIfCancellationRequested(); rows.Add(Deserialize<T>(reader.GetString(0))); }
        return rows.AsReadOnly();
    }

    private static int ClampPage(int requested, int size, long total) =>
        (int)Math.Min(requested, Math.Max(0, (total - 1) / size));
    private static DateTimeOffset ParseTime(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture).ToUniversalTime();
    private static string NormalizeSearch(string value) => value.Normalize(NormalizationForm.FormC).ToUpperInvariant();
    private static string SearchText(Change change) => NormalizeSearch(string.Join('\n',
        new[] { change.StableKey, change.Category.ToString(), change.Kind.ToString() }
            .Concat((change.Before?.Fields ?? new Dictionary<string, FieldValue>()).SelectMany(f => new[] { f.Key, f.Value.Value ?? "", f.Value.ErrorCode ?? "" }))
            .Concat((change.After?.Fields ?? new Dictionary<string, FieldValue>()).SelectMany(f => new[] { f.Key, f.Value.Value ?? "", f.Value.ErrorCode ?? "" }))));

    private static void UpgradeQueries(SqliteConnection connection, CancellationToken token)
    {
        using var check = Command(connection, null, "PRAGMA user_version");
        if ((long)check.ExecuteScalar()! != 1) return;
        using var transaction = connection.BeginTransaction(deferred: false);
        using var lockedCheck = Command(connection, transaction, "PRAGMA user_version");
        if ((long)lockedCheck.ExecuteScalar()! != 1) { transaction.Commit(); return; }
        Execute(connection, transaction, """
            ALTER TABLE changes ADD COLUMN search_text TEXT NOT NULL DEFAULT '';
            ALTER TABLE changes ADD COLUMN summary_json TEXT NOT NULL DEFAULT '{}';
            ALTER TABLE snapshots ADD COLUMN item_count INTEGER NOT NULL DEFAULT 0;
            UPDATE snapshots SET item_count=json_array_length(evidence_json,'$.Items');
            CREATE INDEX changes_category_observed ON changes(category,observed_to_utc DESC,id DESC);
            CREATE INDEX snapshots_category_observed ON snapshots(category,observed_utc DESC,id DESC);
            """);
        long lastRow = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var batch = new List<(long Row, Change Change)>();
            using (var query = Command(connection, transaction, "SELECT rowid,evidence_json FROM changes WHERE rowid>$last ORDER BY rowid LIMIT 128", ("$last", lastRow)))
            using (var reader = query.ExecuteReader())
                while (reader.Read()) batch.Add((reader.GetInt64(0), Deserialize<Change>(reader.GetString(1))));
            if (batch.Count == 0) break;
            foreach (var (row, change) in batch)
            {
                Execute(connection, transaction, "UPDATE changes SET search_text=$search,summary_json=$summary WHERE rowid=$row",
                    ("$row", row), ("$search", SearchText(change)), ("$summary", JsonSerializer.Serialize(ChangePreviews.Create(change))));
                lastRow = row;
            }
        }
        token.ThrowIfCancellationRequested();
        Execute(connection, transaction, "PRAGMA user_version=2");
        transaction.Commit();
    }
}
