using System;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ComponentDetection.Detectors.Tests
{
    [TestClass]
    [TestCategory("Governance/All")]
    [TestCategory("Governance/ComponentDetection")]
    public class GoComponentTests
    {
        private static readonly string TestName = Guid.NewGuid().ToString();
        private static readonly string TestVersion = Guid.NewGuid().ToString();
        private static readonly string TestHash = Guid.NewGuid().ToString();

        [TestInitialize]
        public void TestInitialize()
        {
        }

        [TestMethod]
        public void ConstructorTest_NameVersion()
        {
            GoComponent goComponent = new GoComponent(TestName, TestVersion);
            Assert.AreEqual(TestName, goComponent.Name);
            Assert.AreEqual(TestVersion, goComponent.Version);
            Assert.AreEqual(string.Empty, goComponent.Hash);
            Assert.AreEqual($"{TestName} {TestVersion} - Go", goComponent.Id);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorTest_NameVersion_NullVersion()
        {
            GoComponent goComponent = new GoComponent(TestName, null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorTest_NameVersion_NullName()
        {
            GoComponent goComponent = new GoComponent(null, TestVersion);
        }

        [TestMethod]
        public void ConstructorTest_NameVersionHash()
        {
            GoComponent goComponent = new GoComponent(TestName, TestVersion, TestHash);
            Assert.AreEqual(TestName, goComponent.Name);
            Assert.AreEqual(TestVersion, goComponent.Version);
            Assert.AreEqual(TestHash, goComponent.Hash);
            Assert.AreEqual($"{TestName} {TestVersion} - Go", goComponent.Id);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorTest_NameVersionHash_NullVersion()
        {
            GoComponent goComponent = new GoComponent(TestName, null, TestHash);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorTest_NameVersionHash_NullName()
        {
            GoComponent goComponent = new GoComponent(null, TestVersion, TestHash);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConstructorTest_NameVersionHash_NullHash()
        {
            GoComponent goComponent = new GoComponent(TestName, TestVersion, null);
        }

        [TestMethod]
        public void TestEquals()
        {
            GoComponent goComponent1 = new GoComponent(TestName, TestVersion, TestHash);
            GoComponent goComponent2 = new GoComponent(TestName, TestVersion, TestHash);
            GoComponent goComponent3 = new GoComponent(TestName, TestVersion, Guid.NewGuid().ToString());
            Assert.IsTrue(goComponent1.Equals(goComponent2));
            Assert.IsTrue(((object)goComponent1).Equals(goComponent2));

            Assert.IsFalse(goComponent1.Equals(goComponent3));
            Assert.IsFalse(((object)goComponent1).Equals(goComponent3));
        }

        [TestMethod]
        public void TestGetHashCode()
        {
            GoComponent goComponent1 = new GoComponent(TestName, TestVersion, TestHash);
            GoComponent goComponent2 = new GoComponent(TestName, TestVersion, TestHash);
            Assert.IsTrue(goComponent1.GetHashCode() == goComponent2.GetHashCode());
        }
    }
}
