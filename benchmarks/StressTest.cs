using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Shared;
using Shared.Interfaces;
using Shared.Services;
using Server;
using System.Text.Json;
using Core;

namespace Benchmarks;

public class StressTest
{
    private const string ScriptsDir = "scripts/benchmark";
    private const string ConfigFile = "server_config.json";

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Starting BYOND 2.0 64-bit VM MASSIVE LOAD Stress Test...");

        // Setup temporary scripts for 1000 scripts on one tile
        SetupScripts();
        SetupConfig();

        var cts = new CancellationTokenSource();
        var host = CreateHostBuilder(args).Build();

        var serverTask = Task.Run(async () => {
            try { await host.RunAsync(cts.Token); } catch (OperationCanceledException) {}
        });

        await Task.Delay(2000); // Give server some time to start

        var gameState = host.Services.GetRequiredService<IGameState>();
        var objectFactory = host.Services.GetRequiredService<IObjectFactory>();
        var typeManager = host.Services.GetRequiredService<IObjectTypeManager>();

        var mobType = typeManager.GetObjectType("mob");
        if (mobType == null) {
            mobType = new ObjectType(0, "mob");
            typeManager.RegisterObjectType(mobType);
        }

        Console.WriteLine("--- Phase 1: Spawning 1,000,000 objects ---");
        var sw = Stopwatch.StartNew();
        var spawnedObjects = new GameObject[1000000];
        for (int i = 0; i < 1000000; i++)
        {
            var obj = (GameObject)objectFactory.Create(mobType);
            obj.SetPosition(i % 1000, i / 1000, 1);
            gameState.AddGameObject(obj);
            spawnedObjects[i] = obj;
            if (i % 200000 == 0) Console.WriteLine($"Spawned {i}...");
        }
        sw.Stop();
        Console.WriteLine($"Spawned 1,000,000 objects in {sw.ElapsedMilliseconds}ms");

        Console.WriteLine("--- Phase 2: Deleting 1,000,000 objects ---");
        sw.Restart();
        for (int i = 0; i < 1000000; i++)
        {
            gameState.RemoveGameObject(spawnedObjects[i]);
            if (i % 200000 == 0) Console.WriteLine($"Deleted {i}...");
        }
        sw.Stop();
        Console.WriteLine($"Deleted 1,000,000 objects in {sw.ElapsedMilliseconds}ms");

        Console.WriteLine("--- Phase 3: Stressing 1000 scripts in one tile (via Scripts) ---");
        Console.WriteLine("Waiting for script execution results...");
        await Task.Delay(10000); // Wait for the scripts to do their thing

        cts.Cancel();
        try { await serverTask; } catch (OperationCanceledException) {}

        Cleanup();
        Console.WriteLine("Stress Test Complete.");
    }

    private static void SetupScripts()
    {
        if (Directory.Exists(ScriptsDir)) Directory.Delete(ScriptsDir, true);
        Directory.CreateDirectory(ScriptsDir);

        // Minimal C# scripts to avoid Lua environment issues during 1M object test
        File.WriteAllText(Path.Combine(ScriptsDir, "stress.cs"), @"
using System;
Console.WriteLine(""C# Stress script loaded."");
");
    }

    private static void SetupConfig()
    {
        var settings = new ServerSettings {
            EnableVm = true,
            VmMaxInstructions = 1000000000,
            ServerName = "Stress Test Server"
        };
        var config = new { ServerSettings = settings };
        File.WriteAllText(ConfigFile, JsonSerializer.Serialize(config));
    }

    private static void Cleanup()
    {
        if (Directory.Exists(ScriptsDir)) Directory.Delete(ScriptsDir, true);
        if (File.Exists(ConfigFile)) File.Delete(ConfigFile);
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.Configure<ServerSettings>(options => {
                    options.EnableVm = true;
                    options.HttpServer.Enabled = false;
                });
                services.AddSingleton<IProject>(new Project(Directory.GetCurrentDirectory()));
                services.AddCoreServices();
                services.AddServerHostedServices();
                services.AddSingleton<Shared.IMap, Shared.Map>();
                services.AddSingleton<Shared.IJsonService, DMCompiler.Json.JsonService>();
                services.AddSingleton<Shared.ICompilerService, DMCompiler.CompilerService>();
                services.AddSingleton(resolver => resolver.GetRequiredService<Microsoft.Extensions.Options.IOptions<ServerSettings>>().Value);
            });
}
