using System;
using System.Threading.Tasks;

namespace Shared.Interfaces
{
    public enum ComputeDevice
    {
        Cpu,
        Gpu,
        Cuda, // Specialized Nvidia
        Rocm  // Specialized AMD
    }

    public interface IComputeService
    {
        ComputeDevice BestAvailableDevice { get; }
        bool IsSupported(ComputeDevice device);

        // Foundation for future heavy computations
        Task<T[]> DispatchAsync<T>(string kernelName, T[] data) where T : struct;
    }
}
