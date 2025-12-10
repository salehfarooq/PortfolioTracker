## Portfolio Console App

A menu-driven console app for managing portfolios against the existing `PortfolioDB` SQL Server database. Works with either Entity Framework (EF) or Stored Procedures (SP/ADO.NET) backends without changing the schema.

### Prerequisites
- SQL Server reachable at `localhost:1433` with `PortfolioDB` already created (per `db/Group45_p2.sql`).
- .NET SDK (net10.0 used here).
- Connection string defaults to `Server=localhost,1433;Database=PortfolioDB;User Id=sa;Password=Muhammadsaleh1@;TrustServerCertificate=True;` — override with env `PORTFOLIO_DB_CONNECTION` if needed.

### Quick start
```bash
dotnet build PortfolioSolution.sln
dotnet run --project src/ConsoleApp
```
- At startup, pick backend (EF or SP), optionally adjust connection string, then Continue.
- On first run (when no accounts exist), the app seeds a couple of demo accounts and buys a handful of securities to create sample portfolios (best effort; skipped if accounts already exist).

### Navigation (menu-first)
Main Menu options (all numbered; press Enter after each action to return):
1. **Select account** – choose an active account to work with.
2. **Add account** – create a new user + account (prompts for username/full name/email/account name/type). The new account is auto-selected.
3. **View securities** – choose “Active only” or “All”; non-synthetic tickers appear first; paged list with Previous/Next/Change page size.
4. **View holdings** – shows ticker, qty, avg cost, price, market value, unrealized P/L with totals.
5. **Portfolio snapshot** – shows as-of date, totals (market value, unrealized/realized P/L, return %), plus top 5 holdings.
6. **Recent trades** – enter how many; shows date, ticker, side, qty, price.
7. **Recent cash activity** – enter how many; shows date, type, amount, reference.
8. **Top assets** – pick metric (MarketValue/UnrealizedPL/ReturnPct), enter top N; shows ranked holdings.
9. **Security return series** – enter Security ID and optional date range; shows a preview of returns.
10. **Place order** – buy/sell with Security ID, quantity, price. Sells are pre-validated against current holdings to avoid DB constraint errors.
11. **Switch backend** – reopen startup menu to switch between EF and SP.
12. **Exit** – quit the app.

### Backend selection
- Startup and main menu both let you switch between EF and SP.
- EF path uses `Infrastructure.EF` DbContext; SP path uses `Infrastructure.SP` ADO.NET with stored procedures.

### Seeding behavior
- If no accounts exist, the app tries to create two demo accounts and place a few BUY orders across available securities (prioritizes non-synthetic tickers). Failures are ignored so seeding never blocks you.

### Database setup (SQL Server + PortfolioDB)

#### Windows (SQL Server Developer/Express)
1. Install SQL Server Developer or Express (and SQL Server Management Studio if you want a GUI). During setup, enable mixed-mode auth and set the `sa` password (defaults in this project: `Muhammadsaleh1@`). Ensure TCP/IP is enabled.
2. Open SQL Server Configuration Manager → SQL Server Network Configuration → enable TCP/IP → restart the SQL Server service.
3. Ensure SQL Browser is running (for Express named instances) or use a fixed port. This project assumes `localhost,1433`.
4. Create/restore the database:
   - Launch `sqlcmd` or SSMS.
   - Run the script from the repo root:
     ```bash
     sqlcmd -S localhost,1433 -U sa -P "Muhammadsaleh1@" -i db/Group45_p2.sql
     ```
   - Verify `PortfolioDB` exists and tables/views/procs/functions are created.
5. If you changed the port/credentials, set `PORTFOLIO_DB_CONNECTION` accordingly before running the app.

#### macOS (SQL Server in Docker)
1. Install Docker Desktop.
2. Start a SQL Server container (Developer edition):
   ```bash
   docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Muhammadsaleh1@" \
     -p 1433:1433 --name sql1 -d mcr.microsoft.com/mssql/server:2022-latest
   ```
3. Wait for the container to become ready (`docker logs sql1`).
4. Install sqlcmd (e.g., `brew install mssql-tools` or use the container’s sqlcmd).
5. From the repo root, apply the schema:
   ```bash
   /opt/mssql-tools/bin/sqlcmd -S localhost,1433 -U sa -P "Muhammadsaleh1@" -i db/Group45_p2.sql
   ```
   (Adjust the sqlcmd path if installed elsewhere.)
6. Confirm `PortfolioDB` exists:
   ```bash
   /opt/mssql-tools/bin/sqlcmd -S localhost,1433 -U sa -P "Muhammadsaleh1@" -Q "SELECT name FROM sys.databases WHERE name='PortfolioDB';"
   ```
7. If you change port/password, update `PORTFOLIO_DB_CONNECTION` or the default connection string in the app.

#### Connection string reminder
- Default: `Server=localhost,1433;Database=PortfolioDB;User Id=sa;Password=Muhammadsaleh1@;TrustServerCertificate=True;`
- Override via environment variable `PORTFOLIO_DB_CONNECTION` or edit at app startup.

### Tips & gotchas
- Ensure SQL Server is running and reachable on the given port; otherwise data menus will show errors.
- Security IDs come from the securities list; use “View securities” to find IDs before placing orders.
- Sells are blocked if you try to sell more than you hold (friendly warning shown).
- Connection string masking hides the password in the UI; full string can be set via env var if needed.
