#if WINDOWS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TimeBarX.App;

/// <summary>
/// Publishes taskbar/Start-icon jump-list entries via the classic shell API
/// (<c>ICustomDestinationList</c>). Works in both channels: MSIX and the
/// direct (Inno) build. Each entry launches our own executable with a
/// <c>timebarx://</c> argument, which the URL-scheme handler already routes
/// through <c>App.HandleUri</c>.
///
/// This uses the shell COM API rather than <c>Windows.UI.StartScreen.JumpList</c>
/// (WinRT) because the WinRT variant requires MSIX package identity, so it
/// wouldn't work in the direct build. The shell API works everywhere we have
/// an executable path, which we always do.
/// </summary>
public sealed class WindowsJumpList : IJumpList
{
    /// <summary>
    /// Replaces the current jump list with the provided entries under a
    /// "Timer" category. Any failure (shell rejected the list, exe path
    /// resolution failed, COM plumbing missing) silently degrades to a no-op:
    /// a missing jump list is a soft feature, never worth blocking startup for.
    /// </summary>
    public void Publish(IReadOnlyList<JumpListEntry> entries)
    {
        if (entries.Count == 0) return;

        var exePath = ResolveExecutablePath();
        if (exePath is null) return;

        ICustomDestinationList? list = null;
        IObjectArray? removed = null;
        IObjectCollection? tasks = null;
        try
        {
            list = (ICustomDestinationList)new CDestinationList();
            list.BeginList(out _, typeof(IObjectArray).GUID, out removed);

            tasks = (IObjectCollection)new EnumerableObjectCollection();
            foreach (var entry in entries)
            {
                var link = BuildShellLink(exePath, entry);
                if (link is not null) tasks.AddObject(link);
            }

            var array = (IObjectArray)tasks;
            list.AddUserTasks(array);
            list.CommitList();
        }
        catch
        {
            try { list?.AbortList(); }
            catch { /* the abort itself can throw if the list never opened */ }
        }
        finally
        {
            if (tasks is not null) Marshal.FinalReleaseComObject(tasks);
            if (removed is not null) Marshal.FinalReleaseComObject(removed);
            if (list is not null) Marshal.FinalReleaseComObject(list);
        }
    }

    private static IShellLinkW? BuildShellLink(string exePath, JumpListEntry entry)
    {
        try
        {
            var link = (IShellLinkW)new CShellLink();
            link.SetPath(exePath);
            // Pass the URI as our argv[1]; the URL-scheme handler forwards the
            // same value when a URI activation launches us from outside.
            link.SetArguments(entry.Uri);
            link.SetIconLocation(exePath, 0);

            var store = (IPropertyStore)link;
            // PKEY_Title is what the jump list actually renders as the row label.
            using var title = PropVariant.FromString(entry.Title);
            store.SetValue(ref PKEY_Title, title.Ref);
            store.Commit();
            return link;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveExecutablePath()
    {
        // Process.MainModule.FileName is the actual launched exe — under
        // single-file publish this is the bundled TimeBarX.App.exe, exactly
        // what we want the jump-list shortcut to target.
        try
        {
            var path = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
        }
        catch { /* fall through to the assembly-location fallback */ }

        try
        {
            var path = Assembly.GetEntryAssembly()?.Location;
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
        }
        catch { /* nothing else to try */ }

        return null;
    }

    // ---- Shell COM interop ----

    [ComImport, Guid("77f10cf0-3db5-4966-b520-b7c54fd35ed6")]
    private class CDestinationList { }

    [ComImport, Guid("2d3468c1-36a7-43b6-ac24-d3f02fd9607a")]
    private class EnumerableObjectCollection { }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink { }

    [ComImport, Guid("6332debf-87b5-4670-90c0-5e57b408a49e"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICustomDestinationList
    {
        void SetAppID([MarshalAs(UnmanagedType.LPWStr)] string pszAppID);
        void BeginList(out uint pcMaxSlots, [In] Guid riid, out IObjectArray ppv);
        void AppendCategory([MarshalAs(UnmanagedType.LPWStr)] string pszCategory, IObjectArray poa);
        void AppendKnownCategory(int category);
        void AddUserTasks(IObjectArray poa);
        void CommitList();
        void GetRemovedDestinations([In] Guid riid, out IObjectArray ppv);
        void DeleteList([MarshalAs(UnmanagedType.LPWStr)] string pszAppID);
        void AbortList();
    }

    [ComImport, Guid("92ca9dcd-5622-4bba-a805-5e9f541bd8c9"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IObjectArray
    {
        void GetCount(out uint pcObjects);
        void GetAt(uint uiIndex, [In] Guid riid, out object ppv);
    }

    [ComImport, Guid("5632b1a4-e38a-400a-928a-d4cd63230295"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IObjectCollection
    {
        // Inherited from IObjectArray
        void GetCount(out uint pcObjects);
        void GetAt(uint uiIndex, [In] Guid riid, out object ppv);
        // IObjectCollection additions
        void AddObject([MarshalAs(UnmanagedType.IUnknown)] object pvObject);
        void AddFromArray(IObjectArray poaSource);
        void RemoveObjectAt(uint uiIndex);
        void Clear();
    }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PropertyKey pkey);
        void GetValue(ref PropertyKey key, IntPtr pv);
        void SetValue(ref PropertyKey key, IntPtr propvar);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid fmtid;
        public int pid;
    }

    // PKEY_Title = {F29F85E0-4FF9-1068-AB91-08002B27B3D9}, 2
    private static PropertyKey PKEY_Title = new()
    {
        fmtid = new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"),
        pid = 2,
    };

    /// <summary>
    /// Minimal PROPVARIANT helper — allocates and frees the native memory
    /// that <see cref="IPropertyStore.SetValue"/> needs. Disposed with
    /// <c>using</c> so we don't leak a chunk of unmanaged memory per entry.
    /// </summary>
    private sealed class PropVariant : IDisposable
    {
        private IntPtr _ptr;
        public IntPtr Ref => _ptr;

        public static PropVariant FromString(string value)
        {
            var pv = new PropVariant();
            pv._ptr = Marshal.AllocCoTaskMem(Marshal.SizeOf<NativePropVariant>());
            var native = new NativePropVariant
            {
                vt = 31, // VT_LPWSTR
                pwszVal = Marshal.StringToCoTaskMemUni(value),
            };
            Marshal.StructureToPtr(native, pv._ptr, false);
            return pv;
        }

        public void Dispose()
        {
            if (_ptr == IntPtr.Zero) return;
            try { PropVariantClear(_ptr); }
            catch { /* best-effort */ }
            Marshal.FreeCoTaskMem(_ptr);
            _ptr = IntPtr.Zero;
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(IntPtr pvar);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePropVariant
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr pwszVal;
        public IntPtr padding;
    }
}
#endif
