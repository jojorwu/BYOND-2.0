using System;
using System.Diagnostics;
using System.Threading;
using Core;

namespace Server
{
    public class Game
    {
        private readonly GameState _gameState;
        private readonly ScriptHost _scriptHost;
        private readonly UdpServer _udpServer;
        private readonly ServerSettings _settings;
        private bool _isRunning = true;

        public Game(Project project, ServerSettings settings)
        {
            _settings = settings;
            _gameState = new GameState();
            _scriptHost = new ScriptHost(project, _gameState, _settings);
            _udpServer = new UdpServer(System.Net.IPAddress.Any, _settings.Network.UdpPort, _scriptHost, _gameState, _settings);
        }

        public void Start()
        {
            _scriptHost.Start();
            _udpServer.Start();

            var stopwatch = new Stopwatch();
            var tickInterval = TimeSpan.FromSeconds(1.0 / _settings.Performance.TickRate);

            while (_isRunning)
            {
                stopwatch.Restart();
                Tick();
                var elapsed = stopwatch.Elapsed;
                var sleepTime = tickInterval - elapsed;
                if (sleepTime > TimeSpan.Zero)
                {
                    Thread.Sleep(sleepTime);
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
        }

        private void Tick()
        {
            _scriptHost.Tick();
            var snapshot = _gameState.GetSnapshot();
            _udpServer.BroadcastSnapshot(snapshot);
        }
    }
}
