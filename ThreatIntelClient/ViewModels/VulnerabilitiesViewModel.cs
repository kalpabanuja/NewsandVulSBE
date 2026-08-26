using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using ThreatIntelClient.Models;
using ThreatIntelClient.Services;
using System.Collections.Generic;

namespace ThreatIntelClient.ViewModels;

public partial class VulnerabilitiesViewModel : BaseViewModel
{
    private readonly LocalDatabaseService _dbService;
    private CancellationTokenSource _searchCts;
    private int _currentOffset = 0;
    private const int PageSize = 50;

    public ObservableCollection<Cve> Cves { get; } = new();

    [ObservableProperty]
    private string _searchQuery;

    [ObservableProperty]
    private string _selectedSeverity = "ALL";

    public List<string> Severities { get; } = new() { "ALL", "CRITICAL", "HIGH", "MEDIUM", "LOW" };

    public VulnerabilitiesViewModel(LocalDatabaseService dbService)
    {
        _dbService = dbService;
        Title = "Vulnerabilities";
    }

    partial void OnSearchQueryChanged(string value)
    {
        DebounceSearch();
    }

    partial void OnSelectedSeverityChanged(string value)
    {
        _ = LoadCvesAsync(true);
    }

    private void DebounceSearch()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        Task.Delay(300, token).ContinueWith(async t =>
        {
            if (t.IsCompletedSuccessfully)
            {
                await MainThread.InvokeOnMainThreadAsync(() => LoadCvesAsync(true));
            }
        }, TaskScheduler.Default);
    }

    [RelayCommand]
    public async Task LoadCvesAsync(bool refresh = false)
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            if (refresh)
            {
                _currentOffset = 0;
                Cves.Clear();
            }

            var cves = await _dbService.SearchCvesAsync(SearchQuery, SelectedSeverity, PageSize, _currentOffset);
            
            foreach (var cve in cves)
            {
                Cves.Add(cve);
            }

            _currentOffset += cves.Count;
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    [RelayCommand]
    public async Task LoadMoreAsync()
    {
        await LoadCvesAsync(false);
    }
}
