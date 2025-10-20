---
languages:
- csharp
products:
- dotnet
- dotnet-orleans
- dotnet-aspire
page_type: sample
name: "Orleans Voting sample app on Aspire"
urlFragment: "orleans-voting-sample-app-on-aspire"
description: "An Orleans sample demonstrating a voting app on Aspire."
---

# .NET Aspire Orleans sample app

This is a simple .NET app that shows how to use Orleans with .NET Aspire orchestration.

## Demonstrates

- How to configure a .NET Aspire app to work with Orleans

## Sample prerequisites

This sample is written in C# and targets .NET 8.0. It requires the [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later.

## Building the sample

To download and run the sample, follow these steps:

1. Clone the `dotnet/aspire-samples` repository.
2. In Visual Studio (2022 or later):
    1. On the menu bar, choose **File** > **Open** > **Project/Solution**.
    2. Navigate to the folder that holds the sample code, and open the solution (.sln) file.
    3. Right click the _OrleansVoting.AppHost_ project in the solution explore and choose it as the startup project.
    4. Choose the <kbd>F5</kbd> key to run with debugging, or <kbd>Ctrl</kbd>+<kbd>F5</kbd> keys to run the project without debugging.
3. From the command line:
   1. Navigate to the folder that holds the sample code.
   2. At the command line, type [`dotnet run`](https://docs.microsoft.com/dotnet/core/tools/dotnet-run).

To run the game, run the .NET Aspire app by executing the following at the command prompt (opened to the base directory of the sample):

``` bash
dotnet run --project OrleansVoting.AppHost
```

1. On the **Resources** page, click on one of the endpoints for the listed project. This launches the simple .NET app.
2. In the .NET app:
    1. Enter a poll title, some questions, and click **Create**, *or* click **DEMO: auto-fill poll** to auto-fill the poll.
    2. On the poll page, Click one of the poll options to vote for it.
    3. The results of the poll are displayed. Click the **DEMO: simulate other voters** button to simulate other voters voting on the poll and watch the results update.

For more information about using Orleans, see the [Orleans documentation](https://learn.microsoft.com/dotnet/orleans).


## Production deployment notes: required environment variables

When deploying this sample to production (outside of the local Aspire developer orchestrator), set the following environment variables so the Silo and the Service can discover the cluster and Redis correctly. These map directly to configuration keys consumed in `OrleansVoting.Silo/Program.cs` and `OrleansVoting.Service/AppHost.cs`.

Required for both Silo and Service:

- Connection string for Redis (clustering, and storage on Silo):
    - Environment: `ConnectionStrings__voting-redis`
    - Example: `ConnectionStrings__voting-redis=redis:6380,password=...;ssl=True;abortConnect=False`
    - Used by: `builder.Configuration.GetConnectionString("voting-redis")`

- Orleans Cluster identity (must match across Silo and Service):
    - `Orleans__ClusterOptions__ClusterId` (default: `voting-cluster`)
    - `Orleans__ClusterOptions__ServiceId` (default: `voting-app`)
    - Used by: `ClusterOptions.ClusterId` and `ClusterOptions.ServiceId`

Silo-specific:

- Grain storage provider uses Redis via the same connection string. No extra env var is required beyond `ConnectionStrings__voting-redis`.

Service-specific:

- None beyond the shared variables above.

Aspire AppHost (if you run orchestration in non-dev):

- The AppHost’s launch profiles already set the dashboard endpoints for development. If running AppHost without launch profiles, you must set at least one of:
    - `ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL` (gRPC) or `ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL` (HTTP)
    - And ensure ASP.NET Core binding is configured via `ASPNETCORE_URLS` if not using a launch profile.

General ASP.NET Core hosting:

- `ASPNETCORE_URLS` to bind Kestrel in container/VM scenarios, for example: `ASPNETCORE_URLS=http://0.0.0.0:8080`
- `DOTNET_ENVIRONMENT` or `ASPNETCORE_ENVIRONMENT` as needed (`Production` recommended)

Containerization tips:

- Set the above environment variables in your orchestrator (Docker Compose, Kubernetes, App Service), and mount them for both the Silo and the Service.
- Ensure network connectivity from Service to the Orleans gateways (Silo). Since clustering uses Redis, both must reach the Redis endpoint specified by `ConnectionStrings__voting-redis`.
- Keep `ClusterId` and `ServiceId` consistent across all deployments to join the same cluster. Use unique values per environment (e.g., `voting-cluster-prod`).

Security & secrets:

- Do not hardcode credentials in `appsettings.json`. Prefer environment variables or your platform’s secret store (e.g., Azure App Configuration/Key Vault, Kubernetes Secrets).
- If using managed services (e.g., Azure Cache for Redis), use TLS and rotate keys regularly.

