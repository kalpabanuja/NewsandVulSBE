# Backend API Update Guide

To support the new Cache Selector and "Search Full Database" features, the backend API must be updated to process query parameters for filtering data. Currently, the API ignores parameters like `year` and `query` and just returns the default dataset.

## Target File
Locate the controller or minimal API endpoint for `/api/vulnerabilities` in your `NewsandVulSBE` project.

## Required Changes

You need to accept the following query parameters in your endpoint:
- `int? year`
- `string? query`
- `string? severity`

### Example Implementation (ASP.NET Core Minimal API / Controller)

Modify your database query logic to apply these filters before pagination.

```csharp
[HttpGet]
public async Task<IActionResult> GetVulnerabilities(
    [FromQuery] int page = 1, 
    [FromQuery] int limit = 50,
    [FromQuery] int? year = null,
    [FromQuery] string? query = null,
    [FromQuery] string? severity = null)
{
    var queryable = _dbContext.Vulnerabilities.AsQueryable();

    // 1. Filter by Year
    if (year.HasValue)
    {
        // Assuming your database has a PublishedDate field
        queryable = queryable.Where(v => v.PublishedDate.HasValue && v.PublishedDate.Value.Year == year.Value);
    }

    // 2. Filter by Severity
    if (!string.IsNullOrWhiteSpace(severity) && severity != "ALL")
    {
        queryable = queryable.Where(v => v.Severity == severity);
    }

    // 3. Filter by Keyword (Query)
    if (!string.IsNullOrWhiteSpace(query))
    {
        // Adjust the fields you want to search through (e.g., CveId, Description, Title)
        queryable = queryable.Where(v => 
            v.CveId.Contains(query) || 
            (v.Description != null && v.Description.Contains(query)));
    }

    // 4. Apply Pagination
    int skip = (page - 1) * limit;
    var results = await queryable
        .OrderByDescending(v => v.PublishedDate)
        .Skip(skip)
        .Take(limit)
        .ToListAsync();

    return Ok(results);
}
```

### Why this is needed:
1. **Downloading Cache**: The frontend will loop through pages calling `/api/vulnerabilities?year=2024&page=1` etc. The backend MUST return only records from 2024.
2. **Search Full Database**: When the user searches for "Log4j" with the full database toggle ON, the frontend will call `/api/vulnerabilities?query=Log4j`. The backend MUST execute that search across the entire database and return the results.

## Phase 2: Total Count Endpoint (Required for Pagination Math)

To support the accurate "Page X of Total" counter and the new "Total Results" UI box when searching the full database, your backend needs an endpoint that returns the total matching count for any given filters, WITHOUT pagination.

### Required Endpoint

Create a new endpoint at `/api/vulnerabilities/count` that accepts the exact same filter parameters as above:
- `int? year`
- `string? query`
- `string? severity`

### Example Implementation

```csharp
[HttpGet("count")]
public async Task<IActionResult> GetVulnerabilitiesCount(
    [FromQuery] int? year = null,
    [FromQuery] string? query = null,
    [FromQuery] string? severity = null)
{
    var queryable = _dbContext.Vulnerabilities.AsQueryable();

    // 1. Filter by Year
    if (year.HasValue)
    {
        queryable = queryable.Where(v => v.PublishedDate.HasValue && v.PublishedDate.Value.Year == year.Value);
    }

    // 2. Filter by Severity
    if (!string.IsNullOrWhiteSpace(severity) && severity != "ALL")
    {
        queryable = queryable.Where(v => v.Severity == severity);
    }

    // 3. Filter by Keyword (Query)
    if (!string.IsNullOrWhiteSpace(query))
    {
        queryable = queryable.Where(v => 
            v.CveId.Contains(query) || 
            (v.Description != null && v.Description.Contains(query)));
    }

    // Return just the integer count
    int count = await queryable.CountAsync();
    return Ok(count);
}
```

Please implement this new endpoint so the frontend pagination and results counter will function correctly!
