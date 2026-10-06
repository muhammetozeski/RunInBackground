using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace RunInBackGround;

internal class Program
{
    [STAThread]
    /// <summary>
    /// This method validates arguments and launches the background desktop process if the path is valid.
    /// </summary>
    /// <param name="arguments">The array of strings containing the file or directory path and the desktop index.</param>
    static void Main(string[] arguments)
    {
        Log("Main method started.");

        if (arguments.Length != 2 )
        {
            Log("You need to enter only 2 parameter. We got " + arguments.Length + " parameters. See the second log for the parameters:");
            Log(arguments);
            return;
        }

        string targetPath = arguments[0];
        string desktopInput = arguments[1];

        if (string.IsNullOrWhiteSpace(targetPath) || !Path.Exists(targetPath))
        {
            Log($"The path was not found or is invalid: {targetPath}");
            return;
        }

        if (!int.TryParse(desktopInput, out int desktopIndex))
        {
            Log($"The input '{desktopInput}' is not a valid integer.");
            return;
        }

        try
        {
            Log($"Executing LaunchInBackgroundDesktop with path {targetPath} and index {desktopIndex}");
            LaunchInBackgroundDesktop(targetPath, desktopIndex);
        }
        catch (UnauthorizedAccessException accessException)
        {
            Log($"Permissions were denied for {targetPath}. {accessException.Message}");
        }
        catch (Exception exception)
        {
            Log($"{exception.GetType().Name} occurred: {exception.Message}");
        }
        finally
        {
            Log("Main method finished.", WaitForLogging: true);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern nint GetShellWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);
    private const int KEYEVENTF_KEYUP = 0x0002;

    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IServiceProvider10
    {
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object QueryService(ref Guid service, ref Guid riid);
    }

    [ComImport, Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IObjectArray
    {
        void GetCount(out int count);
        void GetAt(int index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object obj);
    }

    [ComImport, Guid("372E1D3B-38D3-42E4-A15B-8AB2B178F513"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IApplicationView { }

    [ComImport, Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IApplicationViewCollection
    {
        int GetViews(out IObjectArray array);
        int GetViewsByZOrder(out IObjectArray array);
        int GetViewsByAppUserModelId([MarshalAs(UnmanagedType.LPWStr)] string id, out IObjectArray array);
        int GetViewForHwnd(nint hwnd, out IApplicationView view);
    }

    [ComImport, Guid("3F07F4BE-B107-441A-AF0F-39D82529072C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVirtualDesktop
    {
        bool IsViewVisible(IApplicationView view);
        Guid GetId();
    }

    [ComImport, Guid("53F5CA0B-158F-4124-900C-057158060B27"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IVirtualDesktopManagerInternal
    {
        int GetCount();
        void MoveViewToDesktop(IApplicationView view, IVirtualDesktop desktop);
        bool CanViewMoveDesktops(IApplicationView view);
        IVirtualDesktop GetCurrentDesktop();
        void GetDesktops(out IObjectArray desktops);
        [PreserveSig]
        int GetAdjacentDesktop(IVirtualDesktop from, int direction, out IVirtualDesktop desktop);
        void SwitchDesktop(IVirtualDesktop desktop);
    }

    public static void LaunchInBackgroundDesktop(string executablePath, int targetDesktopIndex)
    {
        Log("Starting LaunchInBackgroundDesktop. Executable: " + executablePath);

        Log("Pre-instantiating ImmersiveShell for Windows 11...");
        Guid shellClsid = new("C2F03A33-21F5-47FA-B4BB-156362A2F239");
        Type? shellType = Type.GetTypeFromCLSID(shellClsid);

        if (shellType == null) return;

        var shellProvider = (IServiceProvider10)Activator.CreateInstance(shellType)!;

        Guid viewCollectionIid = typeof(IApplicationViewCollection).GUID;
        var viewCollection = (IApplicationViewCollection)shellProvider.QueryService(ref viewCollectionIid, ref viewCollectionIid);

        Guid managerInternalClsid = new("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");
        Guid managerInternalIid = typeof(IVirtualDesktopManagerInternal).GUID;
        var internalManager = (IVirtualDesktopManagerInternal)shellProvider.QueryService(ref managerInternalClsid, ref managerInternalIid);

        IVirtualDesktop originalDesktop = internalManager.GetCurrentDesktop();

        int desktopCount = internalManager.GetCount();
        if (desktopCount <= targetDesktopIndex)
        {
            Log("Target desktop missing. Creating via system hotkey...");
            while (desktopCount <= targetDesktopIndex)
            {
                keybd_event(0x5B, 0, 0, 0); // LWin
                keybd_event(0x11, 0, 0, 0); // Ctrl
                keybd_event(0x44, 0, 0, 0); // D
                keybd_event(0x44, 0, KEYEVENTF_KEYUP, 0);
                keybd_event(0x11, 0, KEYEVENTF_KEYUP, 0);
                keybd_event(0x5B, 0, KEYEVENTF_KEYUP, 0);

                internalManager.SwitchDesktop(originalDesktop);
                Thread.Sleep(20);
                desktopCount = internalManager.GetCount();
            }

            internalManager.SwitchDesktop(originalDesktop);
        }

        internalManager.GetDesktops(out IObjectArray desktops);
        Guid desktopIid = typeof(IVirtualDesktop).GUID;
        desktops.GetAt(targetDesktopIndex, ref desktopIid, out object targetDesktopObj);
        var targetDesktop = (IVirtualDesktop)targetDesktopObj;

        nint originalForeground = GetForegroundWindow();
        nint shellWindow = GetShellWindow();

        Log("Starting process minimized...");
        ProcessStartInfo psi = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Minimized
        };

        Process? targetProcess;
        try
        {
            targetProcess = Process.Start(psi);
        }
        catch (Exception exception)
        {
            Log("Process.Start failed: " + exception.Message);
            return;
        }

        Log("Rapidly polling for MainWindowHandle or Foreground change...");
        nint mainWindowHandle = nint.Zero;
        Stopwatch sw = Stopwatch.StartNew();
        string expectedName = Path.GetFileNameWithoutExtension(executablePath).ToLowerInvariant();

        while (sw.ElapsedMilliseconds < 5000)
        {
            if (targetProcess != null)
            {
                try
                {
                    targetProcess.Refresh();
                    if (targetProcess.MainWindowHandle != nint.Zero)
                    {
                        mainWindowHandle = targetProcess.MainWindowHandle;
                        break;
                    }
                }
                catch { }
            }

            nint currentForeground = GetForegroundWindow();
            if (currentForeground != nint.Zero && currentForeground != originalForeground && currentForeground != shellWindow)
            {
                bool isRelated = false;
                GetWindowThreadProcessId(currentForeground, out uint fgPid);

                if (targetProcess != null && fgPid == targetProcess.Id)
                {
                    isRelated = true;
                }
                else
                {
                    try
                    {
                        var fgProcess = Process.GetProcessById((int)fgPid);
                        string fgName = fgProcess.ProcessName.ToLowerInvariant();

                        if ((DateTime.Now - fgProcess.StartTime).TotalSeconds < 3)
                        {
                            isRelated = true;
                        }
                        else if (fgName.Contains("applicationframe") || fgName.Contains("photo") ||
                                 fgName.Contains("dllhost") || fgName.Contains(expectedName) || expectedName.Contains(fgName))
                        {
                            isRelated = true;
                        }
                        else
                        {
                            int length = GetWindowTextLength(currentForeground);
                            if (length > 0)
                            {
                                StringBuilder sb = new StringBuilder(length + 1);
                                GetWindowText(currentForeground, sb, sb.Capacity);
                                string title = sb.ToString().ToLowerInvariant();

                                if (title.Contains(expectedName))
                                {
                                    isRelated = true;
                                }
                            }
                        }
                    }
                    catch
                    {
                        isRelated = true;
                    }
                }

                if (isRelated)
                {
                    try
                    {
                        viewCollection.GetViewForHwnd(currentForeground, out IApplicationView? testView);
                        if (testView != null)
                        {
                            mainWindowHandle = currentForeground;
                            Log("App caught securely via Foreground focus change!");
                            break;
                        }
                    }
                    catch { }
                }
            }

            if (mainWindowHandle != nint.Zero) break;
            Thread.Sleep(20);
        }

        if (mainWindowHandle != nint.Zero)
        {
            try
            {
                IApplicationView? view = null;
                int retries = 10;

                while (retries > 0)
                {
                    try
                    {
                        viewCollection.GetViewForHwnd(mainWindowHandle, out view);
                        if (view != null) break;
                    }
                    catch
                    {
                        Thread.Sleep(10);
                        retries--;
                    }
                }

                if (view != null)
                {
                    Log("Moving View to Target Desktop...");
                    internalManager.MoveViewToDesktop(view, targetDesktop);
                    ShowWindow(mainWindowHandle, SW_RESTORE);

                    Log("Restoring original desktop view...");
                    internalManager.SwitchDesktop(originalDesktop);

                    Log("Move completed instantly in " + sw.ElapsedMilliseconds + "ms!");
                }
            }
            catch (Exception exception)
            {
                Log("Move failed: " + exception.Message);
            }
        }
        else
        {
            Log("Could not catch MainWindowHandle securely within 5 seconds.");
        }

        Log("LaunchInBackgroundDesktop finished.");
    }
}