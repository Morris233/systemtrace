using System.IO;
using Microsoft.Win32;
using SystemTrace.Core;

namespace SystemTrace.App.Services;

public sealed class WindowsLoginStart : ILoginStart
{
    private readonly string path;
    private readonly string valueName;
    private readonly string command;
    public WindowsLoginStart(string executable, string path = @"Software\Microsoft\Windows\CurrentVersion\Run", string valueName = "SystemTrace")
    {
        if (!Path.IsPathFullyQualified(executable) || executable.Contains('"') || executable.Contains('\n') || executable.Contains('\r'))
            throw new ArgumentException("Invalid executable path");
        this.path = path;
        this.valueName = valueName;
        command = "\"" + executable + "\" --login";
    }
    public bool IsEnabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(path, false); return key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string == command; }
    }
    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            using var key = Registry.CurrentUser.CreateSubKey(path, true);
            var existing = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (existing is not null && !Equals(existing, command)) throw new InvalidOperationException("Login entry is owned by another path");
            key.SetValue(valueName, command, RegistryValueKind.String);
        }
        else
        {
            using var key = Registry.CurrentUser.OpenSubKey(path, true);
            if (Equals(key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames), command))
                key!.DeleteValue(valueName, false);
        }
    }
}
