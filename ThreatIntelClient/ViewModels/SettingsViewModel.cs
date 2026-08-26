using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using ThreatIntelClient.Services;
using System;
using System.Collections.Generic;

namespace ThreatIntelClient.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly SettingsService _settingsService;
    private readonly LocalDatabaseService _dbService;

    [ObservableProperty]
    private bool _wifiOnlySync;

    [ObservableProperty]
    private string _themeMode;

    [ObservableProperty]
    private double _offlineRetentionYears;

    [ObservableProperty]
    private string _databaseMetrics;

    public List<string> Themes { get; } = new() { "System Default", "Light", "Dark" };

    public SettingsViewModel(SettingsService settingsService, LocalDatabaseService dbService)
    {
        _settingsService = settingsService;
        _dbService = dbService;
        Title = "Settings";

        WifiOnlySync = _settingsService.WifiOnlySync;
        ThemeMode = _settingsService.ThemeMode;
        OfflineRetentionYears = _settingsService.OfflineRetentionYears;
    }

    partial void OnWifiOnlySyncChanged(bool value)
    {
        _settingsService.WifiOnlySync = value;
    }

    partial void OnThemeModeChanged(string value)
    {
        _settingsService.ThemeMode = value;
        // MAUI App Theme update logic could go here
    }

    partial void OnOfflineRetentionYearsChanged(double value)
    {
        _settingsService.OfflineRetentionYears = value;
    }

    [RelayCommand]
    public async Task RefreshMetricsAsync()
    {
        var (cveCount, newsCount) = await _dbService.GetDatabaseMetricsAsync();
        DatabaseMetrics = $"CVEs: {cveCount} | News Articles: {newsCount}";
    }

    [RelayCommand]
    public async Task PruneDatabaseAsync()
    {
        IsBusy = true;
        try
        {
            int cutoffYear = DateTime.Now.Year - (int)OfflineRetentionYears;
            await _dbService.PruneDatabaseAsync(cutoffYear);
            await RefreshMetricsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
