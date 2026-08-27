# Vulnerabilities Database Flow Instructions

This document outlines the required implementation steps to separate the Vulnerabilities database into a two-table pipeline, update the UI to show accurate polling statuses, and handle initial historical data seeding.

## 1. Database Schema Update (Two Tables)
The `AppDbContext` needs to be updated to replace the single `Vulnerabilities` table with two distinct tables:

*   **`PendingVulnerabilities`**: A temporary holding table for CVE IDs newly discovered from MITRE that have not yet been fully analyzed or released by NIST.
    *   *Columns needed:* `Id`, `CveId`, `LastCheckedWithNist`, `DiscoveredAt`.
*   **`ReleasedVulnerabilities`**: The main database table for fully analyzed vulnerabilities containing rich data.
    *   *Columns needed:* `Id`, `CveId`, `Title`, `Description`, `CvssScore`, `Severity`, `PublishedDate`.

## 2. Automated Pipeline Logic
The background worker services must be updated to facilitate a promotion pipeline between these two tables.

### Step A: MITRE Discovery
1.  The `MitreSyncService` polls the `CVEProject/cvelistV5` GitHub repository for recent commits.
2.  Any newly extracted CVE IDs (e.g., `CVE-YYYY-XXXXX`) should be **inserted** into the `PendingVulnerabilities` table.

### Step B: NIST Analysis & Promotion
1.  The `NistSyncService` queries the `PendingVulnerabilities` table for IDs that need to be checked.
2.  It sends these IDs to the NIST API (`services.nvd.nist.gov`).
3.  If NIST returns an analyzed record (meaning it has a description, CVSS score, and publish date):
    *   **Insert** the rich data into the `ReleasedVulnerabilities` table.
    *   **Delete** the original record from the `PendingVulnerabilities` table.
4.  If NIST returns no analyzed data yet, simply update the `LastCheckedWithNist` timestamp on the pending record so it can be retried later.

## 3. UI Dashboard Updates (Tracking "Last Checked")
When displaying vulnerabilities on the dashboard or passing them through the API, handle the display of pending items gracefully.

*   Instead of displaying `"Published: Date unknown"`, the UI should indicate that the CVE is in the queue and show when the server last checked it.
*   **Format:** `Published: Pending (LastChecked: <date/time>)`
*   *Implementation detail:* The API should return the `LastCheckedWithNist` property so the frontend JavaScript can format this string accurately.

## 4. Initial Database Seeding (Historical Data)
To prevent the application from starting with an empty main database, an automatic historical data seeding process must be implemented.

1.  On application startup (e.g., in `Program.cs` during the migration phase), query the `ReleasedVulnerabilities` table.
2.  If the table returns **less than 5000 records**, the seeding process triggers.
3.  The process must locate the `PreviousVulnerabilities\cvelistV5-main.zip` file in the project's root folder.
4.  It should unzip the archive in memory, recursively traverse all the subdirectories to find every `.json` file (you do **not** need to put them all into a single folder—the code can handle nested folders), parse the historical JSON CVE records, and map them into the `ReleasedVulnerabilities` table to populate the database with all previous vulnerabilities.
