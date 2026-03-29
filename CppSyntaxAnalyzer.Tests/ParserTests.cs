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
    [InlineData("namespace MyMath { int add(int a, int b); }")]
    [InlineData("class Point : public Base { public: int x; protected: int y; };")]
    [InlineData("template <typename T> void swap(T a, T b) {}")]
    [InlineData("using namespace std;")]
    public void ParseTranslationUnit_ValidDeclarations_ParsesSuccessfully(string code)
    {
        var parser = new Parser(GetTokensFromSource(code));
        var exception = Record.Exception(() => parser.ParseTranslationUnit());

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("int main() { if(true) { x = 1; } else { x = 2; } }")]
    [InlineData("int main() { while(false) { break; } }")]
    [InlineData("int main() { do { continue; } while(true); }")]
    [InlineData("int main() { for(int i = 0; i < 10; ++i) { } }")]
    [InlineData("int main() { switch(x) { case 1: return 1; default: return 0; } }")]
    [InlineData("int main() { try { throw 42; } catch(int e) { } }")]
    public void ParseTranslationUnit_ControlFlowStatements_ParsesSuccessfully(string code)
    {
        var parser = new Parser(GetTokensFromSource(code));
        var exception = Record.Exception(() => parser.ParseTranslationUnit());

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("int main() { int a = 1 + 2 * 3 - 4 / 2; }")]
    [InlineData("int main() { bool b = x == y && z != k || a >= b; }")]
    [InlineData("int main() { int c = x << 1 >> 2; }")]
    [InlineData("int main() { int d = a & b | c ^ d; }")]
    [InlineData("int main() { int e = (x > 0) ? x : -x; }")]
    [InlineData("int main() { a += 1; b -= 2; c *= 3; }")]
    [InlineData("int main() { x++; ++x; y--; --y; }")]
    [InlineData("int main() { int* p = &x; int v = *p; }")]
    [InlineData("int main() { obj.method(); ptr->method(); arr[0] = 1; }")]
    [InlineData("int main() { std::vector<int> v{1, 2, 3}; }")]
    public void ParseTranslationUnit_ExpressionsAndOperators_ParsesSuccessfully(string code)
    {
        var parser = new Parser(GetTokensFromSource(code));
        var exception = Record.Exception(() => parser.ParseTranslationUnit());

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("class { };", "Expected class/struct/union name.")]
    [InlineData("namespace demo = ;", "Expected identifier in qualified name.")]
    [InlineData("int main() { for(int i = 0; i < 10 ) { } }", "Expected ';'")]
    [InlineData("int main() { if (x > 0 { } }", "Expected ')'")]
    public void ParseTranslationUnit_InvalidSyntax_ThrowsExpectedError(string invalidCode, string expectedErrorSubString)
    {
        var tokens = GetTokensFromSource(invalidCode);
        var parser = new Parser(tokens);

        var ex = Assert.Throws<Parser.ParseException>(() => parser.ParseTranslationUnit());
        Assert.Contains(expectedErrorSubString, ex.Message);
    }
}