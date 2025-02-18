using System.Text.Json;
using System.Text.Json.Serialization;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

//
// POST Endpoints
//

// Both endpoints use the same processing logic.
app.MapPost("/user-manager", (HttpRequest req, HttpResponse res) => ProcessFrpRequest(req, res, "user-manager"));
app.MapPost("/port-manager", (HttpRequest req, HttpResponse res) => ProcessFrpRequest(req, res, "port-manager"));

async Task ProcessFrpRequest(HttpRequest request, HttpResponse response, string endpoint)
{
    // Read query parameters and header.
    string queryOp = request.Query["op"];
    string queryVersion = request.Query["version"];
    string reqId = request.Headers["X-Frp-Reqid"];

    // Read the entire request body.
    string body = await new StreamReader(request.Body).ReadToEndAsync();

    // Deserialize the common envelope.
    FrpRpcRequest rpcRequest;
    try
    {
        rpcRequest = JsonSerializer.Deserialize<FrpRpcRequest>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? throw new Exception("Deserialization returned null");
    }
    catch (System.Exception ex)
    {
        response.StatusCode = 400;
        await response.WriteAsync("Error parsing JSON: " + ex.Message);
        return;
    }

    // Switch on the op field and call our generic method.
    switch (rpcRequest.Op?.ToLowerInvariant())
    {
        case "login":
            await ProcessOperation<LoginContent>(rpcRequest, endpoint, reqId, queryOp, queryVersion, response, echoContent: false);
            break;
        case "newproxy":
            await ProcessOperation<NewProxyContent>(rpcRequest, endpoint, reqId, queryOp, queryVersion, response, echoContent: true);
            break;
        case "closeproxy":
            await ProcessOperation<CloseProxyContent>(rpcRequest, endpoint, reqId, queryOp, queryVersion, response, echoContent: false);
            break;
        case "ping":
            await ProcessOperation<PingContent>(rpcRequest, endpoint, reqId, queryOp, queryVersion, response, echoContent: false);
            break;
        case "newworkconn":
            await ProcessOperation<NewWorkConnContent>(rpcRequest, endpoint, reqId, queryOp, queryVersion, response, echoContent: false);
            break;
        case "newuserconn":
            await ProcessOperation<NewUserConnContent>(rpcRequest, endpoint, reqId, queryOp, queryVersion, response, echoContent: false);
            break;
        default:
            {
                var result = new { reject = true, reject_reason = "Unsupported operation" };
                response.ContentType = "application/json";
                await response.WriteAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }
            break;
    }
}

/// <summary>
/// Processes an FRP operation generically:
///   1. Deserializes the "content" into the provided type T.
///   2. Creates a log entry and appends it to the corresponding file (named "<op>.json").
///   3. Writes a response based on the echoContent flag.
/// </summary>
async Task ProcessOperation<T>(FrpRpcRequest rpcRequest, string endpoint, string reqId, string queryOp, string queryVersion, HttpResponse response, bool echoContent = false) where T : class
{
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    T content;
    try
    {
        content = rpcRequest.Content.Deserialize<T>(options);
        if (content == null)
        {
            response.StatusCode = 400;
            await response.WriteAsync($"{rpcRequest.Op} content is null");
            return;
        }
    }
    catch (System.Exception ex)
    {
        response.StatusCode = 400;
        await response.WriteAsync($"Error parsing {rpcRequest.Op} content: " + ex.Message);
        return;
    }

    // Create a log entry.
    var logEntry = new FrpOperationLog<T>
    {
        Endpoint = endpoint,
        ReqId = reqId,
        Op = rpcRequest.Op,
        Version = rpcRequest.Version,
        QueryOp = queryOp,
        QueryVersion = queryVersion,
        Content = content,
        Timestamp = System.DateTime.UtcNow
    };

    // Use a file named after the op (e.g. "login.json").
    string fileName = $"{rpcRequest.Op?.ToLowerInvariant()}.json";
    var logs = LoadLogs<T>(fileName);
    logs.Add(logEntry);
    SaveLogs(fileName, logs);

    // Create a response.
    object result = echoContent
        ? new { unchange = false, content }
        : new { reject = false, unchange = true };

    response.ContentType = "application/json";
    await response.WriteAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
}

//
// GET Endpoints
//
// Each GET endpoint loads and returns the entire log file for that operation type.

app.MapGet("/logs/login", () =>
{
    var logs = LoadLogs<LoginContent>("login.json");
    return Results.Json(logs, new JsonSerializerOptions { WriteIndented = true });
});
app.MapGet("/logs/newproxy", () =>
{
    var logs = LoadLogs<NewProxyContent>("newproxy.json");
    return Results.Json(logs, new JsonSerializerOptions { WriteIndented = true });
});
app.MapGet("/logs/closeproxy", () =>
{
    var logs = LoadLogs<CloseProxyContent>("closeproxy.json");
    return Results.Json(logs, new JsonSerializerOptions { WriteIndented = true });
});
app.MapGet("/logs/ping", () =>
{
    var logs = LoadLogs<PingContent>("ping.json");
    return Results.Json(logs, new JsonSerializerOptions { WriteIndented = true });
});
app.MapGet("/logs/newworkconn", () =>
{
    var logs = LoadLogs<NewWorkConnContent>("newworkconn.json");
    return Results.Json(logs, new JsonSerializerOptions { WriteIndented = true });
});
app.MapGet("/logs/newuserconn", () =>
{
    var logs = LoadLogs<NewUserConnContent>("newuserconn.json");
    return Results.Json(logs, new JsonSerializerOptions { WriteIndented = true });
});

app.Run();

//
// Helper methods for loading and saving logs generically
//
List<FrpOperationLog<T>> LoadLogs<T>(string fileName)
{
    if (File.Exists(fileName))
    {
        try
        {
            string json = File.ReadAllText(fileName);
            return JsonSerializer.Deserialize<List<FrpOperationLog<T>>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new List<FrpOperationLog<T>>();
        }
        catch
        {
            return new List<FrpOperationLog<T>>();
        }
    }
    return new List<FrpOperationLog<T>>();
}

void SaveLogs<T>(string fileName, List<FrpOperationLog<T>> logs)
{
    var options = new JsonSerializerOptions { WriteIndented = true };
    string json = JsonSerializer.Serialize(logs, options);
    File.WriteAllText(fileName, json);
}

//
// Models
//

// The common RPC envelope.
public class FrpRpcRequest
{
    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("op")]
    public string Op { get; set; }

    // "content" is stored as a JsonElement and later deserialized into its proper type.
    [JsonPropertyName("content")]
    public JsonElement Content { get; set; }
}

// Generic log entry class.
public class FrpOperationLog<T>
{
    public string Endpoint { get; set; }
    public string ReqId { get; set; }
    public string Op { get; set; }
    public string Version { get; set; }
    public string QueryOp { get; set; }
    public string QueryVersion { get; set; }
    public T Content { get; set; }
    public System.DateTime Timestamp { get; set; }
}

//
// Operation-specific content models
//

public class LoginContent
{
    [JsonPropertyName("version")]
    public string Version { get; set; }
    [JsonPropertyName("hostname")]
    public string Hostname { get; set; }
    [JsonPropertyName("os")]
    public string Os { get; set; }
    [JsonPropertyName("arch")]
    public string Arch { get; set; }
    [JsonPropertyName("user")]
    public string User { get; set; }
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
    [JsonPropertyName("privilege_key")]
    public string PrivilegeKey { get; set; }
    [JsonPropertyName("run_id")]
    public string RunId { get; set; }
    [JsonPropertyName("pool_count")]
    public int PoolCount { get; set; }
    [JsonPropertyName("metas")]
    public Dictionary<string, string>? Metas { get; set; }
    [JsonPropertyName("client_address")]
    public string ClientAddress { get; set; }
}

public class FrpUser
{
    [JsonPropertyName("user")]
    public string User { get; set; }
    [JsonPropertyName("metas")]
    public Dictionary<string, string>? Metas { get; set; }
    [JsonPropertyName("run_id")]
    public string RunId { get; set; }
}

public class NewProxyContent
{
    [JsonPropertyName("user")]
    public FrpUser User { get; set; }
    [JsonPropertyName("proxy_name")]
    public string ProxyName { get; set; }
    [JsonPropertyName("proxy_type")]
    public string ProxyType { get; set; }
    [JsonPropertyName("use_encryption")]
    public bool UseEncryption { get; set; }
    [JsonPropertyName("use_compression")]
    public bool UseCompression { get; set; }
    [JsonPropertyName("bandwidth_limit")]
    public string BandwidthLimit { get; set; }
    [JsonPropertyName("bandwidth_limit_mode")]
    public string BandwidthLimitMode { get; set; }
    [JsonPropertyName("group")]
    public string Group { get; set; }
    [JsonPropertyName("group_key")]
    public string GroupKey { get; set; }
    [JsonPropertyName("remote_port")]
    public int? RemotePort { get; set; }
    [JsonPropertyName("custom_domains")]
    public List<string>? CustomDomains { get; set; }
    [JsonPropertyName("subdomain")]
    public string Subdomain { get; set; }
    [JsonPropertyName("locations")]
    public string Locations { get; set; }
    [JsonPropertyName("http_user")]
    public string HttpUser { get; set; }
    [JsonPropertyName("http_pwd")]
    public string HttpPwd { get; set; }
    [JsonPropertyName("host_header_rewrite")]
    public string HostHeaderRewrite { get; set; }
    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }
    [JsonPropertyName("sk")]
    public string Sk { get; set; }
    [JsonPropertyName("multiplexer")]
    public string Multiplexer { get; set; }
    [JsonPropertyName("metas")]
    public Dictionary<string, string>? Metas { get; set; }
}

public class CloseProxyContent
{
    [JsonPropertyName("user")]
    public FrpUser User { get; set; }
    [JsonPropertyName("proxy_name")]
    public string ProxyName { get; set; }
}

public class PingContent
{
    [JsonPropertyName("user")]
    public FrpUser User { get; set; }
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
    [JsonPropertyName("privilege_key")]
    public string PrivilegeKey { get; set; }
}

public class NewWorkConnContent
{
    [JsonPropertyName("user")]
    public FrpUser User { get; set; }
    [JsonPropertyName("run_id")]
    public string RunId { get; set; }
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
    [JsonPropertyName("privilege_key")]
    public string PrivilegeKey { get; set; }
}

public class NewUserConnContent
{
    [JsonPropertyName("user")]
    public FrpUser User { get; set; }
    [JsonPropertyName("proxy_name")]
    public string ProxyName { get; set; }
    [JsonPropertyName("proxy_type")]
    public string ProxyType { get; set; }
    [JsonPropertyName("remote_addr")]
    public string RemoteAddr { get; set; }
}
