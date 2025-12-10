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

## Ultimate “Just Make It Work” Guide (integrated)

This app is a console-based portfolio manager that needs:

- .NET SDK (to build/run)
- SQL Server with the PortfolioDB schema (from `db/Group45_p2.sql`)

Follow these steps exactly.

### 1) Install prerequisites

#### Windows
1. Install .NET SDK (latest):
   - Download from https://dotnet.microsoft.com/download (choose SDK, not runtime).
   - After install, open Command Prompt and run `dotnet --info` to verify.
2. Install SQL Server (Developer or Express):
   - Download SQL Server from Microsoft (Developer edition preferred).
   - Run installer → choose Mixed Mode auth → set `sa` password to `Muhammadsaleh1@` (or your own; remember it).
   - Finish install.
3. Enable TCP/IP on port 1433:
   - Open “SQL Server Configuration Manager” → SQL Server Network Configuration → enable “TCP/IP”.
   - Right-click TCP/IP → Properties → IP Addresses → set TCP Port to `1433` (at least in IPAll).
   - Restart the SQL Server service.
4. (Optional) Install SQL Server Management Studio (SSMS) for GUI queries.

#### macOS (using Docker)
1. Install Docker Desktop and start it.
2. Run SQL Server in a container:
   ```bash
   docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Muhammadsaleh1@" \
     -p 1433:1433 --name sql1 -d mcr.microsoft.com/mssql/server:2022-latest
   ```
   If the container already exists, start it with `docker start sql1`.
3. Install sqlcmd tooling (Homebrew):
   ```bash
   brew install mssql-tools
   ```
   (If sqlcmd ends up elsewhere, adjust the path below.)

### 2) Get the code
- With Git:
  ```bash
  git clone <your-repo-url> portfolio-app
  cd portfolio-app
  ```
- Or download ZIP, extract, and open a terminal in the folder containing `PortfolioSolution.sln`.

### 3) Create the PortfolioDB from the script
Run these commands in the repo root (folder with `db/Group45_p2.sql`).

#### Windows
```bash
sqlcmd -S localhost,1433 -U sa -P "Muhammadsaleh1@" -i db/Group45_p2.sql
```
If you set a different `sa` password, replace it above.

Verify (optional):
```bash
sqlcmd -S localhost,1433 -U sa -P "YourPassword" -Q "SELECT name FROM sys.databases WHERE name='PortfolioDB';"
```

#### macOS
```bash
/opt/mssql-tools/bin/sqlcmd -S localhost,1433 -U sa -P "Muhammadsaleh1@" -i db/Group45_p2.sql
```
Adjust the sqlcmd path if needed. Verify (optional):
```bash
/opt/mssql-tools/bin/sqlcmd -S localhost,1433 -U sa -P "Muhammadsaleh1@" -Q "SELECT name FROM sys.databases WHERE name='PortfolioDB';"
```

### 4) Connection string (defaults)
The app assumes:
```
Server=localhost,1433;Database=PortfolioDB;User Id=sa;Password=Muhammadsaleh1@;TrustServerCertificate=True;
```
If you changed port/host/password, set an env var before running:

- macOS/Linux:
  ```bash
  export PORTFOLIO_DB_CONNECTION="Server=localhost,1433;Database=PortfolioDB;User Id=sa;Password=YourPassword;TrustServerCertificate=True;"
  ```
- Windows PowerShell:
  ```powershell
  $env:PORTFOLIO_DB_CONNECTION="Server=localhost,1433;Database=PortfolioDB;User Id=sa;Password=YourPassword;TrustServerCertificate=True;"
  ```

### 5) Build and run the app
From the repo root:
```bash
dotnet build PortfolioSolution.sln
dotnet run --project src/ConsoleApp
```

### 6) Using the console app (step by step)
**Startup screen:**
- Choose backend: EF (Entity Framework/LINQ) or Stored Procedures (ADO.NET). You can switch later.
- Review/confirm connection string (password is masked). If it’s wrong, edit it here.
- Continue. If the DB has zero accounts, the app seeds two demo accounts with sample holdings automatically.

**Main menu (all numbered; press Enter after each action):**
1. Select account: Lists active accounts. Type the number to select one. Selection is required for holdings/snapshot/trades/cash/top assets/returns/orders.
2. Add account: Prompts for username, full name, email, account name, account type. Creates a user + account and auto-selects it.
3. View securities: Choose scope: Active-only or All. Non-synthetic tickers show first. Paging is built-in: use Next/Previous and Change page size; a “Synthetic” column indicates generated tickers.
4. View holdings: Shows ticker, qty, avg cost, price, market value, unrealized P/L. Totals at the bottom. Requires a selected account.
5. Portfolio snapshot: Shows as-of date, total market value, unrealized P/L, realized P/L, total return %, plus top 5 holdings. Requires a selected account.
6. Recent trades: Enter how many rows to show. Displays date, ticker, side, qty, price. Requires a selected account.
7. Recent cash activity: Enter how many rows to show. Displays date, type, amount, reference. Requires a selected account.
8. Top assets: Choose metric (MarketValue, UnrealizedPL, ReturnPct), enter top N; shows ranked holdings. Requires a selected account.
9. Security return series: Enter Security ID and optional start/end dates. Shows a preview of returns (date, close, daily %, cumulative %). Use “View securities” to find IDs. Requires a selected account.
10. Place order: Choose Buy or Sell, enter Security ID, quantity, price. SELLs are pre-validated against your current holdings; if you try to sell more than you hold, you get a friendly warning and nothing is sent to the DB. Requires a selected account.
11. Switch backend: Reopen the startup menu and switch between EF and SP without restarting the app.
12. Exit: Quit the app.

**General tips:**
- Always “Select account” after adding or switching backend to be sure you’re acting on the right account.
- Use “View securities” to find Security IDs before placing orders.
- SELL is blocked if you exceed available quantity (pre-check in both EF and SP).
- You can switch EF/SP anytime; both paths hit the same database.

### 7) Common issues and fixes
- Cannot connect: Ensure SQL Server is running, port 1433 open, password correct, and `PORTFOLIO_DB_CONNECTION` matches your setup.
- Login failed for sa: Reset or change sa password and update the connection string/env var.
- `dotnet` not found: Reinstall .NET SDK; verify with `dotnet --info`.
- macOS `sqlcmd` not found: Check `/opt/mssql-tools/bin` or reinstall `mssql-tools`.
- Docker container not running: Start it with `docker start sql1`.

### Tips & gotchas
- Ensure SQL Server is running and reachable on the given port; otherwise data menus will show errors.
- Security IDs come from the securities list; use “View securities” to find IDs before placing orders.
- Sells are blocked if you try to sell more than you hold (friendly warning shown).
- Connection string masking hides the password in the UI; full string can be set via env var if needed.
