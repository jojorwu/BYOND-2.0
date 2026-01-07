using System.Collections.Generic;

namespace DMCompiler.DM {
    internal class DMGlobals {
        public readonly List<DMVariable> Globals = new();
        public readonly Dictionary<string, int> GlobalProcs = new();
        public readonly List<string> Strings = new();
        public readonly HashSet<string> Resources = new();

        public readonly Dictionary<string, int> StringIDs = new();
    }
}
