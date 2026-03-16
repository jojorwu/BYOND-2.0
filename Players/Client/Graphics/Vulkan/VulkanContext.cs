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
        private KhrSwapchain _khrSwapchain;
        private SwapchainKHR _swapchain;
        private Image[] _swapchainImages = null!;
        private ImageView[] _swapchainImageViews = null!;
        private Format _swapchainFormat;
        private Extent2D _swapchainExtent;
        private CommandPool _commandPool;
        private CommandBuffer[] _commandBuffers = null!;
        private Silk.NET.Vulkan.Semaphore _imageAvailableSemaphore;
        private Silk.NET.Vulkan.Semaphore _renderFinishedSemaphore;
        private Fence _inFlightFence;

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

            _khrSwapchain = new KhrSwapchain(_vk.Context);
            CreateSwapchain(window);
            CreateImageViews();
            CreateCommandPool();
            CreateCommandBuffers();
            CreateSyncObjects();
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

        private void CreateSwapchain(IWindow window)
        {
            _khrSurface.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice, _surface, out var capabilities);
            _swapchainExtent = capabilities.CurrentExtent;
            _swapchainFormat = Format.B8G8R8A8Unorm;

            var createInfo = new SwapchainCreateInfoKHR
            {
                SType = StructureType.SwapchainCreateInfoKhr,
                Surface = _surface,
                MinImageCount = Math.Max(capabilities.MinImageCount, 2),
                ImageFormat = _swapchainFormat,
                ImageColorSpace = ColorSpaceKHR.SpaceSrgbNonlinearKhr,
                ImageExtent = _swapchainExtent,
                ImageArrayLayers = 1,
                ImageUsage = ImageUsageFlags.ColorAttachmentBit,
                ImageSharingMode = SharingMode.Exclusive,
                PreTransform = capabilities.CurrentTransform,
                CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
                PresentMode = PresentModeKHR.FifoKhr,
                Clipped = true
            };

            if (_khrSwapchain.CreateSwapchain(_device, &createInfo, null, out _swapchain) != Result.Success)
            {
                throw new Exception("Failed to create Vulkan swapchain.");
            }

            uint imageCount = 0;
            _khrSwapchain.GetSwapchainImages(_device, _swapchain, ref imageCount, null);
            _swapchainImages = new Image[imageCount];
            fixed (Image* pImages = _swapchainImages)
            {
                _khrSwapchain.GetSwapchainImages(_device, _swapchain, ref imageCount, pImages);
            }
        }

        private void CreateImageViews()
        {
            _swapchainImageViews = new ImageView[_swapchainImages.Length];
            for (int i = 0; i < _swapchainImages.Length; i++)
            {
                var createInfo = new ImageViewCreateInfo
                {
                    SType = StructureType.ImageViewCreateInfo,
                    Image = _swapchainImages[i],
                    ViewType = ImageViewType.Type2D,
                    Format = _swapchainFormat,
                    SubresourceRange = new ImageSubresourceRange
                    {
                        AspectMask = ImageAspectFlags.ColorBit,
                        BaseMipLevel = 0,
                        LevelCount = 1,
                        BaseArrayLayer = 0,
                        LayerCount = 1
                    }
                };

                if (_vk.CreateImageView(_device, &createInfo, null, out _swapchainImageViews[i]) != Result.Success)
                {
                    throw new Exception("Failed to create Vulkan image view.");
                }
            }
        }

        private void CreateCommandPool()
        {
            var poolInfo = new CommandPoolCreateInfo
            {
                SType = StructureType.CommandPoolCreateInfo,
                QueueFamilyIndex = _graphicsQueueFamilyIndex,
                Flags = CommandPoolCreateFlags.ResetCommandBufferBit
            };

            if (_vk.CreateCommandPool(_device, &poolInfo, null, out _commandPool) != Result.Success)
            {
                throw new Exception("Failed to create Vulkan command pool.");
            }
        }

        private void CreateCommandBuffers()
        {
            _commandBuffers = new CommandBuffer[_swapchainImages.Length];
            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = _commandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = (uint)_commandBuffers.Length
            };

            fixed (CommandBuffer* pCommandBuffers = _commandBuffers)
            {
                if (_vk.AllocateCommandBuffers(_device, &allocInfo, pCommandBuffers) != Result.Success)
                {
                    throw new Exception("Failed to allocate Vulkan command buffers.");
                }
            }
        }

        private void CreateSyncObjects()
        {
            var semaphoreInfo = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };
            var fenceInfo = new FenceCreateInfo
            {
                SType = StructureType.FenceCreateInfo,
                Flags = FenceCreateFlags.SignaledBit
            };

            if (_vk.CreateSemaphore(_device, in semaphoreInfo, null, out _imageAvailableSemaphore) != Result.Success ||
                _vk.CreateSemaphore(_device, in semaphoreInfo, null, out _renderFinishedSemaphore) != Result.Success ||
                _vk.CreateFence(_device, in fenceInfo, null, out _inFlightFence) != Result.Success)
            {
                throw new Exception("Failed to create Vulkan synchronization objects.");
            }
        }

        private void TransitionImageLayout(CommandBuffer commandBuffer, Image image, ImageLayout oldLayout, ImageLayout newLayout)
        {
            var barrier = new ImageMemoryBarrier
            {
                SType = StructureType.ImageMemoryBarrier,
                OldLayout = oldLayout,
                NewLayout = newLayout,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = image,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1)
            };

            PipelineStageFlags sourceStage;
            PipelineStageFlags destinationStage;

            if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
            {
                barrier.SrcAccessMask = 0;
                barrier.DstAccessMask = AccessFlags.TransferWriteBit;
                sourceStage = PipelineStageFlags.TopOfPipeBit;
                destinationStage = PipelineStageFlags.TransferBit;
            }
            else if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.PresentSrcKhr)
            {
                barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
                barrier.DstAccessMask = 0;
                sourceStage = PipelineStageFlags.TransferBit;
                destinationStage = PipelineStageFlags.BottomOfPipeBit;
            }
            else
            {
                throw new Exception("Unsupported layout transition.");
            }

            _vk.CmdPipelineBarrier(commandBuffer, sourceStage, destinationStage, 0, 0, null, 0, null, 1, &barrier);
        }

        public void Render()
        {
            _vk.WaitForFences(_device, 1, in _inFlightFence, true, ulong.MaxValue);
            _vk.ResetFences(_device, 1, in _inFlightFence);

            uint imageIndex = 0;
            _khrSwapchain.AcquireNextImage(_device, _swapchain, ulong.MaxValue, _imageAvailableSemaphore, default, ref imageIndex);

            var commandBuffer = _commandBuffers[imageIndex];
            _vk.ResetCommandBuffer(commandBuffer, CommandBufferResetFlags.None);

            var beginInfo = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo };
            _vk.BeginCommandBuffer(commandBuffer, in beginInfo);

            TransitionImageLayout(commandBuffer, _swapchainImages[imageIndex], ImageLayout.Undefined, ImageLayout.TransferDstOptimal);

            var clearColor = new ClearColorValue(0.1f, 0.1f, 0.1f, 1.0f);
            var range = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1);
            _vk.CmdClearColorImage(commandBuffer, _swapchainImages[imageIndex], ImageLayout.TransferDstOptimal, &clearColor, 1, &range);

            TransitionImageLayout(commandBuffer, _swapchainImages[imageIndex], ImageLayout.TransferDstOptimal, ImageLayout.PresentSrcKhr);

            _vk.EndCommandBuffer(commandBuffer);

            var waitSemaphores = stackalloc Silk.NET.Vulkan.Semaphore[] { _imageAvailableSemaphore };
            var waitStages = stackalloc PipelineStageFlags[] { PipelineStageFlags.ColorAttachmentOutputBit };
            var signalSemaphores = stackalloc Silk.NET.Vulkan.Semaphore[] { _renderFinishedSemaphore };

            var submitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = waitSemaphores,
                PWaitDstStageMask = waitStages,
                CommandBufferCount = 1,
                PCommandBuffers = &commandBuffer,
                SignalSemaphoreCount = 1,
                PSignalSemaphores = signalSemaphores
            };

            Queue queue;
            _vk.GetDeviceQueue(_device, _graphicsQueueFamilyIndex, 0, out queue);
            _vk.QueueSubmit(queue, 1, &submitInfo, _inFlightFence);

            var swapchains = stackalloc SwapchainKHR[] { _swapchain };
            var presentInfo = new PresentInfoKHR
            {
                SType = StructureType.PresentInfoKhr,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = signalSemaphores,
                SwapchainCount = 1,
                PSwapchains = swapchains,
                PImageIndices = &imageIndex
            };

            _khrSwapchain.QueuePresent(queue, &presentInfo);
        }

        public void Dispose()
        {
            _vk.DestroySemaphore(_device, _imageAvailableSemaphore, null);
            _vk.DestroySemaphore(_device, _renderFinishedSemaphore, null);
            _vk.DestroyFence(_device, _inFlightFence, null);
            _vk.DestroyCommandPool(_device, _commandPool, null);
            foreach (var imageView in _swapchainImageViews)
            {
                _vk.DestroyImageView(_device, imageView, null);
            }
            _khrSwapchain.DestroySwapchain(_device, _swapchain, null);
            _vk.DestroyDevice(_device, null);
            _khrSurface.DestroySurface(_instance, _surface, null);
            _vk.DestroyInstance(_instance, null);
            _vk.Dispose();
        }
    }
}
