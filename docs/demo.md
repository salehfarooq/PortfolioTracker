# Reviewer Demo

Use this guide when showing the project to a recruiter, interviewer, or engineering manager. The fastest path is the non-interactive demo mode, followed by a short explanation of how EF and stored-procedure backends share the same business rules.

## One-Time Setup

```bash
cp .env.example .env
# Edit .env and set PORTFOLIO_DB_PASSWORD to a strong local password.
docker compose up -d
set -a
source .env
set +a
./scripts/setup-db.sh
```

## Build

```bash
dotnet build PortfolioSolution.sln
```

## Run The EF Demo

```bash
dotnet run --project src/ConsoleApp -- --backend ef --demo --no-seed
```

## Run The Stored-Procedure Demo

```bash
dotnet run --project src/ConsoleApp -- --backend sp --demo --no-seed
```

Both commands should print the same account-level story because they use the same database and shared calculation helpers.

## Example Output Shape

Values may vary if you change the demo data, but a fresh `db/PortfolioDB.sql` setup should look similar:

```text
====================
  Portfolio Demo
====================

Backend: EF | Connection: Data Source=localhost,1433;Initial Catalog=PortfolioDB;User ID=sa;Password=*****
--------------------------------------------------------------------------------
Selected Account     : Growth Portfolio (demo_investor1)

Account Overview
----------------
Security Value       : 38,842.00
Cash Balance         : 63,736.00
Portfolio Value      : 102,578.00
Unrealized P/L       : 2,502.00
Realized P/L         : 80.00
Total Return         : 2.58%

Top Holdings
------------
+--------+-------+--------+--------------+------------+
| Ticker | Qty   | Price  | Market Value | Unrealized |
+--------+-------+--------+--------------+------------+
| MSFT   | 40.00 | 428.00 | 17,120.00    | 720.00     |
| NVDA   | 12.00 | 906.00 | 10,872.00    | 432.00     |
| AAPL   | 50.00 | 217.00 | 10,850.00    | 1,350.00   |
+--------+-------+--------+--------------+------------+
```

## What To Point Out

- The app can swap EF and SP backends with `--backend ef` or `--backend sp`.
- The CLI validates the schema before constructing the backend.
- The password is masked in output and supplied through environment variables or a prompt.
- The SQL script creates repeatable demo data with no CSV dependency.
- Orders are validated in `ApplicationCore`, written transactionally, and then database triggers update holdings and cash.
- Unit tests cover the finance calculations and CLI parsing; the optional integration test checks backend equivalence against SQL Server.

## Interactive Walkthrough

After the demo mode, run:

```bash
dotnet run --project src/ConsoleApp
```

Suggested path:

1. Select backend `EF / LINQ`.
2. Select account `Growth Portfolio`.
3. Open `Portfolio snapshot`.
4. Open `Recent trades`.
5. Open `Security return series` and use Security ID `1`.
6. Switch backend to stored procedures.
7. Repeat the snapshot and trades views.

This makes the architecture tangible: two implementations, one contract, one database, consistent output.

## Quality Commands To Show

```bash
dotnet test PortfolioSolution.sln
dotnet format PortfolioSolution.sln --verify-no-changes --verbosity minimal
dotnet list PortfolioSolution.sln package --vulnerable
```

## Troubleshooting

If the app cannot connect, check:

- Docker is running.
- `docker compose ps` shows `portfolio-sql`.
- `.env` is loaded into the current shell before running `setup-db.sh` or the app.
- `PORTFOLIO_DB_PASSWORD` matches the password used when the SQL container was created.
- If you changed the password after the volume already existed, run `docker compose down -v` and initialize again.
