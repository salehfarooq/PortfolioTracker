namespace ConsoleApp.UI;

internal static class UiPrinter
{
    public static void Header(string title)
    {
        Console.Clear();
        var bar = new string('=', Math.Max(24, title.Length + 6));
        Console.WriteLine(bar);
        Console.WriteLine($"  {title}");
        Console.WriteLine(bar);
        Console.WriteLine();
    }

    public static void SubHeader(string title)
    {
        Console.WriteLine();
        Console.WriteLine(title);
        Console.WriteLine(new string('-', Math.Max(12, title.Length)));
    }

    public static void Status(string message)
    {
        Console.WriteLine(message);
        Console.WriteLine(new string('-', Math.Min(80, message.Length + 4)));
    }

    public static void Info(string message)
    {
        Console.WriteLine($"[INFO] {message}");
    }

    public static void Warn(string message)
    {
        Console.WriteLine($"[WARN] {message}");
    }

    public static void Error(string message)
    {
        Console.WriteLine($"[ERROR] {message}");
    }

    public static void Kv(string label, string value)
    {
        Console.WriteLine($"{label,-22}: {value}");
    }

    public static void Table(IReadOnlyList<string> headers, List<string[]> rows, string[]? footer = null)
    {
        var maxCellWidth = CalculateMaxCellWidth(headers.Count);
        var fittedHeaders = headers.Select(h => FitCell(h, maxCellWidth)).ToArray();
        var fittedRows = rows
            .Select(row => row.Select(c => FitCell(c, maxCellWidth)).ToArray())
            .ToList();
        var fittedFooter = footer?.Select(c => FitCell(c, maxCellWidth)).ToArray();

        var widths = fittedHeaders.Select(h => h.Length).ToArray();
        foreach (var row in fittedRows)
        {
            for (var i = 0; i < row.Length; i++)
            {
                widths[i] = Math.Max(widths[i], row[i].Length);
            }
        }

        if (fittedFooter != null)
        {
            for (var i = 0; i < fittedFooter.Length; i++)
            {
                widths[i] = Math.Max(widths[i], fittedFooter[i].Length);
            }
        }

        string Sep(string left, string mid, string right)
        {
            return left + string.Join(mid, widths.Select(w => new string('-', w + 2))) + right;
        }

        Console.WriteLine(Sep("+", "+", "+"));
        Console.WriteLine("| " + string.Join(" | ", fittedHeaders.Select((h, i) => h.PadRight(widths[i]))) + " |");
        Console.WriteLine(Sep("+", "+", "+"));
        foreach (var row in fittedRows)
        {
            Console.WriteLine("| " + string.Join(" | ", row.Select((c, i) => c.PadRight(widths[i]))) + " |");
        }

        if (fittedFooter != null)
        {
            Console.WriteLine(Sep("+", "+", "+"));
            Console.WriteLine("| " + string.Join(" | ", fittedFooter.Select((c, i) => c.PadRight(widths[i]))) + " |");
        }

        Console.WriteLine(Sep("+", "+", "+"));
    }

    internal static string FitCell(string? value, int maxWidth)
    {
        var text = value ?? string.Empty;
        if (maxWidth < 4 || text.Length <= maxWidth)
        {
            return text;
        }

        return text[..(maxWidth - 3)] + "...";
    }

    private static int CalculateMaxCellWidth(int columnCount)
    {
        var width = 120;
        try
        {
            if (!Console.IsOutputRedirected && Console.WindowWidth > 0)
            {
                width = Console.WindowWidth;
            }
        }
        catch (IOException)
        {
            width = 120;
        }

        var available = Math.Max(40, width - (columnCount * 3) - 1);
        return Math.Clamp(available / Math.Max(1, columnCount), 12, 32);
    }
}

internal static class UiPrompts
{
    public static void Pause(string message = "Press Enter to continue...")
    {
        Console.WriteLine();
        Console.Write(message);
        Console.ReadLine();
    }

    public static int Menu(string title, string[] options)
    {
        Console.WriteLine(title);
        for (var i = 0; i < options.Length; i++)
        {
            Console.WriteLine($"  {i + 1}. {options[i]}");
        }
        Console.WriteLine();
        while (true)
        {
            Console.Write("Choose an option: ");
            var input = Console.ReadLine();
            if (int.TryParse(input, out var choice) && choice >= 1 && choice <= options.Length)
            {
                return choice;
            }
            Console.WriteLine("Please enter a valid number.");
        }
    }

    public static string ReadString(string label, string defaultValue, string? hint = null)
    {
        var suffix = string.IsNullOrWhiteSpace(hint) ? string.Empty : $" ({hint})";
        Console.Write($"{label}{suffix} [{defaultValue}]: ");
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
    }

    public static string ReadSecret(string label, string defaultValue = "")
    {
        Console.Write($"{label}{(!string.IsNullOrEmpty(defaultValue) ? " [keep current]" : "")}: ");
        if (Console.IsInputRedirected)
        {
            var redirected = Console.ReadLine();
            return string.IsNullOrWhiteSpace(redirected) ? defaultValue : redirected;
        }

        var chars = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (chars.Count > 0)
                {
                    chars.RemoveAt(chars.Count - 1);
                    Console.Write("\b \b");
                }
                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                chars.Add(key.KeyChar);
                Console.Write("*");
            }
        }

        return chars.Count == 0 ? defaultValue : new string(chars.ToArray());
    }

    public static int ReadInt(string label, int? defaultValue, int? min, int? max)
    {
        while (true)
        {
            Console.Write($"{label}{(defaultValue.HasValue ? $" [{defaultValue}]" : "")}: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) && defaultValue.HasValue)
            {
                return defaultValue.Value;
            }
            if (int.TryParse(input, out var val))
            {
                if (min.HasValue && val < min.Value)
                {
                    Console.WriteLine($"Value must be >= {min}");
                    continue;
                }
                if (max.HasValue && val > max.Value)
                {
                    Console.WriteLine($"Value must be <= {max}");
                    continue;
                }
                return val;
            }
            Console.WriteLine("Enter a valid integer.");
        }
    }

    public static decimal ReadDecimal(string label, decimal? defaultValue, decimal? min, decimal? max)
    {
        while (true)
        {
            Console.Write($"{label}{(defaultValue.HasValue ? $" [{defaultValue}]" : "")}: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) && defaultValue.HasValue)
            {
                return defaultValue.Value;
            }
            if (decimal.TryParse(input, out var val))
            {
                if (min.HasValue && val < min.Value)
                {
                    Console.WriteLine($"Value must be >= {min}");
                    continue;
                }
                if (max.HasValue && val > max.Value)
                {
                    Console.WriteLine($"Value must be <= {max}");
                    continue;
                }
                return val;
            }
            Console.WriteLine("Enter a valid number.");
        }
    }

    public static DateTime? ReadDate(string label, DateTime? defaultValue)
    {
        while (true)
        {
            Console.Write($"{label}{(defaultValue.HasValue ? $" [{defaultValue:yyyy-MM-dd}]" : "")}: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultValue;
            }
            if (DateTime.TryParse(input, out var dt))
            {
                return dt;
            }
            Console.WriteLine("Enter a valid date (yyyy-MM-dd) or leave blank.");
        }
    }
}
