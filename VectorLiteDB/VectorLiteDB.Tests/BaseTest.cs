using NUnit.Framework;
using System.IO;
using VectorLiteDB.Services;

namespace VectorLiteDB.Tests
{
    [TestFixture]
    public abstract class BaseTest
    {
        protected string TestDbPath { get; private set; } = null!;
        protected VectorDbStore Store { get; set; } = null!;

        [SetUp]
        public void BaseSetup()
        {
            TestDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
            Store = new VectorDbStore(TestDbPath);
        }

        [TearDown]
        public void BaseTeardown()
        {
            Store?.Dispose();
            if (File.Exists(TestDbPath))
                File.Delete(TestDbPath);
        }
    }
}