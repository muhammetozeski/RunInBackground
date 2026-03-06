using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace RunInBackGround;

internal class Program
{
    [STAThread]
    static void Main()
    {
        Log("Main method started.");
        LaunchInBackgroundDesktop(@"C:\E\!\Desktop\Eppie.tpmPas.lnk", 1);
        Log("Main method finished.");
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);
    private const int SW_RESTORE = 9;

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
        IVirtualDesktop CreateDesktop();
    }

    public static void LaunchInBackgroundDesktop(string executablePath, int targetDesktopIndex)
    {
        Log("Starting LaunchInBackgroundDesktop. Executable: " + executablePath);

        Log("Pre-instantiating ImmersiveShell for Windows 11...");
        Guid shellClsid = new("C2F03A33-21F5-47FA-B4BB-156362A2F239");
        Type? shellType = Type.GetTypeFromCLSID(shellClsid);

        if (shellType == null)
        {
            Log("ImmersiveShell type not found.");
            return;
        }

        var shellProvider = (IServiceProvider10)Activator.CreateInstance(shellType)!;

        Guid viewCollectionIid = typeof(IApplicationViewCollection).GUID;
        var viewCollection = (IApplicationViewCollection)shellProvider.QueryService(ref viewCollectionIid, ref viewCollectionIid);

        Guid managerInternalClsid = new("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");
        Guid managerInternalIid = typeof(IVirtualDesktopManagerInternal).GUID;
        var internalManager = (IVirtualDesktopManagerInternal)shellProvider.QueryService(ref managerInternalClsid, ref managerInternalIid);

        int desktopCount = internalManager.GetCount();
        while (desktopCount <= targetDesktopIndex)
        {
            internalManager.CreateDesktop();
            desktopCount = internalManager.GetCount();
        }

        internalManager.GetDesktops(out IObjectArray desktops);
        Guid desktopIid = typeof(IVirtualDesktop).GUID;
        desktops.GetAt(targetDesktopIndex, ref desktopIid, out object targetDesktopObj);
        var targetDesktop = (IVirtualDesktop)targetDesktopObj;

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

        if (targetProcess is null) return;

        Log("Rapidly polling for MainWindowHandle...");
        nint mainWindowHandle = nint.Zero;
        Stopwatch sw = Stopwatch.StartNew();
        string expectedName = Path.GetFileNameWithoutExtension(executablePath).ToLowerInvariant();

        while (sw.ElapsedMilliseconds < 5000)
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
            catch
            {
                // UWP süreçleri başlatıldıktan hemen sonra kapanabilir, bu normaldir.
            }

            // UWP/Modern App Fallback: Eğer asıl süreç kapandıysa veya HWND üretmediyse, yeni açılan ilgili süreçleri ara.
            var allProcesses = Process.GetProcesses();
            foreach (var p in allProcesses)
            {
                try
                {
                    if (p.MainWindowHandle != nint.Zero && p.Id != targetProcess.Id)
                    {
                        string pName = p.ProcessName.ToLowerInvariant();
                        if (pName.Contains(expectedName) || expectedName.Contains(pName))
                        {
                            bool isRecent = false;
                            try
                            {
                                // Yalnızca son 5 saniyede başlatılan süreçleri kontrol et.
                                isRecent = (DateTime.Now - p.StartTime).TotalSeconds < 5;
                            }
                            catch
                            {
                                // UWP uygulamalarında yetki hatası alınırsa, isme güvenerek devam et.
                                isRecent = true;
                            }

                            if (isRecent)
                            {
                                mainWindowHandle = p.MainWindowHandle;
                                Log("Fallback process found: " + p.ProcessName + " (PID: " + p.Id + ")");
                                break;
                            }
                        }
                    }
                }
                catch { }
            }

            if (mainWindowHandle != nint.Zero) break;

            Thread.Sleep(50); // CPU'yu yormamak için 50 ms bekle.
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
            Log("Could not catch MainWindowHandle within 5 seconds.");
        }

        Log("LaunchInBackgroundDesktop finished.");
    }
}