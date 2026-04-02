using System;
using Shared.Interfaces;

namespace Shared.Services;

public class NetworkTimeService : INetworkTimeService
{
    private double _serverOffset;
    private double _rtt;
    private bool _initialized;
    private const double EmaAlpha = 0.1; // Smoothing factor for clock sync

    public double ServerTime => (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0) + _serverOffset;

    public void Synchronize(double remoteTimestamp, double roundTripTime)
    {
        double localNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        double estimatedServerNow = remoteTimestamp + (roundTripTime / 2.0);
        double currentOffset = estimatedServerNow - localNow;

        if (!_initialized)
        {
            _rtt = roundTripTime;
            _serverOffset = currentOffset;
            _initialized = true;
        }
        else
        {
            // Exponential Moving Average to filter jitter
            _rtt = (_rtt * (1.0 - EmaAlpha)) + (roundTripTime * EmaAlpha);
            _serverOffset = (_serverOffset * (1.0 - EmaAlpha)) + (currentOffset * EmaAlpha);
        }
    }

    public double LocalToRemoteTime(double localTime) => localTime + _serverOffset;
    public double RemoteToLocalTime(double remoteTime) => remoteTime - _serverOffset;
}
