namespace CppSyntaxAnalyzer;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var printTokens = args.Contains("--tokens", StringComparer.Ordinal);
        var path = args.FirstOrDefault(a => !string.Equals(a, "--tokens", StringComparison.Ordinal));

        if (string.IsNullOrWhiteSpace(path))
        {
            PrintUsage();
            return 1;
        }

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Cannot open file: {path}");
            return 1;
        }

        var source = File.ReadAllText(path);
        var lexer = new Lexer(source);
        var tokens = lexer.TokenizeAll(stopOnError: true);

        foreach (var token in tokens.Where(t => t.Kind == TokenKind.Error))
        {
            Console.Error.WriteLine(token);
            return 2;
        }

        if (printTokens)
        {
            foreach (var token in tokens)
            {
                Console.WriteLine(token);
            }
        }

        var parserTokens = tokens
            .Where(t => t.Kind is not TokenKind.Comment and not TokenKind.Preprocessor)
            .ToList();

        try
        {
            var parser = new Parser(parserTokens);
            parser.ParseTranslationUnit();
            Console.WriteLine("Syntax OK (supported C++ subset).");
            return 0;
        }
        catch (Parser.ParseException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 3;
        }
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  dotnet run -- <path-to-file.cpp>");
        Console.Error.WriteLine("  dotnet run -- --tokens <path-to-file.cpp>");
    }
}
