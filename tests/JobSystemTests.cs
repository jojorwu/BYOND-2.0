using NUnit.Framework;
using Shared.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace tests
{
    [TestFixture]
    public class JobSystemTests
    {
        [Test]
        public async Task ForEachAsync_CorrectlyProcessesAllItems()
        {
            using var jobSystem = new JobSystem();
            const int count = 1000;
            int processedCount = 0;
            var items = Enumerable.Range(0, count).ToList();

            await jobSystem.ForEachAsync(items, i =>
            {
                Interlocked.Increment(ref processedCount);
            });

            Assert.That(processedCount, Is.EqualTo(count));
        }

        [Test]
        public async Task ForEachAsync_HandlesSmallCollections()
        {
            using var jobSystem = new JobSystem();
            const int count = 5;
            int processedCount = 0;
            var items = Enumerable.Range(0, count).ToList();

            await jobSystem.ForEachAsync(items, i =>
            {
                Interlocked.Increment(ref processedCount);
            });

            Assert.That(processedCount, Is.EqualTo(count));
        }

        [Test]
        public async Task LoadBalancing_DistributesWork()
        {
            using var jobSystem = new JobSystem();
            const int jobCount = 200;
            var countdown = new CountdownEvent(jobCount);

            for (int i = 0; i < jobCount; i++)
            {
                jobSystem.Schedule(() =>
                {
                    Thread.Sleep(5);
                    countdown.Signal();
                });
            }

            bool finished = countdown.Wait(2000); // 2 seconds timeout
            Assert.That(finished, Is.True, "Jobs did not complete in time, possible load distribution issue or deadlock.");
        }

        [Test]
        public async Task Dependencies_AreRespected()
        {
            using var jobSystem = new JobSystem();
            int step = 0;

            var handle1 = jobSystem.Schedule(() =>
            {
                Thread.Sleep(50);
                Interlocked.Exchange(ref step, 1);
            });

            var handle2 = jobSystem.Schedule(() =>
            {
                Assert.That(step, Is.EqualTo(1));
                Interlocked.Exchange(ref step, 2);
            }, handle1);

            await handle2.CompleteAsync();
            Assert.That(step, Is.EqualTo(2));
        }
    }
}
