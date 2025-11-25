using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    /// <summary>
    /// Manages TCP client connections for the game server.
    /// </summary>
    public class NetworkServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private readonly object _clientsLock = new object();
        private readonly ScriptHost _scriptHost;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkServer"/> class.
        /// </summary>
        /// <param name="ipAddress">The IP address to listen on.</param>
        /// <param name="port">The port to listen on.</param>
        /// <param name="scriptHost">The script host to execute commands.</param>
        public NetworkServer(IPAddress ipAddress, int port, ScriptHost scriptHost)
        {
            _listener = new TcpListener(ipAddress, port);
            _cancellationTokenSource = new CancellationTokenSource();
            _scriptHost = scriptHost;
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
            lock (_clientsLock)
            {
                _clients.Add(client);
            }
            Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");
            var clientHandler = new ClientHandler(client, _scriptHost);
            Task.Run(clientHandler.HandleClientAsync);
        }

        /// <summary>
        /// Stops the server and disconnects all clients.
        /// </summary>
        public void Stop()
        {
            try
            {
                _cancellationTokenSource.Cancel();
                _listener.Stop();

                lock (_clientsLock)
                {
                    foreach (var client in _clients)
                    {
                        client.Close();
                    }
                    _clients.Clear();
                }
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
        }
    }
}
