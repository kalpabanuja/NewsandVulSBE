using Microsoft.Maui.Storage;
using System;

namespace ThreatIntelClient.Services;

public class SettingsService
{
    public bool WifiOnlySync
    {
        get => Preferences.Default.Get(nameof(WifiOnlySync), true);
        set => Preferences.Default.Set(nameof(WifiOnlySync), value);
    }

    public string ThemeMode
    {
        get => Preferences.Default.Get(nameof(ThemeMode), "System Default");
        set => Preferences.Default.Set(nameof(ThemeMode), value);
    }

    public double OfflineRetentionYears
    {
        get => Preferences.Default.Get(nameof(OfflineRetentionYears), 5.0);
        set => Preferences.Default.Set(nameof(OfflineRetentionYears), value);
    }
}
