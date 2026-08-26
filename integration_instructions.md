# Integration Instructions: Client Applications

This guide explains how to connect your future applications (Android, Windows, Linux) to the Threat Intel Backend.

## 1. REST API Endpoints

The backend currently exposes standard HTTP REST endpoints. These return data in standard JSON format.

### Get Vulnerabilities
*   **Endpoint:** `GET /api/vulnerabilities`
*   **Parameters:**
    *   `page` (int, default: 1)
    *   `pageSize` (int, default: 50)
*   **Response:** An array of vulnerability objects (sorted by newest).

### Get News Articles
*   **Endpoint:** `GET /api/news`
*   **Parameters:**
    *   `page` (int, default: 1)
    *   `pageSize` (int, default: 50)
*   **Response:** An array of news article objects (sorted by newest).

## 2. How the App Knows When an Update Comes

Right now, the background workers on the server update the PostgreSQL database silently. Your client application needs a strategy to find out about these updates. You have two options:

### Option A: Polling (Simple)
The easiest way to integrate is for your application to "poll" the API.
1.  Your app makes a request to `GET /api/vulnerabilities` when it launches.
2.  Your app runs a timer and repeats the `GET` request every 5 minutes in the background.
3.  The app compares the new list to the old list and alerts the user if there are new items at the top.

### Option B: Real-Time Push with SignalR (Advanced/Seamless)
I have added a **SignalR Hub** to the backend, so your applications can be notified *immediately* the very second a new vulnerability is found, without making unnecessary requests.

**How it works:**
1.  Your client app connects to the WebSocket endpoint at `/hubs/threatintel`.
2.  It sits idle, consuming almost no battery/network.
3.  When a new vulnerability or news article is found, the backend broadcasts a message.

**SignalR Events to Listen For:**
*   `ReceiveNewVulnerabilities` (Parameter: `List<string> cveIds`) - Fired when MITRE publishes brand new CVEs.
*   `VulnerabilityAnalyzed` (Parameter: `Vulnerability` object) - Fired when a pending CVE is fully analyzed by NIST.
*   `ReceiveNewNews` (Parameter: `List<NewsArticle>` objects) - Fired when new Hacker News articles are saved.

*(In your client applications, you will need a SignalR client library like `@microsoft/signalr` for JS/TS, or `Microsoft.AspNetCore.SignalR.Client` for C#/.NET).*

## 3. Consuming the Data in Client Apps

Regardless of how you get the data, your client app should parse the JSON and display it. Pay attention to the `status` field on vulnerabilities:

*   **`Pending Research`**: This CVE was *just* released by MITRE. It won't have a CVSS score or detailed description yet. Show this with a warning icon to indicate a brand new, unanalyzed threat.
*   **`Analyzed`**: This CVE has been processed by NIST. It will have a severity rating, a CVSS score, and a full description.
