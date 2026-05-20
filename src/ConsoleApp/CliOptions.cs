namespace ConsoleApp;

internal sealed record CliOptions
{
    public string Backend { get; init; } = "ef";
    public string? Server { get; init; }
    public string? Database { get; init; }
    public string? User { get; init; }
    public bool DemoMode { get; init; }
    public bool NoSeed { get; init; }
    public bool ShowHelp { get; init; }
    public bool HasExplicitStartupOptions { get; init; }

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        var explicitOptions = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            if (!arg.StartsWith("-", StringComparison.Ordinal))
            {
                if (i == 0 && IsBackend(arg))
                {
                    options = options with { Backend = NormalizeBackend(arg) };
                    explicitOptions = true;
                    continue;
                }

                throw new ArgumentException($"Unknown argument '{arg}'. Use --help to see supported options.");
            }

            var (key, inlineValue) = SplitOption(arg);
            explicitOptions = true;

            switch (key)
            {
                case "--help":
                case "-h":
                    options = options with { ShowHelp = true };
                    break;
                case "--backend":
                    options = options with { Backend = NormalizeBackend(ReadValue(args, ref i, inlineValue, key)) };
                    break;
                case "--server":
                    options = options with { Server = ReadValue(args, ref i, inlineValue, key) };
                    break;
                case "--database":
                    options = options with { Database = ReadValue(args, ref i, inlineValue, key) };
                    break;
                case "--user":
                    options = options with { User = ReadValue(args, ref i, inlineValue, key) };
                    break;
                case "--demo":
                    options = options with { DemoMode = true };
                    break;
                case "--no-seed":
                    options = options with { NoSeed = true };
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{key}'. Use --help to see supported options.");
            }
        }

        return options with { HasExplicitStartupOptions = explicitOptions };
    }

    public static string HelpText => """
        Portfolio Console Manager

        Usage:
          dotnet run --project src/ConsoleApp -- [options]

        Options:
          --backend ef|sp       Select Entity Framework or stored-procedure backend.
          --server HOST,PORT    SQL Server endpoint. Default: localhost,1433.
          --database NAME       Database name. Default: PortfolioDB.
          --user USER           SQL login. Default: sa.
          --demo                Run a non-interactive reviewer walkthrough.
          --no-seed             Do not create best-effort demo accounts on startup.
          --help                Show this help text.

        Environment:
          PORTFOLIO_DB_CONNECTION overrides all connection parts.
          PORTFOLIO_DB_SERVER, PORTFOLIO_DB_NAME, PORTFOLIO_DB_USER, and
          PORTFOLIO_DB_PASSWORD provide safe connection defaults.
        """;

    private static (string Key, string? Value) SplitOption(string arg)
    {
        var equalsIndex = arg.IndexOf('=', StringComparison.Ordinal);
        if (equalsIndex < 0)
        {
            return (arg.ToLowerInvariant(), null);
        }

        return (arg[..equalsIndex].ToLowerInvariant(), arg[(equalsIndex + 1)..]);
    }

    private static string ReadValue(string[] args, ref int index, string? inlineValue, string option)
    {
        var value = inlineValue;
        if (value is null)
        {
            if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
            {
                throw new ArgumentException($"{option} requires a value.");
            }

            index++;
            value = args[index];
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{option} requires a non-empty value.");
        }

        return value.Trim();
    }

    private static bool IsBackend(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "ef" or "linq" or "sp" or "sproc";
    }

    private static string NormalizeBackend(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "ef" or "linq" => "ef",
            "sp" or "sproc" => "sp",
            _ => throw new ArgumentException("--backend must be 'ef' or 'sp'.")
        };
    }
}
