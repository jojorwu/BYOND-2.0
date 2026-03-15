using NUnit.Framework;
using Core.VM.Runtime;
using Core.VM.Procs;
using Shared;
using Shared.Enums;
using System;
using System.Collections.Generic;

namespace tests
{
    [TestFixture]
    public class VMFusionTests
    {
        [Test]
        public void LocalFieldTransfer_WorksCorrectly()
        {
            var context = new DreamVMContext();
            context.Strings.Add("field");

            var type = new ObjectType(1, "/obj");
            type.VariableNames.Add("field");
            type.FinalizeVariables();

            var obj = new GameObject(type);
            obj.SetVariableDirect(0, new DreamValue(42.0));

            // Bytecode for:
            // .local[0] = obj
            // .local[1] = .local[0].field
            byte[] bytecode = new byte[] {
                (byte)Opcode.LocalFieldTransfer,
                0, 0, 0, 0, // srcIdx = 0
                0, 0, 0, 0, // nameId = 0 ("field")
                1, 0, 0, 0, // targetIdx = 1
                (byte)Opcode.ReturnNull
            };

            var proc = new DreamProc("test", bytecode, Array.Empty<string>(), 2, context.Strings);
            var thread = new DreamThread(proc, context, 1000);

            // Set local[0] to the object
            thread._stack[thread._callStack[0].LocalBase] = new DreamValue(obj);

            thread.Run(100);

            Assert.That(thread.State, Is.EqualTo(DreamThreadState.Finished));
            var targetVal = thread._stack[thread._callStack[0].LocalBase + 1];
            Assert.That(targetVal.GetValueAsDouble(), Is.EqualTo(42.0));
        }

        [Test]
        public void GlobalJumpIfFalse_WorksCorrectly()
        {
            var context = new DreamVMContext();
            context.InitializeGlobals(1);
            context.SetGlobal(0, DreamValue.False);

            byte[] bytecode = new byte[] {
                (byte)Opcode.GlobalJumpIfFalse,
                0, 0, 0, 0, // globalIdx = 0
                10, 0, 0, 0, // jump to ReturnTrue (at offset 10)
                (byte)Opcode.ReturnFalse, // Offset 9
                (byte)Opcode.ReturnTrue  // Offset 10
            };

            var proc = new DreamProc("test", bytecode, Array.Empty<string>(), 0, context.Strings);
            var thread = new DreamThread(proc, context, 1000);

            thread.Run(100);

            Assert.That(thread.State, Is.EqualTo(DreamThreadState.Finished));
            Assert.That(thread.Pop().GetValueAsDouble(), Is.EqualTo(1.0)); // Should have jumped to ReturnTrue

            // Test when global is true
            context.SetGlobal(0, DreamValue.True);
            thread = new DreamThread(proc, context, 1000);
            thread.Run(100);
            Assert.That(thread.Pop().GetValueAsDouble(), Is.EqualTo(0.0)); // Should NOT have jumped
        }
    }
}
