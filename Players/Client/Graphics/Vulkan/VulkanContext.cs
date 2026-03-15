using System;
using System.Collections.Generic;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace Client.Graphics.Vulkan
{
    public unsafe class VulkanContext : IDisposable
    {
        private Vk _vk;
        private Instance _instance;
        private Device _device;
        private PhysicalDevice _physicalDevice;
        private SurfaceKHR _surface;
        private KhrSurface _khrSurface;

        public Vk Vk => _vk;
        public Instance Instance => _instance;
        public Device Device => _device;
        public PhysicalDevice PhysicalDevice => _physicalDevice;

        public VulkanContext(IWindow window)
        {
            _vk = Vk.GetApi();
            CreateInstance(window);

            if (window.VkSurface == null)
                throw new Exception("Vulkan surface not supported by window.");

            _khrSurface = new KhrSurface(_vk.Context);
            var handle = window.VkSurface.Create<SurfaceKHR>(new Silk.NET.Core.Native.VkHandle(_instance.Handle), null);
            _surface = new SurfaceKHR(handle.Handle);

            SelectPhysicalDevice();
            CreateLogicalDevice();
        }

        private void CreateInstance(IWindow window)
        {
            byte* pAppName = (byte*)System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi("BYOND 2.0");
            byte* pEngineName = (byte*)System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi("RobustEngine");

            try {
                var appInfo = new ApplicationInfo
                {
                    SType = StructureType.ApplicationInfo,
                    PApplicationName = pAppName,
                    ApplicationVersion = Vk.MakeVersion(1, 0, 0),
                    PEngineName = pEngineName,
                    EngineVersion = Vk.MakeVersion(1, 0, 0),
                    ApiVersion = Vk.Version11
                };

                var extensions = window.VkSurface!.GetRequiredExtensions(out uint count);
                var extensionNames = new List<string>();
                for (int i = 0; i < count; i++)
                {
                    extensionNames.Add(System.Runtime.InteropServices.Marshal.PtrToStringAnsi((IntPtr)extensions[i])!);
                }

                byte** ppExtensions = (byte**)Silk.NET.Core.Native.SilkMarshal.StringArrayToPtr(extensionNames);
                try {
                    var createInfo = new InstanceCreateInfo
                    {
                        SType = StructureType.InstanceCreateInfo,
                        PApplicationInfo = &appInfo,
                        EnabledExtensionCount = (uint)extensionNames.Count,
                        PpEnabledExtensionNames = ppExtensions
                    };

                    if (_vk.CreateInstance(&createInfo, null, out _instance) != Result.Success)
                    {
                        throw new Exception("Failed to create Vulkan instance.");
                    }
                } finally {
                    Silk.NET.Core.Native.SilkMarshal.Free((nint)ppExtensions);
                }

                _vk.CurrentInstance = _instance;
            } finally {
                System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)pAppName);
                System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)pEngineName);
            }
        }

        private uint _graphicsQueueFamilyIndex;

        private void SelectPhysicalDevice()
        {
            uint deviceCount = 0;
            _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, null);
            if (deviceCount == 0) throw new Exception("No Vulkan physical devices found.");

            var devices = stackalloc PhysicalDevice[(int)deviceCount];
            _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, devices);

            for (int i = 0; i < deviceCount; i++)
            {
                if (IsDeviceSuitable(devices[i]))
                {
                    _physicalDevice = devices[i];
                    break;
                }
            }

            if (_physicalDevice.Handle == 0) throw new Exception("No suitable Vulkan physical device found.");
        }

        private bool IsDeviceSuitable(PhysicalDevice device)
        {
            uint queueFamilyCount = 0;
            _vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);
            var queueFamilies = stackalloc QueueFamilyProperties[(int)queueFamilyCount];
            _vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, queueFamilies);

            for (uint i = 0; i < queueFamilyCount; i++)
            {
                _khrSurface.GetPhysicalDeviceSurfaceSupport(device, i, _surface, out var presentSupport);
                if ((queueFamilies[i].QueueFlags & QueueFlags.GraphicsBit) != 0 && presentSupport)
                {
                    _graphicsQueueFamilyIndex = i;
                    return true;
                }
            }

            return false;
        }

        private void CreateLogicalDevice()
        {
            float queuePriority = 1.0f;
            var queueCreateInfo = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = _graphicsQueueFamilyIndex,
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };

            var deviceExtensions = new List<string> { KhrSwapchain.ExtensionName };
            byte** ppExtensions = (byte**)Silk.NET.Core.Native.SilkMarshal.StringArrayToPtr(deviceExtensions);

            try {
                var createInfo = new DeviceCreateInfo
                {
                    SType = StructureType.DeviceCreateInfo,
                    QueueCreateInfoCount = 1,
                    PQueueCreateInfos = &queueCreateInfo,
                    EnabledExtensionCount = (uint)deviceExtensions.Count,
                    PpEnabledExtensionNames = ppExtensions
                };

                if (_vk.CreateDevice(_physicalDevice, &createInfo, null, out _device) != Result.Success)
                {
                    throw new Exception("Failed to create Vulkan logical device.");
                }
            } finally {
                Silk.NET.Core.Native.SilkMarshal.Free((nint)ppExtensions);
            }

            _vk.CurrentDevice = _device;
        }

        public void Dispose()
        {
            _vk.DestroyDevice(_device, null);
            _khrSurface.DestroySurface(_instance, _surface, null);
            _vk.DestroyInstance(_instance, null);
            _vk.Dispose();
        }
    }
}
