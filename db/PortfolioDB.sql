USE master;
GO

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

CREATE TABLE dbo.Users (
    UserID       INT IDENTITY(1,1) PRIMARY KEY,
    Username     NVARCHAR(64)   NOT NULL UNIQUE,
    PasswordHash VARBINARY(256) NOT NULL,
    PasswordSalt VARBINARY(128) NOT NULL,
    FullName     NVARCHAR(128)  NOT NULL,
    Email        NVARCHAR(128)  NOT NULL UNIQUE,
    Role         NVARCHAR(16)   NOT NULL,
    JoinDate     DATE           NOT NULL DEFAULT (CAST(GETDATE() AS DATE)),
    CreatedAt    DATETIME2(3)   NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedAt   DATETIME2(3)   NULL
);
GO

CREATE TABLE dbo.Securities (
    SecurityID  INT           NOT NULL PRIMARY KEY,
    Ticker      NVARCHAR(16)  NOT NULL UNIQUE,
    CompanyName NVARCHAR(256) NOT NULL,
    Sector      NVARCHAR(128) NULL,
    ListedIn    NVARCHAR(64)  NULL,
    IsActive    BIT           NOT NULL DEFAULT (1)
);
GO

CREATE TABLE dbo.Accounts (
    AccountID   INT IDENTITY(1,1) PRIMARY KEY,
    UserID      INT          NOT NULL,
    AccountType NVARCHAR(32) NOT NULL,
    AccountName NVARCHAR(64) NOT NULL,
    CreatedDate DATE         NOT NULL DEFAULT (CAST(GETDATE() AS DATE)),
    IsActive    BIT          NOT NULL DEFAULT (1),
    CONSTRAINT FK_Accounts_Users FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID)
);
GO

CREATE TABLE dbo.PriceHistory (
    SecurityID INT           NOT NULL,
    PriceDate  DATE          NOT NULL,
    OpenPrice  DECIMAL(19,4) NULL,
    HighPrice  DECIMAL(19,4) NULL,
    LowPrice   DECIMAL(19,4) NULL,
    ClosePrice DECIMAL(19,4) NOT NULL,
    Volume     BIGINT        NULL,
    ChangePct  DECIMAL(9,4)  NULL,
    CONSTRAINT PK_PriceHistory PRIMARY KEY CLUSTERED (SecurityID, PriceDate),
    CONSTRAINT FK_PriceHistory_Securities FOREIGN KEY (SecurityID) REFERENCES dbo.Securities(SecurityID),
    CONSTRAINT CK_PriceHistory_ClosePrice_Positive CHECK (ClosePrice > 0)
);
GO

CREATE TABLE dbo.Holdings (
    HoldingID  INT IDENTITY(1,1) PRIMARY KEY,
    AccountID  INT           NOT NULL,
    SecurityID INT           NOT NULL,
    Quantity   DECIMAL(19,6) NOT NULL DEFAULT (0),
    AvgCost    DECIMAL(19,4) NOT NULL DEFAULT (0),
    CONSTRAINT FK_Holdings_Accounts FOREIGN KEY (AccountID) REFERENCES dbo.Accounts(AccountID),
    CONSTRAINT FK_Holdings_Securities FOREIGN KEY (SecurityID) REFERENCES dbo.Securities(SecurityID),
    CONSTRAINT UQ_Holdings_Account_Security UNIQUE (AccountID, SecurityID),
    CONSTRAINT CK_Holdings_Quantity_NonNegative CHECK (Quantity >= 0)
);
GO

CREATE TABLE dbo.Orders (
    OrderID    INT IDENTITY(1,1) PRIMARY KEY,
    AccountID  INT           NOT NULL,
    SecurityID INT           NOT NULL,
    OrderType  CHAR(4)       NOT NULL,
    Quantity   DECIMAL(19,6) NOT NULL,
    Price      DECIMAL(19,4) NOT NULL,
    Status     NVARCHAR(16)  NOT NULL,
    OrderDate  DATETIME2(3)  NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Orders_Accounts FOREIGN KEY (AccountID) REFERENCES dbo.Accounts(AccountID),
    CONSTRAINT FK_Orders_Securities FOREIGN KEY (SecurityID) REFERENCES dbo.Securities(SecurityID),
    CONSTRAINT CK_Orders_OrderType CHECK (OrderType IN ('BUY','SELL')),
    CONSTRAINT CK_Orders_Quantity_Positive CHECK (Quantity > 0),
    CONSTRAINT CK_Orders_Price_Positive CHECK (Price > 0)
);
GO

CREATE TABLE dbo.Trades (
    TradeID   INT IDENTITY(1,1) PRIMARY KEY,
    OrderID   INT           NOT NULL,
    TradeDate DATETIME2(3)  NOT NULL,
    Quantity  DECIMAL(19,6) NOT NULL,
    Price     DECIMAL(19,4) NOT NULL,
    Amount    DECIMAL(19,4) NOT NULL,
    Fees      DECIMAL(19,4) NOT NULL DEFAULT (0),
    CONSTRAINT FK_Trades_Orders FOREIGN KEY (OrderID) REFERENCES dbo.Orders(OrderID),
    CONSTRAINT CK_Trades_Quantity_Positive CHECK (Quantity > 0),
    CONSTRAINT CK_Trades_Price_Positive CHECK (Price > 0)
);
GO

CREATE TABLE dbo.CashLedger (
    LedgerID  INT IDENTITY(1,1) PRIMARY KEY,
    AccountID INT          NOT NULL,
    TxnDate   DATETIME2(3) NOT NULL,
    Amount    DECIMAL(19,4) NOT NULL,
    Type      NVARCHAR(16) NOT NULL,
    Reference NVARCHAR(64) NULL,
    CONSTRAINT FK_CashLedger_Accounts FOREIGN KEY (AccountID) REFERENCES dbo.Accounts(AccountID)
);
GO

CREATE TABLE dbo.ComplianceRules (
    RuleID      INT IDENTITY(1,1) PRIMARY KEY,
    RuleType    NVARCHAR(64)  NOT NULL,
    LimitValue  DECIMAL(19,4) NOT NULL,
    Description NVARCHAR(256) NULL
);
GO

CREATE INDEX IX_Accounts_UserID ON dbo.Accounts(UserID);
CREATE INDEX IX_Orders_Account_OrderDate ON dbo.Orders(AccountID, OrderDate) INCLUDE (SecurityID, Status, Quantity, Price);
CREATE INDEX IX_Trades_Order_TradeDate ON dbo.Trades(OrderID, TradeDate) INCLUDE (Quantity, Price, Amount);
CREATE INDEX IX_CashLedger_Account_TxnDate ON dbo.CashLedger(AccountID, TxnDate) INCLUDE (Amount, Type);
CREATE INDEX IX_Holdings_Account_Security ON dbo.Holdings(AccountID, SecurityID) INCLUDE (Quantity, AvgCost);
CREATE INDEX IX_Holdings_Account_NonZeroQty ON dbo.Holdings(AccountID) INCLUDE (SecurityID, Quantity, AvgCost) WHERE Quantity <> 0;
CREATE INDEX IX_PriceHistory_Date_Security ON dbo.PriceHistory(PriceDate, SecurityID) INCLUDE (ClosePrice, Volume);
GO

CREATE TRIGGER dbo.TR_Trades_AfterInsert_UpdateHoldings
ON dbo.Trades
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    WITH Agg AS (
        SELECT
            o.AccountID,
            o.SecurityID,
            SUM(CASE WHEN o.OrderType = 'BUY' THEN i.Quantity ELSE -i.Quantity END) AS DeltaQty,
            SUM(CASE WHEN o.OrderType = 'BUY' THEN i.Quantity * i.Price ELSE 0 END) AS BuyNotional
        FROM inserted i
        JOIN dbo.Orders o ON o.OrderID = i.OrderID
        GROUP BY o.AccountID, o.SecurityID
    )
    UPDATE h
    SET
        h.Quantity = h.Quantity + a.DeltaQty,
        h.AvgCost = CASE
            WHEN a.DeltaQty > 0 AND h.Quantity + a.DeltaQty > 0
            THEN ((h.Quantity * h.AvgCost) + a.BuyNotional) / (h.Quantity + a.DeltaQty)
            ELSE h.AvgCost
        END
    FROM dbo.Holdings h
    JOIN Agg a ON a.AccountID = h.AccountID AND a.SecurityID = h.SecurityID;

    WITH Agg AS (
        SELECT
            o.AccountID,
            o.SecurityID,
            SUM(CASE WHEN o.OrderType = 'BUY' THEN i.Quantity ELSE -i.Quantity END) AS DeltaQty,
            SUM(CASE WHEN o.OrderType = 'BUY' THEN i.Quantity * i.Price ELSE 0 END) AS BuyNotional
        FROM inserted i
        JOIN dbo.Orders o ON o.OrderID = i.OrderID
        GROUP BY o.AccountID, o.SecurityID
    )
    INSERT INTO dbo.Holdings (AccountID, SecurityID, Quantity, AvgCost)
    SELECT a.AccountID, a.SecurityID, a.DeltaQty,
           CASE WHEN a.DeltaQty > 0 THEN a.BuyNotional / a.DeltaQty ELSE 0 END
    FROM Agg a
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.Holdings h
        WHERE h.AccountID = a.AccountID AND h.SecurityID = a.SecurityID
    );
END;
GO

CREATE TRIGGER dbo.TR_Trades_AfterInsert_CashLedger
ON dbo.Trades
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.CashLedger (AccountID, TxnDate, Amount, Type, Reference)
    SELECT
        o.AccountID,
        i.TradeDate,
        CASE WHEN o.OrderType = 'BUY' THEN -(i.Amount + i.Fees) ELSE (i.Amount - i.Fees) END,
        CASE WHEN o.OrderType = 'BUY' THEN 'TRADE_BUY' ELSE 'TRADE_SELL' END,
        CONCAT('Trade:', i.TradeID)
    FROM inserted i
    JOIN dbo.Orders o ON o.OrderID = i.OrderID;
END;
GO

CREATE VIEW dbo.v_SecurityLatestPrice
AS
    WITH Ranked AS (
        SELECT SecurityID, PriceDate, ClosePrice,
               ROW_NUMBER() OVER (PARTITION BY SecurityID ORDER BY PriceDate DESC) AS rn
        FROM dbo.PriceHistory
    )
    SELECT SecurityID, PriceDate AS LatestPriceDate, ClosePrice AS LatestClosePrice
    FROM Ranked
    WHERE rn = 1;
GO

CREATE VIEW dbo.v_AccountHoldingsValue
AS
    SELECT
        a.AccountID,
        u.Username,
        u.FullName,
        h.SecurityID,
        s.Ticker,
        s.CompanyName,
        h.Quantity,
        h.AvgCost,
        lp.LatestPriceDate,
        lp.LatestClosePrice,
        h.Quantity * lp.LatestClosePrice AS MarketValue,
        h.Quantity * (lp.LatestClosePrice - h.AvgCost) AS UnrealizedPL
    FROM dbo.Holdings h
    JOIN dbo.Accounts a ON a.AccountID = h.AccountID
    JOIN dbo.Users u ON u.UserID = a.UserID
    JOIN dbo.Securities s ON s.SecurityID = h.SecurityID
    JOIN dbo.v_SecurityLatestPrice lp ON lp.SecurityID = h.SecurityID
    WHERE h.Quantity <> 0;
GO

CREATE PROCEDURE dbo.usp_GetPortfolioSnapshot
    @AccountID INT,
    @AsOfDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @AsOfDate IS NULL
        SELECT @AsOfDate = MAX(PriceDate) FROM dbo.PriceHistory;

    WITH PriceAsOf AS (
        SELECT ph.SecurityID, ph.PriceDate, ph.ClosePrice,
               ROW_NUMBER() OVER (PARTITION BY ph.SecurityID ORDER BY ph.PriceDate DESC) AS rn
        FROM dbo.PriceHistory ph
        WHERE ph.PriceDate <= @AsOfDate
    )
    SELECT
        h.AccountID,
        h.SecurityID,
        s.Ticker,
        s.CompanyName,
        s.Sector,
        h.Quantity,
        h.AvgCost,
        p.PriceDate,
        p.ClosePrice AS LatestPrice,
        h.Quantity * p.ClosePrice AS MarketValue,
        h.Quantity * (p.ClosePrice - h.AvgCost) AS UnrealizedPL,
        @AsOfDate AS AsOfDate
    FROM dbo.Holdings h
    JOIN dbo.Securities s ON s.SecurityID = h.SecurityID
    JOIN PriceAsOf p ON p.SecurityID = h.SecurityID AND p.rn = 1
    WHERE h.AccountID = @AccountID AND h.Quantity <> 0
    ORDER BY MarketValue DESC;
END;
GO

CREATE PROCEDURE dbo.usp_GetSecurityReturnSeries
    @SecurityID INT,
    @StartDate DATE = NULL,
    @EndDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @StartDate IS NULL SELECT @StartDate = MIN(PriceDate) FROM dbo.PriceHistory WHERE SecurityID = @SecurityID;
    IF @EndDate IS NULL SELECT @EndDate = MAX(PriceDate) FROM dbo.PriceHistory WHERE SecurityID = @SecurityID;

    WITH Points AS (
        SELECT ph.PriceDate, ph.ClosePrice,
               LAG(ph.ClosePrice) OVER (ORDER BY ph.PriceDate) AS PreviousClose
        FROM dbo.PriceHistory ph
        WHERE ph.SecurityID = @SecurityID
          AND ph.PriceDate BETWEEN @StartDate AND @EndDate
    )
    SELECT
        s.SecurityID,
        s.Ticker,
        s.CompanyName,
        p.PriceDate,
        p.ClosePrice,
        CASE WHEN p.PreviousClose IS NULL OR p.PreviousClose = 0 THEN NULL
             ELSE (p.ClosePrice - p.PreviousClose) / p.PreviousClose END AS DailyReturnPct
    FROM Points p
    JOIN dbo.Securities s ON s.SecurityID = @SecurityID
    ORDER BY p.PriceDate;
END;
GO

INSERT INTO dbo.Users (Username, PasswordHash, PasswordSalt, FullName, Email, Role)
VALUES
('demo_investor1', 0x01, 0x02, 'Demo Investor One', 'investor1@example.com', 'User'),
('demo_investor2', 0x03, 0x04, 'Demo Investor Two', 'investor2@example.com', 'User'),
('admin_portfolio', 0x05, 0x06, 'Portfolio Admin', 'admin@example.com', 'Admin');
GO

INSERT INTO dbo.Accounts (UserID, AccountType, AccountName)
VALUES
(1, 'Individual', 'Growth Portfolio'),
(1, 'Retirement', 'Long-Term Retirement'),
(2, 'Individual', 'Income Portfolio');
GO

INSERT INTO dbo.Securities (SecurityID, Ticker, CompanyName, Sector, ListedIn, IsActive)
VALUES
(1, 'MSFT', 'Microsoft Corporation', 'Technology', 'NASDAQ', 1),
(2, 'AAPL', 'Apple Inc.', 'Technology', 'NASDAQ', 1),
(3, 'NVDA', 'NVIDIA Corporation', 'Technology', 'NASDAQ', 1),
(4, 'JPM', 'JPMorgan Chase & Co.', 'Financials', 'NYSE', 1),
(5, 'V', 'Visa Inc.', 'Financials', 'NYSE', 1),
(6, 'JNJ', 'Johnson & Johnson', 'Healthcare', 'NYSE', 1);
GO

DECLARE @d DATE = '2026-01-02';
WITH Days AS (
    SELECT 0 AS n UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
    UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9
),
BasePrices AS (
    SELECT * FROM (VALUES
        (1, 410.00), (2, 190.00), (3, 870.00),
        (4, 180.00), (5, 260.00), (6, 155.00)
    ) v(SecurityID, BasePrice)
)
INSERT INTO dbo.PriceHistory (SecurityID, PriceDate, OpenPrice, HighPrice, LowPrice, ClosePrice, Volume, ChangePct)
SELECT
    bp.SecurityID,
    DATEADD(DAY, d.n, @d),
    CAST(bp.BasePrice + d.n * (bp.SecurityID + 1) AS DECIMAL(19,4)),
    CAST(bp.BasePrice + d.n * (bp.SecurityID + 1) + 2 AS DECIMAL(19,4)),
    CAST(bp.BasePrice + d.n * (bp.SecurityID + 1) - 2 AS DECIMAL(19,4)),
    CAST(bp.BasePrice + d.n * (bp.SecurityID + 1) AS DECIMAL(19,4)),
    1000000 + (bp.SecurityID * 10000) + d.n,
    NULL
FROM BasePrices bp
CROSS JOIN Days d;
GO

INSERT INTO dbo.CashLedger (AccountID, TxnDate, Amount, Type, Reference)
VALUES
(1, '2026-01-02T09:00:00', 100000.00, 'DEPOSIT', 'Initial funding'),
(2, '2026-01-02T09:00:00', 75000.00, 'DEPOSIT', 'Initial funding'),
(3, '2026-01-02T09:00:00', 50000.00, 'DEPOSIT', 'Initial funding');
GO

INSERT INTO dbo.Orders (AccountID, SecurityID, OrderType, Quantity, Price, Status, OrderDate)
VALUES
(1, 1, 'BUY', 40, 410.00, 'Filled', '2026-01-02T10:00:00'),
(1, 2, 'BUY', 60, 190.00, 'Filled', '2026-01-02T10:05:00'),
(1, 3, 'BUY', 12, 870.00, 'Filled', '2026-01-02T10:10:00'),
(1, 2, 'SELL', 10, 198.00, 'Filled', '2026-01-08T10:10:00'),
(2, 4, 'BUY', 100, 180.00, 'Filled', '2026-01-02T10:15:00'),
(2, 6, 'BUY', 120, 155.00, 'Filled', '2026-01-02T10:20:00'),
(3, 5, 'BUY', 90, 260.00, 'Filled', '2026-01-02T10:25:00');
GO

INSERT INTO dbo.Trades (OrderID, TradeDate, Quantity, Price, Amount, Fees)
SELECT OrderID, OrderDate, Quantity, Price, Quantity * Price, 1.00
FROM dbo.Orders;
GO

INSERT INTO dbo.ComplianceRules (RuleType, LimitValue, Description)
VALUES
('MaxPositionSizePct', 35.00, 'No single holding should dominate a demo account.'),
('MinCashReservePct', 5.00, 'Keep a small cash reserve for liquidity.');
GO

SELECT 'PortfolioDB ready' AS Status,
       (SELECT COUNT(*) FROM dbo.Accounts) AS Accounts,
       (SELECT COUNT(*) FROM dbo.Securities) AS Securities,
       (SELECT COUNT(*) FROM dbo.Trades) AS Trades;
GO

