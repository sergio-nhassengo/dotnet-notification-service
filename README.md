# MPDCApiTemplate

An ASP.NET Core 10 Web API template built on Clean Architecture, with CQRS (MediatR), JWT authentication,
EF Core / SQL Server persistence, and OpenTelemetry-based observability wired in out of the box.

## Architecture

The solution is split into five projects, each with a single direction of dependency (outer layers depend on
inner ones, never the reverse):

| Project | Responsibility |
|---|---|
| `Domain` | Entities, domain events, and domain exceptions. No dependencies on anything else in the solution. |
| `Application` | Use cases as MediatR commands/queries + handlers, one folder per feature under `Features/`, FluentValidation validators, AutoMapper projections, and the pipeline behaviours (logging, performance, validation, unhandled-exception) that wrap every request. Depends only on `Domain`. |
| `Infrastructure` | Implementations of the interfaces `Application` declares — JWT issuing, password hashing, the current-user accessor — plus cross-cutting extensions for auth, CORS, Serilog, and OpenTelemetry. |
| `Persistence` | `ApplicationDbContext` (EF Core / SQL Server), entity configurations, and migrations. Implements `Application`'s `IApplicationDbContext`. |
| `Api` | ASP.NET Core host: controllers, the global exception handler, OpenAPI/Swagger setup. Composes the other four layers via DI in `Program.cs`. |

Adding a new use case means adding a new folder under `Application/Features/<FeatureName>/{Commands,Queries}/<Name>/`
— see [FeatureCli](#featurecli-scaffolding-tool) below for a scaffolding tool that generates this structure for you.

## Tech stack

- **ASP.NET Core 10** / EF Core 10 targeting SQL Server
- **MediatR** for CQRS, **FluentValidation** for request validation, **AutoMapper** for query projections
- **JWT bearer auth** (`Microsoft.AspNetCore.Authentication.JwtBearer`) with **BCrypt.Net-Next** password hashing
- **Serilog** (console/file/MSSQL/Grafana Loki sinks) + **OpenTelemetry** (traces, metrics, logs via OTLP, with ASP.NET Core / HttpClient / SqlClient / Hangfire / EF Core instrumentation)
- **xUnit** for testing, with a custom OpenTelemetry test-result exporter (see [Testing](#testing))

## Using this repo as a `dotnet new` template

This repo ships a `.template.config/template.json`, so instead of cloning it and hand-renaming everything,
you can install it as a `dotnet new` template and scaffold fresh, ready-to-build solutions from the CLI.

### Install

```bash
dotnet new install /path/to/this/repo
# or, from inside the repo:
dotnet new install .
```

This registers it as **Clean Architecture Web API** under the short name **`cleanapi`**. Confirm it's there with:

```bash
dotnet new list cleanapi
```

### Scaffold a new solution

```bash
dotnet new cleanapi -n YourCompany.YourProject \
  --DefaultDbInstance "localhost,1433" \
  --LoggingDbInstance "localhost,1433"
```

| Parameter | Required | Description |
|---|---|---|
| `-n, --name <Name>` | yes | Solution/root-namespace name. Also becomes the output folder (`YourCompany.YourProject/`) and the default database name in both connection strings |
| `--DefaultDbInstance <addr>` | yes | SQL Server instance for the `Default` connection string, e.g. `localhost,1433` |
| `--LoggingDbInstance <addr>` | yes | SQL Server instance for the Serilog MSSQL sink's `Logging` connection string |

This renames every occurrence of `MPDCApiTemplate` — namespaces, the `.sln` file, the default database name,
even inside this README — to whatever you pass as `-n`, fills in the two connection-string placeholders, and
swaps in fresh project GUIDs. Verified end-to-end: a solution scaffolded this way builds clean with `dotnet build`,
zero errors.

**Not included in a generated solution:** `tools/FeatureCli` and `nuget-local-feed/` are deliberately excluded
from the template (see the `exclude` list in `.template.config/template.json`) — they're tooling for developing
*this* repo, not part of the shipped API. A scaffolded project's copy of this README will still describe
FeatureCli even though the tool itself isn't there; if you want it in a new project too, copy `tools/FeatureCli`
over by hand and follow [Installing FeatureCli as a global tool](#installing-featurecli-as-a-global-tool).

### Uninstall

```bash
dotnet new uninstall /path/to/this/repo
```

## Getting started

### Prerequisites

- .NET 10 SDK
- A SQL Server instance reachable at the connection string in `Api/appsettings*.json` (defaults to `localhost,14330`)
- Optionally, an OTLP-compatible collector (Jaeger, Aspire Dashboard, the OTel Collector, etc.) at the endpoint configured under `OpenTelemetry:Otlp:Endpoint`

### Configuration

Configuration lives in `Api/appsettings.json` and `Api/appsettings.Development.json`:

- `ConnectionStrings:Default` / `:Logging` — SQL Server connection strings (app data and the Serilog MSSQL sink)
- `Jwt:SigningKey` / `:Issuer` / `:Audience` / `:AccessTokenExpirationMinutes` — token issuing config. The shipped dev key is for local use only; set a real secret (e.g. via user-secrets or environment variables) for anything beyond local development
- `OpenTelemetry:ServiceName` / `:Otlp:Endpoint` — where traces/metrics/logs are exported
- `Cors:AllowedOrigins` — allowed origins for the API's CORS policy

### Running the API

```bash
dotnet run --project Api
```

By default the API serves Swagger UI at `/swagger` (backed by the OpenAPI document at `/openapi/v1.json`).
A `diagnostics/throw-exception` endpoint is included to exercise the global exception handler.

### Database migrations

EF Core migrations live in `Persistence/Migrations`. With the `dotnet-ef` global tool installed:

```bash
dotnet ef database update --project Persistence --startup-project Api
```

### Docker

Each layer has a `Dockerfile`, and `compose.yaml` at the repo root builds an image per project:

```bash
docker compose build
```

This builds the application images only — it does not provision SQL Server or an OTLP collector, which
you're expected to run separately (or add your own services to the compose file for local orchestration).

## Testing

The `Tests/` folder contains one project per layer that has meaningful logic to verify:

| Project | Covers |
|---|---|
| `Domain.UnitTests` | Entity base classes (domain events, audit defaults), exception message formatting |
| `Infrastructure.UnitTests` | `DateTimeService`, `BCryptPasswordHasher`, `JwtTokenGenerator`, `CurrentUserService` |
| `Persistence.UnitTests` | `ApplicationDbContext` audit-field stamping and CRUD, against EF Core InMemory |
| `Application.UnitTests` | CQRS convention checks, handler behaviour (against EF Core InMemory), validators, pipeline behaviours |
| `Api.IntegrationTests` | End-to-end auth flow against an in-process `WebApplicationFactory<Program>` |

Run everything with:

```bash
dotnet test MPDCApiTemplate.sln
```

### Exporting test results to OpenTelemetry

`tools/TestTelemetryExporter` reads a `.trx` results file and re-emits it as OpenTelemetry signals — one span
per test (name, outcome, duration, error status on failure) plus pass/fail/skip counters — to either an OTLP
collector or the console, so a test run can show up next to your app's traces in the same observability backend:

```bash
dotnet test --logger "trx;LogFileName=results.trx"
dotnet run --project tools/TestTelemetryExporter -- <path-to-results.trx> [--otlp-endpoint http://localhost:4317] [--service-name MyService]
```

Omitting `--otlp-endpoint` uses the console exporter. The tool exits non-zero if any test failed, so it can
also be used as a CI gate.

## FeatureCli scaffolding tool

`tools/FeatureCli` is a console tool (`PackageId: FeatureCli`, command name `feature`) that scaffolds new CQRS
features under `<ProjectDir>/Features/<FeatureFolder>/<Queries|Commands>/<Name>/`. It has three subcommands:
`query` and `command` scaffold a single request (as a TODO-marked stub for you to fill in), and `crud` scaffolds
a full working Create/Read/Update/Delete set for one entity in one shot.

### `query` / `command` — single request

```bash
feature query   --feature TodoLists --entity TodoList --name GetTodoListById
```

generates:

```
Application/Features/TodoLists/Queries/GetTodoListById/
├── GetTodoListByIdQuery.cs         # public record GetTodoListByIdQuery : IRequest<GetTodoListByIdResponse>;
├── GetTodoListByIdQueryHandler.cs  # handler stub, throws NotImplementedException with a TODO
└── GetTodoListByIdResponse.cs      # empty response DTO, TODO to fill in properties
```

```bash
feature command --feature TodoLists --entity TodoList --name DeleteTodoList
```

generates the command equivalent — `DeleteTodoListCommand.cs`, `DeleteTodoListCommandHandler.cs`, and
`DeleteTodoListCommandValidator.cs` — each with a `// TODO` marking the part you still need to write.

### `crud` — a full CRUD slice for one entity

The intended workflow is **entity-first**: add the entity class under `Domain/Entities` yourself (it has to
exist for `context.<Plural>` to compile anyway), then let `crud` read its properties straight off that file —
no need to type them out again:

```bash
# Domain/Entities/Category.cs already defines Title (string) and Description (string?)
feature crud --feature Categories --entity Category --plural Categories
```
```
Detected 2 properties from 'Domain/Entities/Category.cs': Title:string, Description:string?
Created Application/Features/Categories/Commands/CreateCategory/CreateCategoryCommand.cs
...
```

`crud` finds `<EntityName>.cs` by searching the `Domain` project (override with `--entity-project` or point at
it directly with `--entity-path`), then picks up every `public ... { get; set; }` / `{ get; init; }` property
whose type is a plain scalar (`string`, `int`, `bool`, `DateTimeOffset`, `Guid`, etc.). Navigation properties
and collections (anything not in that scalar list) are skipped automatically — no risk of trying to scaffold a
command parameter typed as another entity. If no entity file is found, or it has no scalar properties yet,
`crud` falls back to a single `Title:string` and tells you so.

Pass `--properties "Name:Type,..."` explicitly to skip auto-detection entirely — useful if the entity doesn't
exist yet, or you want the generated slice to differ from the entity's actual shape:

```bash
feature crud --feature Categories --entity Category --properties "Title:string,Description:string?"
```

Either way you get five complete, working (not stubbed) features plus a controller in one call, modeled
directly on this template's own `TodoLists` feature:

```
Application/Features/Categories/
├── Commands/
│   ├── CreateCategory/    CreateCategoryCommand.cs, CreateCategoryCommandHandler.cs, CreateCategoryCommandValidator.cs
│   ├── UpdateCategory/    UpdateCategoryCommand.cs, UpdateCategoryCommandHandler.cs, UpdateCategoryCommandValidator.cs
│   └── DeleteCategory/    DeleteCategoryCommand.cs, DeleteCategoryCommandHandler.cs
└── Queries/
    ├── GetCategoryById/   GetCategoryByIdQuery.cs, GetCategoryByIdQueryHandler.cs
    └── GetCategories/     GetCategoriesQuery.cs, GetCategoriesQueryHandler.cs, CategoryDto.cs

Api/Controllers/
└── CategoriesController.cs   GET/GET-by-id/POST/PUT/DELETE wired to the commands/queries above
```

Each handler is fully implemented against `context.Categories` (add/save, `FirstOrDefaultAsync` + `NotFoundException`,
AutoMapper `ProjectTo`), and `CategoryDto` is generated once under `GetCategories/` and shared by both queries —
exactly the pattern `TodoListDto` follows for `TodoLists`. `CategoriesController` (found by searching for
`<ApiProject>.csproj`, default `Api`) follows the same `BaseController`/`Mediator` pattern as
`TodoListsController` — no constructor injection, just `[HttpGet]`/`[HttpPost]`/etc. methods that call
`this.Mediator.Send(...)`. Pass `--no-controller` to skip it. The only other thing left to you is registering
`Category` on `IApplicationDbContext`/`ApplicationDbContext` and adding an EF configuration/migration.

By default `crud` naively pluralizes the entity name by appending `s` (`Category` → `Categorys`) — pass
`--plural Categories` to fix irregular plurals, as in the examples above.

### Options

| Option | Applies to | Description |
|---|---|---|
| `-f, --feature <FeatureFolder>` | all | Feature folder name, e.g. `TodoLists` (required) |
| `-e, --entity <EntityName>` | all | Entity the feature operates on (required) |
| `-n, --name <QueryName\|CommandName>` | `query`, `command` | Name of the query/command, e.g. `GetTodoListById` (required) |
| `--properties <spec>` | `crud` | Comma-separated `Name:Type` pairs for the entity, e.g. `"Title:string,Notes:string?"`. Omit to auto-detect from the entity's own class file instead |
| `--entity-project <Name>` | `crud` | Project to search for `<EntityName>.cs` when auto-detecting (default: `Domain`) |
| `--entity-path <path>` | `crud` | Explicit path to the entity's `.cs` file, skips entity-file discovery |
| `--plural <Name>` | `crud` | DbSet/query name for the entity (default: `<EntityName>s`) |
| `--key-type <Type>` | `crud` | Type of the entity's `Id` (default: `int`) |
| `-p, --project <Name>` | all | `.csproj` to scaffold into, matched by file name (default: `Application`) |
| `--project-path <path>` | all | Explicit path to the target `.csproj`, skips project discovery |
| `--dbcontext <TypeName>` | all | DbContext interface type used by the handler (default: `IApplicationDbContext`) |
| `--dbcontext-namespace <ns>` | all | Namespace of that interface (default: `<RootNamespace>.Common.Interfaces`) |
| `--api-project <Name>` | `crud` | `.csproj` to generate the controller into, matched by file name (default: `Api`) |
| `--api-project-path <path>` | `crud` | Explicit path to the target API `.csproj`, skips project discovery |
| `--no-controller` | `crud` | Skip generating the `{Plural}Controller.cs` |
| `--force` | all | Overwrite the target folder(s)/controller file if they already exist |

Run `dotnet run --project tools/FeatureCli -- --help` (or `feature --help` once installed) for the same
reference at any time.

### Installing FeatureCli as a global tool

FeatureCli isn't published to nuget.org — it's packed and installed from a local package feed
(`nuget-local-feed/`, git-ignored, at the repo root).

**1. Pack it:**

```bash
dotnet pack tools/FeatureCli/FeatureCli.csproj -c Release -o nuget-local-feed
```

**2. Install it globally:**

```bash
dotnet tool install --global --add-source nuget-local-feed FeatureCli
```

This puts a `feature` command on your `PATH` (via `~/.dotnet/tools`), usable from any directory. Verify with:

```bash
feature --help
```

**Updating** after a change to `tools/FeatureCli` — bump `<Version>` in `FeatureCli.csproj`, then:

```bash
dotnet pack tools/FeatureCli/FeatureCli.csproj -c Release -o nuget-local-feed
dotnet tool update --global --add-source nuget-local-feed FeatureCli
```

**Uninstalling:**

```bash
dotnet tool uninstall --global FeatureCli
```

If you'd rather not touch global tools, install it as a **local tool** instead, scoped to a
`.config/dotnet-tools.json` manifest in this repo:

```bash
dotnet new tool-manifest   # only if the repo has no manifest yet
dotnet tool install --add-source nuget-local-feed FeatureCli
dotnet feature --help      # local tools are invoked via `dotnet <command>`
```
