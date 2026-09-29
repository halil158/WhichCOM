namespace WhichCOM.Cli;

internal enum CliCommand
{
    List,
    Latest,
    Help,
    Version,
}

internal sealed record CliOptions
{
    public CliCommand Command { get; init; } = CliCommand.List;

    public bool Json { get; init; }

    /// <summary>Include ports that are hidden by default (Bluetooth).</summary>
    public bool All { get; init; }

    public string? Match { get; init; }

    /// <summary>Set when the arguments are invalid.</summary>
    public string? Error { get; init; }

    public static CliOptions Parse(IReadOnlyList<string> args)
    {
        var options = new CliOptions();

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--json":
                    options = options with { Json = true };
                    break;

                case "--all" or "-a":
                    options = options with { All = true };
                    break;

                case "--latest" or "-l":
                    options = options with { Command = CliCommand.Latest };
                    break;

                case "--match" or "-m":
                    if (i + 1 >= args.Count || args[i + 1].StartsWith('-'))
                    {
                        return options with { Error = $"Option '{arg}' needs a value." };
                    }

                    options = options with { Match = args[++i] };
                    break;

                case "--help" or "-h" or "-?" or "/?":
                    return options with { Command = CliCommand.Help };

                case "--version":
                    return options with { Command = CliCommand.Version };

                default:
                    const string matchPrefix = "--match=";
                    if (arg.StartsWith(matchPrefix, StringComparison.Ordinal) && arg.Length > matchPrefix.Length)
                    {
                        options = options with { Match = arg[matchPrefix.Length..] };
                        break;
                    }

                    return options with { Error = $"Unknown option '{arg}'." };
            }
        }

        return options;
    }
}
