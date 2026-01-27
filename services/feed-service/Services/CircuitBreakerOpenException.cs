namespace FeedService.Services;

public sealed class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string dependency)
        : base($"Circuit breaker open for dependency: {dependency}.")
    {
        Dependency = dependency;
    }

    public string Dependency { get; }
}
