namespace Musiq.Domain.Entities;

public sealed record ScanLocation(
    long Id,
    string Path,
    bool IsEnabled = true,
    DateTimeOffset? LastScanUtc = null);
