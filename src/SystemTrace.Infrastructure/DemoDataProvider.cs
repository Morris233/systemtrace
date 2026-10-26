using SystemTrace.Core;

namespace SystemTrace.Infrastructure;

public sealed class DemoDataProvider : IDemoDataProvider
{
    public DemoOverview GetOverview() => new(true, Array.AsReadOnly(new[]
    {
        new DemoCategory("Software", 3),
        new DemoCategory("Startup", 2),
        new DemoCategory("Drivers", 1),
        new DemoCategory("Network", 2)
    }));
}
