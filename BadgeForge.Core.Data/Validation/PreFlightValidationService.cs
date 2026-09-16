using System.Text.RegularExpressions;
using BadgeForge.Core.Data.Models;
using BadgeForge.Core.Data.Services;
using BadgeForge.Core.Templates.Layers;
using BadgeForge.Core.Templates.Models;
using BadgeForge.Core.Templates.Tokens;

namespace BadgeForge.Core.Data.Validation;

/// <summary>
/// Pre-flight validation engine that inspects batch data records against template definitions
/// to detect errors before physical printing begins.
/// </summary>
public class PreFlightValidationService
{
    private readonly IBarcodeValidator _barcodeValidator;
    private readonly PhotoMatchingService _photoMatchingService;

    public PreFlightValidationService(
        IBarcodeValidator? barcodeValidator = null,
        PhotoMatchingService? photoMatchingService = null)
    {
        _barcodeValidator = barcodeValidator ?? new BarcodeValidator();
        _photoMatchingService = photoMatchingService ?? new PhotoMatchingService();
    }

    /// <summary>
    /// Executes the full pre-flight validation pass across the entire batch dataset.
    /// </summary>
    public PreFlightValidationReport ValidateBatch(
        TemplateDefinition template,
        IReadOnlyList<BadgeRecord> records,
        PhotoMatchingOptions? photoOptions = null,
        PreFlightValidationOptions? validationOptions = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(records);

        var options = validationOptions ?? new PreFlightValidationOptions();
        var effectivePhotoOptions = photoOptions ?? new PhotoMatchingOptions();
        var issues = new List<ValidationIssue>();

        // Pre-extract layers by type
        var textLayers = template.Layers.OfType<TextLayer>().ToList();
        var photoLayers = template.Layers.OfType<PhotoLayer>().ToList();
        var barcodeLayers = template.Layers.OfType<BarcodeLayer>().ToList();

        // Cross-batch duplicate detection trackers
        var seenBarcodesByLayer = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        var seenPrimaryIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Determine primary ID token
        string? primaryKeyToken = options.PrimaryKeyToken;
        if (string.IsNullOrWhiteSpace(primaryKeyToken) && barcodeLayers.Count > 0)
        {
            primaryKeyToken = barcodeLayers[0].GetReferencedTokens().FirstOrDefault();
        }

        for (int i = 0; i < records.Count; i++)
        {
            var record = records[i];
            var recordId = record.GetPrimaryIdentifier(primaryKeyToken);

            // 1. Text Layers validation (missing / blank required fields)
            foreach (var textLayer in textLayers)
            {
                ValidateTextLayer(record, recordId, textLayer, issues, options);
            }

            // 2. Barcode Layers validation (required values, symbology validity, duplicate detection)
            foreach (var barcodeLayer in barcodeLayers)
            {
                ValidateBarcodeLayer(record, recordId, barcodeLayer, issues, seenBarcodesByLayer, options);
            }

            // 3. Photo Layers validation (required tokens, physical file existence)
            foreach (var photoLayer in photoLayers)
            {
                ValidatePhotoLayer(record, recordId, photoLayer, effectivePhotoOptions, issues, options);
            }

            // 4. Primary key duplicate check (if primaryKeyToken is specified or inferred)
            if (options.CheckDuplicates && !string.IsNullOrWhiteSpace(primaryKeyToken))
            {
                var primaryVal = record.GetValue(primaryKeyToken);
                if (!string.IsNullOrWhiteSpace(primaryVal))
                {
                    if (seenPrimaryIds.TryGetValue(primaryVal, out var priorRow))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = options.EnforceRequirements ? ValidationSeverity.Error : ValidationSeverity.Warning,
                            RowNumber = record.RowNumber,
                            BatchIndex = record.BatchIndex,
                            RecordIdentifier = recordId,
                            TargetName = primaryKeyToken,
                            ErrorCode = PreFlightErrorCodes.DuplicateIdentifier,
                            Message = $"Duplicate primary identifier '{primaryVal}' detected in Row {record.RowNumber}, previously seen in Row {priorRow}."
                        });
                    }
                    else
                    {
                        seenPrimaryIds[primaryVal] = record.RowNumber;
                    }
                }
            }
        }

        return new PreFlightValidationReport
        {
            TotalRecords = records.Count,
            Issues = issues
        };
    }

    private static void ValidateTextLayer(
        BadgeRecord record,
        string recordId,
        TextLayer layer,
        List<ValidationIssue> issues,
        PreFlightValidationOptions validationOptions)
    {
        var tokens = layer.GetReferencedTokens().ToList();
        if (tokens.Count == 0)
            return;

        foreach (var token in tokens)
        {
            var val = record.GetValue(token);
            bool isBlank = string.IsNullOrWhiteSpace(val);

            if (isBlank)
            {
                if (layer.IsRequired && validationOptions.EnforceRequirements)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        RowNumber = record.RowNumber,
                        BatchIndex = record.BatchIndex,
                        RecordIdentifier = recordId,
                        TargetName = layer.Name,
                        ErrorCode = PreFlightErrorCodes.MissingRequiredField,
                        Message = $"Required field token '{token}' in layer '{layer.Name}' is missing or blank."
                    });
                }
                else
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        RowNumber = record.RowNumber,
                        BatchIndex = record.BatchIndex,
                        RecordIdentifier = recordId,
                        TargetName = layer.Name,
                        ErrorCode = PreFlightErrorCodes.MissingRequiredField,
                        Message = $"Optional field token '{token}' in layer '{layer.Name}' is empty."
                    });
                }
            }
        }
    }

    private void ValidateBarcodeLayer(
        BadgeRecord record,
        string recordId,
        BarcodeLayer layer,
        List<ValidationIssue> issues,
        Dictionary<string, Dictionary<string, int>> seenBarcodesByLayer,
        PreFlightValidationOptions validationOptions)
    {
        // Resolve barcode payload by replacing tokens
        var tokens = layer.GetReferencedTokens().ToList();
        bool hasMissingToken = false;

        foreach (var token in tokens)
        {
            var val = record.GetValue(token);
            if (string.IsNullOrWhiteSpace(val))
            {
                hasMissingToken = true;
                if (layer.IsRequired && validationOptions.EnforceRequirements)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        RowNumber = record.RowNumber,
                        BatchIndex = record.BatchIndex,
                        RecordIdentifier = recordId,
                        TargetName = layer.Name,
                        ErrorCode = PreFlightErrorCodes.MissingRequiredField,
                        Message = $"Barcode layer '{layer.Name}' references missing required token '{token}'."
                    });
                }
                else
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        RowNumber = record.RowNumber,
                        BatchIndex = record.BatchIndex,
                        RecordIdentifier = recordId,
                        TargetName = layer.Name,
                        ErrorCode = PreFlightErrorCodes.MissingRequiredField,
                        Message = $"Barcode layer '{layer.Name}' references empty token '{token}'."
                    });
                }
            }
        }

        if (hasMissingToken && layer.IsRequired && validationOptions.EnforceRequirements)
        {
            return;
        }

        string resolvedPayload = TokenSyntax.Replace(layer.ContentToken, t => record.GetValue(t) ?? string.Empty);

        if (string.IsNullOrWhiteSpace(resolvedPayload))
        {
            if (layer.IsRequired && validationOptions.EnforceRequirements)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    RowNumber = record.RowNumber,
                    BatchIndex = record.BatchIndex,
                    RecordIdentifier = recordId,
                    TargetName = layer.Name,
                    ErrorCode = PreFlightErrorCodes.MissingRequiredField,
                    Message = $"Barcode layer '{layer.Name}' evaluated to an empty payload."
                });
            }
            return;
        }

        // Validate symbology constraints
        var symbologyResult = _barcodeValidator.Validate(resolvedPayload, layer.Symbology);
        if (!symbologyResult.IsValid)
        {
            issues.Add(new ValidationIssue
            {
                Severity = validationOptions.EnforceRequirements ? ValidationSeverity.Error : ValidationSeverity.Warning,
                RowNumber = record.RowNumber,
                BatchIndex = record.BatchIndex,
                RecordIdentifier = recordId,
                TargetName = layer.Name,
                ErrorCode = PreFlightErrorCodes.InvalidBarcode,
                Message = $"Barcode '{resolvedPayload}' is invalid for {layer.Symbology}: {symbologyResult.ErrorMessage}"
            });
        }

        // Duplicate barcode detection
        if (validationOptions.CheckDuplicates)
        {
            if (!seenBarcodesByLayer.TryGetValue(layer.Id, out var layerDict))
            {
                layerDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                seenBarcodesByLayer[layer.Id] = layerDict;
            }

            if (layerDict.TryGetValue(resolvedPayload, out var priorRow))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = validationOptions.EnforceRequirements ? ValidationSeverity.Error : ValidationSeverity.Warning,
                    RowNumber = record.RowNumber,
                    BatchIndex = record.BatchIndex,
                    RecordIdentifier = recordId,
                    TargetName = layer.Name,
                    ErrorCode = PreFlightErrorCodes.DuplicateIdentifier,
                    Message = $"Duplicate barcode payload '{resolvedPayload}' in layer '{layer.Name}' detected in Row {record.RowNumber}, previously seen in Row {priorRow}."
                });
            }
            else
            {
                layerDict[resolvedPayload] = record.RowNumber;
            }
        }
    }

    private void ValidatePhotoLayer(
        BadgeRecord record,
        string recordId,
        PhotoLayer layer,
        PhotoMatchingOptions photoOptions,
        List<ValidationIssue> issues,
        PreFlightValidationOptions validationOptions)
    {
        // Check tokens referenced by photo layer
        var tokens = layer.GetReferencedTokens().ToList();
        bool tokenMissing = false;
        foreach (var token in tokens)
        {
            var val = record.GetValue(token);
            if (string.IsNullOrWhiteSpace(val))
            {
                tokenMissing = true;
                if (layer.IsRequired && validationOptions.EnforceRequirements)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        RowNumber = record.RowNumber,
                        BatchIndex = record.BatchIndex,
                        RecordIdentifier = recordId,
                        TargetName = layer.Name,
                        ErrorCode = PreFlightErrorCodes.MissingRequiredField,
                        Message = $"Photo layer '{layer.Name}' references missing token '{token}'."
                    });
                }
                else
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        RowNumber = record.RowNumber,
                        BatchIndex = record.BatchIndex,
                        RecordIdentifier = recordId,
                        TargetName = layer.Name,
                        ErrorCode = PreFlightErrorCodes.MissingRequiredField,
                        Message = $"Photo layer '{layer.Name}' references empty token '{token}'."
                    });
                }
            }
        }

        if (tokenMissing && layer.IsRequired && validationOptions.EnforceRequirements)
        {
            return;
        }

        // Use the photo already chosen for this record, otherwise try to match one. Validation only inspects the
        // record: it must never change which photo the badge prints with.
        var resolvedPath = !string.IsNullOrWhiteSpace(record.ResolvedPhotoPath) && File.Exists(record.ResolvedPhotoPath)
            ? record.ResolvedPhotoPath
            : _photoMatchingService.ResolvePhotoPath(record, photoOptions);

        bool fileExists = !string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath);

        if (!fileExists)
        {
            bool fallbackIsEmbedded = layer.FallbackImagePath?.StartsWith("data:image", StringComparison.OrdinalIgnoreCase) == true;
            bool hasValidFallback = !string.IsNullOrWhiteSpace(layer.FallbackImagePath)
                                    && (fallbackIsEmbedded || File.Exists(layer.FallbackImagePath));

            if (hasValidFallback)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    RowNumber = record.RowNumber,
                    BatchIndex = record.BatchIndex,
                    RecordIdentifier = recordId,
                    TargetName = layer.Name,
                    ErrorCode = PreFlightErrorCodes.MissingPhotoFile,
                    Message = $"Photo file not found for '{recordId}'. {(fallbackIsEmbedded ? "The embedded fallback image" : $"Fallback placeholder '{layer.FallbackImagePath}'")} will be used."
                });
            }
            else if (!layer.IsRequired || !validationOptions.EnforceRequirements || validationOptions.AllowMissingPhotoAsWarning)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    RowNumber = record.RowNumber,
                    BatchIndex = record.BatchIndex,
                    RecordIdentifier = recordId,
                    TargetName = layer.Name,
                    ErrorCode = PreFlightErrorCodes.MissingPhotoFile,
                    Message = $"Photo file not found for '{recordId}'. Layer is optional."
                });
            }
            else
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    RowNumber = record.RowNumber,
                    BatchIndex = record.BatchIndex,
                    RecordIdentifier = recordId,
                    TargetName = layer.Name,
                    ErrorCode = PreFlightErrorCodes.MissingPhotoFile,
                    Message = $"Required photo file missing on disk for '{recordId}'."
                });
            }
        }
    }
}
