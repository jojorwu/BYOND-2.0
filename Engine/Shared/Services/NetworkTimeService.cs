using System;
using Shared.Interfaces;

namespace Shared.Services;

public class NetworkTimeService : INetworkTimeService
{
    private double _serverOffset;
    private double _rtt;

    public double ServerTime => (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0) + _serverOffset;

    public void Synchronize(double remoteTimestamp, double roundTripTime)
    {
        _rtt = roundTripTime;
        double localNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        // Estimate server time by assuming the packet took RTT/2 to arrive
        double estimatedServerNow = remoteTimestamp + (roundTripTime / 2.0);
        _serverOffset = estimatedServerNow - localNow;
    }

    public double LocalToRemoteTime(double localTime) => localTime + _serverOffset;
    public double RemoteToLocalTime(double remoteTime) => remoteTime - _serverOffset;
}
