namespace CppSyntaxAnalyzer;

public enum TokenKind
{
    Identifier,
    Keyword,
    IntLiteral,
    FloatLiteral,
    CharLiteral,
    StringLiteral,
    RawStringLiteral,
    Preprocessor,
    Comment,
    Operator,
    Punctuator,
    Error,
    EndOfFile
}

public readonly record struct SourcePosition(int Line, int Column);

public sealed record Token(
    TokenKind Kind,
    string Lexeme,
    SourcePosition Position,
    string Message = "")
{
    public override string ToString()
    {
        var escaped = Lexeme
            .Replace("\\", "\\\\")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");

        var suffix = string.IsNullOrWhiteSpace(Message) ? string.Empty : $" // {Message}";
        return $"<{escaped}, {Kind}, {Position.Line}:{Position.Column}>{suffix}";
    }
}
