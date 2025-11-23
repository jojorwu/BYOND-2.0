
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Client.Console
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                using var client = new TcpClient("127.0.0.1", 7777);
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream);
                using var writer = new StreamWriter(stream) { AutoFlush = true };

                System.Console.WriteLine("Connected to server. Type a Lua command and press Enter to execute.");

                while (true)
                {
                    System.Console.Write("> ");
                    var line = await Task.Run(() => System.Console.ReadLine());

                    if (string.IsNullOrEmpty(line) || line.ToLower() == "exit")
                    {
                        break;
                    }

                    await writer.WriteLineAsync(line);
                    var response = await reader.ReadLineAsync();
                    System.Console.WriteLine(response);
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
