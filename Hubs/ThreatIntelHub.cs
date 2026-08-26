using Microsoft.AspNetCore.SignalR;

namespace NewsandVulSBE.Hubs;

/// <summary>
/// SignalR Hub for pushing real-time Threat Intelligence updates to connected clients (Android/Windows/Linux).
/// </summary>
public class ThreatIntelHub : Hub
{
    // The server will use IHubContext<ThreatIntelHub> to push messages to connected clients.
    // Clients connect to the route /hubs/threatintel and listen for events.
}
