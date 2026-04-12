
namespace CppSyntaxAnalyzer;

public class RealEnvironmentService : IEnvironmentService
{
    public bool FileExists(string path) => File.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteLine(string message) => Console.WriteLine(message);

    public void WriteError(string message) => Console.WriteLine(message);

    public int GetSystemStatus() => 1;
}