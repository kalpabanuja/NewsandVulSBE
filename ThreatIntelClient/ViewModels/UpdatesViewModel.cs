using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.AspNetCore.SignalR.Client;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ThreatIntelClient.Models;
using ThreatIntelClient.Services;
using Microsoft.Maui.Graphics;

namespace ThreatIntelClient.ViewModels;

public partial class UpdatesViewModel : BaseViewModel
{
    private readonly SignalRClientService _signalRService;

    public ObservableCollection<object> LiveFeed { get; } = new();

    [ObservableProperty]
    private string _connectionStatus = "Disconnected";

    [ObservableProperty]
    private Color _statusColor = Colors.Gray;

    public UpdatesViewModel(SignalRClientService signalRService)
    {
        _signalRService = signalRService;
        Title = "Updates";

        _signalRService.OnStateChanged += (state) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                switch (state)
                {
                    case HubConnectionState.Connected:
                        ConnectionStatus = "Live Feed Active";
                        StatusColor = Colors.Green;
                        break;
                    case HubConnectionState.Reconnecting:
                        ConnectionStatus = "Reconnecting...";
                        StatusColor = Colors.Orange;
                        break;
                    case HubConnectionState.Disconnected:
                        ConnectionStatus = "Disconnected";
                        StatusColor = Colors.Red;
                        break;
                }
            });
        };

        _signalRService.OnCveReceived += (cve) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LiveFeed.Insert(0, cve);
            });
        };

        _signalRService.OnNewsArticleReceived += (article) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LiveFeed.Insert(0, article);
            });
        };
    }

    public async Task ConnectAsync()
    {
        await _signalRService.ConnectAsync();
    }
}
