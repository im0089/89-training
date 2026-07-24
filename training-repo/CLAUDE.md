# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**OrderHub** — a small internal order-management web app used as a training codebase. Server-rendered ASP.NET Core MVC (Razor views + Bootstrap + jQuery), single SQL Server database, single deployment. It manages Customers, Products, and Orders for internal staff — there is no authentication, no public API, and no multi-tenancy.

Scale is deliberately modest (seed data: 20 customers, 50 products, 200 orders). Do **not** introduce microservices, message queues, CQRS/MediatR, DTO auto-mapping libraries, or other heavyweight patterns — they are out of proportion to this system. Prefer plain classes and the existing patterns below.

All user-facing text, validation messages, and code comments are in **Traditional Chinese (zh-TW)**. Keep new messages in Traditional Chinese to match.

## Tech stack and versions

- **.NET 8.0** (`net8.0`) — every project targets this. The machine also has the .NET 10 SDK installed, but there is no `global.json`, so builds resolve to the newest SDK while still compiling against net8.0. **Do not bump `<TargetFramework>` to net10** or use net10-only APIs; keep code on the .NET 8 surface.
- **ASP.NET Core MVC** (`Microsoft.NET.Sdk.Web`)
- **Entity Framework Core 8.0.11** — `Microsoft.EntityFrameworkCore.SqlServer` (runtime), `.Design` (Web, for migrations), `.InMemory` (tests)
- **xUnit 2.5.3** + `Microsoft.NET.Test.Sdk` 17.8.0 + coverlet — test framework
- `Nullable` and `ImplicitUsings` are enabled in all projects.

## Common commands

```bash
# Build
dotnet build OrderHub.sln

# Run the web app (auto-applies EF migrations + seeds the DB on startup — see Program.cs)
dotnet run --project src/OrderHub.Web        # http://localhost:5150 / https://localhost:7147

# Run all tests
dotnet test

# Run a single test class or method (filter by fully-qualified name)
dotnet test --filter "FullyQualifiedName~OrderServicePricingTests"
dotnet test --filter "FullyQualifiedName~OrderServicePricingTests.CalculateTotal_AppliesTierDiscountOnSubtotal"

# EF Core migrations (Infrastructure holds the model; Web is the startup project)
dotnet ef migrations add <Name> --project src/OrderHub.Infrastructure --startup-project src/OrderHub.Web
dotnet ef database update --project src/OrderHub.Infrastructure --startup-project src/OrderHub.Web
```

Running the web app requires a reachable SQL Server matching the `Default` connection string in `src/OrderHub.Web/appsettings.json` (`Server=localhost`, Trusted Connection). Tests do **not** need SQL Server — they use EF Core InMemory.

## Architecture and layering

Three source projects plus tests, in a Core / Infrastructure / Web layering. Dependency direction is strict: **Web → Infrastructure → Core**, and Core depends on nothing.

- **`src/OrderHub.Core`** — domain and business logic, no infrastructure dependencies.
  - `Domain/` — POCO entities (`Order`, `OrderItem`, `Customer`, `Product`) and enums (`OrderStatus`, `CustomerTier`).
  - `Interfaces/` — repository abstractions (`IOrderRepository`, etc.), implemented in Infrastructure.
  - `Services/` — business services **and their interfaces live together here** (`IOrderService` + `OrderService`). This is where the real logic lives; controllers stay thin.
  - `Common/` — `ServiceResult<T>` and `PagedResult<T>` (see conventions below).
- **`src/OrderHub.Infrastructure`** — EF Core persistence.
  - `Data/OrderHubDbContext.cs` — all entity configuration is done in `OnModelCreating` (fluent API), not data annotations.
  - `Data/DbSeeder.cs` — idempotent dev seed (no-ops if `Customers` already has rows); uses a fixed random seed for reproducibility.
  - `Repositories/` — repository implementations; `Include`/`ThenInclude` eager-loading lives here.
  - `Migrations/` — generated EF migrations.
- **`src/OrderHub.Web`** — MVC front end. `Controllers/` are thin and delegate to Core services; `ViewModels/` are per-view shapes populated in the controller; `Views/` are Razor + Bootstrap; `Helpers/DisplayHelper.cs` centralizes display formatting (status/tier labels, badge classes, money, local time).
- **`tests/OrderHub.Tests`** — xUnit. `TestSetup.cs` is the shared factory: `CreateContext()` (fresh InMemory DB per test via a GUID name), `CreateOrderService`/`CreateProductService`, and `AddCustomer`/`AddProduct` builders.

Dependency injection is wired in `src/OrderHub.Web/Program.cs` — repositories and services are all registered `Scoped`. Register new services/repositories there.

## Conventions

- **C# style** (enforced via `.editorconfig`): file-scoped namespaces, `var` when the type is apparent, `System` usings sorted first, 4-space indent for `.cs`/`.cshtml`, 2-space for json/js/css.
- **Service return type**: business operations that can fail return `ServiceResult<T>` (`ServiceResult<T>.Ok(value)` / `.Fail("訊息")` / `.Fail(errors)`), never throw for expected validation failures. Controllers translate failures into `ModelState`/`TempData`. Multiple error strings are joined with `；` by `ErrorMessage`.
- **Validation** happens in the service layer (e.g. `OrderService.CreateOrderAsync` checks customer existence, empty lines, non-positive quantity, duplicate products, stock, active status). MVC model binding + `[ValidateAntiForgeryToken]` handles form-shape validation in controllers.
- **Pagination**: return `PagedResult<T>` from repositories.

## Money handling (read before touching pricing)

- All monetary values are `decimal`. EF maps them with `HasPrecision(18, 2)` in `OrderHubDbContext`; rounding uses `Math.Round(value, 2)`.
- **Price snapshotting**: `OrderItem.UnitPriceSnapshot` captures the price at order-creation time. Order totals are computed from the snapshot, not the product's current `UnitPrice`.
- **Tier discounts** are defined once in `OrderService.GetDiscountRate`: Gold 10%, Silver 5%, Standard 0%. `DisplayHelper.Money` formats as `NT$ {amount:N2}`.
- The pricing tests in `OrderServicePricingTests.cs` are the source of truth for expected totals/discounts. If you change any pricing, rounding, or discount logic, run them and make the intended behavior match the tests (update tests deliberately, not incidentally).

## Files to touch carefully

- **`src/OrderHub.Infrastructure/Migrations/*` and `OrderHubDbContextModelSnapshot.cs`** — generated. Don't hand-edit; change the model/`OnModelCreating` and add a migration instead. Editing the snapshot by hand desyncs it from the DB.
- **`src/OrderHub.Web/Program.cs`** — calls `db.Database.Migrate()` and `DbSeeder.SeedAsync` on every startup. Be careful changing startup order or the migrate/seed block.
- **`src/OrderHub.Web/appsettings.json`** — DB connection string.
- **`OrderHubDbContext.OnModelCreating`** — precision, string lengths, the unique `Sku` index, and delete behaviors live here; changing them has schema consequences (new migration required).

## Don'ts

- Don't add NuGet packages or new project dependencies without asking first — the dependency set is intentionally minimal.
- Don't change the target framework or adopt .NET 10-only APIs.
- Don't hand-edit migrations or the model snapshot; regenerate via `dotnet ef`.
- Don't move business logic into controllers or repositories — it belongs in Core `Services`.
- Don't switch user-facing text to another language; keep it Traditional Chinese.
- Don't alter pricing/rounding/discount behavior without running `OrderServicePricingTests`.
