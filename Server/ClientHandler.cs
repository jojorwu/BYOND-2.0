using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Core;

namespace Server
{
    public class ClientHandler
    {
        private readonly TcpClient _client;
        private readonly ScriptHost _scriptHost;

        public ClientHandler(TcpClient client, ScriptHost scriptHost)
        {
            _client = client;
            _scriptHost = scriptHost;
        }

        public async Task HandleClientAsync()
        {
            try
            {
                using var stream = _client.GetStream();
                using var reader = new StreamReader(stream);
                using var writer = new StreamWriter(stream) { AutoFlush = true };

                while (_client.Connected)
                {
                    var command = await reader.ReadLineAsync();
                    if (command == null)
                        break;

                    var result = _scriptHost.ExecuteCommand(command);
                    await writer.WriteLineAsync(result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
            }
            finally
            {
                _client.Close();
            }
        }
    }
}
