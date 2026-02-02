using ImGuiNET;
using Shared;
using Shared.Models;
using Shared.Services;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Numerics;
using System.Threading.Tasks;

namespace Editor.UI
{
    public class ServerBrowserPanel
    {
        private readonly IServerDiscoveryService _serverDiscoveryService;
        private List<ServerInfoEntry> _servers = new();
        private List<ServerInfoEntry> _favorites = new();
        private string _filter = "";
        private bool _isLoading = false;

        public ServerBrowserPanel(IServerDiscoveryService serverDiscoveryService)
        {
            _serverDiscoveryService = serverDiscoveryService;
            _ = LoadServerListAsync();
        }

        private async Task LoadServerListAsync()
        {
            _isLoading = true;
            try
            {
                var serverList = await _serverDiscoveryService.GetServerListAsync();
                _servers = serverList.ToList();
                _favorites = _servers.Where(s => s.IsFavorite).ToList();
                _ = PingServersAsync(_servers);
                _ = PingServersAsync(_favorites);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error loading server list: {e.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task PingServersAsync(IEnumerable<ServerInfoEntry> serverList)
        {
            var pingTasks = serverList.Select(async server =>
            {
                try
                {
                    using var pinger = new Ping();
                    var reply = await pinger.SendPingAsync(server.Address.Split(':')[0], 1000); // 1-second timeout
                    if (reply.Status == IPStatus.Success)
                    {
                        server.Ping = (int)reply.RoundtripTime;
                    }
                    else
                    {
                        server.Ping = -1;
                    }
                }
                catch
                {
                    server.Ping = -1; // Could be invalid hostname, etc.
                }
            });

            await Task.WhenAll(pingTasks);
        }

        public void Draw()
        {
            ImGui.Begin("Server Browser");

            if (ImGui.Button("Refresh"))
            {
                _ = LoadServerListAsync();
            }
            ImGui.SameLine();
            ImGui.InputText("Filter", ref _filter, 256);

            if (_isLoading)
            {
                ImGui.Text("Loading...");
            }
            else
            {
                if (ImGui.BeginTabBar("ServerTabs"))
                {
                    if (ImGui.BeginTabItem("Internet"))
                    {
                        DrawServerTable("InternetServers", _servers);
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("Favorites"))
                    {
                        DrawServerTable("FavoriteServers", _favorites);
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }
            }

            ImGui.End();
        }

        private void DrawServerTable(string tableId, List<ServerInfoEntry> servers)
        {
            if (ImGui.BeginTable(tableId, 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            {
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Players");
                ImGui.TableSetupColumn("Ping");
                ImGui.TableSetupColumn("Address");
                ImGui.TableHeadersRow();

                var filteredServers = string.IsNullOrWhiteSpace(_filter)
                    ? servers
                    : servers.Where(s => s.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase)).ToList();

                foreach (var server in filteredServers)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(server.Name);
                    ImGui.TableNextColumn();
                    ImGui.Text($"{server.CurrentPlayers}/{server.MaxPlayers}");
                    ImGui.TableNextColumn();
                    ImGui.Text(server.Ping == -1 ? "N/A" : server.Ping.ToString());
                    ImGui.TableNextColumn();
                    ImGui.Text(server.Address);
                }

                ImGui.EndTable();
            }
        }
    }
}
