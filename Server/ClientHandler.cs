using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    /// <summary>
    /// Handles communication with a single connected client.
    /// </summary>
    public class ClientHandler
    {
        private readonly TcpClient _client;
        private readonly CancellationToken _cancellationToken;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientHandler"/> class.
        /// </summary>
        /// <param name="client">The TCP client to handle.</param>
        /// <param name="cancellationToken">A token to signal when the server is shutting down.</param>
        public ClientHandler(TcpClient client, CancellationToken cancellationToken)
        {
            _client = client;
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Starts handling the client communication in a loop.
        /// </summary>
        public async Task HandleClientAsync()
        {
            var endPoint = _client.Client.RemoteEndPoint?.ToString() ?? "unknown";
            try
            {
                using var stream = _client.GetStream();
                var buffer = new byte[1024];
                while (!_cancellationToken.IsCancellationRequested)
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, _cancellationToken);
                    if (bytesRead == 0)
                    {
                        break; // Client disconnected
                    }
                    // Process data here...
                }
            }
            catch (OperationCanceledException)
            {
                // Normal on server shutdown
            }
            catch (IOException)
            {
                // Client disconnected abruptly
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error with client {endPoint}: {ex.Message}");
            }
            finally
            {
                _client.Close();
                Console.WriteLine($"Client disconnected: {endPoint}");
            }
        }
    }
}
