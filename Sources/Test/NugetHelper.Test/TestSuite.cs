using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace NugetHelper.Test
{
    /// <summary>
    /// In all test cases, never use a NuGet package that is in use in this solution!
    /// It will be possibly create issues because of the instantiated environment variables.
    /// </summary>
    [TestFixture]
    public class TestSuite
    {
        string OutputDirectory = Path.GetFullPath("Outputdata");
        static Dictionary<string, string> _originalVariables = null;

        [SetUp]
        public void SetUp()
        {
            //Since the code creates environment variable, ensure that the original are backed up
            //in order to restore them before each test.
            if (_originalVariables == null)
            {
                var cp = (System.Collections.Hashtable)System.Environment.GetEnvironmentVariables();
                var keys = cp.Keys.Cast<string>().ToArray();
                var values = cp.Values.Cast<string>().ToArray();

                _originalVariables = new Dictionary<string, string>();
                for (int i = 0; i < keys.Length; i++)
                {
                    _originalVariables[keys[i]] = values[i];
                }
            }
            else
            {
                var cp = (System.Collections.Hashtable)System.Environment.GetEnvironmentVariables();
                var keys = cp.Keys.Cast<string>().ToArray();
                for (int i = 0; i < keys.Length; i++)
                {
                    if (!_originalVariables.ContainsKey(keys[i])) //Reset all new vars
                    {
                        Console.WriteLine("Resetting: {0}", keys[i]);
                        System.Environment.SetEnvironmentVariable(keys[i], null);
                    }
                }
                for (int i = 0; i < _originalVariables.Count(); i++)
                {
                    System.Environment.SetEnvironmentVariable(_originalVariables.Keys.ElementAt(i), _originalVariables.Values.ElementAt(i));
                }
            }

            if (Directory.Exists(OutputDirectory))
            {
                Directory.Delete(OutputDirectory, true);
            }
            Directory.CreateDirectory(OutputDirectory);
        }

        private string GetNugetCachePath()
        {
            return GetOutPath("Cache");
        }

        private string GetLocalTestRepository()
        {
            return Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.FullName, "TestPackages");
        }

        private string GetOutPath(string sub)
        {
            return Path.Combine(OutputDirectory, sub);
        }


        [TestCase()]
        public void EnsureEnvironmentVariable()
        {
            var extraKey = "_TEST_ME_TEST_";

            //Test with isDotNetLib = true
            var p = new NugetPackage("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json", extraKey, true, GetNugetCachePath());
            var expectedFullPath = Path.Combine(GetNugetCachePath(), "Unity.Container.5.11.10", "lib", "netstandard2.0");
            Assert.AreEqual(Environment.GetEnvironmentVariable("Unity_Container"), expectedFullPath, "Invalid path env variable");
            Assert.AreEqual(Environment.GetEnvironmentVariable("_TEST_ME_TEST_"), expectedFullPath, "Invalid path env variable");
            Assert.AreEqual(Environment.GetEnvironmentVariable("Unity_Container_version"), "5.11.10", "Invalid version env variable");
            Assert.AreEqual(Environment.GetEnvironmentVariable("Unity_Container_framework"), "netstandard2.0", "Invalid framework env variable");

            //Reset the extra key
            Environment.SetEnvironmentVariable(extraKey, null);

            //Test the isDotNetLib = false
            p = new NugetPackage("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json", extraKey, false, GetNugetCachePath());
            expectedFullPath = Path.Combine(GetNugetCachePath(), "Unity.Container.5.11.10", "netstandard2.0");
            Assert.AreEqual(Environment.GetEnvironmentVariable("Unity_Container"), expectedFullPath, "Invalid path env variable");
            Assert.AreEqual(Environment.GetEnvironmentVariable(extraKey), expectedFullPath, "Invalid path env variable");
            Assert.AreEqual(Environment.GetEnvironmentVariable("Unity_Container_version"), "5.11.10", "Invalid version env variable");
            Assert.AreEqual(Environment.GetEnvironmentVariable("Unity_Container_framework"), "netstandard2.0", "Invalid framework env variable");


            //Set the extra key to something known
            Environment.SetEnvironmentVariable(extraKey, "AlreadySetByMe");
            p = new NugetPackage("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json", extraKey, false, GetNugetCachePath());
            Assert.AreEqual(Environment.GetEnvironmentVariable(extraKey), "AlreadySetByMe", "Invalid path env variable");
        }

        [TestCase("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json")]
        public void InstallPackage(string id, string version, string target, string source)
        {
            var p = new NugetPackage(id, version, target, source, null, true, GetNugetCachePath());
            var installed = NugetHelper.InstallPackages(new[] { p }, false, null);
            Assert.AreEqual(installed.Count(), 1, "Invalid number of installed packages");
        }

        [TestCase("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json")]
        public void InstallPackageRecursively(string id, string version, string target, string source)
        {
            var p = new NugetPackage(id, version, target, source, null, true, GetNugetCachePath());
            var installed = NugetHelper.InstallPackages(new[] { p }, true, null);
            Assert.AreEqual(installed.Count(), 4, "Invalid number of installed packages");

            NugetHelper.CheckPackagesConsistency(installed);
        }

        [Test]
        public void ConsistencyCheck()
        {
            var packages = new List<NugetPackage>();

            packages.Add(new NugetPackage("Package A", "1.0.0", "net5", "https://api.nuget.org/v3/index.json", null, true, GetNugetCachePath()));
            packages.Add(new NugetPackage("Package A", "2.0.0", "net5", "https://api.nuget.org/v3/index.json", null, true, GetNugetCachePath()));

            Assert.Throws<Exception>(() => NugetHelper.CheckPackagesConsistency(packages));

            /*
             * TestLib1 => CoreLib >= 0.0.1
             * TestLib2 => CoreLib >= 0.0.2
             * TestLib3 => CoreLib >= 1.0.0
             */
            packages.Clear();
            packages.Add(new NugetPackage("TestLib1", "1.0.0", "net5", GetLocalTestRepository(), null, true, GetNugetCachePath()));
            packages.Add(new NugetPackage("TestLib2", "1.0.0", "net5", GetLocalTestRepository(), null, true, GetNugetCachePath()));
            NugetHelper.InstallPackages(packages, false, null);
            //Should assert due to the missing dependency package
            Assert.Throws<Exception>(() => NugetHelper.CheckPackagesConsistency(packages));

            packages.Clear();
            packages.Add(new NugetPackage("TestLib1", "1.0.0", "net5", GetLocalTestRepository(), null, true, GetNugetCachePath()));
            packages.Add(new NugetPackage("TestLib2", "1.0.0", "net5", GetLocalTestRepository(), null, true, GetNugetCachePath()));
            packages.Add(new NugetPackage("CoreLib", "0.0.1", "net5", GetLocalTestRepository(), null, true, GetNugetCachePath()));
            packages.Add(new NugetPackage("CoreLib", "0.0.2", "net5", GetLocalTestRepository(), null, true, GetNugetCachePath()));
            NugetHelper.InstallPackages(packages, false, null);
            //Should assert due to the different versions of the CoreLib package
            Assert.Throws<Exception>(() => NugetHelper.CheckPackagesConsistency(packages));

            packages.Clear();
            packages.Add(new NugetPackage("TestLib2", "1.0.0", "net5", GetLocalTestRepository(), null, true, GetNugetCachePath()));
            packages.Add(new NugetPackage("CoreLib", "0.0.1", "net5", GetLocalTestRepository(), null, true, GetNugetCachePath()));
            NugetHelper.InstallPackages(packages, false, null);
            //Should assert due to the unsupprted version of the CoreLib package
            Assert.Throws<Exception>(() => NugetHelper.CheckPackagesConsistency(packages));

            packages.Clear();
            packages.Add(new NugetPackage("TestLib2", "1.0.0", "net5", GetLocalTestRepository(), null, true, GetNugetCachePath()));
            packages.Add(new NugetPackage("CoreLib", "1.0.0", "net5", GetLocalTestRepository(), null, true, GetNugetCachePath()));
            NugetHelper.InstallPackages(packages, false, null);
            //Although the Lib2 is build against the CoreLib;0.0.2 this test should pass.
            //This because Lib2 was built with version dependency CoreLib>=0.0.2
            NugetHelper.CheckPackagesConsistency(packages);
        }
    }
}
