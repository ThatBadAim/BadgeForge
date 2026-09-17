using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace BadgeForge.Installer;

public static class Program
{
    private const string AppName = "BadgeForge";
    private const string ExeName = "BadgeForge.exe";

    public static int Main(string[] args)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("The installer is designed for Windows.");
            return 1;
        }

        bool uninstall = args.Length > 0 && (args[0].Equals("/uninstall", StringComparison.OrdinalIgnoreCase) || args[0].Equals("--uninstall", StringComparison.OrdinalIgnoreCase) || args[0].Equals("-u", StringComparison.OrdinalIgnoreCase));

        try
        {
            if (uninstall)
            {
                Uninstall();
            }
            else
            {
                Install();
            }
            return 0;
        }
        catch (Exception ex)
        {
            ShowMessage($"An error occurred during {(uninstall ? "uninstall" : "installation")}:\n\n{ex.Message}", $"{AppName} Setup", 0x10); // MB_ICONERROR
            return 1;
        }
    }

    [SupportedOSPlatform("windows")]
    private static void Install()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string installDir = Path.Combine(localAppData, "Programs", AppName);
        string targetExe = Path.Combine(installDir, ExeName);

        Directory.CreateDirectory(installDir);

        // Extract payload
        var assembly = Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream("Payload.BadgeForge.exe"))
        {
            if (stream == null)
            {
                ShowMessage("Embedded application payload was not found inside this setup executable.", $"{AppName} Setup", 0x10);
                return;
            }

            using (var fileStream = new FileStream(targetExe, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.CopyTo(fileStream);
            }
        }

        // Copy setup itself as uninstaller
        string uninstallerPath = Path.Combine(installDir, "BadgeForgeUninstall.exe");
        try
        {
            string? currentExe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(currentExe) && File.Exists(currentExe))
            {
                File.Copy(currentExe, uninstallerPath, overwrite: true);
            }
        }
        catch
        {
            // Non-fatal if uninstaller binary copy fails
        }

        // Create Start Menu shortcut
        string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
        string shortcutPath = Path.Combine(startMenuDir, $"{AppName}.lnk");
        CreateShortcut(shortcutPath, targetExe, installDir, "BadgeForge ID Card Designer & Batch Printer");

        // Create Desktop shortcut
        string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string desktopShortcutPath = Path.Combine(desktopDir, $"{AppName}.lnk");
        CreateShortcut(desktopShortcutPath, targetExe, installDir, "BadgeForge ID Card Designer & Batch Printer");

        // Register in Add/Remove Programs (Windows Settings > Installed Apps)
        RegisterUninstallEntry(installDir, targetExe, uninstallerPath);

        int result = ShowMessage(
            $"{AppName} has been installed successfully!\n\n" +
            $"• Location: {installDir}\n" +
            $"• Shortcuts added to Start Menu and Desktop\n\n" +
            "Would you like to launch BadgeForge now?",
            $"{AppName} Setup",
            0x24); // MB_YESNO | MB_ICONQUESTION

        if (result == 6) // IDYES
        {
            Process.Start(new ProcessStartInfo(targetExe) { UseShellExecute = true });
        }
    }

    [SupportedOSPlatform("windows")]
    private static void Uninstall()
    {
        int confirm = ShowMessage($"Are you sure you want to remove {AppName} from your computer?", $"{AppName} Uninstall", 0x24);
        if (confirm != 6) // Not IDYES
        {
            return;
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string installDir = Path.Combine(localAppData, "Programs", AppName);

        // Remove shortcuts
        string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
        string shortcutPath = Path.Combine(startMenuDir, $"{AppName}.lnk");
        if (File.Exists(shortcutPath))
        {
            try { File.Delete(shortcutPath); } catch { }
        }

        string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string desktopShortcutPath = Path.Combine(desktopDir, $"{AppName}.lnk");
        if (File.Exists(desktopShortcutPath))
        {
            try { File.Delete(desktopShortcutPath); } catch { }
        }

        // Unregister Add/Remove Programs
        UnregisterUninstallEntry();

        // Remove files via delayed cmd script to allow this process to exit
        string batFile = Path.Combine(Path.GetTempPath(), $"cleanup_{Guid.NewGuid():N}.bat");
        File.WriteAllText(batFile, $"@echo off\ntimeout /t 1 /nobreak >nul\nrd /s /q \"{installDir}\"\ndel \"%~f0\"\n");

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{batFile}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        });

        ShowMessage($"{AppName} has been uninstalled.", $"{AppName} Uninstall", 0x40); // MB_ICONINFORMATION
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterUninstallEntry(string installDir, string targetExe, string uninstallerPath)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppName}");
            if (key != null)
            {
                key.SetValue("DisplayName", AppName);
                key.SetValue("DisplayIcon", targetExe);
                key.SetValue("DisplayVersion", "1.0.0");
                key.SetValue("Publisher", "BadgeForge");
                key.SetValue("InstallLocation", installDir);
                key.SetValue("UninstallString", $"\"{uninstallerPath}\" /uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
        catch
        {
            // Registry write permissions in HKCU are user-level, failure is non-fatal
        }
    }

    [SupportedOSPlatform("windows")]
    private static void UnregisterUninstallEntry()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppName}", throwOnMissingSubKey: false);
        }
        catch
        {
        }
    }

    [SupportedOSPlatform("windows")]
    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string description)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell != null)
                {
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetPath;
                    shortcut.WorkingDirectory = workingDir;
                    shortcut.Description = description;
                    shortcut.Save();
                }
            }
        }
        catch
        {
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);

    private static int ShowMessage(string text, string title, uint type)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return MessageBoxW(IntPtr.Zero, text, title, type);
        }
        Console.WriteLine($"[{title}] {text}");
        return 0;
    }
}
