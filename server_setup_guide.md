# Server Setup Guide: News & Vulnerability Backend

This guide walks you through deploying the .NET Backend and PostgreSQL database onto an Ubuntu VPS.

## 🐳 Docker Deployment (Highly Recommended)

Using Docker is the **recommended** way to deploy this application on a server. It packages the .NET application and the PostgreSQL database into isolated containers, meaning you don't need to manually install databases or SDKs on your host machine.

### 1. Install Docker and Git on Ubuntu
```bash
sudo apt update
sudo apt install -y git docker.io docker-compose-v2
sudo systemctl enable --now docker
```

### 2. Run the Application
1. Clone your project onto the server:
   ```bash
   git clone <your-repo-url>
   cd NewsandVulSBE
   ```
2. Simply start the environment using the provided Docker Compose file:
   ```bash
   sudo docker compose up -d
   ```
*That's it! Docker will automatically download PostgreSQL, build your .NET application, and run them both safely in the background.*

---

## 🛠️ Native Ubuntu Deployment (Alternative Method)

If you prefer to run the application directly on the server *without* Docker, follow these instructions to install the raw dependencies.

### 1. Install Prerequisites (Git, PostgreSQL, .NET 8)

Run the following commands on your Ubuntu VPS to install everything required:

```bash
# Update package lists
sudo apt update && sudo apt upgrade -y

# 1. Install Git
sudo apt install -y git

# 2. Install PostgreSQL
sudo apt install -y postgresql postgresql-contrib
sudo systemctl enable --now postgresql

# 3. Install .NET 8 SDK
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

### 2. Database Setup

1.  Access your PostgreSQL instance:
    ```bash
    sudo -u postgres psql
    ```
2.  Create the database and a dedicated user:
    ```sql
    CREATE DATABASE threatintel_db;
    CREATE USER threat_user WITH ENCRYPTED PASSWORD 'YourStrongPasswordHere';
    GRANT ALL PRIVILEGES ON DATABASE threatintel_db TO threat_user;
    \c threatintel_db
    GRANT ALL ON SCHEMA public TO threat_user;
    \q
    ```

### 3. Application Configuration

1.  Clone your project files onto the server: `git clone <your-repo-url>`
2.  Navigate into the project directory and open `appsettings.json`.
3.  Update the **Connection string** to point to your new database user and password.
4.  Ensure your **NIST API Key** is present in the configuration.

### 4. Run Migrations

Before running the app natively, you must create the tables in the database. From the project folder, run:
```bash
# Install the Entity Framework tools globally
dotnet tool install --global dotnet-ef

# Ensure tools are in your path
export PATH="$PATH:$HOME/.dotnet/tools"

# Apply the migrations to create the database schema
dotnet ef database update
```

### 5. Running as a Background Daemon (systemd)

You don't want the application to stop when you close your SSH terminal. You should set it up as a background service.

1.  Publish the compiled app to a server directory: 
    ```bash
    dotnet publish -c Release -o /var/www/threatintel
    ```
2.  Create a service file: 
    ```bash
    sudo nano /etc/systemd/system/threatintel.service
    ```
3.  Paste the following configuration:
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
4.  Enable and start the service so it runs automatically on boot:
    ```bash
    sudo systemctl enable threatintel.service
    sudo systemctl start threatintel.service
    ```
