# Portfolio Console Manager

A database-backed .NET portfolio management system built to demonstrate backend engineering, SQL Server design, and clean application architecture.

Portfolio Console Manager models brokerage-style workflows such as accounts, securities, holdings, orders, trades, cash activity, portfolio snapshots, realized profit/loss, return series, and volatility. The project is intentionally console-first: instead of relying on a decorative UI, it highlights the engineering decisions that matter in backend and database roles.

## Project Snapshot


| --- | --- |
| Backend architecture | Layered .NET solution with separate core, infrastructure, and console composition projects. |
| SQL Server design | Normalized schema, indexes, views, triggers, stored procedures, deterministic seed data. |
| Data-access strategy | Two interchangeable backends: Entity Framework Core and ADO.NET/stored procedures. |
| Business logic | Shared finance calculations for realized P/L, contributions, returns, volatility, and validation. |
| Configuration | Environment-driven connection handling with no hardcoded credentials. |
| Developer quality | Dockerized database setup, automated tests, formatting checks, and package vulnerability audit. |



Demonstrates how the same domain workflows can be implemented through two different persistence strategies while keeping business behavior consistent.

- EF Core backend for expressive LINQ queries and object-oriented data access.
- Stored-procedure backend for SQL-first workflows, typed parameters, and explicit transaction control.
- Shared application contracts so the console can switch between backends without changing user workflows.
- SQL triggers that keep holdings and cash ledger state consistent after trades.
- Core calculation helpers that make financial behavior testable outside the database.
- Reproducible SQL Server demo environment, so the project can be reviewed without private files or local machine assumptions.

## Core Features

- Account and user management.
- Active security lookup with paged console tables.
- Account holdings with market value and unrealized P/L.
- Portfolio snapshots with cash, securities, realized P/L, net contribution, and total return.
- Recent trades and cash ledger activity.
- Buy/sell order placement with validation before database mutation.
- Security return-series preview and volatility calculation.
- Backend switching between EF and stored procedures.
- Non-interactive demo mode for quick project review.

## Architecture At A Glance

```text
ConsoleApp
  CLI parsing, masked prompts, demo mode, startup composition

ApplicationCore
  DTOs, service contracts, validation, finance calculations

Infrastructure.EF
  EF Core DbContext and EF service implementations

Infrastructure.SP
  ADO.NET, typed SQL parameters, stored-procedure service implementations

SQL Server
  Tables, views, indexes, triggers, procedures, deterministic demo data
```

The console project is the composition root. It parses options, resolves configuration, checks database health, and explicitly constructs either the EF backend or the stored-procedure backend. Reflection-based startup was removed so dependencies are visible and easy to reason about.

More detail: [Architecture](docs/architecture.md)

## Database Design Highlights

The SQL Server database includes:

- `Users`, `Accounts`, `Securities`, `PriceHistory`, `Holdings`, `Orders`, `Trades`, and `CashLedger`.
- Indexed valuation views for latest prices and account holding values.
- Stored procedures for portfolio snapshots and security return series.
- Triggers that update holdings and cash movements when trades are inserted.
- Deterministic demo data using fixed securities, dates, prices, deposits, orders, and trades.

The stored-procedure backend uses typed `SqlParameter` helpers instead of `AddWithValue`, and multi-step writes use transactions.

More detail: [Database Design](docs/database.md)

## Quality And Testing

The project includes automated tests for:

- Realized P/L and invested-capital calculations.
- External contribution classification.
- Return-series and volatility behavior.
- Account and order validation edge cases.
- CLI parsing and option precedence.
- Password masking and width-aware table truncation.
- Optional SQL Server integration equivalence between EF and stored-procedure backends.

Verified quality gates:

```bash
dotnet build PortfolioSolution.sln
dotnet test PortfolioSolution.sln
dotnet format PortfolioSolution.sln --verify-no-changes --verbosity minimal
dotnet list PortfolioSolution.sln package --vulnerable
```

## Technical Stack

- .NET 10
- C#
- SQL Server 2022
- Entity Framework Core
- ADO.NET / Microsoft.Data.SqlClient
- xUnit
- Docker Compose
- Mermaid documentation diagrams

The SDK is pinned with `global.json` to make the project reproducible on machines using .NET SDK `10.0.100`.

## Quick Demo for evaluation


```bash
cp .env.example .env
# Edit .env and set PORTFOLIO_DB_PASSWORD to a strong local password.

docker compose up -d

set -a
source .env
set +a

./scripts/setup-db.sh

dotnet run --project src/ConsoleApp -- --backend ef --demo --no-seed
dotnet run --project src/ConsoleApp -- --backend sp --demo --no-seed
```

Both demo commands show the same portfolio story through different backend implementations.

Full walkthrough: [Reviewer Demo](docs/demo.md)

## Repository Map

```text
src/ApplicationCore       Business contracts, DTOs, validation, calculations
src/Infrastructure.EF     Entity Framework implementation
src/Infrastructure.SP     Stored-procedure / ADO.NET implementation
src/ConsoleApp            CLI application and composition root
tests                     Unit tests and optional SQL integration tests
db/PortfolioDB.sql        Complete SQL Server setup script
docs                      Architecture, database, demo, and interview notes
```


