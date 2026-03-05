using Shared;
using Shared.Interfaces;
using Robust.Shared.Maths;

namespace Core.Api;

public class SoundApi : ISoundApi
{
    private readonly IUdpServer _udpServer;
    private readonly IGameState _gameState;
    private readonly IRegionManager _regionManager;
    private readonly ServerSettings _settings;

    public SoundApi(IUdpServer udpServer, IGameState gameState, IRegionManager regionManager, Microsoft.Extensions.Options.IOptions<ServerSettings> settings)
    {
        _udpServer = udpServer;
        _gameState = gameState;
        _regionManager = regionManager;
        _settings = settings.Value;
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
            // For now, simple broadcast, but ideally we'd filter by region in IUdpServer
            _udpServer.BroadcastSound(sound);
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
        _udpServer.BroadcastSound(sound);
    }
}
