namespace WiseWizard.Core.Abstractions;

/// <summary>
/// Abstraction over the current time so time-dependent logic (snapshot age, staleness,
/// single-alert-per-lapse guards) is deterministically testable.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Default clock backed by the system wall clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
