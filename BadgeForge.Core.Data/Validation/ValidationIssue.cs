namespace BadgeForge.Core.Data.Validation;

/// <summary>
/// Severity level of a validation issue.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Non-critical issue (e.g. missing optional photo with fallback).
    /// Can be bypassed explicitly by the operator.
    /// </summary>
    Warning,

    /// <summary>
    /// Critical error that corrupts or invalidates a printed badge.
    /// Cannot be bypassed by the operator.
    /// </summary>
    Error
}

/// <summary>
/// Represents a specific data or asset discrepancy detected during the pre-flight check.
/// </summary>
public record ValidationIssue
{
    /// <summary>
    /// Severity of the issue (Warning or Error).
    /// </summary>
    public ValidationSeverity Severity { get; init; } = ValidationSeverity.Error;

    /// <summary>
    /// Physical CSV row number if associated with a specific record.
    /// </summary>
    public int? RowNumber { get; init; }

    /// <summary>
    /// Zero-based record index in the batch.
    /// </summary>
    public int? BatchIndex { get; init; }

    /// <summary>
    /// Primary identifier of the affected record (e.g. employee ID or badge ID).
    /// </summary>
    public string? RecordIdentifier { get; init; }

    /// <summary>
    /// Name of the field, layer, or token with the issue.
    /// </summary>
    public string TargetName { get; init; } = string.Empty;

    /// <summary>
    /// Machine-readable error code (e.g. "MissingRequiredField", "MissingPhotoFile", "InvalidBarcode", "DuplicateIdentifier").
    /// </summary>
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable explanation of the issue and suggested fix.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    public override string ToString()
    {
        var rowText = RowNumber.HasValue ? $"[Row {RowNumber}] " : string.Empty;
        var idText = !string.IsNullOrEmpty(RecordIdentifier) ? $"(ID: {RecordIdentifier}) " : string.Empty;
        return $"{Severity.ToString().ToUpperInvariant()}: {rowText}{idText}{TargetName} - {Message} [{ErrorCode}]";
    }
}
