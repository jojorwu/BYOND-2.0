using System;

namespace Shared.Interfaces;

public interface INetworkTimeService
{
    double ServerTime { get; }
    double LocalToRemoteTime(double localTime);
    double RemoteToLocalTime(double remoteTime);
    void Synchronize(double remoteTimestamp, double roundTripTime);
}
