using NUnit.Framework;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using VectorLiteDB.Tests.TestData;
using VectorLiteDB.Models;
using VectorLiteDB.Services;

namespace VectorLiteDB.Tests
{
    [TestFixture]
    public class ConcurrencyTests : BaseTest
    {
        [Test]
        public void Concurrent_Adds_MaintainDataIntegrity()
        {
            const int threadCount = 10;
            const int entriesPerThread = 50;

            var exceptions = new ConcurrentBag<Exception>();

            Parallel.For(0, threadCount, threadId =>
            {
                try
                {
                    for (int i = 0; i < entriesPerThread; i++)
                    {
                        var entry = new KnowledgeEntry
                        {
                            Id = $"thread_{threadId}_entry_{i}",
                            Content = $"Content from thread {threadId}",
                            Embedding = new float[384], // Simple vector for speed
                            Metadata = new Dictionary<string, object> { ["thread"] = threadId }
                        };
                        Store.Add(entry);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            Assert.IsEmpty(exceptions, $"Concurrent operations failed: {string.Join(", ", exceptions.Select(e => e.Message))}");

            var stats = Store.GetStats();
            Assert.AreEqual(threadCount * entriesPerThread, stats.TotalEntries);
        }

        [Test]
        public void ReadWrite_Concurrency_NoDataLoss()
        {
            // Setup initial data
            var initialEntries = new TestDataBuilder().WithRandomEntries(100).Build();
            foreach (var entry in initialEntries)
                Store.Add(entry);

            var readExceptions = new ConcurrentBag<Exception>();
            var writeExceptions = new ConcurrentBag<Exception>();

            var cts = new CancellationTokenSource();
            cts.CancelAfter(5000); // 5 second test

            // Reader tasks
            var readers = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var results = Store.Search(new float[384], 5);
                        Assert.IsNotNull(results);
                    }
                    catch (Exception ex)
                    {
                        readExceptions.Add(ex);
                    }
                }
            });

            // Writer tasks
            var writers = Task.Run(() =>
            {
                var counter = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var entry = new KnowledgeEntry
                        {
                            Id = $"concurrent_{counter++}",
                            Content = $"Concurrent write {counter}",
                            Embedding = new float[384]
                        };
                        Store.Add(entry);
                    }
                    catch (Exception ex)
                    {
                        writeExceptions.Add(ex);
                    }
                }
            });

            Task.WaitAll(readers, writers);

            Assert.IsEmpty(readExceptions, "Read operations failed in concurrent scenario");
            Assert.IsEmpty(writeExceptions, "Write operations failed in concurrent scenario");

            var finalStats = Store.GetStats();
            Assert.Greater(finalStats.TotalEntries, 100); // Should have added more entries
        }

        [Test]
        public void MultipleStoreInstances_Isolated()
        {
            // Create another store instance with different DB
            var otherDbPath = TestDbPath.Replace(".db", "_other.db");
            using (var otherStore = new VectorDbStore(otherDbPath))
            {
                // Add to first store
                Store.Add(new KnowledgeEntry
                {
                    Content = "First store entry",
                    Embedding = new float[384]
                });

                // Add to second store
                otherStore.Add(new KnowledgeEntry
                {
                    Content = "Second store entry",
                    Embedding = new float[384]
                });

                // Verify isolation
                Assert.AreEqual(1, Store.GetStats().TotalEntries);
                Assert.AreEqual(1, otherStore.GetStats().TotalEntries);
            }
        }
    }
}