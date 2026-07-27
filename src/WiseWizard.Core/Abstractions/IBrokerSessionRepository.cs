using WiseWizard.Core.Models;

namespace WiseWizard.Core.Abstractions;

/// <summary>Persistence for the singleton Brokerage session state.</summary>
public interface IBrokerSessionRepository
{
    /// <summary>Reads the current session state, returning an <c>Unknown</c> default if none stored yet.</summary>
    Task<BrokerSessionState> GetAsync(CancellationToken ct = default);

    /// <summary>Writes the singleton session state.</summary>
    Task SaveAsync(BrokerSessionState state, CancellationToken ct = default);
}
