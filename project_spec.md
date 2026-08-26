# Project Specification: News and Vulnerability Sync Backend

## 1. Overview
This document outlines the architecture, logic, and workflow for a .NET server-side backend designed to aggregate vulnerability data and cybersecurity news. The backend serves as a centralized data API to seamlessly support future client applications across Android, Windows, and Linux.

## 2. Technology Stack
*   **Backend Framework:** .NET 8 (or latest) - ASP.NET Core Web API.
*   **Database:** PostgreSQL.
*   **ORM:** Entity Framework Core (EF Core).
*   **Background Processing:** .NET Hosted Services (`BackgroundService`) or Hangfire for scheduled, seamless background data synchronization.
*   **Real-time Communication (Optional):** SignalR (to push seamless live updates directly to clients).

## 3. Core Workflows

### 3.1. Vulnerability Ingestion Pipeline
The system implements a two-step synchronization process to ensure the earliest possible awareness of new vulnerabilities (via MITRE) and complete data enrichment (via NIST).

1.  **MITRE Sync (Initial Discovery):**
    *   A background worker periodically queries the MITRE CVE API or repository.
    *   It fetches the most recently assigned/released CVE IDs.
    *   These new vulnerabilities are saved into the PostgreSQL database with a status indicating they are `Pending Research`.
2.  **NIST NVD Enrichment (Deep Scan):**
    *   A separate background process reaches out to the NIST NVD API.
    *   It cross-references the IDs of the `Pending Research` vulnerabilities.
    *   **Match Found:** If NIST has published the vulnerability, the system updates the database record with the full details (CVSS scores, detailed descriptions, CPEs) and marks the status as `Analyzed`.
    *   **No Match Yet:** If NIST has not yet processed the CVE, it remains in the `Pending Research` state.
3.  **Data Visibility:**
    *   The API allows your applications to view all data. Crucially, the pending (MITRE-only) vulnerabilities are fully visible, giving you early warnings before NIST has even processed them.

### 3.2. Cybersecurity News Ingestion Pipeline
1.  **News Fetching:**
    *   A scheduled background worker connects to *The Hacker News* (typically via their public RSS feed).
    *   It extracts the latest news articles (Title, Link, Summary, Publish Date).
2.  **Data Storage:**
    *   Articles are saved to the PostgreSQL database.
    *   The system uses the URL or a unique hash to prevent saving duplicate news entries.

## 4. Seamless Data Access Strategy
To ensure the applications (Android/Windows/Linux) have a "seamless" experience:
1.  **Optimized REST API:** Expose endpoints (e.g., `GET /api/vulnerabilities`, `GET /api/news`) with pagination and filtering. Data is always served from the local PostgreSQL database, meaning client requests never wait for external MITRE/NIST API calls.
2.  **Caching:** Implement in-memory caching or Redis so frequently accessed feeds load instantly.
3.  **Push Notifications (SignalR):** To make updates truly seamless, the backend can use SignalR. When a background worker saves a *new* vulnerability or news article to the database, the backend can instantly broadcast a message to all connected clients to refresh their feeds in real-time.

## 5. Proposed Database Schema

### Table: `Vulnerabilities`
*   `Id` (UUID/Guid, Primary Key)
*   `CveId` (String, Unique Index) - e.g., 'CVE-2024-12345'
*   `Status` (String/Enum) - e.g., 'Pending', 'Analyzed'
*   `Title` (String, Nullable)
*   `Description` (Text, Nullable)
*   `CvssScore` (Float, Nullable)
*   `Severity` (String, Nullable)
*   `PublishedDate` (DateTime)
*   `LastCheckedWithNist` (DateTime) - Used to throttle NIST API requests.

### Table: `NewsArticles`
*   `Id` (UUID/Guid, Primary Key)
*   `Title` (String)
*   `Url` (String, Unique Index)
*   `Summary` (Text)
*   `Source` (String) - Default: 'The Hacker News'
*   `PublishedAt` (DateTime)
*   `CreatedAt` (DateTime) - When it was saved to the DB.
