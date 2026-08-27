using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using System.Threading;

class Program
{
    // Configuration
    // Update this if your database credentials differ
    static string ConnectionString = "Host=db;Database=threatintel_db;Username=postgres;Password=Kalpa2004@";
    
    // Using the user-provided NIST API Key
    static string NistApiKey = "b6494056-a935-438b-a717-9428e920907c";
    
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Historical Vulnerability Sync from NIST NVD...");
        
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(5); // Increase timeout to 5 minutes for slow NIST responses
        httpClient.DefaultRequestHeaders.Add("User-Agent", "ThreatIntelDataSeeder/1.0");
        if (!string.IsNullOrEmpty(NistApiKey))
        {
            httpClient.DefaultRequestHeaders.Add("apiKey", NistApiKey);
            Console.WriteLine("NIST API Key configured.");
        }
        else
        {
            Console.WriteLine("WARNING: Running without API Key. Rate limits will be very strict (5 requests per 30 seconds).");
        }

        using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        Console.WriteLine("Connected to PostgreSQL database.");

        using var countCmd = new NpgsqlCommand("SELECT COUNT(1) FROM \"ReleasedVulnerabilities\"", conn);
        int startIndex = (int)(long)await countCmd.ExecuteScalarAsync();
        Console.WriteLine($"Found {startIndex} existing vulnerabilities. Resuming from index {startIndex}...");

        int totalResults = int.MaxValue; // Will be updated on first request
        int addedCount = 0;

        while (startIndex < totalResults)
        {
            var url = $"https://services.nvd.nist.gov/rest/json/cves/2.0?startIndex={startIndex}";
            Console.WriteLine($"Fetching NIST data from startIndex {startIndex}...");
            
            try 
            {
                var response = await httpClient.GetAsync(url);
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || 
                    response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                    (int)response.StatusCode == 429)
                {
                    Console.WriteLine($"Rate limited by NIST (Status: {response.StatusCode}). Sleeping for 30 seconds...");
                    await Task.Delay(30000);
                    continue; // Retry same index
                }
                
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                
                totalResults = root.GetProperty("totalResults").GetInt32();
                var vulnerabilities = root.GetProperty("vulnerabilities");
                int fetchedCount = vulnerabilities.GetArrayLength();

                if (fetchedCount == 0)
                {
                    break;
                }
                
                using var transaction = await conn.BeginTransactionAsync();
                
                foreach (var vulWrapper in vulnerabilities.EnumerateArray())
                {
                    var cve = vulWrapper.GetProperty("cve");
                    var cveId = cve.GetProperty("id").GetString();
                    if (string.IsNullOrEmpty(cveId)) continue;
                    
                    // Check if already exists to prevent duplicate key error
                    using var checkCmd = new NpgsqlCommand("SELECT COUNT(1) FROM \"ReleasedVulnerabilities\" WHERE \"CveId\" = @id", conn, transaction);
                    checkCmd.Parameters.AddWithValue("id", cveId);
                    var result = await checkCmd.ExecuteScalarAsync();
                    var exists = result != null && (long)result > 0;
                    if (exists) continue;

                    string? description = null;
                    if (cve.TryGetProperty("descriptions", out var descs))
                    {
                        foreach (var desc in descs.EnumerateArray())
                        {
                            if (desc.GetProperty("lang").GetString() == "en")
                            {
                                description = desc.GetProperty("value").GetString();
                                break;
                            }
                        }
                    }

                    float? cvssScore = null;
                    string? severity = null;
                    if (cve.TryGetProperty("metrics", out var metrics))
                    {
                        if (metrics.TryGetProperty("cvssMetricV31", out var cvssV31) && cvssV31.GetArrayLength() > 0)
                        {
                            var cvssData = cvssV31[0].GetProperty("cvssData");
                            cvssScore = (float)cvssData.GetProperty("baseScore").GetDouble();
                            severity = cvssData.GetProperty("baseSeverity").GetString();
                        }
                    }

                    DateTime? publishedDate = null;
                    if (cve.TryGetProperty("published", out var publishedElement))
                    {
                        publishedDate = publishedElement.GetDateTime();
                    }

                    using var insertCmd = new NpgsqlCommand(
                        "INSERT INTO \"ReleasedVulnerabilities\" (\"Id\", \"CveId\", \"Title\", \"Description\", \"CvssScore\", \"Severity\", \"PublishedDate\", \"RawNistJson\") " +
                        "VALUES (@guid, @id, NULL, @desc, @score, @severity, @pub, @rawjson)", conn, transaction);
                    
                    insertCmd.Parameters.AddWithValue("guid", Guid.NewGuid());
                    insertCmd.Parameters.AddWithValue("id", cveId);
                    insertCmd.Parameters.AddWithValue("desc", (object?)description ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("score", (object?)cvssScore ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("severity", (object?)severity ?? DBNull.Value);
                    insertCmd.Parameters.AddWithValue("pub", (object?)publishedDate ?? DBNull.Value);
                    
                    var jsonParam = new NpgsqlParameter("rawjson", NpgsqlDbType.Jsonb);
                    jsonParam.Value = cve.GetRawText();
                    insertCmd.Parameters.Add(jsonParam);
                    
                    await insertCmd.ExecuteNonQueryAsync();
                    addedCount++;
                }
                
                await transaction.CommitAsync();
                
                Console.WriteLine($"Batch complete. Uploaded {addedCount} / {totalResults} total missing CVEs.");
                startIndex += fetchedCount;

                // Delay to respect rate limits. With API key, limit is 50 requests / 30 seconds (1.6 per second).
                // Sleep for 2 seconds to be safe.
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred: {ex.Message}");
                Console.WriteLine("Sleeping 10 seconds before retrying...");
                await Task.Delay(10000);
            }
        }
        
        Console.WriteLine("Sync completed successfully!");
    }
}
