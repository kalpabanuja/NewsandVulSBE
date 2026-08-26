# Threat Intel Backend API & Ingestion Engine

## 📌 Overview
A real-time, event-driven .NET 8 Web API that aggregates cybersecurity vulnerabilities (CVEs) and news, storing them in PostgreSQL. It pushes live updates to client applications via SignalR and provides bulk-sync REST endpoints to support offline SQLite client databases.

### Core Technologies
*   **Framework:** .NET 8 (ASP.NET Core Web API)
*   **Database:** PostgreSQL (Containerized via Docker)
*   **ORM:** Entity Framework Core
*   **Real-Time Engine:** SignalR (WebSockets)
*   **Ingestion:** BackgroundService (IHostedService) with micro-polling

---

## 🏗️ System Architecture

### 1. Ingestion Pipeline
Background workers continuously poll data sources without blocking the main API thread.
*   **MITRE Feed:** Polls GitHub Events API for newly assigned CVE JSON payloads.
*   **NIST NVD:** Polls API 2.0 (`lastModStartDate`) every 60-120 seconds to fetch newly analyzed CVEs.
*   **The Hacker News:** RSS feed parsing checking `ETag` to fetch immediate news updates.

### 2. Real-Time Push Notifications (SignalR)
When the ingestion pipeline detects new data, it saves to PostgreSQL and broadcasts the payload to all connected clients instantly.
*   **Hub Route:** `/hubs/threats`
*   **Events Broadcasted:**
    *   `ReceiveNewCve` (Triggered when NIST/MITRE publishes a new CVE)
    *   `ReceiveNewArticle` (Triggered when a new news article is scraped)
*   **Connection Resilience:** Clients auto-reconnect if the socket drops.

---

## 🗄️ Database Schema (PostgreSQL)

| Table | Primary Purpose | Key Fields |
| :--- | :--- | :--- |
| **PendingCves** | Staging for new MITRE IDs | `CveId`, `DiscoveredDate`, `Status` |
| **AnalyzedCves** | Rich NIST data (CVSS, descriptions) | `CveId`, `BaseScore`, `Severity`, `PublishedDate` |
| **NewsArticles** | Aggregated security news | `Id`, `Title`, `SourceUrl`, `PublishedDate` |

---

## 🔌 API Endpoints 

### Live Queries (Standard App Browsing)
*   `GET /api/vulnerabilities?page=1&limit=50&sort=desc` - Paginated CVE list.
*   `GET /api/vulnerabilities/search?q={query}` - Fast text search for specific CVEs.
*   `GET /api/news?page=1&limit=50` - Paginated news articles.
*   `GET /api/news/search?q={query}` - Responsive news text search.

### Offline Sync / Bulk Export (For Client SQLite Hydration)
These endpoints are optimized for the client app's "Offline Database Duration" settings, allowing clients to download specific date ranges in bulk.
*   `GET /api/sync/cves?startDate=2015-01-01&endDate=2026-12-31`
    *   Returns a compressed JSON payload of all CVEs within the requested range to populate the client's local SQLite DB.
*   `GET /api/sync/news?since={timestamp}`
    *   Fetches all news articles published since the client's last sync.

---

## 🐳 Docker Deployment

The database runs in a Docker container to ensure environment consistency.

**Start the database environment:**
```bash
docker compose up -d