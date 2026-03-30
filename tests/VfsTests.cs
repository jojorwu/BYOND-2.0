using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shared.Interfaces;
using Shared.Services;
using NUnit.Framework;

namespace Tests;

[TestFixture]
public class VfsTests
{
    private string _tempPath = null!;

    [SetUp]
    public void SetUp()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), "vfs_tests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempPath))
            Directory.Delete(_tempPath, true);
    }

    [Test]
    public async Task LocalVfsSource_ReadsFile()
    {
        var filePath = Path.Combine(_tempPath, "test.txt");
        await File.WriteAllTextAsync(filePath, "Hello VFS!");

        using var source = new LocalVfsSource("local", _tempPath);
        Assert.That(source.Exists("test.txt"), Is.True);

        using var stream = await source.OpenReadAsync("test.txt");
        Assert.That(stream, Is.Not.Null);
        using var reader = new StreamReader(stream!);
        var content = await reader.ReadToEndAsync();
        Assert.That(content, Is.EqualTo("Hello VFS!"));
    }

    [Test]
    public async Task VfsManager_PriorityResolution()
    {
        var dir1 = Path.Combine(_tempPath, "dir1");
        var dir2 = Path.Combine(_tempPath, "dir2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        await File.WriteAllTextAsync(Path.Combine(dir1, "test.txt"), "Dir 1");
        await File.WriteAllTextAsync(Path.Combine(dir2, "test.txt"), "Dir 2");

        var manager = new VfsManager();
        manager.Mount(new LocalVfsSource("low", dir1, priority: 0));
        manager.Mount(new LocalVfsSource("high", dir2, priority: 10));

        using var stream = await manager.OpenReadAsync("test.txt");
        using var reader = new StreamReader(stream!);
        var content = await reader.ReadToEndAsync();

        Assert.That(content, Is.EqualTo("Dir 2")); // High priority wins
    }

    [Test]
    public async Task ResourceSystem_HotReloading()
    {
        var filePath = Path.Combine(_tempPath, "test.txt");
        await File.WriteAllTextAsync(filePath, "Original");

        var services = new ServiceCollection();
        services.AddSingleton<IDiagnosticBus, Shared.Services.DiagnosticBus>();
        services.AddSingleton<IVfsManager, VfsManager>();
        services.AddSingleton<IResourceSystem, ResourceSystem>();
        services.AddSingleton<ISoundRegistry, MockSoundRegistry>();
        services.AddSingleton<IResourceLoader<SoundDefinition>, SoundLoader>();

        var sp = services.BuildServiceProvider();
        var vfs = sp.GetRequiredService<IVfsManager>();
        var resourceSystem = sp.GetRequiredService<IResourceSystem>();
        resourceSystem.RegisterLoader(sp.GetRequiredService<IResourceLoader<SoundDefinition>>());

        vfs.Mount(new LocalVfsSource("local", _tempPath, watchForChanges: true));

        // Load initially
        var sound = await resourceSystem.LoadResourceAsync<SoundDefinition>("test.txt");
        Assert.That(sound, Is.Not.Null);

        bool reloaded = false;
        resourceSystem.ResourceReloaded += (path) => { if (path == "test.txt") reloaded = true; };

        // Change file
        await File.WriteAllTextAsync(filePath, "Updated");

        // Wait for FileSystemWatcher (can be slow)
        for (int i = 0; i < 20 && !reloaded; i++) await Task.Delay(100);

        Assert.That(reloaded, Is.True);
    }

    private class MockSoundRegistry : ISoundRegistry
    {
        public bool TryGetSound(string path, out SoundDefinition? definition)
        {
            definition = null;
            return false;
        }
    }
}
