using DMCompiler.Compiler.DM.AST;
using System.Diagnostics.CodeAnalysis;

namespace DMCompiler.DM;

internal class DMObjectBuilder {
    private readonly DMCompiler _compiler;
    private readonly DMObjectTree _objectTree;
    private int _dmObjectIdCounter;
    private int _dmProcIdCounter;
    private readonly Dictionary<DreamPath, int> _pathToTypeId = new();


    public DMObjectBuilder(DMCompiler compiler, DMObjectTree objectTree) {
        _compiler = compiler;
        _objectTree = objectTree;
    }

    public DMProc CreateDMProc(DMObject dmObject, DMASTProcDefinition? astDefinition) {
        DMProc dmProc = new DMProc(_compiler, _dmProcIdCounter++, dmObject, astDefinition);
        _objectTree.AllProcs.Add(dmProc);

        return dmProc;
    }

    public DMObject GetOrCreateDMObject(DreamPath path) {
        if (TryGetDMObject(path, out var dmObject))
            return dmObject;

        DMObject? parent = null;
        if (path.Elements.Length > 1) {
            parent = GetOrCreateDMObject(path.FromElements(0, -2)); // Create all parent classes as dummies, if we're being dummy-created too
        } else if (path.Elements.Length == 1) {
            switch (path.LastElement) {
                case "client":
                case "datum":
                case "list":
                case "alist":
                case "vector":
                case "savefile":
                case "world":
                case "callee":
                    parent = GetOrCreateDMObject(DreamPath.Root);
                    break;
                default:
                    parent = GetOrCreateDMObject(_compiler.Settings.NoStandard ? DreamPath.Root : DreamPath.Datum);
                    break;
            }
        }

        if (path != DreamPath.Root && parent == null) // Parent SHOULD NOT be null here! (unless we're root lol)
            throw new Exception($"Type {path} did not have a parent");

        dmObject = new DMObject(_compiler, _dmObjectIdCounter++, path, parent);
        _objectTree.AllObjects.Add(dmObject);
        _pathToTypeId[path] = dmObject.Id;
        return dmObject;
    }

    public bool TryGetDMObject(DreamPath path, [NotNullWhen(true)] out DMObject? dmObject) {
        if (_pathToTypeId.TryGetValue(path, out int typeId)) {
            dmObject = _objectTree.AllObjects[typeId];
            return true;
        }

        dmObject = null;
        return false;
    }

    public DreamPath? UpwardSearch(DreamPath path, DreamPath search) {
        bool requireProcElement = search.Type == DreamPath.PathType.Absolute;
        string? searchingProcName = null;

        int procElement = path.FindElement("proc");
        if (procElement == -1) procElement = path.FindElement("verb");
        if (procElement != -1) {
            searchingProcName = search.LastElement;
            path = path.RemoveElement(procElement);
            search = search.FromElements(0, -2);
            search.Type = DreamPath.PathType.Relative;
        }

        procElement = search.FindElement("proc");
        if (procElement == -1) procElement = search.FindElement("verb");
        if (procElement != -1) {
            searchingProcName = search.LastElement;
            search = search.FromElements(0, procElement);
            search.Type = DreamPath.PathType.Relative;
        }

        if (searchingProcName == null && requireProcElement)
            return null;

        DreamPath currentPath = path;
        while (true) {
            bool foundType = TryGetDMObject(currentPath.Combine(search), out var dmObject);

            // We're searching for a proc
            if (searchingProcName != null && foundType) {
                DMObject type = _objectTree.AllObjects[dmObject.Id];

                if (type.HasLocalProc(searchingProcName)) {
                    return new DreamPath(type.Path.PathString + "/proc/" + searchingProcName);
                } else if (dmObject.Id == _objectTree.Root.Id && _objectTree.GlobalProcs.ContainsKey(searchingProcName)) {
                    return new DreamPath("/proc/" + searchingProcName);
                }
            } else if (foundType) { // We're searching for a type
                return currentPath.Combine(search);
            }

            if (currentPath == DreamPath.Root) {
                break; // Nothing found
            }

            currentPath = currentPath.AddToPath("..");
        }

        return null;
    }
}
