using Microsoft.Win32;

namespace ClaudeUsageMonitor.Services;

public static class StartupManager
{
    private const string AppName = "ClaudeUsageMonitor";
    private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
        return key?.GetValue(AppName) != null;
    }

    public static void EnableStartup()
    {
        var exePath = Application.ExecutablePath;
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
        key?.SetValue(AppName, $"\"{exePath}\" --minimized");
    }

    public static void DisableStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
        key?.DeleteValue(AppName, false);
    }
}
