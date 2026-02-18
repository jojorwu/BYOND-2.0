using NUnit.Framework;
using Shared;
using System.Diagnostics;
using System;

namespace tests.Performance
{
    [TestFixture]
    public class BuiltinPropertyTests
    {
        [Test]
        public void Benchmark_BuiltinPropertyAccess()
        {
            var type = new ObjectType(1, "/obj");
            type.VariableNames.Add("icon");
            type.VariableNames.Add("custom_var");
            type.FinalizeVariables();

            var obj = new GameObject(type);

            // Warm up
            for(int i=0; i<1000; i++) {
                obj.SetVariable("icon", "test");
                obj.GetVariable("icon");
                obj.SetVariable("custom_var", 1);
                obj.GetVariable("custom_var");
            }

            const int Iterations = 1000000;

            var sw = Stopwatch.StartNew();
            for(int i=0; i<Iterations; i++) {
                obj.SetVariable("icon", "icon.dmi");
                var v = obj.GetVariable("icon");
            }
            sw.Stop();
            long builtinTime = sw.ElapsedMilliseconds;
            TestContext.WriteLine($"Built-in property access (icon): {builtinTime}ms");

            sw.Restart();
            for(int i=0; i<Iterations; i++) {
                obj.SetVariable("custom_var", i);
                var v = obj.GetVariable("custom_var");
            }
            sw.Stop();
            long customTime = sw.ElapsedMilliseconds;
            TestContext.WriteLine($"Custom variable access: {customTime}ms");

            // Built-in should be faster because it avoids the lock and index lookup in the base class
            // for common names, and uses a direct field.
            Assert.That(builtinTime, Is.LessThan(customTime), "Built-in property access should be faster than dictionary/array lookup");
        }
    }
}
