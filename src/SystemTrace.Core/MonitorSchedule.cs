namespace SystemTrace.Core;

public sealed class MonitorSchedule
{
    private (bool Enabled, bool Paused, int Minutes)? configuration;
    public DateTimeOffset? NextDueUtc { get; private set; }
    public void Configure(bool enabled, bool paused, int minutes, DateTimeOffset now)
    {
        if (minutes is < 5 or > 1440) throw new ArgumentOutOfRangeException(nameof(minutes));
        var next = (enabled, paused, minutes);
        if (configuration == next) return;
        configuration = next;
        NextDueUtc = enabled && !paused ? now.AddMinutes(minutes) : null;
    }
    public bool TakeDue(DateTimeOffset now, bool canScan)
    {
        if (NextDueUtc is not { } due || now < due || configuration is not { } options) return false;
        NextDueUtc = now.AddMinutes(options.Minutes);
        return canScan;
    }
}
