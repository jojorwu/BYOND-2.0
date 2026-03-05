namespace Shared;
    public interface IUdpServer
    {
        void BroadcastSnapshot(string snapshot);
        void BroadcastSnapshot(byte[] snapshot);
        void BroadcastSnapshot(MergedRegion region, string snapshot);
        void BroadcastSnapshot(MergedRegion region, byte[] snapshot);
        System.Threading.Tasks.Task SendRegionSnapshotAsync(MergedRegion region, System.Collections.Generic.IEnumerable<IGameObject> objects);
        void SendSound(INetworkPeer peer, SoundData sound);
        void BroadcastSound(SoundData sound);
        void BroadcastSound(SoundData sound, Region region);
        void BroadcastSound(SoundData sound, MergedRegion mergedRegion);
        void StopSound(string file, Region? region = null);
        void StopSoundOn(string file, long objectId, Region? region = null);
    }
