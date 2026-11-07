using System.Text;

namespace MSMask.Cli
{
    /// <summary>
    /// The entry point of the command-line tool.
    /// </summary>
    /// <remarks>
    /// Commands:
    /// <list type="bullet">
    /// <item><description><c>mask &lt;input&gt; &lt;output&gt;</c>: masks one image (see <see cref="MaskCommand"/>).</description></item>
    /// <item><description><c>eval &lt;labels.json&gt;</c>: measures the pipeline on a labeled data set (see <see cref="EvalCommand"/>).</description></item>
    /// </list>
    /// </remarks>
    internal static class Program
    {
        /// <summary>
        /// Runs the command named by the first argument.
        /// </summary>
        /// <param name="args">The command name followed by its arguments.</param>
        /// <returns>The process exit code: 0 on success, 1 on error, 2 for wrong usage.</returns>
        private static async Task<int> Main(string[] args)
        {
            // Turkish characters in file names and OCR text must survive the console.
            Console.OutputEncoding = Encoding.UTF8;

            if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
            {
                PrintUsage();
                return args.Length == 0 ? 2 : 0;
            }

            string[] rest = args[1..];
            try
            {
                return args[0] switch
                {
                    "mask" => await MaskCommand.RunAsync(rest),
                    "eval" => await EvalCommand.RunAsync(rest),
                    _ => UnknownCommand(args[0]),
                };
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException or FormatException)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Reports an unknown command.
        /// </summary>
        /// <param name="name">The command name that was given.</param>
        /// <returns>The exit code for wrong usage (2).</returns>
        private static int UnknownCommand(string name)
        {
            Console.Error.WriteLine($"Unknown command: {name}");
            PrintUsage();
            return 2;
        }

        /// <summary>
        /// Prints the list of commands.
        /// </summary>
        private static void PrintUsage()
        {
            Console.WriteLine("MSMask - masks sensitive data in screenshots");
            Console.WriteLine();
            Console.WriteLine("  mask <input> <output> [--engine tesseract] [--no-checksum] [--ner] [--ner-model dir]");
            Console.WriteLine("  eval <labels.json> [--engine tesseract] [--no-checksum] [--ner] [--ner-model dir] [--all-kinds] [--limit N] [--out prefix]");
        }
    }
}