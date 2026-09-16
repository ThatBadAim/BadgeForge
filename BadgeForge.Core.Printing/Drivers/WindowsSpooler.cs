using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using BadgeForge.Core.Printing.Models;

namespace BadgeForge.Core.Printing.Drivers;

/// <summary>
/// One job in a Windows print queue.
/// </summary>
public sealed record SpoolerJob(uint JobId, string DocumentName, uint Status)
{
    private const uint JobStatusDeleting = 0x4;
    private const uint JobStatusPrinted = 0x80;
    private const uint JobStatusDeleted = 0x100;
    private const uint JobStatusComplete = 0x1000;

    /// <summary>
    /// True once the spooler has handed the job to the printer or is removing it.
    /// </summary>
    public bool IsFinished => (Status & (JobStatusDeleting | JobStatusPrinted | JobStatusDeleted | JobStatusComplete)) != 0;
}

/// <summary>
/// Printer status flags and queued jobs read from the Windows print spooler at one moment.
/// </summary>
public sealed record SpoolerSnapshot(uint PrinterStatus, IReadOnlyList<SpoolerJob> Jobs);

/// <summary>
/// Translates Windows spooler printer and job status flags into a <see cref="PrinterState"/>.
/// Card printer drivers vary in which flags they report, so this needs confirming on the physical printer.
/// </summary>
public static class SpoolerStatusMapper
{
    // PRINTER_STATUS_* (winspool.h)
    private const uint PrinterPaused = 0x1;
    private const uint PrinterError = 0x2;
    private const uint PrinterPendingDeletion = 0x4;
    private const uint PrinterPaperJam = 0x8;
    private const uint PrinterPaperOut = 0x10;
    private const uint PrinterPaperProblem = 0x40;
    private const uint PrinterOffline = 0x80;
    private const uint PrinterBusy = 0x200;
    private const uint PrinterPrinting = 0x400;
    private const uint PrinterOutputBinFull = 0x800;
    private const uint PrinterNotAvailable = 0x1000;
    private const uint PrinterProcessing = 0x4000;
    private const uint PrinterWarmingUp = 0x10000;
    private const uint PrinterNoToner = 0x40000;
    private const uint PrinterUserIntervention = 0x100000;
    private const uint PrinterOutOfMemory = 0x200000;
    private const uint PrinterDoorOpen = 0x400000;
    private const uint PrinterServerUnknown = 0x800000;

    // JOB_STATUS_* (winspool.h)
    private const uint JobError = 0x2;
    private const uint JobOffline = 0x20;
    private const uint JobPaperOut = 0x40;
    private const uint JobBlocked = 0x200;
    private const uint JobUserIntervention = 0x400;

    public static (PrinterState State, string Message) Map(SpoolerSnapshot snapshot, SpoolerJob? activeJob)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        uint status = snapshot.PrinterStatus;

        if ((status & PrinterDoorOpen) != 0)
            return (PrinterState.CoverOpen, "The printer cover is open.");
        if ((status & PrinterPaperJam) != 0)
            return (PrinterState.PaperJam, "The printer reports a card jam.");
        if ((status & PrinterPaperOut) != 0)
            return (PrinterState.OutOfCards, "The printer reports the input hopper is empty. Load cards.");
        if ((status & PrinterNoToner) != 0)
            return (PrinterState.OutOfRibbon, "The printer reports the ribbon is exhausted. Replace the ribbon.");
        if ((status & PrinterOutputBinFull) != 0)
            return (PrinterState.OperatorPauseRequired, "The printer reports the output hopper is full. Empty it.");
        if ((status & (PrinterOffline | PrinterNotAvailable | PrinterServerUnknown | PrinterPendingDeletion)) != 0)
            return (PrinterState.Offline, "The printer is offline or disconnected.");
        if ((status & (PrinterError | PrinterUserIntervention | PrinterPaperProblem | PrinterOutOfMemory)) != 0)
            return (PrinterState.Error, $"The printer reports an error that needs attention (status 0x{status:X}).");

        if (activeJob != null && (activeJob.Status & (JobError | JobOffline | JobPaperOut | JobBlocked | JobUserIntervention)) != 0)
            return (PrinterState.Error, $"Print job '{activeJob.DocumentName}' is stuck in the Windows print queue (status 0x{activeJob.Status:X}).");

        if ((status & PrinterPaused) != 0)
            return (PrinterState.Paused, "The printer's queue is paused in Windows. Resume it in Printers & scanners.");
        if (activeJob != null || (status & (PrinterPrinting | PrinterBusy | PrinterProcessing | PrinterWarmingUp)) != 0)
            return (PrinterState.Printing, "Card printing in progress");

        return (PrinterState.Ready, "Ready (spooler idle)");
    }
}

/// <summary>
/// Reads printer and job status from the Windows print spooler (winspool.drv).
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsSpooler
{
    private const int ErrorInsufficientBuffer = 122;
    private const uint MaxJobs = 1000;

    // winspool SetJob command: remove the job from the queue and stop spooling it
    private const uint JobControlDelete = 5;

    /// <summary>
    /// Returns the printer's status flags and queued jobs, or null when no printer with that name can be opened.
    /// </summary>
    public static SpoolerSnapshot? TryQuery(string printerName)
    {
        if (!OpenPrinter(printerName, out IntPtr handle, IntPtr.Zero))
        {
            return null;
        }

        try
        {
            return new SpoolerSnapshot(ReadPrinterStatus(handle), ReadJobs(handle));
        }
        finally
        {
            ClosePrinter(handle);
        }
    }

    /// <summary>
    /// Deletes queued jobs whose document name matches one the caller sent, stopping any further data reaching the
    /// printer. Returns how many jobs were removed, or -1 when the printer couldn't be opened.
    /// </summary>
    public static int TryCancelJobs(string printerName, IReadOnlyCollection<string> documentNames)
    {
        if (documentNames.Count == 0 || !OpenPrinter(printerName, out IntPtr handle, IntPtr.Zero))
        {
            return documentNames.Count == 0 ? 0 : -1;
        }

        try
        {
            int cancelled = 0;
            foreach (var job in ReadJobs(handle))
            {
                if (!job.IsFinished && documentNames.Contains(job.DocumentName) &&
                    SetJob(handle, job.JobId, 0, IntPtr.Zero, JobControlDelete))
                {
                    cancelled++;
                }
            }

            return cancelled;
        }
        finally
        {
            ClosePrinter(handle);
        }
    }

    private static uint ReadPrinterStatus(IntPtr handle)
    {
        // PRINTER_INFO_6 holds just the status DWORD
        return WithBuffer(
            (IntPtr buffer, uint size, out uint needed) => GetPrinter(handle, 6, buffer, size, out needed),
            buffer => (uint)Marshal.ReadInt32(buffer),
            emptyResult: 0u);
    }

    private static IReadOnlyList<SpoolerJob> ReadJobs(IntPtr handle)
    {
        uint returned = 0;
        return WithBuffer(
            (IntPtr buffer, uint size, out uint needed) => EnumJobs(handle, 0, MaxJobs, 1, buffer, size, out needed, out returned),
            buffer =>
            {
                var jobs = new List<SpoolerJob>((int)returned);
                int entrySize = Marshal.SizeOf<JobInfo1>();
                for (int i = 0; i < returned; i++)
                {
                    var info = Marshal.PtrToStructure<JobInfo1>(buffer + (i * entrySize));
                    jobs.Add(new SpoolerJob(info.JobId, Marshal.PtrToStringUni(info.pDocument) ?? string.Empty, info.Status));
                }
                return (IReadOnlyList<SpoolerJob>)jobs;
            },
            emptyResult: Array.Empty<SpoolerJob>());
    }

    private delegate bool BufferCall(IntPtr buffer, uint size, out uint needed);

    /// <summary>
    /// Calls a winspool "size, then fill" API, retrying if the data grows between the two calls.
    /// </summary>
    private static T WithBuffer<T>(BufferCall call, Func<IntPtr, T> read, T emptyResult)
    {
        call(IntPtr.Zero, 0, out uint needed);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (needed == 0)
            {
                return emptyResult;
            }

            IntPtr buffer = Marshal.AllocHGlobal((int)needed);
            try
            {
                if (call(buffer, needed, out uint stillNeeded))
                {
                    return read(buffer);
                }

                int error = Marshal.GetLastWin32Error();
                if (error != ErrorInsufficientBuffer)
                {
                    throw new Win32Exception(error);
                }

                needed = stillNeeded;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        throw new Win32Exception(ErrorInsufficientBuffer);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemTime
    {
        public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct JobInfo1
    {
        public uint JobId;
        public IntPtr pPrinterName;
        public IntPtr pMachineName;
        public IntPtr pUserName;
        public IntPtr pDocument;
        public IntPtr pDatatype;
        public IntPtr pStatus;
        public uint Status;
        public uint Priority;
        public uint Position;
        public uint TotalPages;
        public uint PagesPrinted;
        public SystemTime Submitted;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "GetPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetPrinter(IntPtr hPrinter, uint level, IntPtr pPrinter, uint cbBuf, out uint pcbNeeded);

    [DllImport("winspool.drv", EntryPoint = "EnumJobsW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumJobs(IntPtr hPrinter, uint firstJob, uint noJobs, uint level, IntPtr pJob, uint cbBuf, out uint pcbNeeded, out uint pcReturned);

    [DllImport("winspool.drv", EntryPoint = "SetJobW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetJob(IntPtr hPrinter, uint jobId, uint level, IntPtr pJob, uint command);
}
