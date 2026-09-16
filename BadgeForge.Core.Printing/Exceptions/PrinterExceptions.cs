namespace BadgeForge.Core.Printing.Exceptions;

/// <summary>
/// Base exception for card printer driver operations.
/// </summary>
public class PrinterException : Exception
{
    public PrinterException(string message) : base(message) { }
    public PrinterException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when probed printer driver capabilities (DPI, canvas size, printable area)
/// do not match the expected card format. The application fails loudly rather than
/// silently stretching or truncating card bitmaps.
/// </summary>
public class PrinterCapabilityMismatchException : PrinterException
{
    public string PrinterName { get; }
    public int DriverDpiX { get; }
    public int DriverDpiY { get; }
    public int ExpectedDpi { get; }
    public int DriverCanvasWidth { get; }
    public int DriverCanvasHeight { get; }
    public int ExpectedCanvasWidth { get; }
    public int ExpectedCanvasHeight { get; }

    public PrinterCapabilityMismatchException(
        string printerName,
        string message,
        int driverDpiX,
        int driverDpiY,
        int expectedDpi,
        int driverCanvasWidth,
        int driverCanvasHeight,
        int expectedCanvasWidth,
        int expectedCanvasHeight)
        : base(message)
    {
        PrinterName = printerName;
        DriverDpiX = driverDpiX;
        DriverDpiY = driverDpiY;
        ExpectedDpi = expectedDpi;
        DriverCanvasWidth = driverCanvasWidth;
        DriverCanvasHeight = driverCanvasHeight;
        ExpectedCanvasWidth = expectedCanvasWidth;
        ExpectedCanvasHeight = expectedCanvasHeight;
    }
}

/// <summary>
/// Thrown when printer hardware reports a physical fault (jam, ribbon snapped, cover open).
/// </summary>
public class PrinterHardwareFaultException : PrinterException
{
    public string FaultCode { get; }

    public PrinterHardwareFaultException(string message, string faultCode) : base(message)
    {
        FaultCode = faultCode;
    }
}

/// <summary>
/// Thrown when an operator intervention is mandatory (e.g. output hopper limit reached at 20 cards).
/// </summary>
public class OperatorPauseRequiredException : PrinterException
{
    public int CardsPrintedInBatch { get; }
    public int MaxCardsBeforePause { get; }

    public OperatorPauseRequiredException(string message, int cardsPrinted, int maxBeforePause)
        : base(message)
    {
        CardsPrintedInBatch = cardsPrinted;
        MaxCardsBeforePause = maxBeforePause;
    }
}
