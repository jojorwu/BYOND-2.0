using Shared;
using System;
using System.Threading.Tasks;
using Shared.Interfaces;

namespace Core.Api;

public class TimeApi : ITimeApi
{
    private readonly ITimerService _timerService;
    private readonly DateTime _startTime;

    public TimeApi(ITimerService timerService)
    {
        _timerService = timerService;
        _startTime = DateTime.UtcNow;
    }

    public double Time => (DateTime.UtcNow - _startTime).TotalSeconds;

    public void Spawn(TimeSpan delay, Action action)
    {
        _timerService.AddTimer(delay, action);
    }

    public void Spawn(int milliseconds, Action action)
    {
        _timerService.AddTimer(TimeSpan.FromMilliseconds(milliseconds), action);
    }
}
