using NetWatcher.Logging;

namespace NetWatcher.Tests;

public sealed class FileLoggerTests
{
    [Fact]
    public void FileLogger_CreatesDirectoryAndAppendsLine()
    {
        var directory = CreateTempDirectory();
        try
        {
            var logger = new FileLogger(directory);

            logger.Info("hello");

            var file = Assert.Single(Directory.GetFiles(directory));
            Assert.Contains("hello", File.ReadAllText(file));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FileLogger_IsThreadSafe()
    {
        var directory = CreateTempDirectory();
        try
        {
            var logger = new FileLogger(directory);

            Parallel.For(0, 100, i => logger.Info($"line {i}"));

            var file = Assert.Single(Directory.GetFiles(directory));
            var lines = File.ReadAllLines(file).Count(line => line.Length > 0);
            Assert.Equal(100, lines);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory() =>
        Path.Combine(Path.GetTempPath(), "netwatcher-tests-" + Guid.NewGuid().ToString("N"));
}
