using Microsoft.ComponentDetection.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;

namespace Microsoft.ComponentDetection.Common.Tests
{
    [TestClass]
    public class EnvironmentVariableServiceTests
    {
        public const string MyEnvVar = nameof(MyEnvVar);

        [TestInitialize]
        public void TestInitialize()
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableServiceTests.MyEnvVar, "true");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableServiceTests.MyEnvVar, null);
        }

        [TestMethod]
        public void EnvironmentVariableService_ChecksAreCaseInsensitive()
        {
            var testSubject = new EnvironmentVariableService();

            Assert.IsFalse(testSubject.DoesEnvironmentVariableExist("THIS_ENVIRONMENT_VARIABLE_DOES_NOT_EXIST"));

            Assert.IsTrue(testSubject.DoesEnvironmentVariableExist(MyEnvVar));
            Assert.IsTrue(testSubject.DoesEnvironmentVariableExist(MyEnvVar.ToLower()));
            Assert.IsTrue(testSubject.DoesEnvironmentVariableExist(MyEnvVar.ToUpper()));
        }
    }
}
