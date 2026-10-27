using SystemTrace.Core;

namespace SystemTrace.Infrastructure;

public sealed partial class SqliteScanRepository : IHistoryMaintenance
{
    public Task ClearAsync(CancellationToken token = default) => MaintainAsync((connection, transaction) =>
        Execute(connection, transaction, """
            DELETE FROM changes;
            DELETE FROM baselines;
            DELETE FROM snapshots;
            DELETE FROM collection_results;
            DELETE FROM scan_runs;
            """), token);

    public Task PruneAsync(DateTimeOffset cutoffUtc, CancellationToken token = default) =>
        MaintainAsync((connection, transaction) =>
        {
            // Preserve category baselines and the latest attempt even when older than the chosen retention.
            Execute(connection, transaction, "DELETE FROM changes WHERE observed_to_utc<$cutoff", ("$cutoff", Utc(cutoffUtc)));
            token.ThrowIfCancellationRequested();
            Execute(connection, transaction, """
                DELETE FROM snapshots WHERE observed_utc<$cutoff AND id NOT IN(SELECT snapshot_id FROM baselines);
                DELETE FROM collection_results WHERE scan_id IN(
                    SELECT id FROM scan_runs WHERE completed_utc<$cutoff
                    AND id NOT IN(SELECT scan_id FROM snapshots)
                    AND id<>(SELECT id FROM scan_runs ORDER BY completed_utc DESC,id DESC LIMIT 1));
                DELETE FROM scan_runs WHERE completed_utc<$cutoff
                    AND id NOT IN(SELECT scan_id FROM snapshots)
                    AND id<>(SELECT id FROM scan_runs ORDER BY completed_utc DESC,id DESC LIMIT 1);
                """, ("$cutoff", Utc(cutoffUtc)));
        }, token);

    private Task MaintainAsync(Action<Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite.SqliteTransaction> action,
        CancellationToken token) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        if (!File.Exists(databasePath)) return;
        using var connection = Open(token);
        Execute(connection, null, "PRAGMA secure_delete=ON");
        using var transaction = connection.BeginTransaction(deferred: false);
        action(connection, transaction);
        token.ThrowIfCancellationRequested();
        transaction.Commit();
    }, token);
}
