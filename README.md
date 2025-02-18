# FastReverseProxy.UserAndProxyManager Server Plugin API

This project is a production‑ready minimal API built using .NET Minimal API, EF Core, and SQLite to implement an external FRP server plugin. It extends [FRP's server plugin interface](https://github.com/fatedier/frp/blob/dev/doc/server_plugin.md) so that FRP (`frps`) can delegate operations such as client login, proxy creation, and connection handling to an external service.

## Overview

The FRP server plugin allows the FRP server to send JSON‑based RPC requests to an external service for operation validation, modification, or rejection. This API:
- Receives RPC requests on separate endpoints (e.g. `/user-manager` and `/port-manager`)
- Deserializes incoming JSON into strongly‑typed objects
- Persists each operation in a SQLite database via EF Core
- Auto‑applies EF Core migrations on startup
- Exposes GET endpoints to retrieve stored operations for each op type

This implementation follows the [FRP server plugin specification](https://github.com/fatedier/frp/blob/dev/doc/server_plugin.md).

## Features

- **Supported Operations:** Login, NewProxy, CloseProxy, Ping, NewWorkConn, NewUserConn
- **Endpoints:**  
  - `POST /user-manager` – typically used for operations such as Login  
  - `POST /port-manager` – typically used for operations such as NewProxy  
  - `GET /logs/<operation>` – retrieve logs for a specific operation (e.g. `/logs/login`)
- **Persistence:** Uses EF Core with SQLite. The database is automatically created and updated via migrations.
- **Production Ready:** Minimal API design that can be deployed behind a reverse proxy with HTTPS.

## Getting Started

### Prerequisites

- [.NET 6 SDK or later](https://dotnet.microsoft.com/download)
- No additional database installation is required—SQLite is used out-of-the-box

### Setup

1. **Clone the Repository**

   ```bash
   git clone <your-repo-url>
   cd FrpServerPluginApi
   ```

2. **Restore Dependencies**

   ```bash
   dotnet restore
   ```

3. **Install Required NuGet Packages**

   ```bash
   dotnet add package Microsoft.EntityFrameworkCore
   dotnet add package Microsoft.EntityFrameworkCore.Sqlite
   dotnet add package Microsoft.EntityFrameworkCore.Design
   ```

4. **Run the Application**

   On startup, the application will automatically apply pending migrations to create or update the SQLite database (`frp.db`).

   ```bash
   dotnet run
   ```

   The API will start on the default port (e.g. 5000) unless otherwise configured.

## Configuration

### FRP Server Plugin Configuration

In your FRP server configuration (e.g., `frps.toml`), set up the HTTP plugins as follows:

```toml
bindPort = 7000

[[httpPlugins]]
name = "user-manager"
addr = "127.0.0.1:5000"
path = "/user-manager"
ops = ["Login"]

[[httpPlugins]]
name = "port-manager"
addr = "127.0.0.1:5000"
path = "/port-manager"
ops = ["NewProxy"]
```

Make sure the `addr` and `path` settings match your deployment environment. For public-facing deployments, it is recommended to serve the API behind a reverse proxy (such as Nginx or Apache) with HTTPS enabled.

## API Endpoints

### POST Endpoints

- **`/user-manager`**  
  Receives FRP RPC requests for user-related operations (e.g., Login).  
  **Example Request:**

  ```http
  POST /user-manager?version=0.1.0&op=Login HTTP/1.1
  Host: 127.0.0.1:5000
  X-Frp-Reqid: abc123
  Content-Type: application/json

  {
      "version": "0.1.0",
      "op": "Login",
      "content": {
          "version": "1.2.3",
          "hostname": "example-host",
          "os": "linux",
          "arch": "amd64",
          "user": "username",
          "timestamp": 1678901234,
          "privilege_key": "your-key",
          "run_id": "run123",
          "pool_count": 1,
          "metas": { "env": "prod" },
          "client_address": "192.168.1.100"
      }
  }
  ```

- **`/port-manager`**  
  Receives FRP RPC requests for port-related operations (e.g., NewProxy).  
  **Example Request:**

  ```http
  POST /port-manager?version=0.1.0&op=NewProxy HTTP/1.1
  Host: 127.0.0.1:5000
  X-Frp-Reqid: def456
  Content-Type: application/json

  {
      "version": "0.1.0",
      "op": "NewProxy",
      "content": {
          "user": {
              "user": "username",
              "metas": { "env": "prod" },
              "run_id": "run123"
          },
          "proxy_name": "example-proxy",
          "proxy_type": "tcp",
          "use_encryption": false,
          "use_compression": false,
          "bandwidth_limit": "0",
          "bandwidth_limit_mode": "auto",
          "group": "default",
          "group_key": "",
          "remote_port": 6000,
          "custom_domains": [],
          "subdomain": "",
          "locations": "",
          "http_user": "",
          "http_pwd": "",
          "host_header_rewrite": "",
          "headers": {},
          "sk": "",
          "multiplexer": "",
          "metas": { "key": "value" }
      }
  }
  ```

### GET Endpoints

Retrieve stored operation logs:

- `GET /logs/login` – Returns all Login operations.
- `GET /logs/newproxy` – Returns all NewProxy operations.
- `GET /logs/closeproxy` – Returns all CloseProxy operations.
- `GET /logs/ping` – Returns all Ping operations.
- `GET /logs/newworkconn` – Returns all NewWorkConn operations.
- `GET /logs/newuserconn` – Returns all NewUserConn operations.

- **Database:**  
  SQLite is suitable for low-to-moderate workloads. For high-traffic scenarios, consider switching to a more robust database provider (e.g. PostgreSQL or SQL Server) and update your EF Core configuration accordingly.

## FRP Server Plugin Documentation

For additional details on the RPC interface and supported operations, refer to the official [FRP Server Plugin Documentation](https://github.com/fatedier/frp/blob/dev/doc/server_plugin.md).

## Contributing

I welcome contributions in the form of pull requests (PRs) for any breaking changes or modifications. However, please ensure that all PRs include a comprehensive sample demonstrating the proposed changes. Upon verification and confirmation of the sample, a new version will be released.
