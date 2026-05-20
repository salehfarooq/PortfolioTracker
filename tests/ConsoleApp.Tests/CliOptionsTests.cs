using ConsoleApp;
using ConsoleApp.UI;

namespace ConsoleApp.Tests;

public class CliOptionsTests
{
    [Fact]
    public void Parse_AcceptsBackendAndConnectionOptions()
    {
        var options = CliOptions.Parse(new[]
        {
            "--backend", "sp",
            "--server=localhost,1433",
            "--database", "PortfolioDB",
            "--user", "sa",
            "--demo",
            "--no-seed"
        });

        Assert.Equal("sp", options.Backend);
        Assert.Equal("localhost,1433", options.Server);
        Assert.Equal("PortfolioDB", options.Database);
        Assert.Equal("sa", options.User);
        Assert.True(options.DemoMode);
        Assert.True(options.NoSeed);
        Assert.True(options.HasExplicitStartupOptions);
    }

    [Fact]
    public void Parse_RejectsUnknownOption()
    {
        Assert.Throws<ArgumentException>(() => CliOptions.Parse(new[] { "--unknown" }));
    }

    [Fact]
    public void FromEnvironment_UsesFullConnectionStringWhenPresent()
    {
        var original = Environment.GetEnvironmentVariable("PORTFOLIO_DB_CONNECTION");
        try
        {
            Environment.SetEnvironmentVariable(
                "PORTFOLIO_DB_CONNECTION",
                "Server=db;Database=PortfolioDB;User Id=sa;Password=secret;Encrypt=True;TrustServerCertificate=True;");

            var settings = DatabaseConnectionSettings.FromEnvironment(new CliOptions { Server = "cli-server" });

            Assert.Equal("db", settings.Server);
            Assert.Contains("Password=secret", settings.ToConnectionString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("PORTFOLIO_DB_CONNECTION", original);
        }
    }

    [Fact]
    public void Mask_HidesPassword()
    {
        var masked = DatabaseConnectionSettings.Mask(
            "Server=localhost;Database=PortfolioDB;User Id=sa;Password=super-secret;Encrypt=True;TrustServerCertificate=True;");

        Assert.Contains("Password=*****", masked);
        Assert.DoesNotContain("super-secret", masked);
    }

    [Fact]
    public void FitCell_TruncatesLongTableCells()
    {
        var value = UiPrinter.FitCell("abcdefghijklmnopqrstuvwxyz", 10);

        Assert.Equal("abcdefg...", value);
    }
}
