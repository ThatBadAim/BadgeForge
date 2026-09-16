using System.Text.RegularExpressions;
using BadgeForge.Core.Templates.Enums;

namespace BadgeForge.Core.Data.Validation;

/// <summary>
/// Standard implementation of IBarcodeValidator checking symbology character sets,
/// lengths, and check digits (EAN-13, UPC-A).
/// </summary>
public class BarcodeValidator : IBarcodeValidator
{
    private static readonly Regex Code39Regex = new(@"^[0-9A-Z\-\.\ \$\/\+\%]+$", RegexOptions.Compiled);

    public BarcodeValidationResult Validate(string? content, BarcodeSymbology symbology)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return BarcodeValidationResult.Failure("Barcode content cannot be null or empty.");
        }

        return symbology switch
        {
            BarcodeSymbology.Code128 => ValidateCode128(content),
            BarcodeSymbology.Code39 => ValidateCode39(content),
            BarcodeSymbology.QrCode => ValidateQrCode(content),
            BarcodeSymbology.Ean13 => ValidateEan13(content),
            BarcodeSymbology.UpcA => ValidateUpcA(content),
            BarcodeSymbology.Pdf417 => ValidatePdf417(content),
            _ => BarcodeValidationResult.Failure($"Unsupported symbology '{symbology}'.")
        };
    }

    private static BarcodeValidationResult ValidateCode128(string content)
    {
        // Code 128 supports ASCII values 0 through 127
        foreach (char c in content)
        {
            if (c > 127)
            {
                return BarcodeValidationResult.Failure($"Code 128 does not support non-ASCII character '{c}' (0x{(int)c:X}).");
            }
        }
        return BarcodeValidationResult.Success();
    }

    private static BarcodeValidationResult ValidateCode39(string content)
    {
        // Lower-case letters aren't standard Code 39: the encoder would switch to Full ASCII pairs such as "+E",
        // which scanners not set up for Full ASCII read back as different text
        if (!Code39Regex.IsMatch(content))
        {
            return BarcodeValidationResult.Failure(
                $"Code 39 payload '{content}' contains invalid characters. Supported characters are: 0-9, upper-case A-Z, space, and symbols: - . $ / + %");
        }
        return BarcodeValidationResult.Success();
    }

    private static BarcodeValidationResult ValidateQrCode(string content)
    {
        if (content.Length > 2953)
        {
            return BarcodeValidationResult.Failure($"QR Code content length ({content.Length}) exceeds standard capacity limit.");
        }
        return BarcodeValidationResult.Success();
    }

    private static BarcodeValidationResult ValidateEan13(string content)
    {
        if (!content.All(char.IsDigit))
        {
            return BarcodeValidationResult.Failure($"EAN-13 payload '{content}' must contain only numeric digits.");
        }

        if (content.Length != 12 && content.Length != 13)
        {
            return BarcodeValidationResult.Failure($"EAN-13 payload length must be 12 (data only) or 13 (with check digit), but was {content.Length}.");
        }

        if (content.Length == 13)
        {
            int expectedCheckDigit = CalculateEan13CheckDigit(content[..12]);
            int actualCheckDigit = content[12] - '0';
            if (expectedCheckDigit != actualCheckDigit)
            {
                return BarcodeValidationResult.Failure(
                    $"EAN-13 check digit mismatch for '{content}': expected {expectedCheckDigit}, found {actualCheckDigit}.");
            }
        }

        return BarcodeValidationResult.Success();
    }

    private static BarcodeValidationResult ValidateUpcA(string content)
    {
        if (!content.All(char.IsDigit))
        {
            return BarcodeValidationResult.Failure($"UPC-A payload '{content}' must contain only numeric digits.");
        }

        if (content.Length != 11 && content.Length != 12)
        {
            return BarcodeValidationResult.Failure($"UPC-A payload length must be 11 (data only) or 12 (with check digit), but was {content.Length}.");
        }

        if (content.Length == 12)
        {
            int expectedCheckDigit = CalculateUpcACheckDigit(content[..11]);
            int actualCheckDigit = content[11] - '0';
            if (expectedCheckDigit != actualCheckDigit)
            {
                return BarcodeValidationResult.Failure(
                    $"UPC-A check digit mismatch for '{content}': expected {expectedCheckDigit}, found {actualCheckDigit}.");
            }
        }

        return BarcodeValidationResult.Success();
    }

    private static BarcodeValidationResult ValidatePdf417(string content)
    {
        if (content.Length > 1100)
        {
            return BarcodeValidationResult.Failure($"PDF417 content length ({content.Length}) exceeds recommended capacity.");
        }
        return BarcodeValidationResult.Success();
    }

    /// <summary>
    /// Calculates the standard Modulo 10 check digit for the first 12 digits of an EAN-13 barcode.
    /// Odd positions (0-indexed: 0, 2, 4, 6, 8, 10) weight = 1
    /// Even positions (0-indexed: 1, 3, 5, 7, 9, 11) weight = 3
    /// </summary>
    public static int CalculateEan13CheckDigit(string first12Digits)
    {
        int sum = 0;
        for (int i = 0; i < 12; i++)
        {
            int digit = first12Digits[i] - '0';
            sum += (i % 2 == 0) ? digit : digit * 3;
        }
        int remainder = sum % 10;
        return (remainder == 0) ? 0 : 10 - remainder;
    }

    /// <summary>
    /// Calculates the standard Modulo 10 check digit for the first 11 digits of a UPC-A barcode.
    /// Odd positions (0-indexed: 0, 2, 4, 6, 8, 10) weight = 3
    /// Even positions (0-indexed: 1, 3, 5, 7, 9) weight = 1
    /// </summary>
    public static int CalculateUpcACheckDigit(string first11Digits)
    {
        int sum = 0;
        for (int i = 0; i < 11; i++)
        {
            int digit = first11Digits[i] - '0';
            sum += (i % 2 == 0) ? digit * 3 : digit;
        }
        int remainder = sum % 10;
        return (remainder == 0) ? 0 : 10 - remainder;
    }
}
