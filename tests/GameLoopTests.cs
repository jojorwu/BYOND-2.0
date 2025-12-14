using Shared;
using NUnit.Framework;
using Moq;
using Server;
using Core;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace tests
{
    [TestFixture]
    public class GameLoopTests
    {
        private Mock<IGameLoopStrategy> _strategyMock = null!;
        private Mock<IRegionManager> _regionManagerMock = null!;
        private ServerSettings _serverSettings = null!;
        private GameLoop _gameLoop = null!;
        private CancellationTokenSource _cancellationTokenSource = null!;

        [SetUp]
        public void SetUp()
        {
            _strategyMock = new Mock<IGameLoopStrategy>();
            _regionManagerMock = new Mock<IRegionManager>();
            _serverSettings = new ServerSettings { Performance = { TickRate = 60 } };

            _gameLoop = new GameLoop(_strategyMock.Object, _regionManagerMock.Object, _serverSettings);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        [TearDown]
        public void TearDown()
        {
            _cancellationTokenSource.Cancel();
            _gameLoop.Dispose();
            _cancellationTokenSource.Dispose();
        }

        [Test]
        public async Task StartAsync_CallsTickOnStrategy()
        {
            // Arrange
            _cancellationTokenSource.CancelAfter(200);

            // Act
            await _gameLoop.StartAsync(_cancellationTokenSource.Token);
            await Task.Delay(100, _cancellationTokenSource.Token); // Give it a moment to tick

            // Assert
            _strategyMock.Verify(s => s.TickAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }
    }
}
