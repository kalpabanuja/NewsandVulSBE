using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using ThreatIntelClient.Models;
using ThreatIntelClient.Services;
using ThreatIntelClient.Views;

namespace ThreatIntelClient.ViewModels;

public partial class NewsViewModel : BaseViewModel
{
    private readonly LocalDatabaseService _dbService;
    private CancellationTokenSource _searchCts;
    private int _currentOffset = 0;
    private const int PageSize = 50;

    public ObservableCollection<NewsArticle> Articles { get; } = new();

    [ObservableProperty]
    private string _searchQuery;

    public NewsViewModel(LocalDatabaseService dbService)
    {
        _dbService = dbService;
        Title = "News";
    }

    partial void OnSearchQueryChanged(string value)
    {
        DebounceSearch();
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
                await MainThread.InvokeOnMainThreadAsync(() => LoadArticlesAsync(true));
            }
        }, TaskScheduler.Default);
    }

    [RelayCommand]
    public async Task LoadArticlesAsync(bool refresh = false)
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            if (refresh)
            {
                _currentOffset = 0;
                Articles.Clear();
            }

            var articles = await _dbService.SearchNewsArticlesAsync(SearchQuery, PageSize, _currentOffset);
            foreach (var article in articles)
            {
                Articles.Add(article);
            }
            _currentOffset += articles.Count;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task LoadMoreAsync()
    {
        await LoadArticlesAsync(false);
    }

    [RelayCommand]
    public async Task OpenArticleAsync(NewsArticle article)
    {
        if (article != null)
        {
            await Shell.Current.Navigation.PushAsync(new NewsArticlePage(article));
        }
    }
}
