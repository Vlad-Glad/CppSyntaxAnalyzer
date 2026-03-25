namespace CppSyntaxAnalyzer;

public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _index;

    private static readonly HashSet<string> DeclarationKeywords = new(StringComparer.Ordinal)
    {
        "alignas","auto","class","const","constexpr","consteval","constinit","enum","explicit","extern","friend",
        "inline","mutable","register","signed","static","struct","template","thread_local","typedef","typename",
        "union","unsigned","using","virtual","volatile","void","bool","char","char8_t","char16_t","char32_t",
        "double","float","int","long","short","wchar_t"
    };

    private static readonly HashSet<string> ControlKeywords = new(StringComparer.Ordinal)
    {
        "if","else","while","for","switch","case","default","return","break","continue","do","try","catch","throw"
    };

    public Parser(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
    }

    public void ParseTranslationUnit()
    {
        while (!Check(TokenKind.EndOfFile))
        {
            ParseTopLevel();
        }
    }

    private void ParseTopLevel()
    {
        if (MatchKeyword("namespace"))
        {
            ParseNamespaceDefinition();
            return;
        }

        if (MatchKeyword("class") || MatchKeyword("struct") || MatchKeyword("union"))
        {
            ParseClassDefinition();
            return;
        }

        if (MatchKeyword("using"))
        {
            ParseUsingDeclaration();
            return;
        }

        ParseDeclarationOrFunction(allowFunctionBody: true, requireSemicolonForDeclaration: true);
    }

    private void ParseNamespaceDefinition()
    {
        if (Current.Kind == TokenKind.Identifier)
        {
            Advance();
        }

        if (MatchLexeme("="))
        {
            ParseQualifiedName();
            ExpectLexeme(";", "Expected ';' after namespace alias.");
            return;
        }

        ExpectLexeme("{", "Expected '{' after namespace.");
        while (!CheckLexeme("}"))
        {
            EnsureNotEof("Expected '}' to close namespace.");
            ParseTopLevel();
        }

        ExpectLexeme("}", "Expected '}' to close namespace.");
    }

    private void ParseClassDefinition()
    {
        ExpectIdentifier("Expected class/struct/union name.");

        if (MatchLexeme(":"))
        {
            ParseBaseSpecifierList();
        }

        ExpectLexeme("{", "Expected '{' after class header.");

        while (!CheckLexeme("}"))
        {
            EnsureNotEof("Expected '}' to close class body.");

            if (MatchKeyword("public") || MatchKeyword("private") || MatchKeyword("protected"))
            {
                ExpectLexeme(":", "Expected ':' after access specifier.");
                continue;
            }

            if (MatchKeyword("class") || MatchKeyword("struct") || MatchKeyword("union"))
            {
                ParseClassDefinition();
                continue;
            }

            ParseDeclarationOrFunction(allowFunctionBody: true, requireSemicolonForDeclaration: true);
        }

        ExpectLexeme("}", "Expected '}' to close class body.");
        ExpectLexeme(";", "Expected ';' after class definition.");
    }

    private void ParseBaseSpecifierList()
    {
        do
        {
            while (MatchKeyword("public") || MatchKeyword("private") || MatchKeyword("protected") || MatchKeyword("virtual"))
            {
            }

            ParseQualifiedName();
        }
        while (MatchLexeme(","));
    }

    private void ParseUsingDeclaration()
    {
        if (MatchKeyword("namespace"))
        {
            ParseQualifiedName();
            ExpectLexeme(";", "Expected ';' after using namespace declaration.");
            return;
        }

        if (Check(TokenKind.Identifier) && Peek().Lexeme == "=")
        {
            Advance();
            ExpectLexeme("=", "Expected '=' in alias declaration.");
            ParseTypeName();
            ExpectLexeme(";", "Expected ';' after alias declaration.");
            return;
        }

        ParseQualifiedName();
        ExpectLexeme(";", "Expected ';' after using declaration.");
    }

    private void ParseDeclarationOrFunction(bool allowFunctionBody, bool requireSemicolonForDeclaration)
    {
        ParseDeclarationPrefix();

        var isFunction = ParseDeclarator();

        if (isFunction)
        {
            while (MatchKeyword("const") || MatchKeyword("noexcept") || MatchKeyword("override") || MatchKeyword("final"))
            {
                if (Previous().Lexeme == "noexcept" && MatchLexeme("("))
                {
                    if (!CheckLexeme(")"))
                    {
                        ParseExpression();
                    }

                    ExpectLexeme(")", "Expected ')' after noexcept expression.");
                }
            }

            if (MatchLexeme("->"))
            {
                ParseTypeName();
            }

            if (allowFunctionBody && CheckLexeme("{"))
            {
                ParseCompoundStatement();
                return;
            }

            if (MatchLexeme("="))
            {
                if (!(MatchKeyword("default") || MatchKeyword("delete")))
                {
                    ParseExpression();
                }

                ExpectLexeme(";", "Expected ';' after function declaration.");
                return;
            }

            ExpectLexeme(";", "Expected ';' or function body after function declaration.");
            return;
        }

        ParseOptionalInitializer();

        while (MatchLexeme(","))
        {
            ParseDeclarator();
            ParseOptionalInitializer();
        }

        if (requireSemicolonForDeclaration)
        {
            ExpectLexeme(";", "Expected ';' after declaration.");
        }
    }

    private void ParseDeclarationPrefix()
    {
        if (MatchKeyword("template"))
        {
            ExpectLexeme("<", "Expected '<' after template.");
            ParseTemplateParameterList();
            ExpectTemplateClose("Expected '>' after template parameter list.");
        }

        var sawAny = false;

        while (IsDeclarationKeyword(Current))
        {
            sawAny = true;
            Advance();

            if (Previous().Lexeme == "class" || Previous().Lexeme == "struct" || Previous().Lexeme == "union" || Previous().Lexeme == "enum")
            {
                if (Current.Kind == TokenKind.Identifier)
                {
                    Advance();
                }
            }
        }

        if (!sawAny)
        {
            if (Current.Kind == TokenKind.Identifier || CheckLexeme("::"))
            {
                ParseTypeName();
                sawAny = true;
            }
        }
        else if (Current.Kind == TokenKind.Identifier &&
                 (Peek().Kind == TokenKind.Identifier ||
                  Peek().Lexeme == "::" ||
                  Peek().Lexeme == "<"))
        {
            ParseTypeName();
        }
        else if (CheckLexeme("::"))
        {
            ParseTypeName();
        }

        if (!sawAny)
        {
            Fail(Current, "Expected declaration specifier or type name.");
        }
    }

    private void ParseTypeName()
    {
        ParseQualifiedName();

        while (MatchLexeme("*") || MatchLexeme("&") || MatchLexeme("&&"))
        {
        }
    }

    private void ParseQualifiedName()
    {
        MatchLexeme("::");

        ExpectIdentifier("Expected identifier in qualified name.");

        while (true)
        {
            if (CheckLexeme("<"))
            {
                ParseTemplateArgumentList();
            }

            if (!MatchLexeme("::"))
            {
                break;
            }

            ExpectIdentifier("Expected identifier after '::'.");
        }
    }

    private void ParseTemplateParameterList()
    {
        if (CheckLexeme(">"))
        {
            return;
        }

        do
        {
            if (MatchKeyword("typename") || MatchKeyword("class"))
            {
                if (Current.Kind == TokenKind.Identifier)
                {
                    Advance();
                }

                if (MatchLexeme("="))
                {
                    ParseTypeName();
                }
            }
            else
            {
                ParseTypeName();

                if (Current.Kind == TokenKind.Identifier)
                {
                    Advance();
                }

                if (MatchLexeme("="))
                {
                    ParseExpression();
                }
            }
        }
        while (MatchLexeme(","));
    }

    private void ParseTemplateArgumentList()
    {
        ExpectLexeme("<", "Expected '<'.");

        var depth = 1;
        while (depth > 0)
        {
            EnsureNotEof("Expected '>' to close template arguments.");

            if (Current.Lexeme == "<")
            {
                depth++;
                Advance();
                continue;
            }

            if (Current.Lexeme == ">")
            {
                depth--;
                Advance();
                continue;
            }

            if (Current.Lexeme == ">>")
            {
                depth -= 2;
                Advance();
                continue;
            }

            if (Current.Lexeme == ">>=")
            {
                Fail(Current, "Unexpected '>>=' inside template argument list.");
            }

            Advance();
        }
    }

    private void ExpectTemplateClose(string message)
    {
        if (Current.Lexeme == ">")
        {
            Advance();
            return;
        }

        if (Current.Lexeme == ">>")
        {
            Advance();
            return;
        }

        Fail(Current, message);
    }

    private bool ParseDeclarator()
    {
        while (MatchLexeme("*") || MatchLexeme("&") || MatchLexeme("&&"))
        {
        }

        if (MatchLexeme("("))
        {
            var nestedIsFunction = ParseDeclarator();
            ExpectLexeme(")", "Expected ')' after declarator.");
            return ParseDeclaratorSuffix(nestedIsFunction);
        }

        ExpectIdentifier("Expected declarator name.");
        return ParseDeclaratorSuffix(false);
    }

    private bool ParseDeclaratorSuffix(bool alreadyFunction)
    {
        var isFunction = alreadyFunction;

        while (true)
        {
            if (MatchLexeme("("))
            {
                isFunction = true;
                ParseParameterList();
                ExpectLexeme(")", "Expected ')' after parameter list.");
                continue;
            }

            if (MatchLexeme("["))
            {
                if (!CheckLexeme("]"))
                {
                    ParseExpression();
                }

                ExpectLexeme("]", "Expected ']' after array declarator.");
                continue;
            }

            break;
        }

        return isFunction;
    }

    private void ParseParameterList()
    {
        if (CheckLexeme(")"))
        {
            return;
        }

        do
        {
            if (MatchLexeme("..."))
            {
                return;
            }

            ParseParameterDeclaration();
        }
        while (MatchLexeme(","));
    }

    private void ParseParameterDeclaration()
    {
        ParseDeclarationPrefix();

        if (Check(TokenKind.Identifier) || CheckLexeme("*") || CheckLexeme("&") || CheckLexeme("&&") || CheckLexeme("("))
        {
            var save = _index;
            try
            {
                ParseDeclarator();
            }
            catch (ParseException)
            {
                _index = save;
            }
        }

        if (MatchLexeme("="))
        {
            ParseExpression();
        }
    }

    private void ParseOptionalInitializer()
    {
        if (MatchLexeme("="))
        {
            ParseExpression();
            return;
        }

        if (CheckLexeme("{"))
        {
            ParseBracedInitializer();
        }
    }

    private void ParseBracedInitializer()
    {
        ExpectLexeme("{", "Expected '{'.");

        if (MatchLexeme("}"))
        {
            return;
        }

        do
        {
            if (CheckLexeme("{"))
            {
                ParseBracedInitializer();
            }
            else
            {
                ParseExpression();
            }
        }
        while (MatchLexeme(","));

        ExpectLexeme("}", "Expected '}' after braced initializer.");
    }

    private void ParseCompoundStatement()
    {
        ExpectLexeme("{", "Expected '{'.");

        while (!CheckLexeme("}"))
        {
            EnsureNotEof("Expected '}' to close block.");
            ParseStatement();
        }

        ExpectLexeme("}", "Expected '}' to close block.");
    }

    private void ParseStatement()
    {
        if (CheckLexeme("{"))
        {
            ParseCompoundStatement();
            return;
        }

        if (MatchKeyword("if"))
        {
            ParseParenthesizedExpression();
            ParseStatement();
            if (MatchKeyword("else"))
            {
                ParseStatement();
            }

            return;
        }

        if (MatchKeyword("while"))
        {
            ParseParenthesizedExpression();
            ParseStatement();
            return;
        }

        if (MatchKeyword("for"))
        {
            ParseForStatement();
            return;
        }

        if (MatchKeyword("switch"))
        {
            ParseParenthesizedExpression();
            ParseStatement();
            return;
        }

        if (MatchKeyword("do"))
        {
            ParseStatement();
            ExpectKeyword("while", "Expected 'while' after do-body.");
            ParseParenthesizedExpression();
            ExpectLexeme(";", "Expected ';' after do-while.");
            return;
        }

        if (MatchKeyword("try"))
        {
            ParseCompoundStatement();
            while (MatchKeyword("catch"))
            {
                ExpectLexeme("(", "Expected '(' after catch.");
                if (!CheckLexeme(")"))
                {
                    ParseParameterDeclaration();
                }

                ExpectLexeme(")", "Expected ')' after catch parameter.");
                ParseCompoundStatement();
            }

            return;
        }

        if (MatchKeyword("return"))
        {
            if (!CheckLexeme(";"))
            {
                ParseExpression();
            }

            ExpectLexeme(";", "Expected ';' after return.");
            return;
        }

        if (MatchKeyword("break") || MatchKeyword("continue"))
        {
            ExpectLexeme(";", "Expected ';'.");
            return;
        }

        if (MatchKeyword("case"))
        {
            ParseExpression();
            ExpectLexeme(":", "Expected ':' after case label.");
            return;
        }

        if (MatchKeyword("default"))
        {
            ExpectLexeme(":", "Expected ':' after default label.");
            return;
        }

        if (MatchKeyword("throw"))
        {
            if (!CheckLexeme(";"))
            {
                ParseExpression();
            }

            ExpectLexeme(";", "Expected ';' after throw.");
            return;
        }

        if (IsLikelyDeclarationStart())
        {
            ParseDeclarationOrFunction(allowFunctionBody: false, requireSemicolonForDeclaration: true);
            return;
        }

        if (MatchLexeme(";"))
        {
            return;
        }

        ParseExpression();
        ExpectLexeme(";", "Expected ';' after expression statement.");
    }

    private void ParseForStatement()
    {
        ExpectLexeme("(", "Expected '(' after for.");

        if (!CheckLexeme(";"))
        {
            if (IsLikelyDeclarationStart())
            {
                ParseDeclarationOrFunction(allowFunctionBody: false, requireSemicolonForDeclaration: true);
            }
            else
            {
                ParseExpression();
                ExpectLexeme(";", "Expected ';' after for-init.");
            }
        }
        else
        {
            ExpectLexeme(";", "Expected ';'.");
        }

        if (!CheckLexeme(";"))
        {
            ParseExpression();
        }

        ExpectLexeme(";", "Expected ';' after for-condition.");

        if (!CheckLexeme(")"))
        {
            ParseExpression();
        }

        ExpectLexeme(")", "Expected ')' after for-clause.");
        ParseStatement();
    }

    private void ParseParenthesizedExpression()
    {
        ExpectLexeme("(", "Expected '('.");
        if (!CheckLexeme(")"))
        {
            ParseExpression();
        }

        ExpectLexeme(")", "Expected ')'.");
    }

    private void ParseExpression() => ParseCommaExpression();

    private void ParseCommaExpression()
    {
        ParseAssignmentExpression();
        while (MatchLexeme(","))
        {
            ParseAssignmentExpression();
        }
    }

    private void ParseAssignmentExpression()
    {
        ParseConditionalExpression();

        if (MatchLexeme("=") || MatchLexeme("+=") || MatchLexeme("-=") || MatchLexeme("*=") ||
            MatchLexeme("/=") || MatchLexeme("%=") || MatchLexeme("&=") || MatchLexeme("|=") ||
            MatchLexeme("^=") || MatchLexeme("<<=") || MatchLexeme(">>="))
        {
            ParseAssignmentExpression();
        }
    }

    private void ParseConditionalExpression()
    {
        ParseLogicalOrExpression();

        if (MatchLexeme("?"))
        {
            ParseExpression();
            ExpectLexeme(":", "Expected ':' in conditional expression.");
            ParseAssignmentExpression();
        }
    }

    private void ParseLogicalOrExpression()
    {
        ParseLogicalAndExpression();
        while (MatchLexeme("||"))
        {
            ParseLogicalAndExpression();
        }
    }

    private void ParseLogicalAndExpression()
    {
        ParseBitwiseOrExpression();
        while (MatchLexeme("&&"))
        {
            ParseBitwiseOrExpression();
        }
    }

    private void ParseBitwiseOrExpression()
    {
        ParseBitwiseXorExpression();
        while (MatchLexeme("|"))
        {
            ParseBitwiseXorExpression();
        }
    }

    private void ParseBitwiseXorExpression()
    {
        ParseBitwiseAndExpression();
        while (MatchLexeme("^"))
        {
            ParseBitwiseAndExpression();
        }
    }

    private void ParseBitwiseAndExpression()
    {
        ParseEqualityExpression();
        while (MatchLexeme("&"))
        {
            ParseEqualityExpression();
        }
    }

    private void ParseEqualityExpression()
    {
        ParseRelationalExpression();
        while (MatchLexeme("==") || MatchLexeme("!="))
        {
            ParseRelationalExpression();
        }
    }

    private void ParseRelationalExpression()
    {
        ParseShiftExpression();
        while (MatchLexeme("<") || MatchLexeme(">") || MatchLexeme("<=") || MatchLexeme(">=") || MatchLexeme("<=>"))
        {
            ParseShiftExpression();
        }
    }

    private void ParseShiftExpression()
    {
        ParseAdditiveExpression();
        while (MatchLexeme("<<") || MatchLexeme(">>"))
        {
            ParseAdditiveExpression();
        }
    }

    private void ParseAdditiveExpression()
    {
        ParseMultiplicativeExpression();
        while (MatchLexeme("+") || MatchLexeme("-"))
        {
            ParseMultiplicativeExpression();
        }
    }

    private void ParseMultiplicativeExpression()
    {
        ParseUnaryExpression();
        while (MatchLexeme("*") || MatchLexeme("/") || MatchLexeme("%"))
        {
            ParseUnaryExpression();
        }
    }

    private void ParseUnaryExpression()
    {
        if (MatchLexeme("+") || MatchLexeme("-") || MatchLexeme("!") || MatchLexeme("~") ||
            MatchLexeme("*") || MatchLexeme("&") || MatchLexeme("++") || MatchLexeme("--"))
        {
            ParseUnaryExpression();
            return;
        }

        if (MatchKeyword("new") || MatchKeyword("delete") || MatchKeyword("sizeof") || MatchKeyword("typeid"))
        {
            if (MatchLexeme("("))
            {
                if (!CheckLexeme(")"))
                {
                    ParseExpression();
                }

                ExpectLexeme(")", "Expected ')' after unary keyword expression.");
            }
            else
            {
                ParseUnaryExpression();
            }

            return;
        }

        ParsePostfixExpression();
    }

    private void ParsePostfixExpression()
    {
        ParsePrimaryExpression();

        while (true)
        {
            if (MatchLexeme("("))
            {
                if (!CheckLexeme(")"))
                {
                    ParseArgumentList();
                }

                ExpectLexeme(")", "Expected ')' after argument list.");
                continue;
            }

            if (MatchLexeme("["))
            {
                if (!CheckLexeme("]"))
                {
                    ParseExpression();
                }

                ExpectLexeme("]", "Expected ']' after subscript.");
                continue;
            }

            if (MatchLexeme(".") || MatchLexeme("->") || MatchLexeme("::"))
            {
                ExpectIdentifier("Expected member name.");
                if (CheckLexeme("<"))
                {
                    ParseTemplateArgumentList();
                }

                continue;
            }

            if (MatchLexeme("++") || MatchLexeme("--"))
            {
                continue;
            }

            break;
        }
    }

    private void ParseArgumentList()
    {
        do
        {
            if (CheckLexeme("{"))
            {
                ParseBracedInitializer();
            }
            else
            {
                ParseExpression();
            }
        }
        while (MatchLexeme(","));
    }

    private void ParsePrimaryExpression()
    {
        if (Match(TokenKind.Identifier) ||
            Match(TokenKind.IntLiteral) ||
            Match(TokenKind.FloatLiteral) ||
            Match(TokenKind.StringLiteral) ||
            Match(TokenKind.RawStringLiteral) ||
            Match(TokenKind.CharLiteral))
        {
            return;
        }

        if (MatchKeyword("true") || MatchKeyword("false") || MatchKeyword("nullptr") || MatchKeyword("this"))
        {
            return;
        }

        if (MatchLexeme("("))
        {
            if (!CheckLexeme(")"))
            {
                ParseExpression();
            }

            ExpectLexeme(")", "Expected ')' after parenthesized expression.");
            return;
        }

        if (CheckLexeme("{"))
        {
            ParseBracedInitializer();
            return;
        }

        Fail(Current, "Expected primary expression.");
    }

    private bool IsLikelyDeclarationStart()
    {
        if (IsDeclarationKeyword(Current))
        {
            return true;
        }

        if (Current.Kind == TokenKind.Identifier && Peek().Kind == TokenKind.Identifier)
        {
            return true;
        }

        if (Current.Kind == TokenKind.Identifier && (Peek().Lexeme == "::" || Peek().Lexeme == "<"))
        {
            return true;
        }

        return false;
    }

    private bool IsDeclarationKeyword(Token token) =>
        token.Kind == TokenKind.Keyword && DeclarationKeywords.Contains(token.Lexeme) && !ControlKeywords.Contains(token.Lexeme);

    private bool Check(TokenKind kind) => Current.Kind == kind;

    private bool Match(TokenKind kind)
    {
        if (!Check(kind))
        {
            return false;
        }

        Advance();
        return true;
    }

    private bool MatchKeyword(string keyword)
    {
        if (!CheckKeyword(keyword))
        {
            return false;
        }

        Advance();
        return true;
    }

    private void ExpectKeyword(string keyword, string message)
    {
        if (!MatchKeyword(keyword))
        {
            Fail(Current, message);
        }
    }

    private bool CheckKeyword(string keyword) =>
        Current.Kind == TokenKind.Keyword && string.Equals(Current.Lexeme, keyword, StringComparison.Ordinal);

    private bool MatchLexeme(string lexeme)
    {
        if (!CheckLexeme(lexeme))
        {
            return false;
        }

        Advance();
        return true;
    }

    private bool CheckLexeme(string lexeme) =>
        string.Equals(Current.Lexeme, lexeme, StringComparison.Ordinal);

    private void ExpectLexeme(string lexeme, string message)
    {
        if (!MatchLexeme(lexeme))
        {
            Fail(Current, message);
        }
    }

    private void ExpectIdentifier(string message)
    {
        if (!Match(TokenKind.Identifier))
        {
            Fail(Current, message);
        }
    }

    private Token Current => _tokens[Math.Min(_index, _tokens.Count - 1)];

    private Token Peek(int offset = 1)
    {
        var idx = Math.Min(_index + offset, _tokens.Count - 1);
        return _tokens[idx];
    }

    private void Advance()
    {
        if (_index < _tokens.Count - 1)
        {
            _index++;
        }
    }

    private Token Previous()
    {
        var idx = Math.Max(0, _index - 1);
        return _tokens[idx];
    }

    private void EnsureNotEof(string message)
    {
        if (Check(TokenKind.EndOfFile))
        {
            Fail(Current, message);
        }
    }

    private static void Fail(Token token, string message) =>
        throw new ParseException($"{message} At {token.Position.Line}:{token.Position.Column}. Near '{token.Lexeme}'.");

    public sealed class ParseException : Exception
    {
        public ParseException(string message) : base(message)
        {
        }
    }
}
