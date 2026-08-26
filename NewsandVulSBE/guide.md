# Cybersecurity Threat Intelligence Dashboard - Specification

## 1. Overview
A real-time Threat Intelligence platform that aggregates, normalizes, and immediately streams the latest cybersecurity news and vulnerability data (CVEs/NVD). The system relies on an event-driven ingestion pipeline designed to surface threats the second they are published.

## 2. Core Features
*   **Real-Time Data Ingestion:** Micro-polling and event streaming for zero-delay updates.
*   **Unified Threat Feed:** An infinitely scrolling, WebSocket-powered timeline of news and vulnerabilities.
*   **Advanced Filtering & Search:** Filter by CVSS score, vendor, CPE, date, and news categories.
*   **Vulnerability Linking:** Automated NLP/Regex tagging to link news articles to specific CVEs.
*   **Live Dashboard:** Metrics on active zero-days, average CVSS scores, and threat trends.

## 3. Data Sources & Real-Time Ingestion Strategy

### A. Vulnerability Databases (Immediate)
*   **MITRE CVE (JSON 5.0):**
    *   **Mechanism:** GitHub Events API Micro-polling (every 60s).
    *   **Source:** `CVEProject/cvelistV5` repository.
    *   **Action:** Detects `PushEvent` and immediately downloads newly added or modified CVE JSON payloads.
*   **NIST NVD (National Vulnerability Database):**
    *   **Mechanism:** API 2.0 High-Frequency Polling (every 60-120s).
    *   **Source:** `https://services.nvd.nist.gov/rest/json/cves/2.0`
    *   **Action:** Uses `lastModStartDate` and `lastModEndDate` query parameters to fetch modifications made in the last two minutes. Requires NVD API Key for higher rate limits (50 req/30s).

### B. Cybersecurity News (Immediate)
*   **Sources:** The Hacker News, BleepingComputer, Dark Reading, KrebsOnSecurity, Threatpost.
*   **Mechanisms:**
    *   **WebSub (PubSubHubbub):** Subscribes to compatible feeds for literal push notifications.
    *   **RSS Micro-Polling:** Daemon running every 60 seconds checking for updates. Strictly uses `ETag` and `If-Modified-Since` headers to avoid rate limiting and bandwidth waste.
    *   **Social Media Streams (Optional):** Integration with X (Twitter) API or Mastodon streams for breaking alerts (e.g., watching `@CVEnew`, `@TheHackersNews`).

## 4. Architecture & Tech Stack

To support immediate updates, the architecture moves away from CRON to a persistent worker daemon model paired with a message broker.

### Backend (Ingestion & API)
*   **Language/Framework:** Node.js (Express/NestJS) or Python (FastAPI).
*   **Message Broker:** **Redis Pub/Sub** or **RabbitMQ**. Ingestion workers publish events here the moment new data is found.
*   **Ingestion Workers:** Separate lightweight processes (e.g., Celery in Python or BullMQ in Node) running infinite loops with intelligent sleep delays for micro-polling.
*   **Database:** PostgreSQL (structured CVE/News data) + Redis (caching and live feeds).

### Frontend (User Interface)
*   **Framework:** React.js or Next.js.
*   **Live Updates:** **WebSockets (Socket.io)** or **Server-Sent Events (SSE)**. When the backend detects a new CVE or News article, it pushes it directly to the browser UI without requiring a page refresh.
*   **Styling:** TailwindCSS for rapid, responsive UI development.

## 5. Database Schema (Relational / PostgreSQL)

### `news_articles`
*   `id` (UUID, Primary Key)
*   `title` (String)
*   `url` (String, Unique)
*   `source` (String) - e.g., 'The Hacker News'
*   `published_at` (Timestamp)
*   `summary` (Text)
*   `created_at` (Timestamp)

### `cve_records`
*   `cve_id` (String, Primary Key) - e.g., 'CVE-2026-12345'
*   `description` (Text)
*   `published_date` (Timestamp)
*   `last_modified_date` (Timestamp)
*   `cvss_v3_score` (Float, Nullable)
*   `cvss_v4_score` (Float, Nullable)
*   `severity` (String) - e.g., 'CRITICAL', 'HIGH'
*   `source` (String) - 'MITRE' or 'NIST'

### `cve_news_links` (Join Table for NLP tagging)
*   `cve_id` (Foreign Key -> cve_records)
*   `news_id` (Foreign Key -> news_articles)

## 6. Real-Time Pipeline Flow

1. **The Ingestion Daemon** polls NIST NVD (requesting only data from the last 2 minutes) and GitHub (for MITRE).
2. **Data Found:** An update is detected (e.g., NIST publishes CVSS scores for a CVE).
3. **Database Upsert:** The backend immediately updates the `cve_records` table.
4. **Event Published:** The backend fires a `NEW_VULNERABILITY` event into the Redis Message Broker.
5. **WebSocket Push:** The API server consumes the Redis event and pushes the updated CVE via WebSocket to all connected frontend clients.
6. **UI Update:** The frontend flashes the new CVE at the top of the user's feed with a "Just Now" timestamp.

## 7. Operational Guardrails (Rate Limit Safety)
Since we are requesting data "immediately," strict API handling is required:
*   **NVD API Key Usage:** Must be passed in the `apiKey` header.
*   **Exponential Backoff:** If NIST or GitHub returns a `429 Too Many Requests`, the worker temporarily backs off (e.g., sleeps for 30s -> 60s -> 120s) before resuming polling.
*   **Feed Etiquette:** RSS pollers *must* check the `ETag` of the XML file. If the tag hasn't changed, the parsing logic is skipped entirely.