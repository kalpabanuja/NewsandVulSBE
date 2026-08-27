# Threat Intel API Endpoints Reference

This document outlines all the available API endpoints provided by the Threat Intel Backend (`NewsandVulSBE`) that you can use to fetch data for your frontend application.

Base URL: `http://localhost:8080` (or whatever your VPS/Server IP is)

---

## 1. Vulnerabilities (CVEs)

### Get Released Vulnerabilities
- **Endpoint:** `GET /api/vulnerabilities`
- **Description:** Returns a paginated list of fully analyzed (released) CVEs, ordered by newest first.
- **Parameters (Query):**
  - `page` (int, optional) - Default: `1`
  - `limit` (int, optional) - Default: `50`
- **Example:** `/api/vulnerabilities?page=1&limit=20`

### Get Pending Vulnerabilities
- **Endpoint:** `GET /api/vulnerabilities/pending`
- **Description:** Returns a paginated list of newly discovered CVEs that are awaiting full NIST analysis.
- **Parameters (Query):**
  - `page` (int, optional) - Default: `1`
  - `limit` (int, optional) - Default: `50`
- **Example:** `/api/vulnerabilities/pending?limit=10`

### Search Vulnerabilities
- **Endpoint:** `GET /api/vulnerabilities/search`
- **Description:** Search across released CVEs by ID or description.
- **Parameters (Query):**
  - `q` (string, required) - The search query.
- **Example:** `/api/vulnerabilities/search?q=CVE-2024`

### Get Detailed Vulnerability (Smart Fetch)
- **Endpoint:** `GET /api/vulnerabilities/{cveId}`
- **Description:** Returns the simplified CVE metadata, the dynamically generated official NIST URL (`OfficialUrl`), and the massively detailed JSON payload natively from NIST (`RawNistData`). If the detailed JSON is not cached in the database, the backend will automatically and seamlessly fetch it live from the NIST API, cache it, and return it.
- **Parameters (Path):**
  - `cveId` (string, required) - The CVE ID (e.g., `CVE-2024-2130`).
- **Example:** `/api/vulnerabilities/CVE-2024-2130`

---

## 2. Security News

### Get News Articles
- **Endpoint:** `GET /api/news`
- **Description:** Returns a paginated list of scraped security news articles, ordered by newest first.
- **Parameters (Query):**
  - `page` (int, optional) - Default: `1`
  - `limit` (int, optional) - Default: `50`
- **Example:** `/api/news?page=1&limit=10`

### Search News
- **Endpoint:** `GET /api/news/search`
- **Description:** Search news articles by title or summary.
- **Parameters (Query):**
  - `q` (string, required) - The search query.
- **Example:** `/api/news/search?q=malware`

---

## 3. Data Synchronization (For Offline/Bulk Fetching)

### Sync CVEs by Date Range
- **Endpoint:** `GET /api/sync/cves`
- **Description:** Fetches all released vulnerabilities published within a specific date range.
- **Parameters (Query):**
  - `startDate` (DateTime, required) - e.g., `2024-01-01T00:00:00Z`
  - `endDate` (DateTime, required) - e.g., `2024-12-31T23:59:59Z`
- **Example:** `/api/sync/cves?startDate=2024-01-01&endDate=2024-01-31`

### Sync News by Date
- **Endpoint:** `GET /api/sync/news`
- **Description:** Fetches all news articles published after a specific date.
- **Parameters (Query):**
  - `since` (DateTime, required)
- **Example:** `/api/sync/news?since=2024-08-01`

---

## 4. System Stats

### Get Database Statistics
- **Endpoint:** `GET /api/stats`
- **Description:** Returns the total count of pending CVEs, released CVEs, and news articles in the database.
- **Example Response:**
  ```json
  {
    "pendingVulnerabilities": 1707,
    "releasedVulnerabilities": 86000,
    "newsArticles": 520
  }
  ```

---

## 5. Real-Time WebSockets (SignalR)

- **Endpoint:** `/hubs/threats`
- **Description:** A SignalR hub that pushes live events to connected clients.
- **Events Emitted:**
  - `ReceiveNewCve(string cveId)` - Triggered when Mitre finds a brand new pending CVE.
  - `ReceiveNewCve(VulnDto cve)` - Triggered when NIST fully releases and analyzes a CVE.
  - `ReceiveNewNews(NewsArticle article)` - Triggered when the scraper finds a new article.
