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

            //Test with isDotNetLib = NugetPackageType.DotNetImplementationAssembly
            var p = new NugetPackage("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json", extraKey, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath());
            var expectedFullPath = Path.Combine(GetNugetCachePath(), "Unity.Container.5.11.10", "lib", "netstandard2.0");
            Assert.AreEqual(expectedFullPath, Environment.GetEnvironmentVariable("Unity_Container"), "Invalid path env variable");
            Assert.AreEqual(expectedFullPath, Environment.GetEnvironmentVariable("_TEST_ME_TEST_"), "Invalid path env variable");
            Assert.AreEqual("5.11.10", Environment.GetEnvironmentVariable("Unity_Container_version"), "Invalid version env variable");
            Assert.AreEqual("netstandard2.0", Environment.GetEnvironmentVariable("Unity_Container_framework"), "Invalid framework env variable");

            Environment.SetEnvironmentVariable(extraKey, null); //Reset the extra key

            //Test with isDotNetLib = NugetPackageType.DotNetCompileTimeAssembly
            p = new NugetPackage("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json", extraKey, NugetPackageType.DotNetCompileTimeAssembly, GetNugetCachePath());
            expectedFullPath = Path.Combine(GetNugetCachePath(), "Unity.Container.5.11.10", "ref", "netstandard2.0");
            Assert.AreEqual(expectedFullPath, Environment.GetEnvironmentVariable("Unity_Container"), "Invalid path env variable");
            Assert.AreEqual(expectedFullPath, Environment.GetEnvironmentVariable("_TEST_ME_TEST_"), "Invalid path env variable");
            Assert.AreEqual("5.11.10", Environment.GetEnvironmentVariable("Unity_Container_version"), "Invalid version env variable");
            Assert.AreEqual("netstandard2.0", Environment.GetEnvironmentVariable("Unity_Container_framework"), "Invalid framework env variable");
            
            Environment.SetEnvironmentVariable(extraKey, null); //Reset the extra key

            //Test the isDotNetLib = false
            p = new NugetPackage("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json", extraKey, NugetPackageType.Other, GetNugetCachePath());
            expectedFullPath = Path.Combine(GetNugetCachePath(), "Unity.Container.5.11.10", "netstandard2.0");
            Assert.AreEqual(expectedFullPath, Environment.GetEnvironmentVariable("Unity_Container"), "Invalid path env variable");
            Assert.AreEqual(expectedFullPath, Environment.GetEnvironmentVariable(extraKey), "Invalid path env variable");
            Assert.AreEqual("5.11.10", Environment.GetEnvironmentVariable("Unity_Container_version"), "Invalid version env variable");
            Assert.AreEqual("netstandard2.0", Environment.GetEnvironmentVariable("Unity_Container_framework"), "Invalid framework env variable");


            //Set the extra key to something known
            Environment.SetEnvironmentVariable(extraKey, "AlreadySetByMe");
            p = new NugetPackage("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json", extraKey, NugetPackageType.Other, GetNugetCachePath());
            Assert.AreEqual("AlreadySetByMe", Environment.GetEnvironmentVariable(extraKey), "Invalid path env variable");
        }

        [TestCase("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json")]
        [TestCase("CommonServiceLocator", "1.3", "portable-net4+sl5+netcore45+wpa81+wp8", "https://api.nuget.org/v3/index.json")] //Short version format.
        public void InstallPackage(string id, string version, string target, string source)
        {
            var p = new NugetPackage(id, version, target, source, null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath());
            var installed = NugetHelper.InstallPackages(new[] { p }, false, null);
            Assert.AreEqual(1, installed.Count(), "Invalid number of installed packages");
        }

        [Test]
        public void CheckFramework()
        {
            var p = new NugetPackage("Unity.Container", "5.11.10", "net472", "https://api.nuget.org/v3/index.json", null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath());
            var ex = Assert.Throws<Exception>(() => NugetHelper.InstallPackages(new[] { p }, false, null));
            Assert.IsInstanceOf<Exceptions.TargetFrameworkNotFoundException>(ex.InnerException.InnerException);
        }

        [TestCase("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json")]
        public void InstallPackageRecursively(string id, string version, string target, string source)
        {
            var p = new NugetPackage(id, version, target, source, null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath());
            var installed = NugetHelper.InstallPackages(new[] { p }, true, null).ToList();
            Assert.AreEqual(4, installed.Count(),  "Invalid number of installed packages");

            NugetHelper.CheckPackagesConsistency(installed);
        }

        [TestCase("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json", new[] { "{0}\\Unity.Container.dll", "{0}\\Unity.Container.pdb" })]
        public void CheckPackageContent(string id, string version, string target, string source, string[] content)
        {
            var p = new NugetPackage(id, version, target, source, null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath());
            var installed = NugetHelper.InstallPackages(new[] { p }, false, null);
            Assert.AreEqual(installed.Count(), 1, "Invalid number of installed packages");

            var installedPackage = installed.Single();
            Assert.AreEqual(installedPackage.Libraries.Count, content.Length, "Invalid number of library files found in the packages");

            foreach (var c in content)
            {
                Assert.IsTrue(installedPackage.Libraries.Contains(string.Format(c, installedPackage.FullPath)));
            }
        }


        [Test]
        public void ConsistencyCheck()
        {
            var packages = new List<NugetPackage>();

            packages.Add(new NugetPackage("Package A", "1.0.0", "net5", "https://api.nuget.org/v3/index.json", null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages.Add(new NugetPackage("Package A", "2.0.0", "net5", "https://api.nuget.org/v3/index.json", null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));

            Assert.Throws<Exceptions.MultiplePackagesFoundException>(() => NugetHelper.CheckPackagesConsistency(packages));

            /*
             * TestLib1 => CoreLib >= 0.0.1
             * TestLib2 => CoreLib >= 0.0.2
             * TestLib3 => CoreLib >= 1.0.0
             */
            packages.Clear();
            packages.Add(new NugetPackage("TestLib1", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages.Add(new NugetPackage("TestLib2", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages = NugetHelper.InstallPackages(packages, false, null).ToList();
            //Should assert due to the missing dependency package
            Assert.Throws<Exceptions.DependencyNotFoundException>(() => NugetHelper.CheckPackagesConsistency(packages));

            packages.Clear();
            packages.Add(new NugetPackage("TestLib1", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages.Add(new NugetPackage("TestLib2", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages.Add(new NugetPackage("CoreLib", "0.0.1", "netstandard2.0", GetLocalTestRepository(), null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages.Add(new NugetPackage("CoreLib", "0.0.2", "netstandard2.0", GetLocalTestRepository(), null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages = NugetHelper.InstallPackages(packages, false, null).ToList();
            //Should assert due to the different versions of the CoreLib checked as dependencies in the TestLib1 package
            Assert.Throws<Exceptions.MultipleDependenciesFoundException>(() => NugetHelper.CheckPackagesConsistency(packages));

            packages.Clear();
            packages.Add(new NugetPackage("TestLib1", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages.Add(new NugetPackage("TestLib2", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages = NugetHelper.InstallPackages(packages, true, null).ToList();
            //Should assert due to the different versions of the CoreLib package
            Assert.Throws<Exceptions.MultipleDependenciesFoundException>(() => NugetHelper.CheckPackagesConsistency(packages));

            packages.Clear();
            packages.Add(new NugetPackage("TestLib2", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages.Add(new NugetPackage("CoreLib", "0.0.1", "netstandard2.0", GetLocalTestRepository(), null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages = NugetHelper.InstallPackages(packages, false, null).ToList();
            //Should assert due to the unsupprted version of the CoreLib package
            Assert.Throws<Exceptions.InvalidDependencyFoundException>(() => NugetHelper.CheckPackagesConsistency(packages));

            packages.Clear();
            packages.Add(new NugetPackage("TestLib2", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages.Add(new NugetPackage("CoreLib", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages = NugetHelper.InstallPackages(packages, false, null).ToList();
            //Although the Lib2 is build against the CoreLib;0.0.2 this test should pass.
            //This because Lib2 was built with version dependency CoreLib>=0.0.2
            NugetHelper.CheckPackagesConsistency(packages);

            //With the flag set to true, this thest should now fail, because the exact min version 0.0.2 is missing
            Assert.Throws<Exceptions.InvalidMinVersionDependencyFoundExceptio>(() => NugetHelper.CheckPackagesConsistency(packages, true));

            packages.Clear();
            packages.Add(new NugetPackage("TestLib2", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath(), false));
            packages.Add(new NugetPackage("CoreLib", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages = NugetHelper.InstallPackages(packages, false, null).ToList();
            //Although the Lib2 is build against the CoreLib;0.0.2 this test should pass.
            //This because hte packge TestLib2 is instantiated with the parameter dependencyForceMinVersion set to false.
            NugetHelper.CheckPackagesConsistency(packages, true);
        }

        /// <summary>
        /// Unity has a reference to CommonServiceLocator version 1.3.0 => the version comes really as 1.3.0!
        /// CommonServiceLocator version 1.3.0 doesn't actually exists, the nuspec specifies the version 1.3!
        /// Check that the code creates the correct FullPath (checked in the NugetHelper code)
        /// 
        /// NOTE: the package CommonServiceLocator is installed under portable-net4+sl5+netcore45+wpa81+wp8
        /// When installed as dependency of Unity, the code currently resolves the framework to portable-net40+sl5+win8+wp8+wpa81
        /// causing this test to fail. 
        /// For this reason, it's set as Ignore!
        /// </summary>
        [Test, Ignore("Currently known to fail")]
        public void VersionConsistencyCheckOnDependency()
        {
            var packages = new List<NugetPackage>();
            packages.Add(new NugetPackage("Unity", "4.0.1", "net45", "https://api.nuget.org/v3/index.json", null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages.Add(new NugetPackage("CommonServiceLocator", "1.3", "portable-net4+sl5+netcore45+wpa81+wp8", "https://api.nuget.org/v3/index.json", null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages = NugetHelper.InstallPackages(packages, true, null).ToList();
            NugetHelper.CheckPackagesConsistency(packages);
        }

        /// <summary>
        /// Unity has a dependency to CommonServiceLocator version 1.3.0 => the version comes really as 1.3.0!
        /// CommonServiceLocator version 1.3.0 doesn't actually exists, the nuspec specifies the version 1.3!
        /// Check that the code creates the correct FullPath (checked in the NugetHelper code) and that the 
        /// consistency check works, since the package 1.3 is in fact equal as the package 1.3.0
        /// </summary>
        [Test]
        public void VersionConsistencyCheckOnPackageOnPackage()
        {
            var packages = new List<NugetPackage>();
            packages.Add(new NugetPackage("CommonServiceLocator", "1.3", "portable-net4+sl5+netcore45+wpa81+wp8", "https://api.nuget.org/v3/index.json", null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages.Add(new NugetPackage("Unity", "4.0.1", "net45", "https://api.nuget.org/v3/index.json", null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages = NugetHelper.InstallPackages(packages, true, null).ToList();
            NugetHelper.CheckPackagesConsistency(packages, true);
        }

        /// <summary>
        /// TestVersionConflict;1.0.0 as a dependency to System.Management.Automation.dll;10.0.10586
        /// System.Management.Automation.dll;10.0.10586 doesn't exist, the version on NuGet.og is actually 10.0.10586.0
        /// 
        /// Check that the consistency-check works, since the package version 10.0.10586 is in fact equal as the package 10.0.10586.0 
        /// </summary>
        [Test]
        public void VersionConsistencyCheckOnPackageOnDependency()
        {
            var packages = new List<NugetPackage>();
            packages.Add(new NugetPackage("TestVersionConflict", "1.0.0", "net45", GetLocalTestRepository(), null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages.Add(new NugetPackage("System.Management.Automation.dll", "10.0.10586.0", "net40", "https://api.nuget.org/v3/index.json", null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages = NugetHelper.InstallPackages(packages, true, null).ToList();
            NugetHelper.CheckPackagesConsistency(packages, true);
        }

        [TestCase("1.0.0", "net45")]
        [TestCase("2.0.0", "Any")]
        [TestCase("3.0.0", "Any")]
        public void TestNearestFrameworkAny(string version, string targetFramework)
        {
            var packages = new List<NugetPackage>();
            packages.Add(new NugetPackage("TestFrameworkAny", version, targetFramework, GetLocalTestRepository(), null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages = NugetHelper.InstallPackages(packages, true, null).ToList();
            NugetHelper.CheckPackagesConsistency(packages, true);
        }

        [TestCase("Newtonsoft.Json", "9.0.1", "netstandard1.0")]
        [TestCase("Newtonsoft.Json", "12.0.3", "netstandard2.0")]
        public void CompileTimeReferencesInstallOnDependency(string id, string version, string framework)
        {
            var packages = new List<NugetPackage>();

            packages.Add(new NugetPackage(id, version, framework, "https://api.nuget.org/v3/index.json", null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages = NugetHelper.InstallPackages(packages, true, null).ToList();
            NugetHelper.CheckPackagesConsistency(packages);
        }

        [Test]
        public void CompileTimeReferencesInstallOnPackage()
        {
            var packages = new List<NugetPackage>();

            packages.Add(new NugetPackage("Microsoft.CSharp", "4.0.1", "netstandard1.0", "https://api.nuget.org/v3/index.json", null, NugetPackageType.DotNetCompileTimeAssembly, GetNugetCachePath()));
            packages = NugetHelper.InstallPackages(packages, true, null).ToList();
            NugetHelper.CheckPackagesConsistency(packages);
            packages.Clear();
        }

        [Test]
        public void CompileTimeReferencesCheck()
        {
            var packages = new List<NugetPackage>();

            packages.Add(new NugetPackage("Newtonsoft.Json", "9.0.1", "netstandard1.0", "https://api.nuget.org/v3/index.json", null, NugetPackageType.DotNetImplementationAssembly, GetNugetCachePath()));
            packages = NugetHelper.InstallPackages(packages, false, null).ToList();
            //Should fail because at least the dependency to Microsoft.CSharp is missing.
            Assert.Throws<Exceptions.DependencyNotFoundException>(() => NugetHelper.CheckPackagesConsistency(packages));

            //Should not fail because the dependency check is inhibited. 
            NugetHelper.CheckPackagesConsistency(packages, false, true);
        }
    }
}
