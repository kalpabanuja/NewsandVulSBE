# Server Setup Guide: News & Vulnerability Backend

This guide walks you through deploying the .NET Backend and PostgreSQL database onto a Linux or Windows server.

## 1. Prerequisites

Before starting, ensure your server has the following installed:
*   **.NET 8 SDK** (or the matching version of your project).
*   **PostgreSQL** (version 13+ recommended).
*   **Git** (if pulling code directly from a repository).

## 2. Database Setup

1.  Access your PostgreSQL instance:
    ```bash
    psql -U postgres
    ```
2.  Create the database and a dedicated user:
    ```sql
    CREATE DATABASE threatintel_db;
    CREATE USER threat_user WITH ENCRYPTED PASSWORD 'YourStrongPasswordHere';
    GRANT ALL PRIVILEGES ON DATABASE threatintel_db TO threat_user;
    \c threatintel_db
    GRANT ALL ON SCHEMA public TO threat_user;
    ```

## 3. Application Configuration

1.  Clone or copy your project files onto the server.
2.  Open `appsettings.json` (or create an `appsettings.Production.json` file).
3.  Update the **Connection string** to point to your new database user and password.
4.  Ensure your **NIST API Key** is present in the configuration.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=threatintel_db;Username=threat_user;Password=YourStrongPasswordHere"
  },
  "NistApiKey": "b6494056-a935-438b-a717-9428e920907c"
}
```

## 4. Run Migrations

Before running the app, you must create the tables in the database.
From the project folder, run:
```bash
dotnet ef database update
```
*(This uses the EF Core tool to apply the `InitialCreate` migration to your PostgreSQL database).*

## 5. Running the Application as a Daemon/Service

You don't want the application to stop when you close your terminal. You should set it up as a background service.

### On Linux (systemd)

1.  Publish the app: `dotnet publish -c Release -o /var/www/threatintel`
2.  Create a service file: `sudo nano /etc/systemd/system/threatintel.service`
3.  Add the following:
    ```ini
    [Unit]
    Description=.NET Threat Intel API

    [Service]
    WorkingDirectory=/var/www/threatintel
    ExecStart=/usr/bin/dotnet /var/www/threatintel/NewsandVulSBE.dll
    Restart=always
    RestartSec=10
    Environment=ASPNETCORE_ENVIRONMENT=Production

    [Install]
    WantedBy=multi-user.target
    ```
4.  Enable and start the service:
    ```bash
    sudo systemctl enable threatintel.service
    sudo systemctl start threatintel.service
    ```

## 6. Accessing the Data
Your background workers are now running continuously inside the application. You can expose the API using a reverse proxy like **Nginx** or **Apache** to route external traffic to your .NET application (which runs on `http://localhost:5000` by default).
