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

        public void CalculateDistancesSIMD(ReadOnlySpan<long> x1, ReadOnlySpan<long> y1, ReadOnlySpan<long> x2, ReadOnlySpan<long> y2, Span<double> results)
        {
            int n = results.Length;
            if (Vector.IsHardwareAccelerated && n >= Vector<long>.Count)
            {
                int i = 0;
                int vectorSize = Vector<long>.Count;
                for (; i <= n - vectorSize; i += vectorSize)
                {
                    var vx1 = new Vector<long>(x1.Slice(i));
                    var vy1 = new Vector<long>(y1.Slice(i));
                    var vx2 = new Vector<long>(x2.Slice(i));
                    var vy2 = new Vector<long>(y2.Slice(i));

                    var dx = Vector.Abs(vx1 - vx2);
                    var dy = Vector.Abs(vy1 - vy2);

                    // Chebyshev distance: max(|x1-x2|, |y1-y2|)
                    var dist = Vector.Max(dx, dy);

                    for (int j = 0; j < vectorSize; j++) results[i + j] = dist[j];
                }
                for (; i < n; i++) results[i] = Math.Max(Math.Abs(x1[i] - x2[i]), Math.Abs(y1[i] - y2[i]));
            }
            else
            {
                for (int i = 0; i < n; i++) results[i] = Math.Max(Math.Abs(x1[i] - x2[i]), Math.Abs(y1[i] - y2[i]));
            }
        }
    }
