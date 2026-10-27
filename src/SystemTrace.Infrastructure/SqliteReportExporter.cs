using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SystemTrace.Core;

namespace SystemTrace.Infrastructure;

public sealed partial class SqliteScanRepository : IReportExporter
{
    public Task ExportAsync(Stream destination, ReportFormat format, ChangeQuery query, bool redact = true, CancellationToken token = default)
    {
        query.Validate();
        if (!Enum.IsDefined(format)) throw new ArgumentOutOfRangeException(nameof(format));
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            using var json = format == ReportFormat.Json ? new Utf8JsonWriter(destination, new JsonWriterOptions { Indented = true }) : null;
            using var csv = format == ReportFormat.Csv ? new StreamWriter(destination, new UTF8Encoding(true), leaveOpen: true) : null;
            if (json is not null) { json.WriteStartObject(); json.WriteNumber("schemaVersion", 1); json.WriteBoolean("redacted", redact); json.WriteString("timeMeaning", "observationWindow"); json.WriteStartArray("changes"); }
            csv?.WriteLine(Csv(["Category", "Kind", "ObservedFromUtc", "ObservedToUtc", "Item", "Field", "BeforeState", "Before", "AfterState", "After"]));
            if (File.Exists(databasePath))
            {
                using var connection = Open(token);
                using var transaction = connection.BeginTransaction(deferred: true);
                using var command = Command(connection, transaction, """
                    SELECT evidence_json FROM changes
                    WHERE ($text='' OR instr(search_text,$text)>0) AND ($category IS NULL OR category=$category)
                    AND ($from IS NULL OR observed_to_utc >= $from) AND ($to IS NULL OR observed_from_utc <= $to)
                    ORDER BY observed_to_utc DESC,id DESC
                    """, ("$text", NormalizeSearch(query.Text.Trim())), ("$category", query.Category is { } c ? (int)c : DBNull.Value),
                    ("$from", query.FromUtc is { } f ? Utc(f) : DBNull.Value), ("$to", query.ToUtc is { } t ? Utc(t) : DBNull.Value));
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    token.ThrowIfCancellationRequested();
                    var change = Deserialize<Change>(reader.GetString(0));
                    var identity = redact ? "item-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(change.StableKey)))[..16] : change.StableKey;
                    var fields = (change.Before?.Fields.Keys ?? []).Union(change.After?.Fields.Keys ?? [], StringComparer.Ordinal).Order(StringComparer.Ordinal)
                        .Select(name => new ExportField(name, ExportValue.Create(change.Before?.Fields.GetValueOrDefault(name), name, redact),
                            ExportValue.Create(change.After?.Fields.GetValueOrDefault(name), name, redact),
                            change.Fields.Any(field => field.Name == name))).ToArray();
                    if (json is not null)
                        JsonSerializer.Serialize(json, new ExportChange(change.Category.ToString(), change.Kind.ToString(),
                            change.ObservedFromUtc, change.ObservedToUtc, identity, fields));
                    if (csv is not null)
                        foreach (var field in fields)
                            csv.WriteLine(Csv([change.Category.ToString(), change.Kind.ToString(), Utc(change.ObservedFromUtc), Utc(change.ObservedToUtc),
                                identity, field.Name, field.Before.State, field.Before.Value ?? "", field.After.State, field.After.Value ?? ""]));
                }
                token.ThrowIfCancellationRequested();
                transaction.Commit();
            }
            if (json is not null) { json.WriteEndArray(); json.WriteEndObject(); json.Flush(); }
            csv?.Flush();
        }, token);
    }

    private static string Csv(IEnumerable<string> values) => string.Join(',', values.Select(value =>
    {
        var trimmed = value.TrimStart();
        if (value.Length > 0 && (value[0] is '\t' or '\r' or '\n' || trimmed.Length > 0 && trimmed[0] is '=' or '+' or '-' or '@'))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }));
    private sealed record ExportChange(string Category, string Kind, DateTimeOffset ObservedFromUtc, DateTimeOffset ObservedToUtc, string Item, IReadOnlyList<ExportField> Fields);
    private sealed record ExportField(string Name, ExportValue Before, ExportValue After, bool Changed);
    private sealed record ExportValue(string State, string? Value, string? ErrorCode)
    {
        private static readonly HashSet<string> SafeFields = ["version", "driver_version", "driver_date", "last_write_utc", "system_component",
            "class", "interface_type", "operational_status", "source", "scope", "registry_hive", "registry_view"];
        public static ExportValue Create(FieldValue? field, string name, bool redact)
        {
            if (field is null) return new("missing", null, null);
            if (field.ErrorCode is not null) return new("unreadable", null, field.ErrorCode);
            if (field.Value is null) return new("null", null, null);
            if (field.Value.Length == 0) return new("empty", "", null);
            var value = field.Value;
            if (redact)
                value = SafeFields.Contains(name)
                    ? Regex.Replace(value, @"(?i)[a-z]:[\\/]+Users[\\/]+[^\\/\s""'<>]+", "<user>", RegexOptions.CultureInvariant)
                    : "[redacted]";
            return new("value", value, null);
        }
    }
}
