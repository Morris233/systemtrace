using System.Security.Cryptography;
using System.Text;

namespace SystemTrace.App.Services;

public sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle activation;
    private RegisteredWaitHandle? wait;
    public bool IsOwner { get; }
    public SingleInstance(string identity)
    {
        var name = @"Local\SystemTrace." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..24];
        mutex = new Mutex(true, name, out var created);
        IsOwner = created;
        activation = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Activate");
        if (!created) activation.Set();
    }
    public void Listen(Action show) => wait = ThreadPool.RegisterWaitForSingleObject(activation, (_, _) => show(), null, Timeout.Infinite, false);
    public void Dispose()
    {
        wait?.Unregister(null);
        activation.Dispose();
        if (IsOwner) mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
