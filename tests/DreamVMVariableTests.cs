using NUnit.Framework;
using Shared;
using Core.VM;
using Core.VM.Runtime;
using Core.VM.Procs;
using Core.VM.Types;
using Core.VM.Opcodes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace tests
{
    [TestFixture]
    public class DreamVMVariableTests
    {
        private DreamVM _vm = null!;

        [SetUp]
        public void SetUp()
        {
            _vm = new DreamVM(new ServerSettings());
        }

        private DreamValue RunTest(byte[] bytecode, DreamObject instance)
        {
            var proc = new DreamProc(string.Empty, bytecode, Array.Empty<string>(), 0);
            var thread = new DreamThread(proc, _vm, 1000);
            thread.CallStack.Pop();
            thread.CallStack.Push(new CallFrame(proc, 0, 0, instance));
            thread.Run(1000);
            return thread.Stack.Last();
        }

        [Test]
        public void GetAndSetVariable_WorksCorrectly()
        {
            var objectType = new ObjectType(1, "test");
            objectType.Variables = new List<object> { null };
            objectType.VariableNameIds = new Dictionary<string, int> { { "myVar", 0 } };

            var bytecode = new List<byte>();
            bytecode.Add((byte)Opcode.PushFloat);
            bytecode.AddRange(BitConverter.GetBytes(42f));
            bytecode.Add((byte)Opcode.SetVariable);
            bytecode.AddRange(BitConverter.GetBytes(0)); // Variable ID for "myVar"
            bytecode.Add((byte)Opcode.GetVariable);
            bytecode.AddRange(BitConverter.GetBytes(0)); // Variable ID for "myVar"
            bytecode.Add((byte)Opcode.Return);

            var instance = new DreamObject(objectType);
            var result = RunTest(bytecode.ToArray(), instance);

            Assert.That(result.TryGetValue(out float value), Is.True);
            Assert.That(value, Is.EqualTo(42f));
        }
    }
}
