namespace FlashFix.Hardware;

public sealed record HardwareSnapshot(
    string Cpu, string Gpu, string Windows, string FormFactor,
    string Network, string Display, string Memory, string Storage,
    string StorageType, double? CpuPercent, double? GpuPercent,
    double? MemoryPercent, double? StorageUsedPercent, double? DiskActivityPercent,
    DateTime CapturedAt);
