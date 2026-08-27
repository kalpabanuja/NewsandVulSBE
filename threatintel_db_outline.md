# Database Outline: `threatintel_db` [cite: 1]

This document provides a comprehensive outline of the `threatintel_db` database, including its schema, table definitions, indexes, and sample data records based on the provided database dump [cite: 1].

## Overview

The `threatintel_db` database contains four main relations under the `public` schema [cite: 1]:
1. `NewsArticles` [cite: 1]
2. `PendingVulnerabilities` [cite: 1]
3. `ReleasedVulnerabilities` [cite: 1]
4. `__EFMigrationsHistory` (Entity Framework migrations tracking table) [cite: 1]

---

## 1. Table: `NewsArticles`

Stores threat intelligence news articles, summaries, and their respective sources [cite: 1].

### Schema
| Column | Type | Nullable | Description |
| :--- | :--- | :--- | :--- |
| **Id** | `uuid` | `not null` | Primary Key [cite: 1] |
| **Title** | `text` | `not null` | The title of the news article [cite: 1] |
| **Url** | `text` | `not null` | The URL of the news article (Unique) [cite: 1] |
| **ImageUrl** | `text` | `nullable` | A direct link to the article's thumbnail image |
| **Summary** | `text` | `nullable` | A summary of the article's contents [cite: 1] |
| **Source** | `character varying(100)` | `not null` | The publisher or source of the article [cite: 1] |
| **PublishedAt** | `timestamp with time zone` | `not null` | The date and time the article was published [cite: 1] |
| **CreatedAt** | `timestamp with time zone` | `not null` | The date and time the record was created in the database [cite: 1] |

### Indexes
* `"PK_NewsArticles"`: `PRIMARY KEY, btree ("Id")` [cite: 1]
* `"IX_NewsArticles_Url"`: `UNIQUE, btree ("Url")` [cite: 1]

### Sample Data
| Id | Title | Url | Source | PublishedAt |
| :--- | :--- | :--- | :--- | :--- |
| `02e63b57...` | E4del and PINHOLE RATs Turn FTP Banners Into Dead Drops... [cite: 1] | `https://thehackernews.com/2026/08/e4del-and-pinhole-rats-turn-ftp-banners.html` [cite: 1] | The Hacker News [cite: 1] | 2026-08-25 11:33:44+00 [cite: 1] |
| `09b7220c...` | U.S. Sanctions Iran-Linked Hackers Behind Critical Infrastructure Breaches [cite: 1] | `https://thehackernews.com/2026/08/us-sanctions-iran-linked-hackers-behind.html` [cite: 1] | The Hacker News [cite: 1] | 2026-08-25 18:17:17+00 [cite: 1] |

---

## 2. Table: `PendingVulnerabilities`

Tracks CVEs that have been discovered but are awaiting full descriptions or sync from MITRE/NIST [cite: 1].

### Schema
| Column | Type | Nullable | Description |
| :--- | :--- | :--- | :--- |
| **Id** | `uuid` | `not null` | Primary Key [cite: 1] |
| **CveId** | `character varying(50)` | `not null` | The Common Vulnerabilities and Exposures ID (Unique) [cite: 1] |
| **DiscoveredAt** | `timestamp with time zone`| `not null` | The date and time the vulnerability was discovered by the system [cite: 1] |
| **LastCheckedWithNist**| `timestamp with time zone`| `nullable` | Last sync timestamp with NIST [cite: 1] |
| **Description** | `text` | `nullable` | The description of the vulnerability (e.g., "Awaiting description...") [cite: 1] |

### Indexes
* `"PK_PendingVulnerabilities"`: `PRIMARY KEY, btree ("Id")` [cite: 1]
* `"IX_PendingVulnerabilities_CveId"`: `UNIQUE, btree ("CveId")` [cite: 1]

### Sample Data
| CveId | DiscoveredAt | Description |
| :--- | :--- | :--- |
| `CVE-2026-64908` [cite: 1] | 2026-08-27 02:03:40.402646+00 [cite: 1] | Awaiting description from MITRE/NIST. [cite: 1] |
| `CVE-2026-75414` [cite: 1] | 2026-08-27 02:03:40.940862+00 [cite: 1] | In AntFlow V2.0.0, ActivitiTest.java enables users to execute JUEL expressions... [cite: 1] |
| `CVE-2026-18272` [cite: 1] | 2026-08-27 02:03:40.989361+00 [cite: 1] | Kenwood DNR1007XR startUpdateProcess Command Injection Vulnerability... [cite: 1] |
| `CVE-2025-61164` [cite: 1] | 2026-08-27 02:03:41.489101+00 [cite: 1] | Cohere North AI v1.1.5 was discovered to contain an information leak via the WebSocket Endpoint. [cite: 1] |

---

## 3. Table: `ReleasedVulnerabilities`

Maintains a definitive list of officially released vulnerabilities, along with severity classifications and CVSS scores [cite: 1].

### Schema
| Column | Type | Nullable | Description |
| :--- | :--- | :--- | :--- |
| **Id** | `uuid` | `not null` | Primary Key [cite: 1] |
| **CveId** | `character varying(50)` | `not null` | The Common Vulnerabilities and Exposures ID (Unique) [cite: 1] |
| **Title** | `text` | `nullable` | Optional title of the CVE [cite: 1] |
| **Description** | `text` | `nullable` | Full description of the released vulnerability [cite: 1] |
| **CvssScore** | `real` | `nullable` | The numerical Common Vulnerability Scoring System (CVSS) score [cite: 1] |
| **Severity** | `character varying(20)` | `nullable` | Qualitative severity rating (e.g., HIGH) [cite: 1] |
| **PublishedDate** | `timestamp with time zone`| `nullable` | The date and time the vulnerability was officially published [cite: 1] |
| **RawNistJson** | `jsonb`| `nullable` | The raw, complete detailed JSON payload from NIST |

### Indexes
* `"PK_ReleasedVulnerabilities"`: `PRIMARY KEY, btree ("Id")` [cite: 1]
* `"IX_ReleasedVulnerabilities_CveId"`: `UNIQUE, btree ("CveId")` [cite: 1]

### Sample Data
| CveId | CvssScore | Severity | PublishedDate | Description (Snippet) |
| :--- | :--- | :--- | :--- | :--- |
| `CVE-1999-0095` [cite: 1] | `null` | `null` | 1988-10-01 04:00:00+00 [cite: 1] | The debug command in Sendmail is enabled, allowing attackers to execute commands as root. [cite: 1] |
| `CVE-1999-0084` [cite: 1] | 8.4 [cite: 1] | HIGH [cite: 1] | 1990-05-01 04:00:00+00 [cite: 1] | Certain NFS servers allow users to use mknod to gain privileges by creating a writable kmem device... [cite: 1] |
| `CVE-1999-1471` [cite: 1] | `null` | `null` | 1989-01-01 05:00:00+00 [cite: 1] | Buffer overflow in passwd in BSD based operating systems 4.3 and earlier... [cite: 1] |
