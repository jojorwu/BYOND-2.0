using System;
using System.Collections.Generic;

namespace Shared.Json
{
    public interface ICompiledJson
    {
        IReadOnlyList<string> Strings { get; }
        IReadOnlyList<ICompiledProcJson> Procs { get; }
        IReadOnlyList<ICompiledTypeJson> Types { get; }
        int[]? GlobalProcs { get; }
        IGlobalListJson? Globals { get; }
        ICompiledProcJson? GlobalInitProc { get; }
    }

    public interface IGlobalListJson {
        int GlobalCount { get; }
        IReadOnlyList<string> Names { get; }
        IReadOnlyDictionary<int, object> Globals { get; }
    }

    public interface ICompiledProcJson
    {
        string Name { get; }
        IReadOnlyList<byte> Bytecode { get; }
        IReadOnlyList<ICompiledArgumentJson> Arguments { get; }
        int Locals { get; }
        ProcAttributes Attributes { get; }
        int MaxStackSize { get; }
        bool IsVerb { get; }
        string? VerbName { get; }
        string? VerbCategory { get; }
        string? VerbDesc { get; }
        sbyte Invisibility { get; }
    }

    public interface ICompiledArgumentJson
    {
        string Name { get; }
        DMValueType Type { get; }
    }

    public interface ICompiledTypeJson
    {
        string Path { get; }
        int? Parent { get; }
        IReadOnlyDictionary<string, object> Variables { get; }
        int? InitProc { get; }
        List<List<int>>? Procs { get; }
        HashSet<string>? Verbs { get; }
        Dictionary<string, int>? GlobalVariables { get; }
        HashSet<string>? ConstVariables { get; }
        HashSet<string>? TmpVariables { get; }
    }
}
