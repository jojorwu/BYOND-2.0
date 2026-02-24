using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Shared.Interfaces;
using Microsoft.Extensions.Logging;

using System.Numerics;

namespace Shared.Services;
    public class ComputeService : IComputeService, IAsyncInitializable
    {
        private readonly ILogger<ComputeService>? _logger;
        public ComputeDevice BestAvailableDevice { get; private set; }
        public bool HasSimdSupport => Vector.IsHardwareAccelerated;

        public ComputeService(ILogger<ComputeService>? logger = null)
        {
            _logger = logger;
            BestAvailableDevice = ComputeDevice.Cpu;
        }

        public Task InitializeAsync()
        {
            return Task.Run(DetectCapabilities);
        }

        private void DetectCapabilities()
        {
            BestAvailableDevice = ComputeDevice.Cpu;

            // Foundation for detection logic
            try
            {
                if (IsNvidiaDetected())
                {
                    _logger?.LogInformation("Nvidia GPU detected. CUDA support available.");
                    BestAvailableDevice = ComputeDevice.Cuda;
                }
                else if (IsAmdDetected())
                {
                    _logger?.LogInformation("AMD GPU detected. ROCm/OpenCL support available.");
                    BestAvailableDevice = ComputeDevice.Rocm;
                }
                else
                {
                    _logger?.LogInformation("No specialized GPU backend detected. Using CPU/Generic GPU.");
                }

                if (HasSimdSupport)
                {
                    _logger?.LogInformation("SIMD hardware acceleration is available (Vector size: {Size} bits).", Vector<float>.Count * sizeof(float) * 8);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error detecting hardware capabilities.");
            }
        }

        private bool IsNvidiaDetected()
        {
            // Simple check for Nvidia drivers or libs
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return File.Exists("C:\\Windows\\System32\\nvcuda.dll");
            }
            return false; // Linux check would be different (e.g., /proc/driver/nvidia)
        }

        private bool IsAmdDetected()
        {
            // Simple check for AMD libs
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return File.Exists("C:\\Windows\\System32\\amdocl64.dll");
            }
            return false;
        }

        public bool IsSupported(ComputeDevice device)
        {
            if (device == ComputeDevice.Cpu) return true;
            if (device == ComputeDevice.Cuda) return IsNvidiaDetected();
            if (device == ComputeDevice.Rocm) return IsAmdDetected();
            return false;
        }

        public Task<T[]> DispatchAsync<T>(string kernelName, T[] data) where T : struct
        {
            _logger?.LogDebug("Dispatching compute task '{Kernel}' to {Device}", kernelName, BestAvailableDevice);

            // For now, fall back to CPU-based processing
            return Task.FromResult(data);
        }
    }
