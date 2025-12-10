------------------------------------------------------------
-- Phase 2 - Group 45
-- PortfolioDB: Database creation + core tables
------------------------------------------------------------

IF DB_ID('PortfolioDB') IS NOT NULL
BEGIN
    ALTER DATABASE PortfolioDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE PortfolioDB;
END;
GO

CREATE DATABASE PortfolioDB;
GO

USE PortfolioDB;
GO

------------------------------------------------------------
-- Users
------------------------------------------------------------

CREATE TABLE Users (
    UserID       INT IDENTITY(1,1) PRIMARY KEY,
    Username     NVARCHAR(64)   NOT NULL UNIQUE,
    PasswordHash VARBINARY(256) NOT NULL,
    PasswordSalt VARBINARY(128) NOT NULL,
    FullName     NVARCHAR(128)  NOT NULL,
    Email        NVARCHAR(128)  NOT NULL UNIQUE,
    Role         NVARCHAR(16)   NOT NULL, -- 'Admin', 'User'
    JoinDate     DATE           NOT NULL DEFAULT (CAST(GETDATE() AS DATE)),
    CreatedAt    DATETIME2(3)   NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedAt   DATETIME2(3)   NULL
);
GO

------------------------------------------------------------
-- Securities 
------------------------------------------------------------

CREATE TABLE Securities (
    SecurityID   INT           NOT NULL PRIMARY KEY,
    Ticker       NVARCHAR(16)  NOT NULL UNIQUE,
    CompanyName  NVARCHAR(256) NOT NULL,
    Sector       NVARCHAR(128) NULL,
    ListedIn     NVARCHAR(64)  NULL,
    IsActive     BIT           NOT NULL DEFAULT (1)
);
GO

------------------------------------------------------------
-- Accounts 
------------------------------------------------------------

CREATE TABLE Accounts (
    AccountID    INT IDENTITY(1,1) PRIMARY KEY,
    UserID       INT          NOT NULL,
    AccountType  NVARCHAR(32) NOT NULL,  -- 'Demo', 'Individual', etc.
    AccountName  NVARCHAR(64) NOT NULL,
    CreatedDate  DATE         NOT NULL DEFAULT (CAST(GETDATE() AS DATE)),
    IsActive     BIT          NOT NULL DEFAULT (1),

    CONSTRAINT FK_Accounts_Users
        FOREIGN KEY (UserID) REFERENCES Users(UserID)
);
GO

CREATE NONCLUSTERED INDEX IX_Accounts_UserID
    ON Accounts(UserID);
GO


------------------------------------------------------------
-- PriceHistory
------------------------------------------------------------

CREATE TABLE PriceHistory (
    SecurityID  INT           NOT NULL,
    PriceDate   DATE          NOT NULL,
    OpenPrice   DECIMAL(19,4) NULL,
    HighPrice   DECIMAL(19,4) NULL,
    LowPrice    DECIMAL(19,4) NULL,
    ClosePrice  DECIMAL(19,4) NOT NULL,
    Volume      BIGINT        NULL,
    ChangePct   DECIMAL(9,4)  NULL,

    CONSTRAINT PK_PriceHistory
        PRIMARY KEY CLUSTERED (SecurityID, PriceDate),

    CONSTRAINT FK_PriceHistory_Securities
        FOREIGN KEY (SecurityID) REFERENCES Securities(SecurityID),

    CONSTRAINT CK_PriceHistory_ClosePrice_Positive
        CHECK (ClosePrice > 0)
);
GO


------------------------------------------------------------
-- Holdings
------------------------------------------------------------

CREATE TABLE Holdings (
    HoldingID   INT IDENTITY(1,1) PRIMARY KEY,
    AccountID   INT           NOT NULL,
    SecurityID  INT           NOT NULL,
    Quantity    DECIMAL(19,6) NOT NULL DEFAULT (0),
    AvgCost     DECIMAL(19,4) NOT NULL DEFAULT (0),

    CONSTRAINT FK_Holdings_Accounts
        FOREIGN KEY (AccountID)  REFERENCES Accounts(AccountID),

    CONSTRAINT FK_Holdings_Securities
        FOREIGN KEY (SecurityID) REFERENCES Securities(SecurityID),

    CONSTRAINT UQ_Holdings_Account_Security
        UNIQUE (AccountID, SecurityID),

    CONSTRAINT CK_Holdings_Quantity_NonNegative
        CHECK (Quantity >= 0)
);
GO

CREATE NONCLUSTERED INDEX IX_Holdings_Account_Security
    ON Holdings(AccountID, SecurityID)
    INCLUDE (Quantity, AvgCost);
GO


------------------------------------------------------------
-- Orders 
------------------------------------------------------------

CREATE TABLE Orders (
    OrderID     INT IDENTITY(1,1) PRIMARY KEY,
    AccountID   INT           NOT NULL,
    SecurityID  INT           NOT NULL,
    OrderType   CHAR(4)       NOT NULL,  -- 'BUY' or 'SELL'
    Quantity    DECIMAL(19,6) NOT NULL,
    Price       DECIMAL(19,4) NOT NULL,
    Status      NVARCHAR(16)  NOT NULL,  -- 'New', 'Filled', 'Partial', 'Cancelled'
    OrderDate   DATETIME2(3)  NOT NULL DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT FK_Orders_Accounts
        FOREIGN KEY (AccountID)  REFERENCES Accounts(AccountID),

    CONSTRAINT FK_Orders_Securities
        FOREIGN KEY (SecurityID) REFERENCES Securities(SecurityID),

    CONSTRAINT CK_Orders_Quantity_Positive
        CHECK (Quantity > 0),

    CONSTRAINT CK_Orders_Price_NonNegative
        CHECK (Price >= 0)
);
GO

CREATE NONCLUSTERED INDEX IX_Orders_Account_OrderDate
    ON Orders(AccountID, OrderDate)
    INCLUDE (SecurityID, Status, Quantity, Price);
GO


------------------------------------------------------------
-- Trades (execution-level; drives Holdings and P/L)
------------------------------------------------------------

CREATE TABLE Trades (
    TradeID     INT IDENTITY(1,1) PRIMARY KEY,
    OrderID     INT           NOT NULL,
    TradeDate   DATETIME2(3)  NOT NULL,
    Quantity    DECIMAL(19,6) NOT NULL,
    Price       DECIMAL(19,4) NOT NULL,
    Amount      DECIMAL(19,4) NOT NULL,  -- Quantity * Price +/- Fees
    Fees        DECIMAL(19,4) NOT NULL DEFAULT (0),

    CONSTRAINT FK_Trades_Orders
        FOREIGN KEY (OrderID) REFERENCES Orders(OrderID),

    CONSTRAINT CK_Trades_Quantity_Positive
        CHECK (Quantity > 0),

    CONSTRAINT CK_Trades_Price_NonNegative
        CHECK (Price >= 0)
);
GO

CREATE NONCLUSTERED INDEX IX_Trades_Order_TradeDate
    ON Trades(OrderID, TradeDate)
    INCLUDE (Quantity, Price, Amount);
GO


------------------------------------------------------------
-- CashLedger (cash movements per account)
------------------------------------------------------------

CREATE TABLE CashLedger (
    LedgerID    INT IDENTITY(1,1) PRIMARY KEY,
    AccountID   INT           NOT NULL,
    TxnDate     DATETIME2(3)  NOT NULL,
    Amount      DECIMAL(19,4) NOT NULL,   -- +ve inflow, -ve outflow
    Type        NVARCHAR(16)  NOT NULL,   -- 'DEPOSIT','WITHDRAWAL','DIVIDEND','FEES',...
    Reference   NVARCHAR(64)  NULL,       -- e.g. 'Trade:123', 'Order:45'

    CONSTRAINT FK_CashLedger_Accounts
        FOREIGN KEY (AccountID) REFERENCES Accounts(AccountID)
);
GO

CREATE NONCLUSTERED INDEX IX_CashLedger_Account_TxnDate
    ON CashLedger(AccountID, TxnDate)
    INCLUDE (Amount, Type);
GO

------------------------------------------------------------
-- ComplianceRules (basic portfolio/account rules)
------------------------------------------------------------

CREATE TABLE ComplianceRules (
    RuleID      INT IDENTITY(1,1) PRIMARY KEY,
    RuleType    NVARCHAR(64)  NOT NULL,  -- e.g. 'MaxPositionSizePct'
    LimitValue  DECIMAL(19,4) NOT NULL,
    Description NVARCHAR(256) NULL
);
GO

USE PortfolioDB;
GO

SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE';
GO


CREATE TABLE Securities_Staging (
    SecurityID   INT,
    Ticker       NVARCHAR(16),
    CompanyName  NVARCHAR(256),
    Sector       NVARCHAR(128),
    ListedIn     NVARCHAR(64),
    IsActiveRaw  NVARCHAR(32)  -- keep as text for now
);
GO

BULK INSERT Securities_Staging
FROM '/var/opt/mssql/import/Securities.csv'
WITH (
    FIRSTROW = 2,
    FIELDTERMINATOR = ',',
    ROWTERMINATOR = '0x0a',
    TABLOCK
);
GO

SELECT TOP 20 * FROM Securities_Staging;
GO

INSERT INTO Securities (
    SecurityID,
    Ticker,
    CompanyName,
    Sector,
    ListedIn,
    IsActive
)
SELECT
    SecurityID,
    Ticker,
    CompanyName,
    Sector,
    ListedIn,
    CASE 
        WHEN IsActiveRaw IS NULL OR LTRIM(RTRIM(IsActiveRaw)) = '' THEN 0
        WHEN LTRIM(RTRIM(IsActiveRaw)) IN ('1', 'TRUE', 'True', 'true', 'Y', 'Yes')  THEN 1
        WHEN LTRIM(RTRIM(IsActiveRaw)) IN ('0', 'FALSE', 'False', 'false', 'N', 'No') THEN 0
        ELSE 0
    END AS IsActive
FROM Securities_Staging;
GO

SELECT COUNT(*) AS SecuritiesCount FROM Securities;
GO

CREATE TABLE PriceHistory_Staging (
    SecurityID    INT,
    Ticker        NVARCHAR(16),
    CompanyName   NVARCHAR(256),
    PriceDateRaw  NVARCHAR(50),   -- keep as TEXT here
    OpenPriceRaw  NVARCHAR(50),
    HighPriceRaw  NVARCHAR(50),
    LowPriceRaw   NVARCHAR(50),
    ClosePriceRaw NVARCHAR(50),
    VolumeRaw     NVARCHAR(50),
    ChangePctRaw  NVARCHAR(50)
);
GO

BULK INSERT PriceHistory_Staging
FROM '/var/opt/mssql/import/PriceHistory.csv'
WITH (
    FIRSTROW = 2,
    FIELDTERMINATOR = ',',
    ROWTERMINATOR = '0x0a',
    TABLOCK
);
GO

SELECT * FROM PriceHistory_Staging;
GO

INSERT INTO PriceHistory (
    SecurityID,
    PriceDate,
    OpenPrice,
    HighPrice,
    LowPrice,
    ClosePrice,
    Volume,
    ChangePct
)
SELECT
    SecurityID,
    COALESCE(
        TRY_CONVERT(date, PriceDateRaw, 23),  -- yyyy-mm-dd
        TRY_CONVERT(date, PriceDateRaw, 105), -- dd-mm-yyyy
        TRY_CONVERT(date, PriceDateRaw, 103), -- dd/mm/yyyy
        TRY_CONVERT(date, PriceDateRaw)       -- fallback
    ) AS PriceDate,
    TRY_CONVERT(DECIMAL(19,4), OpenPriceRaw)  AS OpenPrice,
    TRY_CONVERT(DECIMAL(19,4), HighPriceRaw)  AS HighPrice,
    TRY_CONVERT(DECIMAL(19,4), LowPriceRaw)   AS LowPrice,
    TRY_CONVERT(DECIMAL(19,4), ClosePriceRaw) AS ClosePrice,
    TRY_CONVERT(BIGINT,          VolumeRaw)   AS Volume,
    TRY_CONVERT(DECIMAL(9,4),    ChangePctRaw) AS ChangePct
FROM PriceHistory_Staging
WHERE
    COALESCE(
        TRY_CONVERT(date, PriceDateRaw, 23),
        TRY_CONVERT(date, PriceDateRaw, 105),
        TRY_CONVERT(date, PriceDateRaw, 103),
        TRY_CONVERT(date, PriceDateRaw)
    ) IS NOT NULL
  AND TRY_CONVERT(DECIMAL(19,4), ClosePriceRaw) IS NOT NULL;
GO

SELECT COUNT(*) AS PriceHistoryCount FROM PriceHistory;
GO

SELECT COUNT(DISTINCT SecurityID) AS DistinctSecurities
FROM PriceHistory;
GO

SELECT MIN(PriceDate) AS MinDate, MAX(PriceDate) AS MaxDate
FROM PriceHistory;
GO

DROP TABLE PriceHistory_Staging;
GO

DROP TABLE Securities_Staging;
GO

DECLARE @User1ID INT, @User2ID INT, @User3ID INT;

-- User 1
IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'demo_investor1')
BEGIN
    INSERT INTO Users (
        Username,
        PasswordHash,
        PasswordSalt,
        FullName,
        Email,
        Role
    )
    VALUES (
        'demo_investor1',
        0x0101,        -- placeholder hash
        0x0202,        -- placeholder salt
        'Demo Investor One',
        'investor1@example.com',
        'User'
    );
END;

SELECT @User1ID = UserID
FROM Users
WHERE Username = 'demo_investor1';


-- User 2
IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'demo_investor2')
BEGIN
    INSERT INTO Users (
        Username,
        PasswordHash,
        PasswordSalt,
        FullName,
        Email,
        Role
    )
    VALUES (
        'demo_investor2',
        0x0101,
        0x0202,
        'Demo Investor Two',
        'investor2@example.com',
        'User'
    );
END;

SELECT @User2ID = UserID
FROM Users
WHERE Username = 'demo_investor2';


-- User 3 (admin)
IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'admin_portfolio')
BEGIN
    INSERT INTO Users (
        Username,
        PasswordHash,
        PasswordSalt,
        FullName,
        Email,
        Role
    )
    VALUES (
        'admin_portfolio',
        0x0101,
        0x0202,
        'Portfolio Admin',
        'admin@example.com',
        'Admin'
    );
END;

SELECT @User3ID = UserID
FROM Users
WHERE Username = 'admin_portfolio';


------------------------------------------------------------
-- Seed demo accounts for each user
------------------------------------------------------------

-- Investor 1 accounts
IF @User1ID IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM Accounts
        WHERE UserID = @User1ID AND AccountName = 'Investor1 Demo Portfolio'
    )
    BEGIN
        INSERT INTO Accounts (
            UserID,
            AccountType,
            AccountName
        )
        VALUES (
            @User1ID,
            'Demo',
            'Investor1 Demo Portfolio'
        );
    END;

    IF NOT EXISTS (
        SELECT 1 FROM Accounts
        WHERE UserID = @User1ID AND AccountName = 'Investor1 Long-Term'
    )
    BEGIN
        INSERT INTO Accounts (
            UserID,
            AccountType,
            AccountName
        )
        VALUES (
            @User1ID,
            'Individual',
            'Investor1 Long-Term'
        );
    END;
END;


-- Investor 2 accounts
IF @User2ID IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM Accounts
        WHERE UserID = @User2ID AND AccountName = 'Investor2 Growth'
    )
    BEGIN
        INSERT INTO Accounts (
            UserID,
            AccountType,
            AccountName
        )
        VALUES (
            @User2ID,
            'Individual',
            'Investor2 Growth'
        );
    END;

    IF NOT EXISTS (
        SELECT 1 FROM Accounts
        WHERE UserID = @User2ID AND AccountName = 'Investor2 Income'
    )
    BEGIN
        INSERT INTO Accounts (
            UserID,
            AccountType,
            AccountName
        )
        VALUES (
            @User2ID,
            'Individual',
            'Investor2 Income'
        );
    END;
END;


-- Admin accounts (for testing system-wide views)
IF @User3ID IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM Accounts
        WHERE UserID = @User3ID AND AccountName = 'Admin Demo Book'
    )
    BEGIN
        INSERT INTO Accounts (
            UserID,
            AccountType,
            AccountName
        )
        VALUES (
            @User3ID,
            'Demo',
            'Admin Demo Book'
        );
    END;
END;



SELECT UserID, Username, Role
FROM Users
ORDER BY UserID;
GO

SELECT AccountID, UserID, AccountType, AccountName, CreatedDate, IsActive
FROM Accounts
ORDER BY UserID, AccountID;
GO

IF OBJECT_ID('dbo.TR_Trades_AfterInsert_UpdateHoldings', 'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_Trades_AfterInsert_UpdateHoldings;
GO

CREATE TRIGGER dbo.TR_Trades_AfterInsert_UpdateHoldings
ON dbo.Trades
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    -- Aggregate inserted trades per (AccountID, SecurityID)
    DECLARE @Agg TABLE (
        AccountID   INT,
        SecurityID  INT,
        DeltaQty    DECIMAL(19,6),
        BuyNotional DECIMAL(19,4)
    );

    INSERT INTO @Agg (AccountID, SecurityID, DeltaQty, BuyNotional)
    SELECT
        o.AccountID,
        o.SecurityID,
        SUM(CASE 
                WHEN o.OrderType = 'BUY'  THEN i.Quantity
                WHEN o.OrderType = 'SELL' THEN -i.Quantity
                ELSE 0
            END) AS DeltaQty,
        SUM(CASE 
                WHEN o.OrderType = 'BUY' THEN i.Quantity * i.Price
                ELSE 0
            END) AS BuyNotional
    FROM inserted AS i
    JOIN dbo.Orders AS o
      ON o.OrderID = i.OrderID
    GROUP BY o.AccountID, o.SecurityID;

    -- 1) Update existing holdings
    UPDATE h
    SET
        h.Quantity = h.Quantity + a.DeltaQty,
        h.AvgCost  =
            CASE
                WHEN (h.Quantity + a.DeltaQty) > 0
                     AND (h.Quantity * h.AvgCost + a.BuyNotional) > 0
                THEN (h.Quantity * h.AvgCost + a.BuyNotional)
                     / (h.Quantity + a.DeltaQty)
                ELSE h.AvgCost
            END
    FROM dbo.Holdings AS h
    JOIN @Agg AS a
      ON h.AccountID  = a.AccountID
     AND h.SecurityID = a.SecurityID;

    -- 2) Insert new holdings where none exist yet
    INSERT INTO dbo.Holdings (AccountID, SecurityID, Quantity, AvgCost)
    SELECT
        a.AccountID,
        a.SecurityID,
        a.DeltaQty,
        CASE
            WHEN a.DeltaQty > 0 AND a.BuyNotional > 0
            THEN a.BuyNotional / a.DeltaQty
            ELSE 0
        END AS AvgCost
    FROM @Agg AS a
    LEFT JOIN dbo.Holdings AS h
      ON h.AccountID  = a.AccountID
     AND h.SecurityID = a.SecurityID
    WHERE h.HoldingID IS NULL
      AND a.DeltaQty <> 0;
END
GO



IF OBJECT_ID('dbo.TR_Holdings_InsteadOfDelete', 'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_Holdings_InsteadOfDelete;
GO

CREATE TRIGGER dbo.TR_Holdings_InsteadOfDelete
ON dbo.Holdings
INSTEAD OF DELETE
AS
BEGIN
    SET NOCOUNT ON;

    -- Block delete if any deleted row still has non-zero quantity
    IF EXISTS (
        SELECT 1
        FROM deleted
        WHERE Quantity <> 0
    )
    BEGIN
        RAISERROR(
            'Cannot delete holding with non-zero quantity. Close the position first.',
            16, 1
        );
        RETURN;
    END;

    -- Safe to delete (all are flat)
    DELETE H
    FROM dbo.Holdings AS H
    JOIN deleted AS d
      ON H.HoldingID = d.HoldingID;
END;
GO

IF OBJECT_ID('dbo.TR_Trades_AfterInsert_CashLedger', 'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_Trades_AfterInsert_CashLedger;
GO

CREATE TRIGGER dbo.TR_Trades_AfterInsert_CashLedger
ON dbo.Trades
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    /*
        For each inserted trade:
        - Join to Orders to know AccountID and OrderType.
        - Compute cash amount:
            BUY  -> -(Amount + Fees)
            SELL ->  (Amount - Fees)
        - Insert a row into CashLedger.
    */

    INSERT INTO dbo.CashLedger (
        AccountID,
        TxnDate,
        Amount,
        Type,
        Reference
    )
    SELECT
        o.AccountID,
        i.TradeDate,
        CASE 
            WHEN o.OrderType = 'BUY'
                THEN -(i.Amount + i.Fees)
            WHEN o.OrderType = 'SELL'
                THEN  (i.Amount - i.Fees)
            ELSE 0
        END AS Amount,
        CASE 
            WHEN o.OrderType = 'BUY'  THEN 'TRADE_BUY'
            WHEN o.OrderType = 'SELL' THEN 'TRADE_SELL'
            ELSE 'TRADE_OTHER'
        END AS Type,
        CONCAT('Trade:', i.TradeID) AS Reference
    FROM inserted AS i
    JOIN dbo.Orders AS o
      ON o.OrderID = i.OrderID;
END;
GO

IF OBJECT_ID('dbo.TR_Accounts_InsteadOfDelete', 'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_Accounts_InsteadOfDelete;
GO

CREATE TRIGGER dbo.TR_Accounts_InsteadOfDelete
ON dbo.Accounts
INSTEAD OF DELETE
AS
BEGIN
    SET NOCOUNT ON;

    /*
        Soft-delete semantics:
        - Any attempted delete on Accounts just sets IsActive = 0.
        - Rows remain in place so Holdings / Orders / Trades / CashLedger stay valid.
    */
     
      
    UPDATE a
    SET IsActive = 0
    FROM dbo.Accounts AS a
    JOIN deleted AS d
      ON a.AccountID = d.AccountID;
END;
GO

IF OBJECT_ID('dbo.v_SecurityLatestPrice', 'V') IS NOT NULL
    DROP VIEW dbo.v_SecurityLatestPrice;
GO

CREATE VIEW dbo.v_SecurityLatestPrice
AS
    WITH Ranked AS (
        SELECT
            SecurityID,
            PriceDate,
            ClosePrice,
            ROW_NUMBER() OVER (
                PARTITION BY SecurityID
                ORDER BY PriceDate DESC
            ) AS rn
        FROM dbo.PriceHistory
    )
    SELECT
        SecurityID,
        PriceDate  AS LatestPriceDate,
        ClosePrice AS LatestClosePrice
    FROM Ranked
    WHERE rn = 1;
GO

IF OBJECT_ID('dbo.fn_GetSecurityPriceAsOf', 'IF') IS NOT NULL
    DROP FUNCTION dbo.fn_GetSecurityPriceAsOf;
GO

CREATE FUNCTION dbo.fn_GetSecurityPriceAsOf
(
    @SecurityID INT,
    @AsOfDate   DATE
)
RETURNS TABLE
AS
RETURN
(
    SELECT TOP 1
        SecurityID,
        PriceDate,
        ClosePrice
    FROM dbo.PriceHistory
    WHERE SecurityID = @SecurityID
      AND PriceDate  <= @AsOfDate
    ORDER BY PriceDate DESC
);
GO

IF OBJECT_ID('dbo.usp_GetPortfolioSnapshot', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_GetPortfolioSnapshot;
GO

CREATE PROCEDURE dbo.usp_GetPortfolioSnapshot
    @AccountID INT,
    @AsOfDate  DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Default AsOfDate to latest available price date
    IF @AsOfDate IS NULL
    BEGIN
        SELECT @AsOfDate = MAX(PriceDate)
        FROM dbo.PriceHistory;
    END;

    ;WITH NonZeroHoldings AS (
        SELECT
            h.AccountID,
            h.SecurityID,
            h.Quantity,
            h.AvgCost
        FROM dbo.Holdings AS h
        WHERE h.AccountID = @AccountID
          AND h.Quantity  <> 0
    )
    SELECT
        nh.AccountID,
        nh.SecurityID,
        s.Ticker,
        s.CompanyName,
        nh.Quantity,
        nh.AvgCost,
        p.PriceDate      AS PriceDate,
        p.ClosePrice     AS LastClosePrice,
        nh.Quantity * p.ClosePrice                  AS MarketValue,
        nh.Quantity * (p.ClosePrice - nh.AvgCost)   AS UnrealizedPL
    FROM NonZeroHoldings AS nh
    JOIN dbo.Securities AS s
      ON s.SecurityID = nh.SecurityID
    CROSS APPLY dbo.fn_GetSecurityPriceAsOf(nh.SecurityID, @AsOfDate) AS p
    ORDER BY MarketValue DESC;
END;
GO


IF OBJECT_ID('dbo.fn_CalcReturnPct', 'FN') IS NOT NULL
    DROP FUNCTION dbo.fn_CalcReturnPct;
GO

CREATE FUNCTION dbo.fn_CalcReturnPct
(
    @PrevClose  DECIMAL(19,4),
    @CurrClose  DECIMAL(19,4)
)
RETURNS DECIMAL(9,4)
AS
BEGIN
    -- If there is no previous close or it is zero, return NULL
    IF @PrevClose IS NULL OR @PrevClose = 0
        RETURN NULL;

    RETURN (@CurrClose - @PrevClose) * 100.0 / @PrevClose;
END;
GO

IF OBJECT_ID('dbo.usp_GetSecurityReturnSeries', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_GetSecurityReturnSeries;
GO

CREATE PROCEDURE dbo.usp_GetSecurityReturnSeries
    @SecurityID INT,
    @StartDate  DATE = NULL,
    @EndDate    DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Default date range to the available range for this security
    IF @StartDate IS NULL
    BEGIN
        SELECT @StartDate = MIN(PriceDate)
        FROM dbo.PriceHistory
        WHERE SecurityID = @SecurityID;
    END;

    IF @EndDate IS NULL
    BEGIN
        SELECT @EndDate = MAX(PriceDate)
        FROM dbo.PriceHistory
        WHERE SecurityID = @SecurityID;
    END;

    ;WITH PH AS (
        SELECT
            ph.PriceDate,
            ph.ClosePrice,
            LAG(ph.ClosePrice) OVER (ORDER BY ph.PriceDate) AS PrevClose
        FROM dbo.PriceHistory AS ph
        WHERE ph.SecurityID = @SecurityID
          AND ph.PriceDate BETWEEN @StartDate AND @EndDate
    )
    SELECT
        s.SecurityID,
        s.Ticker,
        s.CompanyName,
        ph.PriceDate,
        ph.ClosePrice,
        dbo.fn_CalcReturnPct(ph.PrevClose, ph.ClosePrice) AS DailyReturnPct,
        SUM(
            ISNULL(dbo.fn_CalcReturnPct(ph.PrevClose, ph.ClosePrice), 0.0)
        ) OVER (
            ORDER BY ph.PriceDate
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
        ) AS CumReturnApprox
    FROM PH AS ph
    JOIN dbo.Securities AS s
      ON s.SecurityID = @SecurityID
    ORDER BY ph.PriceDate;
END;
GO


IF OBJECT_ID('dbo.v_AccountHoldingsValue', 'V') IS NOT NULL
    DROP VIEW dbo.v_AccountHoldingsValue;
GO

CREATE VIEW dbo.v_AccountHoldingsValue
AS
    WITH NonZeroHoldings AS (
        SELECT
            h.AccountID,
            h.SecurityID,
            h.Quantity,
            h.AvgCost
        FROM dbo.Holdings AS h
        WHERE h.Quantity <> 0
    )
    SELECT
        a.AccountID,
        u.Username,
        u.FullName,
        nz.SecurityID,
        s.Ticker,
        s.CompanyName,
        nz.Quantity,
        nz.AvgCost,
        lp.LatestPriceDate,
        lp.LatestClosePrice,
        nz.Quantity * lp.LatestClosePrice                      AS MarketValue,
        nz.Quantity * (lp.LatestClosePrice - nz.AvgCost)        AS UnrealizedPL
    FROM NonZeroHoldings      AS nz
    JOIN dbo.Accounts         AS a  ON a.AccountID   = nz.AccountID
    JOIN dbo.Users            AS u  ON u.UserID      = a.UserID
    JOIN dbo.Securities       AS s  ON s.SecurityID  = nz.SecurityID
    JOIN dbo.v_SecurityLatestPrice AS lp
         ON lp.SecurityID = nz.SecurityID;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_PriceHistory_Date_Security'
      AND object_id = OBJECT_ID('dbo.PriceHistory')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_PriceHistory_Date_Security
    ON dbo.PriceHistory(PriceDate, SecurityID)
    INCLUDE (ClosePrice, Volume);
END;
GO

-- Filtered index on non-zero holdings (most common case in reports)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Holdings_Account_NonZeroQty'
      AND object_id = OBJECT_ID('dbo.Holdings')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Holdings_Account_NonZeroQty
    ON dbo.Holdings(AccountID)
    INCLUDE (SecurityID, Quantity, AvgCost)
    WHERE Quantity <> 0;
END;
GO


IF NOT EXISTS (
    SELECT 1 FROM sys.partition_functions
    WHERE name = 'pfPriceDateYear'
)
BEGIN
    CREATE PARTITION FUNCTION pfPriceDateYear (DATE)
    AS RANGE RIGHT FOR VALUES
    (
        ('2020-01-01'),
        ('2021-01-01'),
        ('2022-01-01'),
        ('2023-01-01'),
        ('2024-01-01')
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.partition_schemes
    WHERE name = 'psPriceDateYear'
)
BEGIN
    CREATE PARTITION SCHEME psPriceDateYear
    AS PARTITION pfPriceDateYear
    ALL TO ([PRIMARY]);      -- all partitions on PRIMARY filegroup (simple demo)
END;
GO

-------

ALTER TABLE dbo.PriceHistory
    DROP CONSTRAINT PK_PriceHistory;
GO

ALTER TABLE dbo.PriceHistory
    ADD CONSTRAINT PK_PriceHistory
        PRIMARY KEY CLUSTERED (SecurityID, PriceDate)
        ON psPriceDateYear(PriceDate);
GO


IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_PriceHistory_Date_Security_Partitioned'
      AND object_id = OBJECT_ID('dbo.PriceHistory')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_PriceHistory_Date_Security_Partitioned
    ON dbo.PriceHistory(PriceDate, SecurityID)
    INCLUDE (ClosePrice, Volume)
    ON psPriceDateYear(PriceDate);  
END;
GO

DECLARE @NumSyntheticSec INT = 1000;

DECLARE @ExistingMaxSecID INT;
SELECT @ExistingMaxSecID = ISNULL(MAX(SecurityID), 0)
FROM dbo.Securities;

;WITH SecNums AS (
    SELECT TOP (@NumSyntheticSec)
           ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects o1
    CROSS JOIN sys.all_objects o2
)
INSERT INTO dbo.Securities (
    SecurityID,
    Ticker,
    CompanyName,
    Sector,
    ListedIn,
    IsActive
)
SELECT
    @ExistingMaxSecID + n                         AS SecurityID,
    CONCAT('SYN_', @ExistingMaxSecID + n)         AS Ticker,
    CONCAT('Synthetic Security ', @ExistingMaxSecID + n) AS CompanyName,
    'Synthetic'                                   AS Sector,
    'Synthetic'                                   AS ListedIn,
    1                                             AS IsActive
FROM SecNums;
GO

DECLARE @NumSyntheticSec INT = 1000;
DECLARE @DaysPerSec     INT = 1000;
DECLARE @StartDate      DATE = '2010-01-01';

;WITH Secs AS (
    SELECT TOP (@NumSyntheticSec)
           ROW_NUMBER() OVER (ORDER BY SecurityID) AS RowNum,
           SecurityID
    FROM dbo.Securities
    WHERE CompanyName LIKE 'Synthetic Security %'
    ORDER BY SecurityID
),
Days AS (
    SELECT TOP (@DaysPerSec)
           ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS DayOffset
    FROM sys.all_objects o1
    CROSS JOIN sys.all_objects o2
),
Grid AS (
    SELECT
        s.SecurityID,
        DATEADD(DAY, d.DayOffset, @StartDate) AS PriceDate,
        ((ABS(CHECKSUM(NEWID())) % 4001) - 2000) / 100000.0 AS Eps
        -- Eps in roughly [-0.0200, +0.0200]
    FROM Secs s
    CROSS JOIN Days d
),
RandomWalk AS (
    SELECT
        g.SecurityID,
        g.PriceDate,
        100.0 * EXP(
            SUM(LOG(1.0 + g.Eps)) OVER (
                PARTITION BY g.SecurityID
                ORDER BY g.PriceDate
                ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
            )
        ) AS RawPrice
    FROM Grid g
),
ClippedWalk AS (
    SELECT
        SecurityID,
        PriceDate,
        CASE
            WHEN RawPrice < 0.0001 THEN 0.0001  -- avoid rounding to 0.0000
            ELSE RawPrice
        END AS WalkPrice
    FROM RandomWalk
)
INSERT INTO dbo.PriceHistory (
    SecurityID,
    PriceDate,
    OpenPrice,
    HighPrice,
    LowPrice,
    ClosePrice,
    Volume,
    ChangePct
)
SELECT
    c.SecurityID,
    c.PriceDate,
    CAST(c.WalkPrice AS DECIMAL(19,4)) AS OpenPrice,
    CAST(c.WalkPrice AS DECIMAL(19,4)) AS HighPrice,
    CAST(c.WalkPrice AS DECIMAL(19,4)) AS LowPrice,
    CAST(c.WalkPrice AS DECIMAL(19,4)) AS ClosePrice,
    CAST(ABS(CHECKSUM(NEWID())) % 900000 + 1000 AS BIGINT) AS Volume,
    NULL AS ChangePct
FROM ClippedWalk AS c;
GO

SELECT COUNT(*) from PriceHistory
GO