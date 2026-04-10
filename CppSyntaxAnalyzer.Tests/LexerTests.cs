using Xunit;
using CppSyntaxAnalyzer;
using System.Linq;
using System.ComponentModel.DataAnnotations;

namespace CppSyntaxAnalyzer.Tests;

public class LexerTests
{
    // Parameterized test
    [Theory]
    [InlineData("int", TokenKind.Keyword)]
    [InlineData("return", TokenKind.Keyword)]
    [InlineData("42", TokenKind.IntLiteral)]
    [InlineData("3.14", TokenKind.FloatLiteral)]
    [InlineData("++", TokenKind.Operator)]
    [InlineData("my_var", TokenKind.Identifier)]
    [InlineData("{", TokenKind.Punctuator)]
    public void GetNextToken_SingleLexeme_ReturnCorrectKind(string source, TokenKind expectedKind)
    {
        var lexer = new Lexer(source);
        var token = lexer.GetNextToken();

        Assert.Equal(expectedKind, token.Kind);
        Assert.Equal(source, token.Lexeme);
    }

    // Complex Assert (collections)
    [Fact]
    public void TokenizeAll_SimpleStatement_ReturnsCorrectTokenSequence()
    {
        var source = "int x = 10;";
        var lexer = new Lexer(source);

        var tokens = lexer.TokenizeAll();

        Assert.NotNull(tokens);
        Assert.Collection(tokens,
            t => { Assert.Equal(TokenKind.Keyword, t.Kind); Assert.Equal("int", t.Lexeme); },
            t => { Assert.Equal(TokenKind.Identifier, t.Kind); Assert.Equal("x", t.Lexeme); },
            t => { Assert.Equal(TokenKind.Operator, t.Kind); Assert.Equal("=", t.Lexeme); },
            t => { Assert.Equal(TokenKind.IntLiteral, t.Kind); Assert.Equal("10", t.Lexeme); },
            t => { Assert.Equal(TokenKind.Operator, t.Kind); Assert.Equal(";", t.Lexeme); },
            t => { Assert.Equal(TokenKind.EndOfFile, t.Kind); }
        );
    }

    //Complex Assert (lines)
    [Fact]
    public void TokenizeAll_UnterminatedString_ReturnsErrorTokenWithMessage()
    {
        var source = "\"unterminated";
        var lexer = new Lexer(source);

        var tokens = lexer.TokenizeAll();
        var errorToken = tokens.First();

        Assert.Equal(TokenKind.Error, errorToken.Kind);

        Assert.StartsWith("Unterminated", errorToken.Message);
    }

    //Exception test
    [Fact]
    public void Constructor_NullSource_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new Lexer(null));

        Assert.Contains("Source code cannot be null", ex.Message);
    }

    [Theory]
    [InlineData("#include <iostream>\n// Line comment\n/* Block comment */")]
    [InlineData("int hex = 0xFF; float f = 3.14f; char c = 'a';")]
    [InlineData("string s = \"test\"; string raw = R\"(raw string)\";")]
    [InlineData("if (hex == 0xFF && f >= 3.0f || !false) { hex++; }")]
    public void TokenizeAll_ValidLanguageFeatures_IncreasesCoverageAndGeneratesNoErrors(string source)
    {
        var lexer = new Lexer(source);

        var tokens = lexer.TokenizeAll();

        Assert.NotEmpty(tokens);
        Assert.DoesNotContain(tokens, t => t.Kind == TokenKind.Error);
    }
}