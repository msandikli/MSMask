namespace MSMask.Cli
{
    /// <summary>
    /// The arguments of one command, split into positional values, flags and options with a value.
    /// </summary>
    /// <remarks>
    /// <c>eval data/dev/labels.json --limit 10 --no-checksum</c> gives the positional value
    /// <c>data/dev/labels.json</c>, the option <c>--limit</c> with value <c>10</c> and the flag <c>--no-checksum</c>.
    /// Which names take a value is decided by the command, so a typo is reported instead of silently ignored.
    /// </remarks>
    public sealed class CommandLineArguments
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CommandLineArguments"/> class.
        /// </summary>
        /// <param name="positionals">The values that are not options.</param>
        /// <param name="flags">The options without a value.</param>
        /// <param name="values">The options with a value.</param>
        private CommandLineArguments(List<string> positionals, HashSet<string> flags, Dictionary<string, string> values)
        {
            Positionals = positionals;
            Flags = flags;
            Values = values;
        }

        /// <summary>
        /// Gets the values that are not options, in order.
        /// </summary>
        public IReadOnlyList<string> Positionals { get; }

        /// <summary>
        /// Gets the options without a value, for example <c>--no-checksum</c>.
        /// </summary>
        public IReadOnlySet<string> Flags { get; }

        /// <summary>
        /// Gets the options with a value, for example <c>--limit</c> → <c>10</c>.
        /// </summary>
        public IReadOnlyDictionary<string, string> Values { get; }

        /// <summary>
        /// Splits the arguments of a command.
        /// </summary>
        /// <param name="args">The arguments after the command name.</param>
        /// <param name="flagNames">The allowed options without a value.</param>
        /// <param name="valueNames">The allowed options that take a value.</param>
        /// <returns>The split arguments.</returns>
        /// <exception cref="ArgumentException">Thrown when an option is unknown or a value is missing.</exception>
        public static CommandLineArguments Parse(IReadOnlyList<string> args, IReadOnlySet<string> flagNames,
            IReadOnlySet<string> valueNames)
        {
            var positionals = new List<string>();
            var flags = new HashSet<string>(StringComparer.Ordinal);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < args.Count; i++)
            {
                string arg = args[i];
                if (!arg.StartsWith("--", StringComparison.Ordinal))
                {
                    positionals.Add(arg);
                }
                else if (flagNames.Contains(arg))
                {
                    flags.Add(arg);
                }
                else if (valueNames.Contains(arg))
                {
                    if (i + 1 >= args.Count)
                    {
                        throw new ArgumentException($"The option {arg} needs a value.");
                    }
                    values[arg] = args[++i];
                }
                else
                {
                    throw new ArgumentException($"Unknown option: {arg}");
                }
            }
            return new CommandLineArguments(positionals, flags, values);
        }

        /// <summary>
        /// Gets the value of an option, or a default when the option is not given.
        /// </summary>
        /// <param name="name">The option name, for example <c>--engine</c>.</param>
        /// <param name="defaultValue">The value to return when the option is not given.</param>
        /// <returns>The option value or <paramref name="defaultValue"/>.</returns>
        public string? Value(string name, string? defaultValue = null) =>
            Values.TryGetValue(name, out string? value) ? value : defaultValue;
    }
}