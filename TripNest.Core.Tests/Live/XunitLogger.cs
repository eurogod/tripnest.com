using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace TripNest.Core.Tests.Live;

/// <summary>Forwards ILogger output (incl. the senders' provider errors) into xUnit test output.</summary>
public class XunitLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;
    public XunitLogger(ITestOutputHelper output) => _output = output;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        try
        {
            _output.WriteLine($"    [{logLevel}] {formatter(state, exception)}");
            if (exception != null)
                _output.WriteLine($"      → {exception.Message}");
        }
        catch { /* test output may be unavailable after the test ends */ }
    }
}
