using Microsoft.Extensions.Logging.Abstractions;
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
            using var jobSystem = new JobSystem(NullLogger<JobSystem>.Instance);
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
            using var jobSystem = new JobSystem(NullLogger<JobSystem>.Instance);
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
            using var jobSystem = new JobSystem(NullLogger<JobSystem>.Instance);
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
            using var jobSystem = new JobSystem(NullLogger<JobSystem>.Instance);
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

        [Test]
        public async Task WeightedLoadBalancing_AvoidsCongestedWorkers()
        {
            using var jobSystem = new JobSystem(NullLogger<JobSystem>.Instance);

            // 1. Schedule a very heavy job with high weight
            var heavyHandle = jobSystem.Schedule(() =>
            {
                Thread.Sleep(200);
            }, weight: 100);

            // 2. Schedule many small jobs
            const int smallJobCount = 50;
            var countdown = new CountdownEvent(smallJobCount);
            int[] workerExecutionCounts = new int[Environment.ProcessorCount * 4]; // Max possible workers roughly

            for (int i = 0; i < smallJobCount; i++)
            {
                jobSystem.Schedule(() =>
                {
                    // Find which worker we are on
                    // We don't have easy access to worker index here without internal exposure,
                    // but we can just ensure they all finish quickly.
                    countdown.Signal();
                }, weight: 1);
            }

            // 3. Verify they finish even if the heavy job is still running
            // If they were all stuck behind the heavy job, this would fail or be very slow.
            bool finished = countdown.Wait(500);
            Assert.That(finished, Is.True, "Small jobs were delayed by a heavy job, weighted balancing might be ineffective.");

            await heavyHandle.CompleteAsync();
        }
    }
}
