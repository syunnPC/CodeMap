namespace CodeMap.Storage;

public sealed record SnapshotPersistenceResult(
    long SnapshotId,
    string DatabasePath);
