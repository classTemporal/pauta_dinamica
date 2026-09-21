using System;
using System.Windows;
using PautaDinamicaApp.Services;
using WpfMessageBox = System.Windows.MessageBox;

namespace PautaDinamicaApp;

/// <summary>
/// Central MessageBox helper that respects the "ShowNonCriticalMessages" setting.
/// Critical messages (errors, backup warnings, structural changes, validation) are always shown.
/// Non-critical messages (success confirmations, "open folder?" prompts, harmless confirmations)
/// are suppressed when the user disables them in Config General.
/// </summary>
public static class MessageBoxHelper
{
    /// <summary>
    /// Always-shown message (critical). Use for errors, backup/structural warnings, validation failures.
    /// </summary>
    public static MessageBoxResult Show(string message, string title,
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.Information)
    {
        return WpfMessageBox.Show(message, title, buttons, icon);
    }

    /// <summary>
    /// Non-critical message. Only shown when Settings.ShowNonCriticalMessages is true.
    /// When suppressed, returns the safe default for the given buttons (No for YesNo, OK for OK).
    /// </summary>
    public static MessageBoxResult ShowNonCritical(string message, string title,
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.Information)
    {
        var settings = LoadSettings();
        if (settings.ShowNonCriticalMessages)
        {
            return WpfMessageBox.Show(message, title, buttons, icon);
        }

        // Return safe default without showing dialog
        return buttons switch
        {
            MessageBoxButton.OK => MessageBoxResult.OK,
            MessageBoxButton.YesNo => MessageBoxResult.No,
            MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
            MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
            _ => MessageBoxResult.OK,
        };
    }

    /// <summary>
    /// Convenience overload that auto-detects critical vs non-critical based on the icon.
    /// Error, Warning on destructive operations, and Question on structural changes are treated as critical.
    /// Callers should prefer the explicit Show/ShowNonCritical methods when in doubt.
    /// </summary>
    public static MessageBoxResult Show(string message, string title,
        MessageBoxButton buttons,
        MessageBoxImage icon,
        bool isCritical)
    {
        return isCritical
            ? Show(message, title, buttons, icon)
            : ShowNonCritical(message, title, buttons, icon);
    }

    private static Models.AppSettings LoadSettings()
    {
        try
        {
            var storage = new StorageService();
            return storage.LoadSettings();
        }
        catch
        {
            return new Models.AppSettings { ShowNonCriticalMessages = true };
        }
    }
}
