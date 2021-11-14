using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace NuGetClientHelper.Test
{
    /// <summary>
    /// In all test cases, never use a NuGet package that is in use in this solution!
    /// It will be possibly create issues because of the instantiated environment variables.
    /// 
    /// Test packages
    /// 
    /// TestLib1 => CoreLib >= 0.0.1
    /// TestLib2 => CoreLib >= 0.0.2
    /// TestLib3 => CoreLib >= 1.0.0
    /// 
    /// TestVersionConflict;1.0.0 as a dependency to System.Management.Automation.dll;10.0.10586
    /// System.Management.Automation.dll;10.0.10586 doesn't exist, the version on NuGet.og is actually 10.0.10586.0
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

        private string GetNuGetCachePath()
        {
            return GetOutPath("Cache");
        }

        private string GetLocalTestRepository()
        {
            return Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.FullName, "TestPackages");
        }

        private string GetLocalTestLibOnlyRepository()
        {
            return Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.FullName, "TestLibOnlyPackages");
        }

        private string GetOutPath(string sub)
        {
            return Path.Combine(OutputDirectory, sub);
        }


        [TestCase()]
        public void EnsureEnvironmentVariable()
        {
            var extraKey = "_TEST_ME_TEST_";

            //Test with isDotNetLib = NuGetPackageType.DotNetImplementationAssembly
            var p = new NuGetPackage("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json", extraKey, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath());
            var expectedFullPath = Path.Combine(GetNuGetCachePath(), "Unity.Container.5.11.10", "lib", "netstandard2.0");
            Assert.AreEqual(expectedFullPath, Environment.GetEnvironmentVariable("Unity_Container"), "Invalid path env variable");
            Assert.AreEqual(expectedFullPath, Environment.GetEnvironmentVariable("_TEST_ME_TEST_"), "Invalid path env variable");
            Assert.AreEqual("5.11.10", Environment.GetEnvironmentVariable("Unity_Container_version"), "Invalid version env variable");
            Assert.AreEqual("netstandard2.0", Environment.GetEnvironmentVariable("Unity_Container_framework"), "Invalid framework env variable");

            Environment.SetEnvironmentVariable(extraKey, null); //Reset the extra key

            //Test with isDotNetLib = NuGetPackageType.DotNetCompileTimeAssembly
            p = new NuGetPackage("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json", extraKey, NuGetPackageType.DotNetCompileTimeAssembly, GetNuGetCachePath());
            expectedFullPath = Path.Combine(GetNuGetCachePath(), "Unity.Container.5.11.10", "ref", "netstandard2.0");
            Assert.AreEqual(expectedFullPath, Environment.GetEnvironmentVariable("Unity_Container"), "Invalid path env variable");
            Assert.AreEqual(expectedFullPath, Environment.GetEnvironmentVariable("_TEST_ME_TEST_"), "Invalid path env variable");
            Assert.AreEqual("5.11.10", Environment.GetEnvironmentVariable("Unity_Container_version"), "Invalid version env variable");
            Assert.AreEqual("netstandard2.0", Environment.GetEnvironmentVariable("Unity_Container_framework"), "Invalid framework env variable");
            
            Environment.SetEnvironmentVariable(extraKey, null); //Reset the extra key

            //Test the isDotNetLib = false
            p = new NuGetPackage("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json", extraKey, NuGetPackageType.Other, GetNuGetCachePath());
            expectedFullPath = Path.Combine(GetNuGetCachePath(), "Unity.Container.5.11.10", "netstandard2.0");
            Assert.AreEqual(expectedFullPath, Environment.GetEnvironmentVariable("Unity_Container"), "Invalid path env variable");
            Assert.AreEqual(expectedFullPath, Environment.GetEnvironmentVariable(extraKey), "Invalid path env variable");
            Assert.AreEqual("5.11.10", Environment.GetEnvironmentVariable("Unity_Container_version"), "Invalid version env variable");
            Assert.AreEqual("netstandard2.0", Environment.GetEnvironmentVariable("Unity_Container_framework"), "Invalid framework env variable");


            //Set the extra key to something known
            Environment.SetEnvironmentVariable(extraKey, "AlreadySetByMe");
            p = new NuGetPackage("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json", extraKey, NuGetPackageType.Other, GetNuGetCachePath());
            Assert.AreEqual("AlreadySetByMe", Environment.GetEnvironmentVariable(extraKey), "Invalid path env variable");
        }

        [TestCase("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json")]
        [TestCase("CommonServiceLocator", "1.3", "portable-net4+sl5+netcore45+wpa81+wp8", "https://api.nuget.org/v3/index.json")] //Short version format.
        public void InstallPackage(string id, string version, string target, string source)
        {
            var p = new NuGetPackage(id, version, target, source, null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath());
            var installed = NuGetClientHelper.InstallPackages(new[] { p }, false, null);
            Assert.AreEqual(1, installed.Count(), "Invalid number of installed packages");
        }

        [Test]
        public void CheckFramework()
        {
            var p = new NuGetPackage("Unity.Container", "5.11.10", "net472", "https://api.nuget.org/v3/index.json", null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath());
            var ex = Assert.Throws<Exceptions.PackageInstallationException>(() => NuGetClientHelper.InstallPackages(new[] { p }, false, null));
            Assert.IsInstanceOf<Exceptions.TargetFrameworkNotFoundException>(ex.InnerException.InnerException);
        }

        [TestCase("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json")]
        public void InstallPackageRecursively(string id, string version, string target, string source)
        {
            var p = new NuGetPackage(id, version, target, source, null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath());
            var installed = NuGetClientHelper.InstallPackages(new[] { p }, true, null).ToList();
            Assert.AreEqual(4, installed.Count(),  "Invalid number of installed packages");

            NuGetClientHelper.CheckPackagesConsistency(installed);
        }

        [TestCase("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json", new[] { "{0}\\Unity.Container.dll", "{0}\\Unity.Container.pdb" })]
        public void CheckPackageContent(string id, string version, string target, string source, string[] content)
        {
            var p = new NuGetPackage(id, version, target, source, null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath());
            var installed = NuGetClientHelper.InstallPackages(new[] { p }, false, null);
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
            var packages = new List<NuGetPackage>();

            packages.Add(new NuGetPackage("Package A", "1.0.0", "net5", "https://api.nuget.org/v3/index.json", null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages.Add(new NuGetPackage("Package A", "2.0.0", "net5", "https://api.nuget.org/v3/index.json", null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));

            Assert.Throws<Exceptions.MultiplePackagesFoundException>(() => NuGetClientHelper.CheckPackagesConsistency(packages));

            packages.Clear();
            packages.Add(new NuGetPackage("TestLib1", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages.Add(new NuGetPackage("TestLib2", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages = NuGetClientHelper.InstallPackages(packages, false, null).ToList();
            //Should assert due to the missing dependency package
            Assert.Throws<Exceptions.DependencyNotFoundException>(() => NuGetClientHelper.CheckPackagesConsistency(packages));

            packages.Clear();
            packages.Add(new NuGetPackage("TestLib1", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages.Add(new NuGetPackage("TestLib2", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages.Add(new NuGetPackage("CoreLib", "0.0.1", "netstandard2.0", GetLocalTestRepository(), null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages.Add(new NuGetPackage("CoreLib", "0.0.2", "netstandard2.0", GetLocalTestRepository(), null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages = NuGetClientHelper.InstallPackages(packages, false, null).ToList();
            //Should assert due to the different versions of the CoreLib checked as dependencies in the TestLib1 package
            Assert.Throws<Exceptions.MultipleDependenciesFoundException>(() => NuGetClientHelper.CheckPackagesConsistency(packages));

            packages.Clear();
            packages.Add(new NuGetPackage("TestLib1", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages.Add(new NuGetPackage("TestLib2", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages = NuGetClientHelper.InstallPackages(packages, true, null).ToList();
            //Should assert due to the different versions of the CoreLib package
            Assert.Throws<Exceptions.MultipleDependenciesFoundException>(() => NuGetClientHelper.CheckPackagesConsistency(packages));

            packages.Clear();
            packages.Add(new NuGetPackage("TestLib2", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages.Add(new NuGetPackage("CoreLib", "0.0.1", "netstandard2.0", GetLocalTestRepository(), null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages = NuGetClientHelper.InstallPackages(packages, false, null).ToList();
            //Should assert due to the unsupprted version of the CoreLib package
            Assert.Throws<Exceptions.InvalidDependencyFoundException>(() => NuGetClientHelper.CheckPackagesConsistency(packages));

            packages.Clear();
            packages.Add(new NuGetPackage("TestLib2", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages.Add(new NuGetPackage("CoreLib", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages = NuGetClientHelper.InstallPackages(packages, false, null).ToList();
            //Although the Lib2 is build against the CoreLib;0.0.2 this test should pass.
            //This because Lib2 was built with version dependency CoreLib>=0.0.2
            NuGetClientHelper.CheckPackagesConsistency(packages);

            //With the flag set to true, this thest should now fail, because the exact min version 0.0.2 is missing
            Assert.Throws<Exceptions.InvalidMinVersionDependencyFoundException>(() => NuGetClientHelper.CheckPackagesConsistency(packages, true));

            packages.Clear();
            packages.Add(new NuGetPackage("TestLib2", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath(), false));
            packages.Add(new NuGetPackage("CoreLib", "1.0.0", "netstandard2.0", GetLocalTestRepository(), null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages = NuGetClientHelper.InstallPackages(packages, false, null).ToList();
            //Although the Lib2 is build against the CoreLib;0.0.2 this test should pass.
            //This because hte packge TestLib2 is instantiated with the parameter dependencyForceMinVersion set to false.
            NuGetClientHelper.CheckPackagesConsistency(packages, true);
        }

        /// <summary>
        /// Unity has a reference to CommonServiceLocator version 1.3.0 => the version comes really as 1.3.0!
        /// CommonServiceLocator version 1.3.0 doesn't actually exists, the nuspec specifies the version 1.3!
        /// Check that the code creates the correct FullPath (checked in the NuGetClientHelper code)
        /// 
        /// NOTE: the package CommonServiceLocator is installed under portable-net4+sl5+netcore45+wpa81+wp8
        /// When installed as dependency of Unity, the code currently resolves the framework to portable-net40+sl5+win8+wp8+wpa81
        /// causing this test to fail. 
        /// For this reason, it's set as Ignore!
        /// </summary>
        [Test, Ignore("Currently known to fail")]
        public void VersionConsistencyCheckOnDependency()
        {
            var packages = new List<NuGetPackage>();
            packages.Add(new NuGetPackage("Unity", "4.0.1", "net45", "https://api.nuget.org/v3/index.json", null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages.Add(new NuGetPackage("CommonServiceLocator", "1.3", "portable-net4+sl5+netcore45+wpa81+wp8", "https://api.nuget.org/v3/index.json", null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages = NuGetClientHelper.InstallPackages(packages, true, null).ToList();
            NuGetClientHelper.CheckPackagesConsistency(packages);
        }

        /// <summary>
        /// Unity has a dependency to CommonServiceLocator version 1.3.0 => the version comes really as 1.3.0!
        /// CommonServiceLocator version 1.3.0 doesn't actually exists, the nuspec specifies the version 1.3!
        /// Check that the code creates the correct FullPath (checked in the NuGetClientHelper code) and that the 
        /// consistency check works, since the package 1.3 is in fact equal as the package 1.3.0
        /// </summary>
        [Test]
        public void VersionConsistencyCheckOnPackageOnPackage()
        {
            var packages = new List<NuGetPackage>();
            packages.Add(new NuGetPackage("CommonServiceLocator", "1.3", "portable-net4+sl5+netcore45+wpa81+wp8", "https://api.nuget.org/v3/index.json", null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages.Add(new NuGetPackage("Unity", "4.0.1", "net45", "https://api.nuget.org/v3/index.json", null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages = NuGetClientHelper.InstallPackages(packages, true, null).ToList();
            NuGetClientHelper.CheckPackagesConsistency(packages, true);
        }

        /// <summary>
        /// Check that the consistency-check works, since the package version 10.0.10586 is in fact equal as the package 10.0.10586.0 
        /// </summary>
        [Test]
        public void VersionConsistencyCheckOnPackageOnDependency()
        {
            var packages = new List<NuGetPackage>();
            packages.Add(new NuGetPackage("TestVersionConflict", "1.0.0", "net45", GetLocalTestRepository(), null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages.Add(new NuGetPackage("System.Management.Automation.dll", "10.0.10586.0", "net40", "https://api.nuget.org/v3/index.json", null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages = NuGetClientHelper.InstallPackages(packages, true, null).ToList();
            NuGetClientHelper.CheckPackagesConsistency(packages, true);
        }

        [TestCase("1.0.0", "net45")]
        [TestCase("2.0.0", "Any")]
        [TestCase("3.0.0", "Any")]
        public void TestNearestFrameworkAny(string version, string targetFramework)
        {
            var packages = new List<NuGetPackage>();
            packages.Add(new NuGetPackage("TestFrameworkAny", version, targetFramework, GetLocalTestRepository(), null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages = NuGetClientHelper.InstallPackages(packages, true, null).ToList();
            NuGetClientHelper.CheckPackagesConsistency(packages, true);
        }

        /// <summary>
        /// The provided test packages have both compileTime and implementation assemblies compatible with the requested framework.
        /// This test ensures that the the implementation assemblies are selected.
        /// </summary>
        [TestCase("System.Buffers", "4.5.1", "net461")]
        [TestCase("System.Numerics.Vectors", "4.5.0", "net46")]
        [TestCase("System.Runtime.CompilerServices.Unsafe", "4.5.3", "net461")]
        public void EnsureImplementationAssembliesWhenAvailable(string id, string version, string framework)
        {
            var packages = new List<NuGetPackage>();

            packages.Add(new NuGetPackage(id, version, framework, "https://api.nuget.org/v3/index.json", null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages = NuGetClientHelper.InstallPackages(packages, false, null).ToList();

            Assert.IsTrue(packages.First().PackageType == NuGetPackageType.DotNetImplementationAssembly);
        }

        /// <summary>
        /// System.Resources.Extensions is a compile time assembly, with dependencies to (directly, or indirectly):
        /// - System.Memory
        /// - System.Buffers
        /// - System.Numerics.Vectors
        /// - System.Runtime.CompilerServices.Unsafe
        /// Numerics.Vectors and CompilerServices.Unsafe have both compileTime and implementation assemblies compatible with the requested framework.
        /// This test ensures that the the implementation assemblies are selected when the packages are installed as dependencies.
        /// </summary>
        [Test]
        public void EnsureImplementationAssembliesWhenAvailableOnDependency()
        {
            var packages = new List<NuGetPackage>();

            packages.Add(new NuGetPackage("System.Resources.Extensions", "5.0.0", "net461", "https://api.nuget.org/v3/index.json", null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages = NuGetClientHelper.InstallPackages(packages, true, null).ToList();

            Assert.IsTrue(packages.Any(x => x.Identity.Id == "System.Memory"));
            Assert.IsTrue(packages.Any(x => x.Identity.Id == "System.Buffers"));
            Assert.IsTrue(packages.Any(x => x.Identity.Id == "System.Numerics.Vectors"));
            Assert.IsTrue(packages.Any(x => x.Identity.Id == "System.Runtime.CompilerServices.Unsafe"));

            Assert.IsTrue(packages.Where(x => x.Identity.Id == "System.Numerics.Vectors").First().PackageType == NuGetPackageType.DotNetImplementationAssembly);
            Assert.IsTrue(packages.Where(x => x.Identity.Id == "System.Runtime.CompilerServices.Unsafe").First().PackageType == NuGetPackageType.DotNetImplementationAssembly);

            NuGetClientHelper.CheckPackagesConsistency(packages);
        }

        [TestCase("Newtonsoft.Json", "9.0.1", "netstandard1.0")]
        [TestCase("Newtonsoft.Json", "12.0.3", "netstandard2.0")]
        public void CompileTimeReferencesInstallOnDependency(string id, string version, string framework)
        {
            var packages = new List<NuGetPackage>();

            packages.Add(new NuGetPackage(id, version, framework, "https://api.nuget.org/v3/index.json", null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages = NuGetClientHelper.InstallPackages(packages, true, null).ToList();
            NuGetClientHelper.CheckPackagesConsistency(packages);
        }

        [Test]
        public void CompileTimeReferencesInstallOnPackage()
        {
            var packages = new List<NuGetPackage>();

            packages.Add(new NuGetPackage("Microsoft.CSharp", "4.0.1", "netstandard1.0", "https://api.nuget.org/v3/index.json", null, NuGetPackageType.DotNetCompileTimeAssembly, GetNuGetCachePath()));
            packages = NuGetClientHelper.InstallPackages(packages, true, null).ToList();
            NuGetClientHelper.CheckPackagesConsistency(packages);
            packages.Clear();
        }

        [Test]
        public void CompileTimeReferencesCheck()
        {
            var packages = new List<NuGetPackage>();

            packages.Add(new NuGetPackage("Newtonsoft.Json", "9.0.1", "netstandard1.0", "https://api.nuget.org/v3/index.json", null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            packages = NuGetClientHelper.InstallPackages(packages, false, null).ToList();
            //Should fail because at least the dependency to Microsoft.CSharp is missing.
            Assert.Throws<Exceptions.DependencyNotFoundException>(() => NuGetClientHelper.CheckPackagesConsistency(packages));

            //Should not fail because the dependency check is inhibited. 
            NuGetClientHelper.CheckPackagesConsistency(packages, false, true);
        }


        [Test]
        public void CheckPackageLibraryContent()
        {
            var p = new NuGetPackage("Unity", "4.0.1", "net45", "https://api.nuget.org/v3/index.json", null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath());
            var installed = NuGetClientHelper.InstallPackages(new[] { p }, false, null).First();

            Assert.AreEqual(6, installed.Libraries.Count());
        }

        [Test]
        public void CheckAdditionalDependencySources()
        {
            var packages = new List<NuGetPackage>();

            packages.Clear();
            packages.Add(new NuGetPackage("TestLib1", "1.0.0", "netstandard2.0", GetLocalTestLibOnlyRepository(), null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));

            //Should fail due to the missing dependency package in the repository
            var ex = Assert.Throws<Exceptions.PackageInstallationException>(() => NuGetClientHelper.InstallPackages(packages, true, null));
            Assert.IsInstanceOf<Exceptions.DependencyNotFoundException>(ex.InnerException.InnerException);

            //This should work withouth excpetions
            packages.Clear();
            packages.Add(new NuGetPackage("TestLib1", "1.0.0", "netstandard2.0", GetLocalTestLibOnlyRepository(), new[] { GetLocalTestRepository() }, null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            NuGetClientHelper.InstallPackages(packages, true, null).ToList();
        }

        [Test]
        public void CheckDependencyConfusion()
        {
            var packages = new List<NuGetPackage>();
            packages.Clear();
            packages.Add(new NuGetPackage("CoreLib", "0.0.1", "netstandard2.0", GetLocalTestLibOnlyRepository(), new[] { GetLocalTestRepository() }, null, NuGetPackageType.DotNetImplementationAssembly, GetNuGetCachePath()));
            try
            {
                //Should fail becauese CoreLib is found in one of the dependency repos, not in the source one.
                NuGetClientHelper.InstallPackages(packages, true, null).ToList();
            }
            catch (Exceptions.PackageInstallationException e)
            {
                Assert.AreEqual(typeof(Exceptions.DependencyConfusionException), e.InnerException.InnerException.GetType(), "DependencyNotFoundException not found in test call.");
            }
        }
    }
}
