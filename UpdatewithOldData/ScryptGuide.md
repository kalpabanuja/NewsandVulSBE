# NIST Historical Vulnerability Seeder Guide

This standalone C# console application retrieves all historical vulnerabilities directly from the official NIST NVD API (`https://services.nvd.nist.gov/rest/json/cves/2.0`) and securely injects them into your `threatintel_db` PostgreSQL database.

## 📋 Prerequisites

1.  **.NET 8 SDK**: You need the .NET 8 SDK installed to run this script (you already have this if you are running the backend).
2.  **Running Database**: The `threatintel_db` PostgreSQL database must be actively running and initialized with the latest migrations (tables `ReleasedVulnerabilities` must exist).

## ⚙️ Configuration

Open `Program.cs` in this folder and verify the two settings at the top of the script:

1.  **`ConnectionString`**: This defaults to `Host=localhost;Database=threatintel_db;Username=postgres;Password=postgres`. If you changed your PostgreSQL password in Docker, update it here.
2.  **`NistApiKey`**: Your provided API key (`b6494056-a935-438b-a717-9428e920907c`) is already baked in! This increases your rate limit from 5 requests/30s to 50 requests/30s, making the script run 10x faster.

## 🚀 How to Run

1.  Open your terminal on your VPS.
2.  Navigate to this folder:
    ```bash
    cd NewsandVulSBE/UpdatewithOldData
    ```
3.  Run the script using the .NET CLI:
    ```bash
    dotnet run
    ```

The script will begin paginating through the NIST API, starting from index 0. It will print its progress to the console.

## 🛑 Important Notes

*   **Rate Limiting:** Even with the API key, the script respects the NIST rate limits and will pause (sleep) between API calls and automatically handle `429 Too Many Requests` bans by pausing for 30 seconds.
*   **Idempotency (Safe to restart):** You can stop the script at any time by pressing `Ctrl + C`. If you restart it, it will ignore CVEs that are already in the database and continue fetching, so you won't get duplicate errors.
*   **Runtime:** Fetching hundreds of thousands of historical CVEs over the internet takes time. It is recommended to run this in a `tmux` or `screen` session on your VPS so you can safely close your SSH connection while it runs in the background.
