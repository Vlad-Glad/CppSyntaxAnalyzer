using System;
using System.Linq;

namespace CppSyntaxAnalyzer;

public class AnalyzerRunner
{
    private IEnvironmentService _env;

    public AnalyzerRunner(IEnvironmentService env)
    {
        _env = env;
    }

    public int Run(string[] args)
    {
        if (args.Length == 0)
        {
            _env.WriteError("Usage:");
            _env.WriteError(" dotnet run -- <path-to-file.cpp>");
            _env.WriteError(" dotnet run -- --tokens <path-to-file.cpp>");
            return 1;
        }

        var printTokens = args.Contains("--tokens", StringComparer.Ordinal);
        var path = args.FirstOrDefault(a => !string.Equals(a, "--tokens", StringComparison.Ordinal));

        if (string.IsNullOrWhiteSpace(path))
        {
            _env.WriteError("Usage error: path is missing.");
            return 1;
        }

        int status1 = _env.GetSystemStatus();
        int status2 = _env.GetSystemStatus();
        if (status1 != status2)
        {
            _env.WriteError("System is unstable.");
            return 4;
        }

        if (!_env.FileExists(path))
        {
            _env.WriteError($"Cannot open file: {path}");
            return 1;
        }

        string source;
        try
        {
            source = _env.ReadAllText(path);
        }
        catch (Exception ex)
        {
            _env.WriteError($"IO Error: {ex.Message}");
            return 5;
        }

        var lexer = new Lexer(source);
        var tokens = lexer.TokenizeAll(stopOnError: true);

        if (tokens.Any(t => t.Kind == TokenKind.Error))
        {
            var firstError = tokens.First(t => t.Kind == TokenKind.Error);
            _env.WriteError(firstError.ToString());
            return 2;
        }

        if (printTokens)
        {
            foreach (var token in tokens)
            {
                _env.WriteLine(token.ToString());
            }
        }

        var parserTokens = tokens
            .Where(t => t.Kind is not TokenKind.Comment and not TokenKind.Preprocessor)
            .ToList();

        try
        {
            var parser = new Parser(parserTokens);
            parser.ParseTranslationUnit();
            _env.WriteLine("Syntax OK (supported C++ subset).");
            return 0;
        }
        catch (Parser.ParseException ex)
        {
            _env.WriteError(ex.Message);
            return 3;
        }
    }
}