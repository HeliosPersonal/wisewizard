using Microsoft.Extensions.Logging;
using WiseWizard.Core.Abstractions;

namespace WiseWizard.Core.Services;

/// <summary>
/// Retention cleanup for Raw documents (PRD §5 AC-08, §6 — 90-day window). Deletes documents
/// whose <c>fetched_at</c> is older than the retention window so the store does not grow without
/// bound, while keeping documents within the window available for auditing. "Now" comes from
/// <see cref="IClock"/>; deletion is delegated to the repository.
/// </summary>
public sealed class RetentionService(
    IRawDocumentRepository repository,
    IClock clock,
    ILogger<RetentionService> logger)
{
    /// <summary>Raw documents are kept for this long, then removed (PRD §6).</summary>
    public static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(90);

    private readonly IRawDocumentRepository _repository = repository;
    private readonly IClock _clock = clock;
    private readonly ILogger<RetentionService> _logger = logger;

    /// <summary>
    /// Removes Raw documents fetched before (now - retention window). Returns the number removed.
    /// </summary>
    public async Task<int> CleanupAsync(CancellationToken ct = default)
    {
        var cutoff = _clock.UtcNow - RetentionWindow;
        var removed = await _repository.DeleteOlderThanAsync(cutoff, ct);

        _logger.LogInformation(
            "Retention cleanup removed {Removed} raw documents fetched before {Cutoff:O}",
            removed, cutoff);

        return removed;
    }
}
