using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Core;

namespace Server
{
    /// <summary>
    /// Manages TCP client connections for the game server.
    /// </summary>
    public class NetworkServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ConcurrentBag<Task> _clientTasks = new ConcurrentBag<Task>();
        private readonly EngineSettings _engineSettings;
        private readonly SemaphoreSlim _maxConnectionsSemaphore;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkServer"/> class.
        /// </summary>
        /// <param name="port">The port to listen on.</param>
        /// <param name="engineSettings">The engine settings to use.</param>
        public NetworkServer(int port, EngineSettings engineSettings)
        {
            _engineSettings = engineSettings;
            _listener = new TcpListener(IPAddress.Any, port);
            _cancellationTokenSource = new CancellationTokenSource();
            _maxConnectionsSemaphore = new SemaphoreSlim(_engineSettings.EffectiveNumberOfThreads);
        }

        /// <summary>
        /// Starts the server and begins listening for client connections.
        /// </summary>
        public void Start()
        {
            try
            {
                _listener.Start();
                Console.WriteLine($"Server started on port {_listener.LocalEndpoint}");
                Task.Run(() => ListenForClients(_cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting server: {ex.Message}");
            }
        }

        private async Task ListenForClients(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await _maxConnectionsSemaphore.WaitAsync(cancellationToken);
                    var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                    HandleNewClient(client);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when the server is stopped
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting client: {ex.Message}");
            }
        }

        private void HandleNewClient(TcpClient client)
        {
            Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");
            var clientHandler = new ClientHandler(client, _cancellationTokenSource.Token);
            var clientTask = clientHandler.HandleClientAsync().ContinueWith(_ => _maxConnectionsSemaphore.Release());

            _clientTasks.Add(clientTask);
        }

        /// <summary>
        /// Stops the server and disconnects all clients.
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_cancellationTokenSource.IsCancellationRequested) return;

                _cancellationTokenSource.Cancel();
                _listener.Stop();

                // Wait for all client tasks to complete
                Task.WaitAll(_clientTasks.ToArray(), TimeSpan.FromSeconds(5));

                Console.WriteLine("Server stopped.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping server: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes the network server resources.
        /// </summary>
        public void Dispose()
        {
            Stop();
            _cancellationTokenSource.Dispose();
            _maxConnectionsSemaphore.Dispose();
        }
    }
}
