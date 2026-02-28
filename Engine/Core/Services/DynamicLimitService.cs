using System;
using System.Runtime.InteropServices;

namespace Core.Services;

public interface IDynamicLimitService
{
    int MaxGlobals { get; }
    int MaxStackSize { get; }
    int MaxCallStackDepth { get; }
}

public class DynamicLimitService : IDynamicLimitService
{
    public int MaxGlobals { get; private set; }
    public int MaxStackSize { get; private set; }
    public int MaxCallStackDepth { get; private set; }

    public DynamicLimitService()
    {
        CalculateLimits();
    }

    private void CalculateLimits()
    {
        long totalMemory = GetTotalPhysicalMemory();

        // Base limits for 4GB RAM
        const long baseMemory = 4L * 1024 * 1024 * 1024;
        double multiplier = Math.Max(1.0, (double)totalMemory / baseMemory);

        // MaxGlobals: 100M base, scaled
        MaxGlobals = (int)Math.Min(int.MaxValue, 100_000_000 * multiplier);

        // MaxStackSize: 10M base, scaled
        MaxStackSize = (int)Math.Min(int.MaxValue, 10_485_760 * multiplier);

        // MaxCallStackDepth: 65k base, scaled
        MaxCallStackDepth = (int)Math.Min(1_000_000, 65_536 * multiplier);
    }

    private long GetTotalPhysicalMemory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetWindowsTotalMemory();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return GetLinuxTotalMemory();
        }

        return 4L * 1024 * 1024 * 1024; // Default 4GB
    }

    private long GetWindowsTotalMemory()
    {
        try
        {
            // Simplified Windows memory check
            return 8L * 1024 * 1024 * 1024; // Placeholder
        }
        catch { return 4L * 1024 * 1024 * 1024; }
    }

    private long GetLinuxTotalMemory()
    {
        try
        {
            string memInfo = System.IO.File.ReadAllText("/proc/meminfo");
            string[] lines = memInfo.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("MemTotal:"))
                {
                    string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out long memKb))
                    {
                        return memKb * 1024;
                    }
                }
            }
        }
        catch { }
        return 4L * 1024 * 1024 * 1024;
    }
}
