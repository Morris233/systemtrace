using SystemTrace.Core;
using Xunit;

namespace SystemTrace.Tests;

public sealed class InventoryTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-10-26T00:00:00Z");
    internal static SnapshotItem Item(string key = "demo/item", string? value = "1", string field = "value") =>
        new(key, new Dictionary<string, FieldValue> { [field] = new(value) });
    private static Snapshot Snapshot(SnapshotItem[] items, int seconds = 0, InventoryCategory category = InventoryCategory.Software) =>
        new(Guid.NewGuid(), Guid.NewGuid(), category, Time.AddSeconds(seconds), items);

    [Fact]
    public void FirstSuccessIsBaselineAndOrderDoesNotCreateAChange()
    {
        var engine = new DiffEngine();
        var a = new SnapshotItem("a", new Dictionary<string, FieldValue> { ["z"] = new("2"), ["a"] = new("1") });
        var reordered = new SnapshotItem("a", new Dictionary<string, FieldValue> { ["a"] = new("1"), ["z"] = new("2") });
        var before = Snapshot([a, Item("b")]);
        Assert.Empty(engine.Compare(null, before));
        Assert.Empty(engine.Compare(before, Snapshot([Item("b"), reordered], 1)));
    }

    [Fact]
    public void AddsRemovesAndModifiesKeepEvidenceAndObservationWindow()
    {
        var before = Snapshot([Item("removed"), Item("updated", "1")]);
        var after = Snapshot([Item("added"), Item("updated", "2")], 10);
        var engine = new DiffEngine();
        var changes = engine.Compare(before, after);
        Assert.Equal(3, changes.Count);
        Assert.Equal(new[] { ChangeKind.Added, ChangeKind.Removed, ChangeKind.Modified }, changes.Select(c => c.Kind));
        var modification = Assert.Single(changes, c => c.Kind == ChangeKind.Modified);
        Assert.Equal("1", Assert.Single(modification.Fields).Before!.Value);
        Assert.Equal("2", modification.After!.Fields["value"].Value);
        Assert.All(changes, c => { Assert.Equal(Time, c.ObservedFromUtc); Assert.Equal(Time.AddSeconds(10), c.ObservedToUtc); });
        Assert.Equal(changes.Select(c => c.Id), engine.Compare(before, after).Select(c => c.Id));
    }

    [Fact]
    public void MissingNullEmptyAndWhitespaceAreDifferent()
    {
        var engine = new DiffEngine();
        var missing = new SnapshotItem("demo/item", new Dictionary<string, FieldValue>());
        Assert.Single(engine.Compare(Snapshot([missing]), Snapshot([Item(value: null)], 1)));
        Assert.Single(engine.Compare(Snapshot([Item(value: null)]), Snapshot([Item(value: "")], 1)));
        Assert.Single(engine.Compare(Snapshot([Item(value: "")]), Snapshot([Item(value: " ")], 1)));
    }

    [Fact]
    public void IdentityNormalizesWindowsSourcesWithoutLosingScope()
    {
        Assert.Equal(StableKeys.Software("HKLM", "Registry64", "Software/App"), StableKeys.Software("hklm", "registry64", @"software\app"));
        Assert.NotEqual(StableKeys.Software("HKLM", "Registry64", "App"), StableKeys.Software("HKLM", "Registry32", "App"));
        Assert.NotEqual(StableKeys.Software("HKCU", "Registry64", "App"), StableKeys.Software("HKLM", "Registry64", "App"));
        Assert.NotEqual(StableKeys.StartupRegistry("A", "B", "C:D", "E"), StableKeys.StartupRegistry("A", "B", "C", "D:E"));
        Assert.Equal(StableKeys.Device("abc"), StableKeys.Device("ABC"));
        Assert.Equal(StableKeys.StartupFile("User", "a/b"), StableKeys.StartupFile("user", @"A\B"));
        Assert.Throws<ArgumentException>(() => StableKeys.Adapter(Guid.Empty));
    }

    [Fact]
    public void ContractsCopyInputsAndRejectDuplicateOrInvalidEvidence()
    {
        var fields = new Dictionary<string, FieldValue> { ["value"] = new("original") };
        var item = new SnapshotItem("demo/item", fields);
        fields["value"] = new("mutated");
        Assert.Equal("original", item.Fields["value"].Value);
        Assert.Throws<ArgumentException>(() => new CollectionResult(InventoryCategory.Software, CollectionStatus.Succeeded, Time, Time, [item, item]));
        var unreadable = new SnapshotItem("x", new Dictionary<string, FieldValue> { ["value"] = new(null, "AccessDenied") });
        Assert.Throws<ArgumentException>(() => new CollectionResult(InventoryCategory.Software, CollectionStatus.Succeeded, Time, Time, [unreadable]));
        Assert.Throws<ArgumentException>(() => new CollectionResult(InventoryCategory.Software, CollectionStatus.Failed, Time, Time, [item]));
        Assert.Throws<ArgumentException>(() => new ScanRun(Guid.NewGuid(), Time, Time, true, []));
        Assert.Throws<ArgumentException>(() => new DiffEngine().Compare(Snapshot([item]), Snapshot([item], -1)));
        Assert.Throws<ArgumentException>(() => new DiffEngine().Compare(Snapshot([item]), Snapshot([item], 1, InventoryCategory.Network)));
    }
}
