namespace CppSyntaxAnalyzer;

internal static class Program
{
    private static int Main(string[] args)
    {
        IEnvironmentService realEnvironment = new RealEnvironmentService();

        var runner = new AnalyzerRunner(realEnvironment);

        return runner.Run(args);
    }
}
