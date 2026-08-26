using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;
using ThreatIntelClient.Models;

namespace ThreatIntelClient.Services;

public class SignalRClientService
{
    private readonly HubConnection _hubConnection;
    private readonly LocalDatabaseService _dbService;

    public event Action<Cve> OnCveReceived;
    public event Action<NewsArticle> OnNewsArticleReceived;
    public event Action<HubConnectionState> OnStateChanged;

    public SignalRClientService(LocalDatabaseService dbService)
    {
        _dbService = dbService;
        
        string backendUrl = "https://localhost:5001/hubs/threats"; 

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(backendUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.Closed += async (error) => 
        {
            OnStateChanged?.Invoke(_hubConnection.State);
            await Task.CompletedTask;
        };
        _hubConnection.Reconnecting += async (error) =>
        {
            OnStateChanged?.Invoke(_hubConnection.State);
            await Task.CompletedTask;
        };
        _hubConnection.Reconnected += async (connectionId) =>
        {
            OnStateChanged?.Invoke(_hubConnection.State);
            await Task.CompletedTask;
        };

        _hubConnection.On<Cve>("ReceiveNewCve", async (cve) =>
        {
            await _dbService.SaveCveAsync(cve);
            OnCveReceived?.Invoke(cve);
        });

        _hubConnection.On<NewsArticle>("ReceiveNewArticle", async (article) =>
        {
            await _dbService.SaveNewsArticleAsync(article);
            OnNewsArticleReceived?.Invoke(article);
        });
    }

    public async Task ConnectAsync()
    {
        try
        {
            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                await _hubConnection.StartAsync();
                OnStateChanged?.Invoke(_hubConnection.State);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to SignalR: {ex.Message}");
            OnStateChanged?.Invoke(_hubConnection.State);
        }
    }

    public async Task DisconnectAsync()
    {
        if (_hubConnection.State != HubConnectionState.Disconnected)
        {
            await _hubConnection.StopAsync();
            OnStateChanged?.Invoke(_hubConnection.State);
        }
    }
}
