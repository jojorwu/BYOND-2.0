using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Shared;
using Shared.Services;
using Server;
using System.Text.Json;
using Core;

namespace Benchmarks;

public class BenchmarkTool
{
    private const string ScriptsDir = "scripts/benchmark";
    private const string ConfigFile = "server_config.json";

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Starting BYOND 2.0 64-bit VM Benchmark...");

        // Setup temporary scripts
        SetupScripts();

        // Setup config with VM enabled
        SetupConfig();

        var cts = new CancellationTokenSource();
        var host = CreateHostBuilder(args).Build();

        var serverTask = Task.Run(async () => {
            try {
                await host.RunAsync(cts.Token);
            } catch (OperationCanceledException) {}
            catch (Exception ex) {
                Console.WriteLine($"Server error: {ex.Message}");
            }
        });

        Console.WriteLine("Server started. Monitoring performance for 10 seconds...");

        var process = Process.GetCurrentProcess();

        // Performance data collection
        var ramUsage = new List<long>();
        var cpuUsage = new List<float>();

        try {
            for (int i = 0; i < 10; i++)
            {
                ramUsage.Add(process.WorkingSet64);
                cpuUsage.Add(GetCpuUsage(process));
                Console.WriteLine($"Step {i+1}/10: RAM={ramUsage.Last()/1024/1024}MB, CPU={cpuUsage.Last():F2}%");
                await Task.Delay(1000);
            }
        } catch (Exception ex) {
            Console.WriteLine($"Error during monitoring: {ex.Message}");
        }

        Console.WriteLine("Stopping server...");
        cts.Cancel();
        try { await serverTask; } catch (OperationCanceledException) {}

        Cleanup();

        Console.WriteLine("\n--- Benchmark Results ---");
        Console.WriteLine($"Average RAM Usage: {ramUsage.Average() / 1024 / 1024:F2} MB");
        Console.WriteLine($"Peak RAM Usage: {ramUsage.Max() / 1024 / 1024:F2} MB");
        Console.WriteLine($"Average CPU Usage: {cpuUsage.Average():F2}%");
        Console.WriteLine($"Peak CPU Usage: {cpuUsage.Max():F2}%");
        Console.WriteLine("--------------------------");
    }

    private static float GetCpuUsage(Process process)
    {
        var startTime = DateTime.UtcNow;
        var startCpuUsage = process.TotalProcessorTime;
        Thread.Sleep(100);
        var endTime = DateTime.UtcNow;
        var endCpuUsage = process.TotalProcessorTime;
        var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
        var totalMsPassed = (endTime - startTime).TotalMilliseconds;
        var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
        return (float)(cpuUsageTotal * 100);
    }

    private static void SetupScripts()
    {
        if (Directory.Exists(ScriptsDir)) Directory.Delete(ScriptsDir, true);
        Directory.CreateDirectory(ScriptsDir);

        // DM Script
        File.WriteAllText(Path.Combine(ScriptsDir, "bench.dm"), @"
/world/New()
    ..()
    for(var/z=1; z<=10; z++)
        new /mob(locate(1,1,z))
");

        // Lua Script
        File.WriteAllText(Path.Combine(ScriptsDir, "bench.lua"), @"
function OnServerStart()
    for z=1,10 do
        Game:CreateObject(1, 1, 1, z)
    end
end
");

        // C# Script
        File.WriteAllText(Path.Combine(ScriptsDir, "bench.cs"), @"
for (int z = 1; z <= 10; z++) {
    Game.Objects.CreateObject(1, 1, 1, z);
}
");
    }

    private static void SetupConfig()
    {
        var settings = new ServerSettings {
            EnableVm = true,
            VmMaxInstructions = 1000000000,
            ServerName = "Benchmark Server"
        };
        var config = new { ServerSettings = settings };
        File.WriteAllText(ConfigFile, JsonSerializer.Serialize(config));
    }

    private static void Cleanup()
    {
        if (Directory.Exists(ScriptsDir))
            Directory.Delete(ScriptsDir, true);

        if (File.Exists(ConfigFile))
            File.Delete(ConfigFile);
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.Configure<ServerSettings>(options => {
                    options.EnableVm = true;
                    options.Performance.TickRate = 100; // Fast ticks for benchmark
                });
                services.AddSingleton<IProject>(new Project("."));
                services.AddCoreServices();
                services.AddServerHostedServices();

                services.AddSingleton<Shared.IMap, Shared.Map>();
                services.AddSingleton<Shared.IJsonService, DMCompiler.Json.JsonService>();
                services.AddSingleton<Shared.ICompilerService, DMCompiler.CompilerService>();
                services.AddSingleton(resolver => resolver.GetRequiredService<Microsoft.Extensions.Options.IOptions<ServerSettings>>().Value);
            });
}
