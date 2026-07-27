using WiseWizard.Core.Models;

namespace WiseWizard.Core.Abstractions;

/// <summary>
/// Read-only access to the Broker. This interface deliberately exposes NO order-placing
/// capability — the read-only invariant is expressed at the type level (ADR-0002).
/// </summary>
public interface IBrokerReader
{
    /// <summary>Reads the Owner's current Positions from the live Brokerage session.</summary>
    Task<IReadOnlyList<Position>> ReadPositionsAsync(CancellationToken ct = default);

    /// <summary>Reports whether the Brokerage session is currently authenticated.</summary>
    Task<bool> IsSessionLiveAsync(CancellationToken ct = default);

    /// <summary>Sends a keep-alive ping to hold the session open. Returns true if still authenticated.</summary>
    Task<bool> KeepAliveAsync(CancellationToken ct = default);
}
