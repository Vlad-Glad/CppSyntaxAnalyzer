/*
Використовуючи Moq, NSubstitute або інший імітаційний (мок) фреймворк потрібно написати тести, які б використовували мок об’єкти.
Для цього потрібно в проекті, для якого задаються тести провести рефакторинг — додати інтерфейс, винести зчитування з диску, доступ до БД та ін. в окремий клас, та утворити “зазор” для використання мок об’єктів.

Написані тести повинні відповідати наступним вимогам:
- для мок об’єкту задається принаймні 3 сценарії, один з яких буде стосуватись обробки виключень;
- має бути реалізована перевірка того, що методи було викликано певну кількість разів та в певному порядку;
- один сценарій за якого мок об’єкт має згенерувати виключення.
- один зі сценаріїв має використовувати засоби співставлення параметрів методу, що викликається для задання більш складної поведінки.
- один зі сценаріїв має задавати різні відповіді для кожного наступного виклику методу. 
*/

using System;
using Moq;
using Xunit;
using CppSyntaxAnalyzer;

namespace CppSyntaxAnalyzer.Tests;

public class AnalyzerRunnerTests
{
    [Fact]
    public void Run_WhenReadAllTextThrowsException_ShouldCatchAndReturnErrorCode5()
    {
        // Arrange
        var mockEnv = new Mock<IEnvironmentService>();

        mockEnv.Setup(e => e.GetSystemStatus()).Returns(1);
        mockEnv.Setup(e => e.FileExists(It.IsAny<string>())).Returns(true);

        mockEnv.Setup(e => e.ReadAllText(It.IsAny<string>())).Throws<UnauthorizedAccessException>();

        var runner = new AnalyzerRunner(mockEnv.Object);

        // Act
        var result = runner.Run(new[] { "test.cpp" });

        // Assert
        Assert.Equal(5, result);

        mockEnv.Verify(e => e.WriteError(It.Is<string>(s => s.Contains("IO Error"))), Times.Once);
    }

    [Fact]
    public void Run_WithValidSyntax_ShouldCallMethodsInCorrectOrderAndExactCount()
    {
        // Arrange
        var mockEnv = new Mock<IEnvironmentService>(MockBehavior.Strict);
        var sequence = new MockSequence();

        var code = "int main() { return 0; }";

        mockEnv.InSequence(sequence).Setup(e => e.GetSystemStatus()).Returns(1);
        mockEnv.InSequence(sequence).Setup(e => e.GetSystemStatus()).Returns(1);
        mockEnv.InSequence(sequence).Setup(e => e.FileExists("valid.cpp")).Returns(true);
        mockEnv.InSequence(sequence).Setup(e => e.ReadAllText("valid.cpp")).Returns(code);
        mockEnv.InSequence(sequence).Setup(e => e.WriteLine("Syntax OK (supported C++ subset)."));

        var runner = new AnalyzerRunner(mockEnv.Object);

        // Act
        var result = runner.Run(new[] { "valid.cpp" });

        // Assert
        Assert.Equal(0, result);

        mockEnv.Verify(e => e.GetSystemStatus(), Times.Exactly(2));
        mockEnv.Verify(e => e.FileExists(It.IsAny<string>()), Times.Once);
        mockEnv.Verify(e => e.ReadAllText(It.IsAny<string>()), Times.Once);
        mockEnv.Verify(e => e.WriteLine(It.IsAny<string>()), Times.Once);
        mockEnv.Verify(e => e.WriteError(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Run_FileExistsBehavior_DependsOnFileExtensionUsingParameterMatching()
    {
        // Arrange
        var mockEnv = new Mock<IEnvironmentService>();
        mockEnv.Setup(e => e.GetSystemStatus()).Returns(1);

        mockEnv.Setup(e => e.FileExists(It.Is<string>(path => path.EndsWith(".cpp")))).Returns(true);
        mockEnv.Setup(e => e.FileExists(It.Is<string>(path => !path.EndsWith(".cpp")))).Returns(false);

        var runner = new AnalyzerRunner(mockEnv.Object);

        // Act
        var result = runner.Run(new[] { "readme.txt" });

        // Assert
        Assert.Equal(1, result);
        mockEnv.Verify(e => e.WriteError(It.Is<string>(s => s.Contains("Cannot open file"))), Times.Once);
    }

    [Fact]
    public void Run_WhenSystemStatusChangesSequentially_ShouldReturnErrorCode4()
    {
        // Arrange
        var mockEnv = new Mock<IEnvironmentService>();

        mockEnv.SetupSequence(e => e.GetSystemStatus())
               .Returns(1)
               .Returns(2);

        var runner = new AnalyzerRunner(mockEnv.Object);

        // Act
        var result = runner.Run(new[] { "test.cpp" });

        // Assert
        Assert.Equal(4, result);

        mockEnv.Verify(e => e.FileExists(It.IsAny<string>()), Times.Never);
    }
}