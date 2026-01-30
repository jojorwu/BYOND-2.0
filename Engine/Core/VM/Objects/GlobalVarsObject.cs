using Shared;
using Core.VM.Runtime;

namespace Core.VM.Objects
{
    public class GlobalVarsObject : DreamObject
    {
        private readonly DreamVMContext _context;

        public GlobalVarsObject(DreamVMContext context) : base(null!)
        {
            _context = context;
        }

        public override DreamValue GetVariable(string name)
        {
            if (_context.GlobalNames.TryGetValue(name, out var index))
            {
                return _context.GetGlobal(index);
            }
            return DreamValue.Null;
        }

        public override void SetVariable(string name, DreamValue value)
        {
            if (_context.GlobalNames.TryGetValue(name, out var index))
            {
                _context.SetGlobal(index, value);
            }
        }
    }
}
