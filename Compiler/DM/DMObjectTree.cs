using System.Diagnostics.CodeAnalysis;
using DMCompiler.Compiler;
using DMCompiler.Json;

namespace DMCompiler.DM;

internal class DMObjectTree {
    public readonly List<DMObject> AllObjects = new();
    public readonly List<DMProc> AllProcs = new();

    public DMObject Root => _compiler.DMObjectBuilder.GetOrCreateDMObject(DreamPath.Root);

    public List<string> StringTable => _globals.Strings;
    public List<DMVariable> Globals => _globals.Globals;
    public HashSet<string> Resources => _globals.Resources;
    public Dictionary<string, int> GlobalProcs => _globals.GlobalProcs;

    private readonly DMCompiler _compiler;
    private readonly DMGlobals _globals;

    public DMObjectTree(DMCompiler compiler) {
        _compiler = compiler;
        _globals = compiler.Globals;
    }

    public int AddString(string value) {
        if (!_globals.StringIDs.TryGetValue(value, out var stringId)) {
            stringId = _globals.Strings.Count;

            _globals.Strings.Add(value);
            _globals.StringIDs.Add(value, stringId);
        }

        return stringId;
    }

    /// <summary>
    /// Returns the "New()" DMProc for a given object type ID
    /// </summary>
    /// <returns></returns>
    public DMProc? GetNewProc(int id) {
        var obj = AllObjects[id];
        var procs = obj.GetLocalProcs("New");

        if (procs != null)
            return AllProcs[procs[0]];
        else
            return null;
    }

    public bool TryGetGlobalProc(string name, [NotNullWhen(true)] out DMProc? proc) {
        if (!GlobalProcs.TryGetValue(name, out var id)) {
            proc = null;
            return false;
        }

        proc = AllProcs[id];
        return true;
    }

    /// <returns>True if the path exists, false if not. Keep in mind though that we may just have not found this object path yet while walking in ObjectBuilder.</returns>
    public bool TryGetTypeId(DreamPath path, out int typeId) {
        if (_compiler.DMObjectBuilder.TryGetDMObject(path, out var dmObject)) {
            typeId = dmObject.Id;
            return true;
        }

        typeId = -1;
        return false;
    }

    public int CreateGlobal(out DMVariable global, DreamPath? type, string name, bool isConst, bool isFinal, DMComplexValueType valType) {
        int id = Globals.Count;

        global = new DMVariable(type, name, true, isConst, isFinal, false, valType);
        Globals.Add(global);
        return id;
    }

    public void AddGlobalProc(DMProc proc) {
        if (GlobalProcs.ContainsKey(proc.Name)) {
            _compiler.Emit(WarningCode.DuplicateProcDefinition, proc.Location, $"Global proc {proc.Name} is already defined");
            return;
        }

        GlobalProcs[proc.Name] = proc.Id;
    }

    public (DreamTypeJson[], ProcDefinitionJson[]) CreateJsonRepresentation() {
        DreamTypeJson[] types = new DreamTypeJson[AllObjects.Count];
        ProcDefinitionJson[] procs = new ProcDefinitionJson[AllProcs.Count];

        foreach (DMObject dmObject in AllObjects) {
            types[dmObject.Id] = dmObject.CreateJsonRepresentation();
        }

        foreach (DMProc dmProc in AllProcs) {
            procs[dmProc.Id] = dmProc.GetJsonRepresentation();
        }

        return (types, procs);
    }
}
