using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using CppSyntaxAnalyzer;
using Newtonsoft.Json.Bson;

namespace CppSyntaxAnalyzer.Tests;

public class ParserTests: IDisposable
{
    private readonly List<Token> _baseTokens;

    //Setup (Fixture)
    public ParserTests()
    {
        _baseTokens = GetTokensFromSource("int main() {return 0; }");
    }

    public void Dispose()
    {
        _baseTokens.Clear();
    }

    private List<Token> GetTokensFromSource(string source)
    {
        var lexer = new Lexer(source);
        return lexer.TokenizeAll(stopOnError: true)
            .Where(t => t.Kind is not TokenKind.Comment and not TokenKind.Preprocessor)
            .ToList();
    }

    [Fact]
    public void ParseTranslationUnit_ValidBaseToknes_DoesNotThrough()
    {
        var parser = new Parser(_baseTokens);

        var exception = Record.Exception(() => parser.ParseTranslationUnit());

        Assert.Null(exception);
    }

    [Fact]
    public void ParseTranslationUnit_MissingSemicolon_ThrowsParseException()
    {
        var tokens = GetTokensFromSource("int x = 10");
        var parser = new Parser(tokens);

        var ex = Assert.Throws<Parser.ParseException>(() => parser.ParseTranslationUnit());

        Assert.Contains("Expected ';'", ex.Message);
    }

    [Theory]
    [InlineData("class { };", "Expected class/struct/union name.")]
    [InlineData("namespace demo = ;", "Expected identifier in qualified name.")]
    public void ParseTranslationUnit_InvalidSyntax_ThrowsAndChecksMessage(string invalidCode, string expectedErrorSubString)
    {
        var tokens = GetTokensFromSource(invalidCode);
        var parser = new Parser(tokens);

        var ex = Assert.Throws<Parser.ParseException>(() => parser.ParseTranslationUnit());
        Assert.Contains(expectedErrorSubString, ex.Message);
    }
}