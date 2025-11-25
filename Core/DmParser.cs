using System;
using System.Collections.Generic;
using System.IO;

namespace Core
{
public class DmParser
{
private readonly ObjectTypeManager _typeManager;

public DmParser(ObjectTypeManager typeManager)
    {
        _typeManager = typeManager;
    }

    public void ParseFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        ParseLines(lines);
    }

    public void ParseString(string content)
    {
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        ParseLines(lines);
    }

    private void ParseLines(string[] lines)
    {
        var pathStack = new Stack<(int Indent, string Path)>();
        pathStack.Push((-1, "")); // Root context

        string currentPath = "";

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//")) continue;

            // 1. Calculate indentation (tabs or 4 spaces)
            int indent = 0;
            int spaces = 0;
            foreach (char c in line)
            {
                if (c == '\t') { indent++; spaces = 0; }
                else if (c == ' ') { spaces++; if (spaces == 4) { indent++; spaces = 0; } }
                else break;
            }

            string content = line.Trim();

            // 2. Determine context (pop stack if indentation decreased or stayed same)
            // We pop if the current stack top has indentation >= current line's indentation
            // But we always keep the root (-1)
            while (pathStack.Count > 1 && pathStack.Peek().Indent >= indent)
            {
                pathStack.Pop();
            }

            var parentContext = pathStack.Peek();

            // 3. Parse line content

            // Variant A: Type Declaration (/obj/item)
            // Logic: If it starts with /, it's a full path (relative to root implicitly in DM terms)
            if (content.StartsWith("/"))
            {
                string newPath = content;
                RegisterType(newPath);
                pathStack.Push((indent, newPath));
                currentPath = newPath;
            }
            // Variant B: Nested name without leading slash (inheritance by indentation)
            // Logic: It's a subtype of the parent context
            else if (!content.Contains("=") && !content.Contains("(") && IsIdentifier(content))
            {
                string newPath = parentContext.Path == "" ? "/" + content : parentContext.Path + "/" + content;
                RegisterType(newPath);
                pathStack.Push((indent, newPath));
                currentPath = newPath;
            }
            // Variant C: Property assignment (var = val)
            else if (content.Contains("="))
            {
                var parts = content.Split(new[] { '=' }, 2);
                string key = parts[0].Trim();
                string valueStr = parts[1].Trim();

                // Simple check to ensure it's a property assignment and not something else
                if (!string.IsNullOrEmpty(key))
                {
                    object value = ParseValue(valueStr);

                    // If currentPath is empty, we might be at global scope (not supported by ObjectType yet)
                    // or just invalid DM. We'll skip if no type context.
                    if (!string.IsNullOrEmpty(currentPath))
                    {
                        var type = _typeManager.GetObjectType(currentPath);
                        if (type != null)
                        {
                            type.DefaultProperties[key] = value;
                            // Console.WriteLine($"[DM] Set {currentPath}.{key} = {value}");
                        }
                    }
                }
            }
        }
    }

    private bool IsIdentifier(string s)
    {
        // Basic check if string looks like a type name and not code
        // DM allows alphanumeric and underscores
        if (string.IsNullOrEmpty(s)) return false;
        return char.IsLetter(s[0]) || s[0] == '_';
    }

    private void RegisterType(string fullPath)
    {
        // Remove trailing slashes
        if (fullPath.EndsWith("/")) fullPath = fullPath.TrimEnd('/');
        if (string.IsNullOrEmpty(fullPath)) return;

        // Ensure it starts with / for consistency in our system
        if (!fullPath.StartsWith("/")) fullPath = "/" + fullPath;

        // If type exists, we don't need to re-register, but we update currentPath context
        if (_typeManager.GetObjectType(fullPath) != null) return;

        // Determine parent
        string? parentPath = null;
        int lastSlash = fullPath.LastIndexOf('/');

        // If path is like /obj, lastSlash is 0. Substring(0,0) is empty.
        // If path is /obj/item, lastSlash is 4. Substring(0,4) is /obj.
        if (lastSlash > 0)
        {
            parentPath = fullPath.Substring(0, lastSlash);
        }

        var newType = new ObjectType(fullPath);
        if (!string.IsNullOrEmpty(parentPath))
        {
            newType.ParentName = parentPath;
        }

        try
        {
            _typeManager.RegisterObjectType(newType);
            // Console.WriteLine($"[DM] Registered type: {fullPath} (Parent: {parentPath})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DM Error] Failed to register {fullPath}: {ex.Message}");
        }
    }

    private object ParseValue(string val)
    {
        // Integer
        if (int.TryParse(val, out int i)) return i;

        // Float
        if (float.TryParse(val, out float f)) return f;

        // File/Icon 'icon.dmi'
        if (val.StartsWith("'") && val.EndsWith("'")) return val.Trim('\'');

        // String "text"
        if (val.StartsWith("\"") && val.EndsWith("\"")) return val.Trim('\"');

        // Null
        if (val == "null") return null!;

        // Return as string if nothing else matches
        return val;
    }
}

}
