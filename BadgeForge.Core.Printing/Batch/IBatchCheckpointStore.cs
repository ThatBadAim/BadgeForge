namespace BadgeForge.Core.Printing.Batch;

/// <summary>
/// Interface for persisting and loading batch run checkpoints to disk.
/// Guarantees safe resumption following application crashes, jams, or operator halts.
/// </summary>
public interface IBatchCheckpointStore
{
    /// <summary>
    /// Atomically persists the current checkpoint state to disk.
    /// </summary>
    Task SaveCheckpointAsync(BatchCheckpoint checkpoint, string filePath, CancellationToken ct = default);

    /// <summary>
    /// Loads an existing checkpoint from disk.
    /// </summary>
    Task<BatchCheckpoint?> LoadCheckpointAsync(string filePath, CancellationToken ct = default);
}
