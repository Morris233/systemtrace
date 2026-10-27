using System.Globalization;
using SystemTrace.App.Services;
using SystemTrace.Core;

namespace SystemTrace.App.ViewModels;

public sealed record OverviewPageViewModel(MainViewModel Model);
public sealed record TimelinePageViewModel(MainViewModel Model);
public sealed record SnapshotsPageViewModel(MainViewModel Model);
public sealed record TroubleshootPageViewModel(MainViewModel Model);
public sealed record SettingsPageViewModel(MainViewModel Model);
public sealed record CategoryOption(InventoryCategory? Value, string Label);
public sealed record WindowOption(int Hours, string Label);
public sealed record CategoryRow(string Label, string ChangeCount, string ItemCount, string Status, string Hint);
public sealed record ChangeRow(ChangePreview Preview, string Label, string Category, string Kind, string Values, string Window);
public sealed record FieldRow(string Name, string Before, string After, bool Changed, string? BeforeError, string? AfterError);
public sealed record ChangeDetail(Change Evidence, string Title, string Category, string Kind, string Window, string Identity, IReadOnlyList<FieldRow> Fields);
public sealed record SnapshotOption(SnapshotHeader Header)
{
    public string Label => $"{Header.ObservedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss} · {Header.ItemCount}";
}

public sealed class HistoryPresenter(IAppearanceService appearance)
{
    public string Text(string key) => appearance.Text(key);
    public string Time(DateTimeOffset? time) => time?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture) ?? "—";
    public string Page(int page, int total) => string.Format(CultureInfo.CurrentCulture, Text("PageFormat"), page + 1, total);
    public string Count(long count) => string.Format(CultureInfo.CurrentCulture, Text("ResultCountFormat"), count);
    public CategoryOption[] Categories(bool all) => (all ? new[] { new CategoryOption(null, Text("AllCategories")) } : [])
        .Concat(Enum.GetValues<InventoryCategory>().Select(c => new CategoryOption(c, Text(c.ToString())))).ToArray();
    public WindowOption[] Windows() => [new(1, Text("OneHour")), new(6, Text("SixHours")), new(24, Text("OneDay")), new(168, Text("SevenDays"))];

    public CategoryRow Category(CategoryOverview category)
    {
        var hint = string.Join("\n", category.Warnings.Select(code => Text(code is "SoftwareCoverageLimited" or "StartupCoverageLimited"
            or "RunOnceTransient" or "PresentDevicesOnly" or "NetworkOperationalStatusOnly" or "AccessDenied" ? code : "ReadWarning")));
        if (category.ErrorCode is not null && hint.Length == 0) hint = Text("ReadWarning");
        if (category.SkippedItems > 0) hint += "\n" + string.Format(CultureInfo.CurrentCulture, Text("SkippedItems"), category.SkippedItems);
        if (category.BaselineAtUtc is { } observed) hint += "\n" + Text("BaselineAt") + " " + Time(observed);
        return new CategoryRow(Text(category.Category.ToString()), category.CurrentChanges?.ToString(CultureInfo.CurrentCulture) ?? "—",
            category.BaselineItems is { } items ? string.Format(CultureInfo.CurrentCulture, Text("BaselineItemsFormat"), items) : Text("NoBaseline"),
            Text(category.Status?.ToString() ?? "NotScanned"), hint.Trim());
    }

    public ChangeRow Row(ChangePreview preview) => new(preview,
        string.IsNullOrWhiteSpace(preview.Label) ? Text(preview.Category.ToString()) : preview.Label,
        Text(preview.Category.ToString()), Kind(preview.Category, preview.Kind),
        $"{Value(preview.Before, preview.FieldName)} → {Value(preview.After, preview.FieldName)}",
        $"{Time(preview.ObservedFromUtc)} → {Time(preview.ObservedToUtc)}");

    public ChangeDetail Detail(Change change)
    {
        var item = change.After ?? change.Before!;
        var fields = (change.Before?.Fields.Keys ?? []).Union(change.After?.Fields.Keys ?? [], StringComparer.Ordinal)
            .OrderByDescending(name => change.Fields.Any(delta => delta.Name == name)).ThenBy(name => name, StringComparer.Ordinal).Select(name =>
            {
                var before = change.Before?.Fields.GetValueOrDefault(name);
                var after = change.After?.Fields.GetValueOrDefault(name);
                var key = "Field_" + name;
                var label = Text(key);
                return new FieldRow(label == key ? name : label, Value(before, name), Value(after, name),
                    before != after, before?.ErrorCode, after?.ErrorCode);
            }).ToArray();
        return new ChangeDetail(change, item.Fields.GetValueOrDefault("display_name")?.Value ?? Text(change.Category.ToString()),
            Text(change.Category.ToString()), Kind(change.Category, change.Kind),
            $"{Time(change.ObservedFromUtc)} → {Time(change.ObservedToUtc)}", change.StableKey, fields);
    }

    private string Kind(InventoryCategory category, ChangeKind kind) => Text(category == InventoryCategory.Drivers && kind == ChangeKind.Removed ? "NotDetected" : kind.ToString());
    private string Value(FieldValue? field, string name) => field switch
    {
        null => Text("ValueMissing"),
        { ErrorCode: not null } => Text("ValueUnreadable"),
        { Value: null } => Text("ValueNull"),
        { Value.Length: 0 } => Text("ValueEmpty"),
        _ => name == "operational_status" ? Text(field.Value!) : field.Value!
    };
}
