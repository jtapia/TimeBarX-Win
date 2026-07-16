#if WINDOWS
using System;
using System.Runtime.InteropServices;
using System.Security;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace TimeBarX.App;

/// <summary>
/// Raises completion toasts through the classic <see cref="ToastNotificationManager"/>
/// XML API, which works in both channels: the MSIX (Store) build has package
/// identity for free, and the direct (Inno) build supplies an explicit
/// AppUserModelID plus a Start-menu shortcut carrying it (see installer.iss).
///
/// Every public call is wrapped so a missing/blocked notification platform
/// (Focus Assist, notifications disabled, no shortcut) degrades to a no-op
/// rather than throwing into the completion path.
/// </summary>
public sealed class WindowsToastNotifier : IToastNotifier
{
    /// <summary>
    /// Explicit AUMID for the direct (non-MSIX) build. Must match the AppUserModelID
    /// stamped on the Start-menu shortcut the Inno installer creates, or Windows
    /// silently drops the toast. The MSIX build ignores this — the OS derives the
    /// AUMID from package identity.
    /// </summary>
    public const string DirectChannelAumid = "EduardoTapia.TimeBarX.Direct";

    private readonly bool _available;

    public WindowsToastNotifier()
    {
        // Set the process AUMID only for the direct build (no package identity).
        // Under MSIX this call is unnecessary and can interfere with the
        // package-supplied identity, so skip it when running packaged.
        try
        {
            if (!HasPackageIdentity())
                SetCurrentProcessExplicitAppUserModelID(DirectChannelAumid);
            _available = true;
        }
        catch
        {
            _available = false;
        }
    }

    public void ShowCompletion(ToastCompletionInfo info)
    {
        if (!_available) return;
        try
        {
            var xml = BuildToastXml(info);
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var toast = new ToastNotification(doc);
            Notifier().Show(toast);
        }
        catch
        {
            // Focus Assist / notifications disabled / no registered shortcut /
            // platform unavailable — the sound + overlay already fired, so a
            // missing toast is a soft degradation, never an error.
        }
    }

    private ToastNotifier Notifier() =>
        HasPackageIdentity()
            ? ToastNotificationManager.CreateToastNotifier()
            : ToastNotificationManager.CreateToastNotifier(DirectChannelAumid);

    // Toast buttons use activationType="protocol": Windows launches the
    // timebarx:// argument through the registered scheme, which both channels
    // already register, so clicks route through App.HandleUri whether the app is
    // running or closed. Body text is escaped to keep a stray label from breaking
    // the XML.
    private static string BuildToastXml(ToastCompletionInfo info)
    {
        var title = SecurityElement.Escape(info.Title) ?? string.Empty;
        var body = SecurityElement.Escape(info.Body) ?? string.Empty;
        var restart = SecurityElement.Escape(info.RestartUri) ?? string.Empty;
        var extend = SecurityElement.Escape(info.ExtendUri) ?? string.Empty;

        var bodyLine = string.IsNullOrEmpty(body)
            ? string.Empty
            : $"<text>{body}</text>";

        return $"""
            <toast activationType="protocol">
              <visual>
                <binding template="ToastGeneric">
                  <text>{title}</text>
                  {bodyLine}
                </binding>
              </visual>
              <actions>
                <action content="Restart" activationType="protocol" arguments="{restart}" />
                <action content="+5 min" activationType="protocol" arguments="{extend}" />
              </actions>
            </toast>
            """;
    }

    /// <summary>True when running with MSIX package identity (Store build).</summary>
    private static bool HasPackageIdentity()
    {
        try
        {
            int length = 0;
            // ERROR_INSUFFICIENT_BUFFER (122) => a package identity exists;
            // APPMODEL_ERROR_NO_PACKAGE (15700) => none (direct build).
            var rc = GetCurrentPackageFullName(ref length, null);
            return rc != AppModelErrorNoPackage;
        }
        catch
        {
            return false;
        }
    }

    private const int AppModelErrorNoPackage = 15700;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string AppID);
}
#endif
