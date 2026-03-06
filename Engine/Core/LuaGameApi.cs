using Shared;
using System;
using System.Threading.Tasks;

namespace Core
{
    public class LuaGameApi
    {
        private readonly IGameApi _gameApi;

        public LuaGameApi(IGameApi gameApi)
        {
            _gameApi = gameApi;
        }

        public IMap? GetMap() => _gameApi.Map.GetMap();
        public ITurf? GetTurf(long x, long y, long z) => _gameApi.Map.GetTurf(x, y, z);
        public void SetTurf(long x, long y, long z, int turfId) => _gameApi.Map.SetTurf(x, y, z, turfId);
        public GameObject? CreateObject(int typeId, long x, long y, long z) => _gameApi.Objects.CreateObject(typeId, x, y, z);
        public GameObject? GetObject(long id) => _gameApi.Objects.GetObject(id);
        public void DestroyObject(long id) => _gameApi.Objects.DestroyObject(id);

        public void LoadMap(string filePath)
        {
            try
            {
                Task.Run(async () => await _gameApi.Map.LoadMapAsync(filePath)).Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] LuaGameApi.LoadMap failed: {e.Message}");
            }
        }

        public void SaveMap(string filePath)
        {
            try
            {
                Task.Run(async () => await _gameApi.Map.SaveMapAsync(filePath)).Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[ERROR] LuaGameApi.SaveMap failed: {e.Message}");
            }
        }

        public System.Collections.Generic.List<string> ListScriptFiles() => _gameApi.Scripts.ListScriptFiles();
        public bool ScriptFileExists(string filename) => _gameApi.Scripts.ScriptFileExists(filename);
        public string ReadScriptFile(string filename) => _gameApi.Scripts.ReadScriptFile(filename);

        public void PlaySound(string file, float volume = 100f, float pitch = 1f, bool repeat = false) => _gameApi.Sounds.Play(file, volume, pitch, repeat);
        public void PlaySoundAt(string file, long x, long y, long z, float volume = 100f, float pitch = 1f, float falloff = 1f) => _gameApi.Sounds.PlayAt(file, x, y, z, volume, pitch, falloff);
        public void PlaySoundOn(string file, GameObject obj, float volume = 100f, float pitch = 1f, float falloff = 1f) => _gameApi.Sounds.PlayOn(file, obj, volume, pitch, falloff);
        public void StopSound(string file) => _gameApi.Sounds.Stop(file);
        public void StopSoundOn(string file, GameObject obj) => _gameApi.Sounds.StopOn(file, obj);

        public void PlayNamedSound(string soundName) => _gameApi.Sounds.PlayNamed(soundName);
        public void PlayNamedSoundAt(string soundName, long x, long y, long z) => _gameApi.Sounds.PlayNamedAt(soundName, x, y, z);
        public void PlayNamedSoundOn(string soundName, GameObject obj) => _gameApi.Sounds.PlayNamedOn(soundName, obj);

        public double GetTime() => _gameApi.Time.Time;
        public void Spawn(int ms, NLua.LuaFunction func) => _gameApi.Time.Spawn(ms, () => func.Call());

        public void PublishEvent(string name, params object[] args) => _gameApi.Events.Publish(name, args);
        public void SubscribeEvent(string name, NLua.LuaFunction func) => _gameApi.Events.Subscribe(name, args => func.Call(args));
        public bool CanMove(GameObject obj, long x, long y, long z) => _gameApi.Map.CanMove(obj, x, y, z);

        public List<GameObject> FindObjectsByType(string typePath) => _gameApi.Objects.FindObjectsByType(typePath);
        public void AddComponent(long id, string type) => _gameApi.Objects.AddComponent(id, type);
        public void RemoveComponent(long id, string type) => _gameApi.Objects.RemoveComponent(id, type);
        public bool HasComponent(long id, string type) => _gameApi.Objects.HasComponent(id, type);

        public void RegisterSound(string name, string filePath, float volume = 100f, float pitch = 1f, float falloff = 1f)
        {
            _gameApi.SoundRegistry.RegisterSound(name, new Shared.Config.SoundDefinition(filePath, volume, pitch, falloff));
        }

        public void InternalRegisterCommand(string name, string description, string help, NLua.LuaFunction func)
        {
            _gameApi.Commands.RegisterCommand(new LuaConsoleCommand(name, description, help, func));
        }

        private class LuaConsoleCommand : Shared.Config.IConsoleCommand
        {
            public string Command { get; }
            public string Description { get; }
            public string Help { get; }
            private readonly NLua.LuaFunction _func;

            public LuaConsoleCommand(string name, string description, string help, NLua.LuaFunction func)
            {
                Command = name;
                Description = description;
                Help = help;
                _func = func;
            }

            public Task<string> Execute(string[] args)
            {
                var result = _func.Call(args);
                return Task.FromResult(result?.FirstOrDefault()?.ToString() ?? "Command executed.");
            }
        }
    }
}
