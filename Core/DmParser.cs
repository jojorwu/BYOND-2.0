using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Core
{
    public class DmParser
    {
        private readonly ObjectTypeManager _typeManager;
        // Regex to capture a full type path and a proc definition on the same line.
        private static readonly Regex ProcDefinitionRegex = new Regex(@"^(?<path>/[\w/]+)/proc/(?<proc>\w+)\(.*\)$", RegexOptions.Compiled);


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

        private int CalculateIndent(string line)
        {
            int indent = 0;
            int spaces = 0;
            foreach (char c in line)
            {
                if (c == '\t') { indent++; spaces = 0; }
                else if (c == ' ') { spaces++; if (spaces == 4) { indent++; spaces = 0; } }
                else break;
            }
            return indent;
        }

        private void ParseLines(string[] lines)
        {
            var pathStack = new Stack<(int Indent, string Path)>();
            pathStack.Push((-1, "")); // Root context

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//")) continue;

                int indent = CalculateIndent(line);
                string content = line.Trim();

                while (pathStack.Count > 1 && pathStack.Peek().Indent >= indent)
                {
                    pathStack.Pop();
                }

                var parentContext = pathStack.Peek();
                var currentPath = parentContext.Path;

                var procMatch = ProcDefinitionRegex.Match(content);

                if (procMatch.Success)
                {
                    string objectPath = procMatch.Groups["path"].Value;
                    string procName = procMatch.Groups["proc"].Value;

                    RegisterType(objectPath); // Ensure the type exists
                    var objectType = _typeManager.GetObjectType(objectPath);
                    if (objectType == null) continue;

                    // Capture proc body
                    var bodyLines = new List<string>();
                    int procIndent = indent;
                    int j = i + 1;
                    for (; j < lines.Length; j++)
                    {
                        string nextLine = lines[j];
                        if (string.IsNullOrWhiteSpace(nextLine)) continue;

                        int nextIndent = CalculateIndent(nextLine);
                        if (nextIndent > procIndent)
                        {
                            bodyLines.Add(nextLine);
                        }
                        else
                        {
                            break;
                        }
                    }
                    i = j - 1;
                    objectType.DmProcedures[procName] = string.Join("\n", bodyLines);

                    // Since this line defined a type, push it to the stack
                    pathStack.Push((indent, objectPath));
                }
                else if (content.StartsWith("/"))
                {
                    RegisterType(content);
                    pathStack.Push((indent, content));
                }
                else if (!content.Contains("=") && !content.Contains("(") && IsIdentifier(content))
                {
                    string newPath = parentContext.Path == "" ? "/" + content : parentContext.Path + "/" + content;
                    RegisterType(newPath);
                    pathStack.Push((indent, newPath));
                }
                else if (content.Contains("="))
                {
                    var currentType = _typeManager.GetObjectType(currentPath);
                    if (currentType == null) continue;

                    var parts = content.Split(new[] { '=' }, 2);
                    string key = parts[0].Trim();
                    string valueStr = parts[1].Trim();

                    if (!string.IsNullOrEmpty(key))
                    {
                        object value = ParseValue(valueStr);
                        currentType.DefaultProperties[key] = value;
                    }
                }
            }
        }

        private bool IsIdentifier(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            // Stricter regex: must start with a letter or underscore.
            var match = Regex.Match(s, @"^[a-zA-Z_]\w*$");
            return match.Success;
        }

        private void RegisterType(string fullPath)
        {
            if (fullPath.EndsWith("/")) fullPath = fullPath.TrimEnd('/');
            if (string.IsNullOrEmpty(fullPath)) return;
            if (!fullPath.StartsWith("/")) fullPath = "/" + fullPath;
            if (_typeManager.GetObjectType(fullPath) != null) return;

            string? parentPath = null;
            int lastSlash = fullPath.LastIndexOf('/');
            if (lastSlash > 0)
            {
                parentPath = fullPath.Substring(0, lastSlash);
                // Recursively register parents
                RegisterType(parentPath);
            }

            var newType = new ObjectType(fullPath);
            if (!string.IsNullOrEmpty(parentPath))
            {
                newType.ParentName = parentPath;
            }

            try
            {
                _typeManager.RegisterObjectType(newType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DM Error] Failed to register {fullPath}: {ex.Message}");
            }
        }

        private object ParseValue(string val)
        {
            if (int.TryParse(val, out int i)) return i;
            if (float.TryParse(val, out float f)) return f;
            if (val.StartsWith("'") && val.EndsWith("'")) return val.Trim('\'');
            if (val.StartsWith("\"") && val.EndsWith("\"")) return val.Trim('\"');
            if (val == "null") return null!;
            return val;
        }
    }
}
