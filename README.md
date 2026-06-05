# SyncBook Server

ASP.NET Core 9 Web API backend for SyncBook.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)

## Run locally

```bash
dotnet restore
dotnet run
```

The API listens on:

- HTTP: `http://localhost:5266`
- HTTPS: `https://localhost:7289`

OpenAPI is available in Development at `/openapi/v1.json`.

## Sample endpoint

- `GET /api/weatherforecast` — sample data used to verify frontend integration

## Frontend

The Angular app lives in the sibling repo `SyncBook-front`. During development it proxies `/api/*` requests to this server.
