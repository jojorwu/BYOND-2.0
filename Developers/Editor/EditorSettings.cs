using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
namespace Editor
{
    public class EditorSettings
    {
        public string ServerExecutablePath { get; set; } = "Server.exe";
        public string ClientExecutablePath { get; set; } = "Client.exe";
    }
}
