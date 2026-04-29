using System;
using System.Collections.Generic;
using System.Linq;

namespace dznetcut.CLI
{
    internal sealed class CliArguments
    {
        private CliArguments(bool showHelp, bool launchGui, string? command, IReadOnlyDictionary<string, string?> options, IReadOnlyList<string> positionals, string? parseError)
        {
            ShowHelp = showHelp;
            LaunchGui = launchGui;
            Command = command;
            Options = options;
            Positionals = positionals;
            ParseError = parseError;
        }

        public string? Command { get; }
        public bool LaunchGui { get; }
        public IReadOnlyDictionary<string, string?> Options { get; }
        public string? ParseError { get; }
        public IReadOnlyList<string> Positionals { get; }
        public bool ShowHelp { get; }

        public bool TryGetOption(string key, out string? value) => Options.TryGetValue(key, out value);

        public static CliArguments Parse(string[] args)
        {
            if (args.Length == 0)
            {
                return new CliArguments(showHelp: false, launchGui: true, command: null, options: new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase), positionals: Array.Empty<string>(), parseError: null);
            }

            var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var positionals = new List<string>();

            for (var i = 0; i < args.Length; i++)
            {
                var token = args[i];
                if (token.StartsWith("--", StringComparison.Ordinal))
                {
                    if (string.Equals(token, "--help", StringComparison.OrdinalIgnoreCase))
                    {
                        return new CliArguments(showHelp: true, launchGui: false, command: null, options, positionals, parseError: null);
                    }

                    if (string.Equals(token, "--gui", StringComparison.OrdinalIgnoreCase))
                    {
                        return new CliArguments(showHelp: false, launchGui: true, command: null, options, positionals, parseError: null);
                    }

                    if (string.Equals(token, "--verbose", StringComparison.OrdinalIgnoreCase))
                    {
                        options["verbose"] = null;
                        continue;
                    }

                    if (string.Equals(token, "--no-arp-protection", StringComparison.OrdinalIgnoreCase))
                    {
                        options["no-arp-protection"] = null;
                        continue;
                    }

                    var separatorIndex = token.IndexOf('=');
                    if (separatorIndex > 2)
                    {
                        var key = token.Substring(2, separatorIndex - 2);
                        var value = token.Substring(separatorIndex + 1);
                        options[key] = value;
                        continue;
                    }

                    var optionKey = token.Substring(2);
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                    {
                        options[optionKey] = args[i + 1];
                        i++;
                    }
                    else
                    {
                        options[optionKey] = null;
                    }

                    continue;
                }

                if (token.StartsWith("-", StringComparison.Ordinal))
                {
                    if (string.Equals(token, "-h", StringComparison.OrdinalIgnoreCase))
                    {
                        return new CliArguments(showHelp: true, launchGui: false, command: null, options, positionals, parseError: null);
                    }

                    if (string.Equals(token, "-v", StringComparison.OrdinalIgnoreCase))
                    {
                        options["verbose"] = null;
                        continue;
                    }

                    if (string.Equals(token, "-nap", StringComparison.OrdinalIgnoreCase))
                    {
                        options["no-arp-protection"] = null;
                        continue;
                    }

                    return new CliArguments(showHelp: false, launchGui: false, command: null, options, positionals, parseError: $"Unknown option: {token}");
                }

                positionals.Add(token);
            }

            if (positionals.Count == 0)
            {
                return new CliArguments(showHelp: true, launchGui: false, command: null, options, positionals, parseError: null);
            }

            var command = positionals.First();
            var remainingPositionals = positionals.Skip(1).ToArray();
            return new CliArguments(showHelp: string.Equals(command, "help", StringComparison.OrdinalIgnoreCase), launchGui: false, command: command, options, remainingPositionals, parseError: null);
        }
    }
}
