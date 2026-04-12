
namespace CppSyntaxAnalyzer;

public interface IEnvironmentService
{
    bool FileExists(string path);
    string ReadAllText(string path);

    void WriteLine(string message);
    void WriteError(string message);

    int GetSystemStatus();
}