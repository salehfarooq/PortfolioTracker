using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.EF.Generated;

public partial class PortfolioDbContext : ApplicationCore.DataAccess.PortfolioDbContext
{
    public PortfolioDbContext(DbContextOptions<PortfolioDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<CashLedger> CashLedgers { get; set; }

    public virtual DbSet<ComplianceRule> ComplianceRules { get; set; }

    public virtual DbSet<Holding> Holdings { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<PriceHistory> PriceHistories { get; set; }

    public virtual DbSet<Security> Securities { get; set; }

    public virtual DbSet<Trade> Trades { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<v_AccountHoldingsValue> v_AccountHoldingsValues { get; set; }

    public virtual DbSet<v_SecurityLatestPrice> v_SecurityLatestPrices { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.AccountID).HasName("PK__Accounts__349DA586C47B9688");

            entity.ToTable(tb => tb.HasTrigger("TR_Accounts_InsteadOfDelete"));

            entity.HasIndex(e => e.UserID, "IX_Accounts_UserID");

            entity.Property(e => e.AccountName).HasMaxLength(64);
            entity.Property(e => e.AccountType).HasMaxLength(32);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(CONVERT([date],getdate()))");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.User).WithMany(p => p.Accounts)
                .HasForeignKey(d => d.UserID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Accounts_Users");
        });

        modelBuilder.Entity<CashLedger>(entity =>
        {
            entity.HasKey(e => e.LedgerID).HasName("PK__CashLedg__AE70E0AF329A4E60");

            entity.ToTable("CashLedger");

            entity.HasIndex(e => new { e.AccountID, e.TxnDate }, "IX_CashLedger_Account_TxnDate");

            entity.Property(e => e.Amount).HasColumnType("decimal(19, 4)");
            entity.Property(e => e.Reference).HasMaxLength(64);
            entity.Property(e => e.TxnDate).HasPrecision(3);
            entity.Property(e => e.Type).HasMaxLength(16);

            entity.HasOne(d => d.Account).WithMany(p => p.CashLedgers)
                .HasForeignKey(d => d.AccountID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CashLedger_Accounts");
        });

        modelBuilder.Entity<ComplianceRule>(entity =>
        {
            entity.HasKey(e => e.RuleID).HasName("PK__Complian__110458C2E9A46AD4");

            entity.Property(e => e.Description).HasMaxLength(256);
            entity.Property(e => e.LimitValue).HasColumnType("decimal(19, 4)");
            entity.Property(e => e.RuleType).HasMaxLength(64);
        });

        modelBuilder.Entity<Holding>(entity =>
        {
            entity.HasKey(e => e.HoldingID).HasName("PK__Holdings__E524B50D92172900");

            entity.ToTable(tb => tb.HasTrigger("TR_Holdings_InsteadOfDelete"));

            entity.HasIndex(e => e.AccountID, "IX_Holdings_Account_NonZeroQty").HasFilter("([Quantity]<>(0))");

            entity.HasIndex(e => new { e.AccountID, e.SecurityID }, "IX_Holdings_Account_Security");

            entity.HasIndex(e => new { e.AccountID, e.SecurityID }, "UQ_Holdings_Account_Security").IsUnique();

            entity.Property(e => e.AvgCost).HasColumnType("decimal(19, 4)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(19, 6)");

            entity.HasOne(d => d.Account).WithMany(p => p.Holdings)
                .HasForeignKey(d => d.AccountID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Holdings_Accounts");

            entity.HasOne(d => d.Security).WithMany(p => p.Holdings)
                .HasForeignKey(d => d.SecurityID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Holdings_Securities");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderID).HasName("PK__Orders__C3905BAF17EA707C");

            entity.HasIndex(e => new { e.AccountID, e.OrderDate }, "IX_Orders_Account_OrderDate");

            entity.Property(e => e.OrderDate)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.OrderType)
                .HasMaxLength(4)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.Price).HasColumnType("decimal(19, 4)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.Status).HasMaxLength(16);

            entity.HasOne(d => d.Account).WithMany(p => p.Orders)
                .HasForeignKey(d => d.AccountID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_Accounts");

            entity.HasOne(d => d.Security).WithMany(p => p.Orders)
                .HasForeignKey(d => d.SecurityID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Orders_Securities");
        });

        modelBuilder.Entity<PriceHistory>(entity =>
        {
            entity.HasKey(e => new { e.SecurityID, e.PriceDate });

            entity.ToTable("PriceHistory");

            entity.HasIndex(e => new { e.PriceDate, e.SecurityID }, "IX_PriceHistory_Date_Security");

            entity.HasIndex(e => new { e.PriceDate, e.SecurityID }, "IX_PriceHistory_Date_Security_Partitioned");

            entity.Property(e => e.ChangePct).HasColumnType("decimal(9, 4)");
            entity.Property(e => e.ClosePrice).HasColumnType("decimal(19, 4)");
            entity.Property(e => e.HighPrice).HasColumnType("decimal(19, 4)");
            entity.Property(e => e.LowPrice).HasColumnType("decimal(19, 4)");
            entity.Property(e => e.OpenPrice).HasColumnType("decimal(19, 4)");

            entity.HasOne(d => d.Security).WithMany(p => p.PriceHistories)
                .HasForeignKey(d => d.SecurityID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PriceHistory_Securities");
        });

        modelBuilder.Entity<Security>(entity =>
        {
            entity.HasKey(e => e.SecurityID).HasName("PK__Securiti__9F8B095046FA93E6");

            entity.HasIndex(e => e.Ticker, "UQ__Securiti__42AC12F00B493A9A").IsUnique();

            entity.Property(e => e.SecurityID).ValueGeneratedNever();
            entity.Property(e => e.CompanyName).HasMaxLength(256);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ListedIn).HasMaxLength(64);
            entity.Property(e => e.Sector).HasMaxLength(128);
            entity.Property(e => e.Ticker).HasMaxLength(16);
        });

        modelBuilder.Entity<Trade>(entity =>
        {
            entity.HasKey(e => e.TradeID).HasName("PK__Trades__3028BABB32BD4BC8");

            entity.ToTable(tb =>
                {
                    tb.HasTrigger("TR_Trades_AfterInsert_CashLedger");
                    tb.HasTrigger("TR_Trades_AfterInsert_UpdateHoldings");
                });

            entity.HasIndex(e => new { e.OrderID, e.TradeDate }, "IX_Trades_Order_TradeDate");

            entity.Property(e => e.Amount).HasColumnType("decimal(19, 4)");
            entity.Property(e => e.Fees).HasColumnType("decimal(19, 4)");
            entity.Property(e => e.Price).HasColumnType("decimal(19, 4)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.TradeDate).HasPrecision(3);

            entity.HasOne(d => d.Order).WithMany(p => p.Trades)
                .HasForeignKey(d => d.OrderID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Trades_Orders");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserID).HasName("PK__Users__1788CCACE063019A");

            entity.HasIndex(e => e.Username, "UQ__Users__536C85E48CD4C6E6").IsUnique();

            entity.HasIndex(e => e.Email, "UQ__Users__A9D10534232DAE75").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(3)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(128);
            entity.Property(e => e.FullName).HasMaxLength(128);
            entity.Property(e => e.JoinDate).HasDefaultValueSql("(CONVERT([date],getdate()))");
            entity.Property(e => e.ModifiedAt).HasPrecision(3);
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.PasswordSalt).HasMaxLength(128);
            entity.Property(e => e.Role).HasMaxLength(16);
            entity.Property(e => e.Username).HasMaxLength(64);
        });

        modelBuilder.Entity<v_AccountHoldingsValue>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_AccountHoldingsValue");

            entity.Property(e => e.AvgCost).HasColumnType("decimal(19, 4)");
            entity.Property(e => e.CompanyName).HasMaxLength(256);
            entity.Property(e => e.FullName).HasMaxLength(128);
            entity.Property(e => e.LatestClosePrice).HasColumnType("decimal(19, 4)");
            entity.Property(e => e.MarketValue).HasColumnType("decimal(38, 9)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(19, 6)");
            entity.Property(e => e.Ticker).HasMaxLength(16);
            entity.Property(e => e.UnrealizedPL).HasColumnType("decimal(38, 8)");
            entity.Property(e => e.Username).HasMaxLength(64);
        });

        modelBuilder.Entity<v_SecurityLatestPrice>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_SecurityLatestPrice");

            entity.Property(e => e.LatestClosePrice).HasColumnType("decimal(19, 4)");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
