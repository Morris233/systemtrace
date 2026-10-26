using System.Net.NetworkInformation;

namespace SystemTrace.Collectors;

public sealed class WindowsNetworkSource : INetworkSource
{
    public SourceBatch<AdapterEntry> Read(CancellationToken token)
    {
        var adapters = new List<AdapterEntry>();
        var errors = new List<string>();
        // Enumeration failure propagates as a failed collector, never a successful empty list.
        foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (!Guid.TryParse(network.Id, out var id) || id == Guid.Empty)
                { errors.Add("InvalidAdapterIdentity"); continue; }
                adapters.Add(new AdapterEntry(id, network.Name, network.Description,
                    network.NetworkInterfaceType.ToString(), network.OperationalStatus.ToString()));
            }
            catch (Exception exception) when (CollectorBase.IsReadFailure(exception))
            { errors.Add(CollectorBase.ErrorCode(exception)); }
        }
        return new SourceBatch<AdapterEntry>(adapters, errors);
    }
}
