
using Microsoft.Extensions.Logging;

namespace OnanGensetControl.Tests;

internal class TestLoggerFactory : ILoggerFactory
{
    public void AddProvider(ILoggerProvider provider)
    {
        
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new DebugLogger();
    }

    public void Dispose()
    {
    }
}
