using System.Text;

namespace CppSyntaxAnalyzer;

public sealed class Lexer
{
    private readonly string _source;
    private int _index;
    private int _line = 1;
    private int _column = 1;

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "alignas","alignof","and","and_eq","asm","auto","bitand","bitor","bool","break","case","catch",
        "char","char8_t","char16_t","char32_t","class","compl","concept","const","consteval","constexpr",
        "constinit","const_cast","continue","co_await","co_return","co_yield","decltype","default","delete",
        "do","double","dynamic_cast","else","enum","explicit","export","extern","false","float","for","friend",
        "goto","if","inline","int","long","mutable","namespace","new","noexcept","not","not_eq","nullptr",
        "operator","or","or_eq","private","protected","public","register","reinterpret_cast","requires","return",
        "short","signed","sizeof","static","static_assert","static_cast","struct","switch","template","this",
        "thread_local","throw","true","try","typedef","typeid","typename","union","unsigned","using","virtual",
        "void","volatile","wchar_t","while","xor","xor_eq"
    };

    private static readonly string[] Operators =
    {
        "<=>","->*","<<=",">>=","...","::","->","++","--","+=","-=","*=","/=","%=",
        "&=","|=","^=","==","!=","<=",">=","&&","||","<<",">>","##",".*","?",";",":",".",
        "=","+","-","*","/","%","!","~","<",">","&","|","^","#",","
    };

    private static readonly string[] Punctuators =
    {
        "(",")","{","}","[","]"
    };

    public Lexer(string source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source), "Source code cannot be null");
        }

        _source = source;
    }

    public List<Token> TokenizeAll(bool stopOnError = true)
    {
        var result = new List<Token>();

        while (true)
        {
            var token = GetNextToken();
            result.Add(token);

            if (token.Kind == TokenKind.EndOfFile)
            {
                break;
            }

            if (stopOnError && token.Kind == TokenKind.Error)
            {
                break;
            }
        }

        return result;
    }

    public Token GetNextToken()
    {
        SkipWhitespace();

        if (IsEndOfInput())
        {
            return MakeToken(TokenKind.EndOfFile, string.Empty, CurrentPosition());
        }

        var start = CurrentPosition();

        if (Current == '#')
        {
            return LexPreprocessor(start);
        }

        if (StartsWith("//"))
        {
            return LexLineComment(start);
        }

        if (StartsWith("/*"))
        {
            return LexBlockComment(start);
        }

        if (TryDetectRawStringPrefix(out var rawPrefixLength))
        {
            return LexRawString(start, rawPrefixLength);
        }

        if (TryDetectStringPrefix(out var stringPrefixLength))
        {
            return LexQuotedLiteral(start, stringPrefixLength, '"', TokenKind.StringLiteral, "Unterminated string literal");
        }

        if (TryDetectCharPrefix(out var charPrefixLength))
        {
            return LexQuotedLiteral(start, charPrefixLength, '\'', TokenKind.CharLiteral, "Unterminated char literal");
        }

        if (char.IsDigit(Current) || (Current == '.' && char.IsDigit(Peek())))
        {
            return LexNumber(start);
        }

        if (IsIdentifierStart(Current))
        {
            return LexIdentifierOrKeyword(start);
        }

        foreach (var op in Operators)
        {
            if (StartsWith(op))
            {
                Advance(op.Length);
                return MakeToken(TokenKind.Operator, op, start);
            }
        }

        foreach (var punctuator in Punctuators)
        {
            if (StartsWith(punctuator))
            {
                Advance(punctuator.Length);
                return MakeToken(TokenKind.Punctuator, punctuator, start);
            }
        }

        var bad = Current.ToString();
        Advance();
        return MakeToken(TokenKind.Error, bad, start, "Unknown token");
    }

    private Token LexPreprocessor(SourcePosition start)
    {
        var sb = new StringBuilder();

        while (!IsEndOfInput())
        {
            if (Current == '\\' && Peek() == '\n')
            {
                sb.Append(Current);
                Advance();
                sb.Append(Current);
                Advance();
                continue;
            }

            if (Current == '\r' && Peek() == '\n')
            {
                break;
            }

            if (Current == '\n')
            {
                break;
            }

            sb.Append(Current);
            Advance();
        }

        return MakeToken(TokenKind.Preprocessor, sb.ToString(), start);
    }

    private Token LexLineComment(SourcePosition start)
    {
        var sb = new StringBuilder();
        sb.Append(Current);
        Advance();
        sb.Append(Current);
        Advance();

        while (!IsEndOfInput() && Current != '\n')
        {
            sb.Append(Current);
            Advance();
        }

        return MakeToken(TokenKind.Comment, sb.ToString(), start);
    }

    private Token LexBlockComment(SourcePosition start)
    {
        var sb = new StringBuilder();
        sb.Append(Current);
        Advance();
        sb.Append(Current);
        Advance();

        while (!IsEndOfInput())
        {
            if (StartsWith("*/"))
            {
                sb.Append(Current);
                Advance();
                sb.Append(Current);
                Advance();
                return MakeToken(TokenKind.Comment, sb.ToString(), start);
            }

            sb.Append(Current);
            Advance();
        }

        return MakeToken(TokenKind.Error, sb.ToString(), start, "Unterminated block comment");
    }

    private Token LexRawString(SourcePosition start, int prefixLength)
    {
        var initial = ReadAndAdvance(prefixLength);
        if (Current != '"')
        {
            return MakeToken(TokenKind.Error, initial, start, "Malformed raw string literal");
        }

        var sb = new StringBuilder(initial);
        sb.Append(Current);
        Advance();

        var delimiter = new StringBuilder();
        while (!IsEndOfInput() && Current != '(' && Current != '\n')
        {
            delimiter.Append(Current);
            sb.Append(Current);
            Advance();
        }

        if (IsEndOfInput() || Current != '(')
        {
            return MakeToken(TokenKind.Error, sb.ToString(), start, "Malformed raw string literal");
        }

        sb.Append(Current);
        Advance();

        var closing = ")" + delimiter + "\"";

        while (!IsEndOfInput())
        {
            if (StartsWith(closing))
            {
                sb.Append(closing);
                Advance(closing.Length);
                return MakeToken(TokenKind.RawStringLiteral, sb.ToString(), start);
            }

            sb.Append(Current);
            Advance();
        }

        return MakeToken(TokenKind.Error, sb.ToString(), start, "Unterminated raw string literal");
    }

    private Token LexQuotedLiteral(
        SourcePosition start,
        int prefixLength,
        char quote,
        TokenKind kind,
        string errorMessage)
    {
        var sb = new StringBuilder();

        if (prefixLength > 0)
        {
            sb.Append(ReadAndAdvance(prefixLength));
        }

        if (Current != quote)
        {
            return MakeToken(TokenKind.Error, sb.ToString(), start, errorMessage);
        }

        sb.Append(Current);
        Advance();

        var escaped = false;

        while (!IsEndOfInput())
        {
            var ch = Current;
            sb.Append(ch);
            Advance();

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (ch == '\\')
            {
                escaped = true;
                continue;
            }

            if (ch == quote)
            {
                return MakeToken(kind, sb.ToString(), start);
            }

            if (ch == '\n')
            {
                return MakeToken(TokenKind.Error, sb.ToString(), start, errorMessage);
            }
        }

        return MakeToken(TokenKind.Error, sb.ToString(), start, errorMessage);
    }

    private Token LexNumber(SourcePosition start)
    {
        var sb = new StringBuilder();
        bool isFloat = false;

        if (StartsWith("0x") || StartsWith("0X"))
        {
            sb.Append(ReadAndAdvance(2));
            while (IsHexDigit(Current) || Current == '\'')
            {
                sb.Append(Current);
                Advance();
            }

            while (char.IsLetter(Current))
            {
                sb.Append(Current);
                Advance();
            }

            return MakeToken(TokenKind.IntLiteral, sb.ToString(), start);
        }

        if (StartsWith("0b") || StartsWith("0B"))
        {
            sb.Append(ReadAndAdvance(2));
            while (Current is '0' or '1' or '\'')
            {
                sb.Append(Current);
                Advance();
            }

            while (char.IsLetter(Current))
            {
                sb.Append(Current);
                Advance();
            }

            return MakeToken(TokenKind.IntLiteral, sb.ToString(), start);
        }

        while (char.IsDigit(Current) || Current == '\'')
        {
            sb.Append(Current);
            Advance();
        }

        if (Current == '.')
        {
            isFloat = true;
            sb.Append(Current);
            Advance();

            while (char.IsDigit(Current) || Current == '\'')
            {
                sb.Append(Current);
                Advance();
            }
        }

        if (Current is 'e' or 'E' or 'p' or 'P')
        {
            isFloat = true;
            sb.Append(Current);
            Advance();

            if (Current is '+' or '-')
            {
                sb.Append(Current);
                Advance();
            }

            while (char.IsDigit(Current) || Current == '\'')
            {
                sb.Append(Current);
                Advance();
            }
        }

        while (char.IsLetter(Current))
        {
            sb.Append(Current);
            Advance();
        }

        return MakeToken(isFloat ? TokenKind.FloatLiteral : TokenKind.IntLiteral, sb.ToString(), start);
    }

    private Token LexIdentifierOrKeyword(SourcePosition start)
    {
        var sb = new StringBuilder();
        sb.Append(Current);
        Advance();

        while (IsIdentifierPart(Current))
        {
            sb.Append(Current);
            Advance();
        }

        var lexeme = sb.ToString();
        var kind = Keywords.Contains(lexeme) ? TokenKind.Keyword : TokenKind.Identifier;
        return MakeToken(kind, lexeme, start);
    }

    private bool TryDetectRawStringPrefix(out int prefixLength)
    {
        prefixLength = 0;
        foreach (var prefix in new[] { "u8R", "uR", "UR", "LR", "R" })
        {
            if (StartsWith(prefix) && Peek(prefix.Length) == '"')
            {
                prefixLength = prefix.Length;
                return true;
            }
        }

        return false;
    }

    private bool TryDetectStringPrefix(out int prefixLength)
    {
        prefixLength = 0;

        if (Current == '"')
        {
            return true;
        }

        foreach (var prefix in new[] { "u8", "u", "U", "L" })
        {
            if (StartsWith(prefix) && Peek(prefix.Length) == '"')
            {
                prefixLength = prefix.Length;
                return true;
            }
        }

        return false;
    }

    private bool TryDetectCharPrefix(out int prefixLength)
    {
        prefixLength = 0;

        if (Current == '\'')
        {
            return true;
        }

        foreach (var prefix in new[] { "u8", "u", "U", "L" })
        {
            if (StartsWith(prefix) && Peek(prefix.Length) == '\'')
            {
                prefixLength = prefix.Length;
                return true;
            }
        }

        return false;
    }

    private void SkipWhitespace()
    {
        while (!IsEndOfInput() && char.IsWhiteSpace(Current))
        {
            Advance();
        }
    }

    private char Current => _index < _source.Length ? _source[_index] : '\0';

    private char Peek(int offset = 1)
    {
        var idx = _index + offset;
        return idx >= 0 && idx < _source.Length ? _source[idx] : '\0';
    }

    private bool StartsWith(string value)
    {
        if (_index + value.Length > _source.Length)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            if (_source[_index + i] != value[i])
            {
                return false;
            }
        }

        return true;
    }

    private void Advance(int count = 1)
    {
        for (var i = 0; i < count && _index < _source.Length; i++)
        {
            var ch = _source[_index++];
            if (ch == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
        }
    }

    private string ReadAndAdvance(int count)
    {
        var safeCount = Math.Max(0, Math.Min(count, _source.Length - _index));
        var text = _source.Substring(_index, safeCount);
        Advance(safeCount);
        return text;
    }

    private SourcePosition CurrentPosition() => new(_line, _column);

    private bool IsEndOfInput() => _index >= _source.Length;

    private static bool IsIdentifierStart(char ch) => ch == '_' || char.IsLetter(ch);

    private static bool IsIdentifierPart(char ch) => ch == '_' || char.IsLetterOrDigit(ch);

    private static bool IsHexDigit(char ch) =>
        (ch >= '0' && ch <= '9') ||
        (ch >= 'a' && ch <= 'f') ||
        (ch >= 'A' && ch <= 'F');

    private static Token MakeToken(TokenKind kind, string lexeme, SourcePosition position, string message = "") =>
        new(kind, lexeme, position, message);
}
