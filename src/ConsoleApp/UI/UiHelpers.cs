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
        var widths = headers.Select(h => h.Length).ToArray();
        foreach (var row in rows)
        {
            for (var i = 0; i < row.Length; i++)
            {
                widths[i] = Math.Max(widths[i], row[i].Length);
            }
        }

        if (footer != null)
        {
            for (var i = 0; i < footer.Length; i++)
            {
                widths[i] = Math.Max(widths[i], footer[i].Length);
            }
        }

        string Sep(string left, string mid, string right)
        {
            return left + string.Join(mid, widths.Select(w => new string('-', w + 2))) + right;
        }

        Console.WriteLine(Sep("+", "+", "+"));
        Console.WriteLine("| " + string.Join(" | ", headers.Select((h, i) => h.PadRight(widths[i]))) + " |");
        Console.WriteLine(Sep("+", "+", "+"));
        foreach (var row in rows)
        {
            Console.WriteLine("| " + string.Join(" | ", row.Select((c, i) => c.PadRight(widths[i]))) + " |");
        }

        if (footer != null)
        {
            Console.WriteLine(Sep("+", "+", "+"));
            Console.WriteLine("| " + string.Join(" | ", footer.Select((c, i) => c.PadRight(widths[i]))) + " |");
        }

        Console.WriteLine(Sep("+", "+", "+"));
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

    public static string ReadString(string label, string defaultValue)
    {
        Console.Write($"{label} [{defaultValue}]: ");
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
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
