using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;

namespace FlashFix.Hardware;

[SupportedOSPlatform("windows")]
public sealed class WindowsHardwareDetector
{
    public Task<HardwareSnapshot> CaptureAsync() => Task.Run(Capture);

    private static HardwareSnapshot Capture()
    {
        var cpu = Text(First("SELECT Name FROM Win32_Processor", "Name"), "CPU não identificada");
        var video = FirstVideo();
        var os = Text(First("SELECT Caption FROM Win32_OperatingSystem", "Caption"),
            Environment.OSVersion.Version.Build >= 22000 ? "Windows 11" : "Windows 10");
        var machineType = Number(First("SELECT PCSystemType FROM Win32_ComputerSystem", "PCSystemType"));
        var formFactor = machineType == 2 || ChassisType() is 8 or 9 or 10 or 14
            ? "Notebook" : machineType == 1 ? "Desktop" : "Não identificado";
        var activeNetwork = NetworkInterface.GetAllNetworkInterfaces()
            .Where(x => x.OperationalStatus == OperationalStatus.Up &&
                x.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
            .OrderBy(x => x.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 0 : 1)
            .FirstOrDefault();
        var network = activeNetwork is null ? "Nenhuma conexão identificada" :
            $"{(activeNetwork.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? "Ethernet" : "Wi-Fi")} · {activeNetwork.Name}";

        var totalKb = Number(First("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem", "TotalVisibleMemorySize"));
        var freeKb = Number(First("SELECT FreePhysicalMemory FROM Win32_OperatingSystem", "FreePhysicalMemory"));
        var memory = totalKb > 0 ? $"{totalKb / 1024d / 1024d:0.#} GB" : "Não identificada";
        var memoryPercent = totalKb > 0 && freeKb <= totalKb
            ? Math.Clamp((totalKb - freeKb) * 100d / totalKb, 0, 100) : (double?)null;

        var volumes = LocalVolumes();
        var totalBytes = volumes.Sum(x => x.Total);
        var freeBytes = volumes.Sum(x => x.Free);
        var storage = totalBytes > 0 ? $"{totalBytes / 1024d / 1024d / 1024d:0} GB" : "Não identificado";
        var storagePercent = totalBytes > 0 ? Math.Clamp((totalBytes - freeBytes) * 100d / totalBytes, 0, 100) : (double?)null;
        var storageType = PhysicalDiskTypes();
        var cpuLoad = CpuUsagePercent();

        var resolution = video.Width > 0 && video.Height > 0
            ? $"{video.Width} × {video.Height}" + (video.Hertz > 0 ? $" · {video.Hertz} Hz" : "")
            : "Não identificada";

        var gpuLoad = video.Name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)
            ? NvidiaUsagePercent() : null;
        return new HardwareSnapshot(cpu, video.Name, os.Trim(), formFactor, network, resolution,
            memory, storage, storageType, cpuLoad, gpuLoad,
            memoryPercent, storagePercent, null, DateTime.Now);
    }

    private static object? First(string query, string property, string scope = "root\\cimv2")
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(scope, query);
            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item) return item[property];
            }
        }
        catch (ManagementException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }

    private static (string Name, ulong Width, ulong Height, ulong Hertz) FirstVideo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\cimv2",
                "SELECT Name,CurrentHorizontalResolution,CurrentVerticalResolution,CurrentRefreshRate FROM Win32_VideoController");
            using var results = searcher.Get();
            var videos = new List<(string Name, ulong Width, ulong Height, ulong Hertz)>();
            foreach (ManagementObject item in results)
            {
                using (item) videos.Add((Text(item["Name"], "GPU não identificada"),
                    Number(item["CurrentHorizontalResolution"]), Number(item["CurrentVerticalResolution"]),
                    Number(item["CurrentRefreshRate"])));
            }
            return videos.OrderByDescending(x => x.Width * x.Height).FirstOrDefault() is var best && best.Name is not null
                ? best : ("GPU não identificada", 0, 0, 0);
        }
        catch (ManagementException) { return ("GPU não identificada", 0, 0, 0); }
        catch (UnauthorizedAccessException) { return ("GPU não identificada", 0, 0, 0); }
    }

    private static List<(double Total, double Free)> LocalVolumes()
    {
        var result = new List<(double Total, double Free)>();
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\cimv2",
                "SELECT Size,FreeSpace FROM Win32_LogicalDisk WHERE DriveType=3");
            using var volumes = searcher.Get();
            foreach (ManagementObject volume in volumes)
            {
                using (volume) result.Add((Number(volume["Size"]), Number(volume["FreeSpace"])));
            }
        }
        catch (ManagementException) { }
        catch (UnauthorizedAccessException) { }
        return result;
    }

    private static string PhysicalDiskTypes()
    {
        var types = new HashSet<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\Microsoft\\Windows\\Storage",
                "SELECT MediaType FROM MSFT_PhysicalDisk");
            using var disks = searcher.Get();
            foreach (ManagementObject disk in disks)
            {
                using (disk)
                {
                    var label = Number(disk["MediaType"]) switch { 3 => "HDD", 4 => "SSD", 5 => "SCM", _ => null };
                    if (label is not null) types.Add(label);
                }
            }
        }
        catch (ManagementException) { }
        catch (UnauthorizedAccessException) { }
        return types.Count == 0 ? "Tipo não identificado" : string.Join(" + ", types.Order());
    }

    private static ulong ChassisType()
    {
        var value = First("SELECT ChassisTypes FROM Win32_SystemEnclosure", "ChassisTypes");
        return value is Array entries && entries.Length > 0 ? Number(entries.GetValue(0)) : Number(value);
    }

    private static string Text(object? value, string fallback) =>
        value?.ToString() is { Length: > 0 } text ? text.Trim() : fallback;

    private static ulong Number(object? value) =>
        ulong.TryParse(value?.ToString(), out var result) ? result : 0;

    private static double? CpuUsagePercent()
    {
        if (!GetSystemTimes(out var idleStart, out var kernelStart, out var userStart))
            return null;
        Thread.Sleep(500);
        if (!GetSystemTimes(out var idleEnd, out var kernelEnd, out var userEnd))
            return null;
        var idle = idleEnd.Value - idleStart.Value;
        var total = kernelEnd.Value - kernelStart.Value + userEnd.Value - userStart.Value;
        return total == 0 ? null : Math.Clamp(100d * (1d - (double)idle / total), 0, 100);
    }

    private static double? NvidiaUsagePercent()
    {
        try
        {
            if (NvmlInit() != 0) return null;
            try
            {
                if (NvmlDeviceByIndex(0, out var device) != 0) return null;
                if (NvmlUtilization(device, out var utilization) != 0) return null;
                return utilization.Gpu <= 100 ? utilization.Gpu : null;
            }
            finally { NvmlShutdown(); }
        }
        catch (DllNotFoundException) { return null; }
        catch (EntryPointNotFoundException) { return null; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NvmlUtilizationData
    {
        public uint Gpu;
        public uint Memory;
    }

    [DllImport("nvml.dll", EntryPoint = "nvmlInit_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int NvmlInit();
    [DllImport("nvml.dll", EntryPoint = "nvmlShutdown", CallingConvention = CallingConvention.Cdecl)]
    private static extern int NvmlShutdown();
    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetHandleByIndex_v2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int NvmlDeviceByIndex(uint index, out IntPtr device);
    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetUtilizationRates", CallingConvention = CallingConvention.Cdecl)]
    private static extern int NvmlUtilization(IntPtr device, out NvmlUtilizationData utilization);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
        public readonly ulong Value => ((ulong)High << 32) | Low;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
}
