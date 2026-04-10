using Shared;
using Shared.Attributes;
using Shared.Interfaces;
using Robust.Shared.Maths;

namespace Core.Api;

[EngineService(typeof(ISoundApi))]
public class SoundApi : ISoundApi
{
    public string Name => "Sounds";
    private readonly IUdpServer _udpServer;
    private readonly IGameState _gameState;
    private readonly IRegionManager _regionManager;
    private readonly ServerSettings _settings;
    private readonly Shared.Config.ISoundRegistry _registry;

    public SoundApi(IUdpServer udpServer, IGameState gameState, IRegionManager regionManager, Microsoft.Extensions.Options.IOptions<ServerSettings> settings, Shared.Config.ISoundRegistry registry)
    {
        _udpServer = udpServer;
        _gameState = gameState;
        _regionManager = regionManager;
        _settings = settings.Value;
        _registry = registry;
    }

    public void Play(string file, float volume = 100f, float pitch = 1f, bool repeat = false)
    {
        var sound = new SoundData(file, volume, pitch, repeat);
        _udpServer.BroadcastSound(sound);
    }

    public void PlayAt(string file, long x, long y, long z, float volume = 100f, float pitch = 1f, float falloff = 1f)
    {
        var sound = new SoundData(file, volume, pitch, false)
        {
            X = x,
            Y = y,
            Z = z,
            Falloff = falloff
        };

        // Find regions within activation range to target players
        var (chunkCoords, _) = Map.GlobalToChunk(x, y);
        var regionSize = _settings.Performance.RegionalProcessing.RegionSize;
        var regionCoords = (
            (long)Math.Floor((double)chunkCoords.X / regionSize),
            (long)Math.Floor((double)chunkCoords.Y / regionSize)
        );

        if (_regionManager.TryGetRegion((int)z, regionCoords, out var region))
        {
            _udpServer.BroadcastSound(sound, region);
        }
    }

    public void PlayOn(string file, IGameObject obj, float volume = 100f, float pitch = 1f, float falloff = 1f)
    {
        var sound = new SoundData(file, volume, pitch, false)
        {
            ObjectId = obj.Id,
            X = obj.X,
            Y = obj.Y,
            Z = obj.Z,
            Falloff = falloff
        };

        var (chunkCoords, _) = Map.GlobalToChunk(obj.X, obj.Y);
        var regionSize = _settings.Performance.RegionalProcessing.RegionSize;
        var regionCoords = (
            (long)Math.Floor((double)chunkCoords.X / regionSize),
            (long)Math.Floor((double)chunkCoords.Y / regionSize)
        );

        if (_regionManager.TryGetRegion((int)obj.Z, regionCoords, out var region))
        {
            _udpServer.BroadcastSound(sound, region);
        }
        else
        {
            // Fallback for objects not currently in a managed region
            _udpServer.BroadcastSound(sound);
        }
    }

    public void Stop(string file)
    {
        _udpServer.StopSound(file);
    }

    public void StopOn(string file, IGameObject obj)
    {
        var (chunkCoords, _) = Map.GlobalToChunk(obj.X, obj.Y);
        var regionSize = _settings.Performance.RegionalProcessing.RegionSize;
        var regionCoords = (
            (long)Math.Floor((double)chunkCoords.X / regionSize),
            (long)Math.Floor((double)chunkCoords.Y / regionSize)
        );

        Region? region = null;
        _regionManager.TryGetRegion((int)obj.Z, regionCoords, out region);
        _udpServer.StopSoundOn(file, obj.Id, region);
    }

    public void PlayNamed(string soundName)
    {
        if (_registry.TryGetSound(soundName, out var def))
        {
            Play(def.FilePath, def.DefaultVolume, def.DefaultPitch, false);
        }
    }

    public void PlayNamedAt(string soundName, long x, long y, long z)
    {
        if (_registry.TryGetSound(soundName, out var def))
        {
            PlayAt(def.FilePath, x, y, z, def.DefaultVolume, def.DefaultPitch, def.DefaultFalloff);
        }
    }

    public void PlayNamedOn(string soundName, IGameObject obj)
    {
        if (_registry.TryGetSound(soundName, out var def))
        {
            PlayOn(def.FilePath, obj, def.DefaultVolume, def.DefaultPitch, def.DefaultFalloff);
        }
    }
}
