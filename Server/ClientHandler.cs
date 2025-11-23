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
        private readonly ScriptHost _scriptHost;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientHandler"/> class.
        /// </summary>
        /// <param name="client">The TCP client to handle.</param>
        /// <param name="cancellationToken">A token to signal when the server is shutting down.</param>
        /// <param name="scriptHost">The script host to execute commands.</param>
        public ClientHandler(TcpClient client, CancellationToken cancellationToken, ScriptHost scriptHost)
        {
            _client = client;
            _cancellationToken = cancellationToken;
            _scriptHost = scriptHost;
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
                var reader = new StreamReader(stream);
                var writer = new StreamWriter(stream) { AutoFlush = true };

                while (!_cancellationToken.IsCancellationRequested)
                {
                    var command = await reader.ReadLineAsync();
                    if (command == null)
                    {
                        break; // Client disconnected
                    }

                    var result = _scriptHost.ExecuteCommand(command);
                    await writer.WriteLineAsync(result);
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
