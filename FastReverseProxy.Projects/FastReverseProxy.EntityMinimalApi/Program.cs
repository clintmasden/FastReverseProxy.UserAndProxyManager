using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Register the DbContext with a SQLite connection.
builder.Services.AddDbContext<FrpDbContext>(options =>
    options.UseSqlite("Data Source=frp.db"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}


// Auto-apply migrations on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FrpDbContext>();
    db.Database.Migrate();
}

//
// POST Endpoints: /user-manager and /port-manager
//
app.MapPost("/user-manager", async (HttpRequest req, HttpResponse res) =>
{
    await ProcessFrpRequest(req, res, "user-manager");
});

app.MapPost("/port-manager", async (HttpRequest req, HttpResponse res) =>
{
    await ProcessFrpRequest(req, res, "port-manager");
});

async Task ProcessFrpRequest(HttpRequest request, HttpResponse response, string endpoint)
{
    // Read query parameters and header.
    string queryOp = request.Query["op"];
    string queryVersion = request.Query["version"];
    string reqId = request.Headers["X-Frp-Reqid"];

    // Read the request body.
    string body = await new StreamReader(request.Body).ReadToEndAsync();

    // Deserialize the common RPC envelope.
    FrpRpcRequest rpcRequest;
    try
    {
        rpcRequest = JsonSerializer.Deserialize<FrpRpcRequest>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? throw new Exception("Deserialization returned null");
    }
    catch (Exception ex)
    {
        response.StatusCode = 400;
        await response.WriteAsync("Error parsing JSON: " + ex.Message);
        return;
    }

    // Options for inner deserialization.
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    // Obtain the EF Core DbContext.
    var db = request.HttpContext.RequestServices.GetRequiredService<FrpDbContext>();

    // Process based on the "op" field.
    switch (rpcRequest.Op?.ToLowerInvariant())
    {
        case "login":
            {
                var login = rpcRequest.Content.Deserialize<LoginContent>(options);
                if (login == null)
                {
                    response.StatusCode = 400;
                    await response.WriteAsync("Login content is null");
                    return;
                }
                var entity = new LoginOperation
                {
                    Endpoint = endpoint,
                    ReqId = reqId,
                    Op = rpcRequest.Op,
                    Version = rpcRequest.Version,
                    QueryOp = queryOp,
                    QueryVersion = queryVersion,
                    Timestamp = DateTime.UtcNow,
                    ContentJson = JsonSerializer.Serialize(login, new JsonSerializerOptions { WriteIndented = true })
                };
                db.LoginOperations.Add(entity);
                await db.SaveChangesAsync();
                var result = new { reject = false, unchange = true };
                response.ContentType = "application/json";
                await response.WriteAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }
            break;

        case "newproxy":
            {
                var newproxy = rpcRequest.Content.Deserialize<NewProxyContent>(options);
                if (newproxy == null)
                {
                    response.StatusCode = 400;
                    await response.WriteAsync("NewProxy content is null");
                    return;
                }
                var entity = new NewProxyOperation
                {
                    Endpoint = endpoint,
                    ReqId = reqId,
                    Op = rpcRequest.Op,
                    Version = rpcRequest.Version,
                    QueryOp = queryOp,
                    QueryVersion = queryVersion,
                    Timestamp = DateTime.UtcNow,
                    ContentJson = JsonSerializer.Serialize(newproxy, new JsonSerializerOptions { WriteIndented = true })
                };
                db.NewProxyOperations.Add(entity);
                await db.SaveChangesAsync();
                // For NewProxy we demonstrate a modified response by echoing back the content.
                var result = new { unchange = false, content = newproxy };
                response.ContentType = "application/json";
                await response.WriteAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }
            break;

        case "closeproxy":
            {
                var closeproxy = rpcRequest.Content.Deserialize<CloseProxyContent>(options);
                if (closeproxy == null)
                {
                    response.StatusCode = 400;
                    await response.WriteAsync("CloseProxy content is null");
                    return;
                }
                var entity = new CloseProxyOperation
                {
                    Endpoint = endpoint,
                    ReqId = reqId,
                    Op = rpcRequest.Op,
                    Version = rpcRequest.Version,
                    QueryOp = queryOp,
                    QueryVersion = queryVersion,
                    Timestamp = DateTime.UtcNow,
                    ContentJson = JsonSerializer.Serialize(closeproxy, new JsonSerializerOptions { WriteIndented = true })
                };
                db.CloseProxyOperations.Add(entity);
                await db.SaveChangesAsync();
                var result = new { reject = false, unchange = true };
                response.ContentType = "application/json";
                await response.WriteAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }
            break;

        case "ping":
            {
                var ping = rpcRequest.Content.Deserialize<PingContent>(options);
                if (ping == null)
                {
                    response.StatusCode = 400;
                    await response.WriteAsync("Ping content is null");
                    return;
                }
                var entity = new PingOperation
                {
                    Endpoint = endpoint,
                    ReqId = reqId,
                    Op = rpcRequest.Op,
                    Version = rpcRequest.Version,
                    QueryOp = queryOp,
                    QueryVersion = queryVersion,
                    Timestamp = DateTime.UtcNow,
                    ContentJson = JsonSerializer.Serialize(ping, new JsonSerializerOptions { WriteIndented = true })
                };
                db.PingOperations.Add(entity);
                await db.SaveChangesAsync();
                var result = new { reject = false, unchange = true };
                response.ContentType = "application/json";
                await response.WriteAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }
            break;

        case "newworkconn":
            {
                var newworkconn = rpcRequest.Content.Deserialize<NewWorkConnContent>(options);
                if (newworkconn == null)
                {
                    response.StatusCode = 400;
                    await response.WriteAsync("NewWorkConn content is null");
                    return;
                }
                var entity = new NewWorkConnOperation
                {
                    Endpoint = endpoint,
                    ReqId = reqId,
                    Op = rpcRequest.Op,
                    Version = rpcRequest.Version,
                    QueryOp = queryOp,
                    QueryVersion = queryVersion,
                    Timestamp = DateTime.UtcNow,
                    ContentJson = JsonSerializer.Serialize(newworkconn, new JsonSerializerOptions { WriteIndented = true })
                };
                db.NewWorkConnOperations.Add(entity);
                await db.SaveChangesAsync();
                var result = new { reject = false, unchange = true };
                response.ContentType = "application/json";
                await response.WriteAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }
            break;

        case "newuserconn":
            {
                var newuserconn = rpcRequest.Content.Deserialize<NewUserConnContent>(options);
                if (newuserconn == null)
                {
                    response.StatusCode = 400;
                    await response.WriteAsync("NewUserConn content is null");
                    return;
                }
                var entity = new NewUserConnOperation
                {
                    Endpoint = endpoint,
                    ReqId = reqId,
                    Op = rpcRequest.Op,
                    Version = rpcRequest.Version,
                    QueryOp = queryOp,
                    QueryVersion = queryVersion,
                    Timestamp = DateTime.UtcNow,
                    ContentJson = JsonSerializer.Serialize(newuserconn, new JsonSerializerOptions { WriteIndented = true })
                };
                db.NewUserConnOperations.Add(entity);
                await db.SaveChangesAsync();
                var result = new { reject = false, unchange = true };
                response.ContentType = "application/json";
                await response.WriteAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }
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

//
// GET Endpoints: Retrieve stored operations
//
app.MapGet("/logs/login", async (FrpDbContext db) =>
    await db.LoginOperations.ToListAsync());

app.MapGet("/logs/newproxy", async (FrpDbContext db) =>
    await db.NewProxyOperations.ToListAsync());

app.MapGet("/logs/closeproxy", async (FrpDbContext db) =>
    await db.CloseProxyOperations.ToListAsync());

app.MapGet("/logs/ping", async (FrpDbContext db) =>
    await db.PingOperations.ToListAsync());

app.MapGet("/logs/newworkconn", async (FrpDbContext db) =>
    await db.NewWorkConnOperations.ToListAsync());

app.MapGet("/logs/newuserconn", async (FrpDbContext db) =>
    await db.NewUserConnOperations.ToListAsync());

app.Run();

//
// EF Core DbContext and Entity Models
//
public class FrpDbContext : DbContext
{
    public FrpDbContext(DbContextOptions<FrpDbContext> options) : base(options) { }

    public DbSet<LoginOperation> LoginOperations { get; set; }
    public DbSet<NewProxyOperation> NewProxyOperations { get; set; }
    public DbSet<CloseProxyOperation> CloseProxyOperations { get; set; }
    public DbSet<PingOperation> PingOperations { get; set; }
    public DbSet<NewWorkConnOperation> NewWorkConnOperations { get; set; }
    public DbSet<NewUserConnOperation> NewUserConnOperations { get; set; }
}

public class LoginOperation
{
    public int Id { get; set; }
    public string Endpoint { get; set; }
    public string ReqId { get; set; }
    public string Op { get; set; }
    public string Version { get; set; }
    public string QueryOp { get; set; }
    public string QueryVersion { get; set; }
    public DateTime Timestamp { get; set; }
    public string ContentJson { get; set; }
}

public class NewProxyOperation
{
    public int Id { get; set; }
    public string Endpoint { get; set; }
    public string ReqId { get; set; }
    public string Op { get; set; }
    public string Version { get; set; }
    public string QueryOp { get; set; }
    public string QueryVersion { get; set; }
    public DateTime Timestamp { get; set; }
    public string ContentJson { get; set; }
}

public class CloseProxyOperation
{
    public int Id { get; set; }
    public string Endpoint { get; set; }
    public string ReqId { get; set; }
    public string Op { get; set; }
    public string Version { get; set; }
    public string QueryOp { get; set; }
    public string QueryVersion { get; set; }
    public DateTime Timestamp { get; set; }
    public string ContentJson { get; set; }
}

public class PingOperation
{
    public int Id { get; set; }
    public string Endpoint { get; set; }
    public string ReqId { get; set; }
    public string Op { get; set; }
    public string Version { get; set; }
    public string QueryOp { get; set; }
    public string QueryVersion { get; set; }
    public DateTime Timestamp { get; set; }
    public string ContentJson { get; set; }
}

public class NewWorkConnOperation
{
    public int Id { get; set; }
    public string Endpoint { get; set; }
    public string ReqId { get; set; }
    public string Op { get; set; }
    public string Version { get; set; }
    public string QueryOp { get; set; }
    public string QueryVersion { get; set; }
    public DateTime Timestamp { get; set; }
    public string ContentJson { get; set; }
}

public class NewUserConnOperation
{
    public int Id { get; set; }
    public string Endpoint { get; set; }
    public string ReqId { get; set; }
    public string Op { get; set; }
    public string Version { get; set; }
    public string QueryOp { get; set; }
    public string QueryVersion { get; set; }
    public DateTime Timestamp { get; set; }
    public string ContentJson { get; set; }
}

//
// Common RPC Envelope and Operation Content Models
//
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
