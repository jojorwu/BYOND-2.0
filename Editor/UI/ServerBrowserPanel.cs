using ImGuiNET;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Editor.UI
{
    public class ServerBrowserPanel
    {
        private const int DiscoveryPort = 12345;
        private readonly object _serversLock = new object();
        private List<ServerInfo> _servers = new List<ServerInfo>();
        private readonly LocalizationManager _localizationManager;

        public ServerBrowserPanel(LocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
        }

        public void Draw()
        {
            if (ImGui.BeginTabItem(_localizationManager.GetString("Server Browser")))
            {
                if (ImGui.Button(_localizationManager.GetString("Refresh")))
                {
                    DiscoverServers();
                }

                ImGui.BeginTable("ServerList", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg);
                ImGui.TableSetupColumn(_localizationManager.GetString("Server Name"));
                ImGui.TableSetupColumn(_localizationManager.GetString("Address"));
                ImGui.TableHeadersRow();

                lock (_serversLock)
                {
                    foreach (var server in _servers)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        ImGui.Text(server.Name);
                        ImGui.TableSetColumnIndex(1);
                        ImGui.Text($"{server.Address}:{server.Port}");
                    }
                }

                ImGui.EndTable();
                ImGui.EndTabItem();
            }
        }

        private void DiscoverServers()
        {
            Task.Run(() =>
            {
                var discoveredServers = new List<ServerInfo>();
                using var udpClient = new UdpClient();
                udpClient.EnableBroadcast = true;
                var requestData = Encoding.ASCII.GetBytes("BYOND2_DISCOVERY");
                var serverEp = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);

                udpClient.Send(requestData, requestData.Length, serverEp);

                var fromEp = new IPEndPoint(IPAddress.Any, 0);
                while (true)
                {
                    try
                    {
                        udpClient.Client.ReceiveTimeout = 1000;
                        var responseData = udpClient.Receive(ref fromEp);
                        var response = Encoding.ASCII.GetString(responseData);
                        var parts = response.Split(':');
                        if (parts.Length == 2 && parts[0] == "BYOND2_SERVER")
                        {
                            discoveredServers.Add(new ServerInfo
                            {
                                Name = parts[1],
                                Address = fromEp.Address.ToString(),
                                Port = fromEp.Port
                            });
                        }
                    }
                    catch (SocketException)
                    {
                        break;
                    }
                }

                lock (_serversLock)
                {
                    _servers = discoveredServers;
                }
            });
        }
    }

    public class ServerInfo
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public int Port { get; set; }
    }
}
