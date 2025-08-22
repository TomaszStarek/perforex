using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Wiring
{
    public class Camera
    {
        public static async Task<bool> Checker(string hostname, string program, string jobId, string NazwaDoLogu)
        {
            //
            // ------------------------------
            // Top-level program
            // ------------------------------
            //var hostname = "plkwim0rje05";
            //List<string> jobIds = new List<string> { "d0bebeb2-773b-11f0-880c-0242ac120003", "6ab1c1b4-7ce5-11f0-b2f3-0242ac120003"};

            var baseUrl = "http://" + hostname + "/api/public/v1.0/";
            using var http = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(60)
            };

            // Ensure output directory
            var outputDir = Path.Combine(AppContext.BaseDirectory, "zdjecia_z_kamery");
            Directory.CreateDirectory(outputDir);

            // Shared JSON options: case-insensitive + custom DateTime converter + pretty print for files
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
            jsonOptions.Converters.Add(new DateTimeYmdHmsConverter());

            // 1) Open program
            using (var openResponse = await http.PostAsync(
                "programs/" + program + "/open",
                new StringContent("{}", Encoding.UTF8, "application/json")))
            {
                try
                {
                    openResponse.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    ErrorHandling(ex, outputDir);
                    return false;
                }
                Console.WriteLine($"[OPEN] {(int)openResponse.StatusCode} {openResponse.ReasonPhrase}");
            }

            // 2) Start Job
            using (var openResponse = await http.PostAsync(
                "programs/0/execute",
                new StringContent(@"{ ""state"":2 }", Encoding.UTF8, "application/json")))
            {
                try
                {
                    openResponse.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    ErrorHandling(ex, outputDir);
                    return false;
                }
                Console.WriteLine($"[OPEN] {(int)openResponse.StatusCode} {openResponse.ReasonPhrase}");
            }

            // 3) doing the jobs
            using (var execResponse = await http.PostAsync(
                "programs/0/execute",
                new StringContent($"{{\"job_id\":\"{jobId}\"}}", Encoding.UTF8, "application/json")))
            {
                try
                {
                    execResponse.EnsureSuccessStatusCode();

                    // Read raw JSON once (so we can both save it and deserialize from it)
                    var rawJson = await execResponse.Content.ReadAsStringAsync();

                    // Deserialize to strong types
                    var result = JsonSerializer.Deserialize<ProgramJob>(rawJson, jsonOptions);

                    // Remove "execution_image" from each job's execution
                    if (result?.JobExecution is not null) result.JobExecution.ExecutionImage = null;

                    string executionResultLabel = result?.JobExecution?.ExecutionResult switch
                    {
                        1 => "NieTrenowane",
                        2 => "Pass",
                        3 => "Fail",
                        4 => "NieZnaleziono",
                        5 => "NieUkonczono",
                        _ => "Inne"
                    };

                    var baseName = NazwaDoLogu + "--" + result?.JobName + "--" + executionResultLabel + "--" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
                    baseName = SanitizeFileName(baseName);

                    if (result?.JobExecution?.ExecutionResult != 2)
                    {
                        // Paths for outputs
                        var typedJsonPath = Path.Combine(outputDir, $"{baseName}.json");

                        // Save TYPED JSON (round-tripped through our models; dates preserved as "yyyy-MM-dd HH:mm:ss")
                        var typedJson = JsonSerializer.Serialize(result, jsonOptions);
                        await File.WriteAllTextAsync(typedJsonPath, typedJson, Encoding.UTF8);
                        Console.WriteLine();
                        Console.WriteLine("Saved:");
                        Console.WriteLine($" - Typed JSON: {typedJsonPath}");
                    }

                    result = JsonSerializer.Deserialize<ProgramJob>(rawJson, jsonOptions);

                    Console.WriteLine(NazwaDoLogu);
                    Console.WriteLine($"Job: {result?.JobName} ({result?.JobId})");

                    if (result?.JobExecution is not null)
                    {
                        Console.WriteLine($"  Result         : {result.JobExecution.ExecutionResult}");
                        Console.WriteLine($"  Start          : {result.JobExecution.ExecutionStartTime}");
                        Console.WriteLine($"  End            : {result.JobExecution.ExecutionEndTime}");
                        Console.WriteLine($"  Time (units?)  : {result.JobExecution.ExecutionTime}");

                        // Save image if present
                        if (!string.IsNullOrWhiteSpace(result.JobExecution.ExecutionImage))
                        {
                            var pathWithoutExt = Path.Combine(outputDir, baseName);

                            try
                            {
                                var savedPath = SaveImageFromBase64(result.JobExecution.ExecutionImage!, pathWithoutExt);
                                Console.WriteLine($"  Image saved    : {savedPath}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"  Image save failed: {ex.Message}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("  No image data.");
                        }
                    }
                    if (result?.JobExecution?.ExecutionResult != 2)
                    {
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    ErrorHandling(ex, outputDir);
                    //EndJob(http, outputDir);
                    return false;
                }
            }
            //EndJob(http, outputDir);
            return true;
        }
        // ------------------------------
        // End Job jako funkcja
        // ------------------------------

        private static async void EndJob(HttpClient http, string outputDir)
        {
            using (var openResponse = await http.PostAsync(
                "programs/0/execute",
                new StringContent(@"{ ""state"":1 }", Encoding.UTF8, "application/json")))
            {
                try
                {
                    openResponse.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    ErrorHandling(ex, outputDir);
                }
                Console.WriteLine($"[OPEN] {(int)openResponse.StatusCode} {openResponse.ReasonPhrase}");
            }
        }

        // ------------------------------
        // Helpers
        // ------------------------------

        private static void ErrorHandling(Exception ex, string outputDir)
        {
            var errorDetails = new
            {
                Timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                Error = ex.GetType().Name,
                Message = ex.Message,
                StackTrace = ex.StackTrace
            };
            var json = System.Text.Json.JsonSerializer.Serialize(errorDetails, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            var fileName = $"Error_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            fileName = SanitizeFileName(fileName);
            System.IO.File.WriteAllText(Path.Combine(outputDir, fileName), json);
            Console.WriteLine($"Error occurred. Details saved to {Path.Combine(outputDir, fileName)}");
        }
        private static string? SanitizeFileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            // Collapse spaces
            return name.Trim().Replace(' ', '_');
        }

        private static string SaveImageFromBase64(string base64OrDataUri, string pathWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(base64OrDataUri))
                throw new ArgumentException("Empty image payload.");

            // Strip data URI prefix if present
            var commaIdx = base64OrDataUri.IndexOf(',');
            string b64 = (base64OrDataUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIdx >= 0)
                ? base64OrDataUri[(commaIdx + 1)..]
                : base64OrDataUri;

            // Base64 decode
            byte[] bytes;
            try
            {
                // Remove whitespace/newlines that may be present
                b64 = b64.Trim().Replace("\r", "").Replace("\n", "");
                bytes = Convert.FromBase64String(b64);
            }
            catch (FormatException fe)
            {
                throw new InvalidDataException("Image payload is not valid Base64.", fe);
            }

            var ext = ".jpg";
            var fullPath = Path.ChangeExtension(pathWithoutExtension, ext);
            File.WriteAllBytes(fullPath, bytes);
            return fullPath;
        }

        // ------------------------------
        // Custom DateTime converter: "yyyy-MM-dd HH:mm:ss"
        // ------------------------------
        internal sealed class DateTimeYmdHmsConverter : JsonConverter<DateTime>
        {
            private const string Format = "yyyy-MM-dd HH:mm:ss";

            public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.String)
                    throw new JsonException("Expected string for DateTime.");

                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s))
                    return default;

                if (DateTime.TryParseExact(
                        s, Format, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces, out var dt))
                {
                    return dt;
                }

                // Fallback: try general parse
                if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dt))
                {
                    return dt;
                }

                throw new JsonException($"Invalid date format '{s}'. Expected {Format}.");
            }

            public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
                writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
        }
        // ------------------------------
        // Models matching your JSON
        // ------------------------------

        internal sealed class ProgramJob
        {
            [JsonPropertyName("job_id")]
            public string? JobId { get; set; } // switch to Guid if always a valid UUID

            [JsonPropertyName("job_description")]
            public string? JobDescription { get; set; }

            [JsonPropertyName("job_name")]
            public string? JobName { get; set; }

            [JsonPropertyName("job_execution")]
            public JobExecution? JobExecution { get; set; }

            // Replace with a typed class when you know the schema
            [JsonPropertyName("job_tasks")]
            public List<JsonElement> JobTasks { get; set; } = new();

            [JsonExtensionData]
            public Dictionary<string, JsonElement>? Extra { get; set; }
        }

        internal sealed class JobExecution
        {
            [JsonPropertyName("execution_end_time")]
            public DateTime ExecutionEndTime { get; set; }

            [JsonPropertyName("execution_id")]
            public string? ExecutionId { get; set; }

            [JsonPropertyName("execution_result")]
            public int ExecutionResult { get; set; }

            [JsonPropertyName("execution_start_time")]
            public DateTime ExecutionStartTime { get; set; }

            [JsonPropertyName("execution_time")]
            public long ExecutionTime { get; set; }

            [JsonPropertyName("execution_image")]
            public string? ExecutionImage { get; set; } // keep as string; decode later if needed

            [JsonPropertyName("image_acquisition_time")]
            public long ImageAcquisitionTime { get; set; }

            [JsonExtensionData]
            public Dictionary<string, JsonElement>? Extra { get; set; }
        }
    }
}
