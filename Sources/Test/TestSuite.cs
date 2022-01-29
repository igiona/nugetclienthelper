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

        private string GetNugetCachePath()
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

        /// <summary>
        /// Test with isDotNetLib = NuGetPackageType.DotNet
        /// </summary>
        [Test]
        public void EnsureEnvironmentVariable_DotNetImplementationAssembly()
        {
            var target = "netstandard2.0";
            var p = new NuGetPackageInfo("Unity.Container", "5.11.10", "https://api.nuget.org/v3/index.json", NuGetPackageType.DotNet, GetNugetCachePath());
            var expectedFullPath = Path.Combine(GetNugetCachePath(), "Unity.Container.5.11.10", "lib", target);
            NuGetClientHelper.InstallPackages(new[] { p }, false, null, target);
            Assert.AreEqual(expectedFullPath, Environment.GetEnvironmentVariable("Unity_Container"), "Invalid path environment variable");
            Assert.AreEqual("5.11.10", Environment.GetEnvironmentVariable("Unity_Container_version"), "Invalid version environment variable");
            Assert.AreEqual(target, Environment.GetEnvironmentVariable("Unity_Container_framework"), "Invalid framework environment variable");
        }

        /// <summary>
        /// Test with isDotNetLib = NuGetPackageType.DotNetCompileTimeAssembly
        /// </summary>
        [Test]
        public void EnsureEnvironmentVariable_DotNetCompileTimeAssembly()
        {
            var target = "netstandard1.0";
            var p = new NuGetPackageInfo("Microsoft.CSharp", "4.0.1", "https://api.nuget.org/v3/index.json", NuGetPackageType.DotNet, GetNugetCachePath());
            var expectedFullPath = Path.Combine(GetNugetCachePath(), "Microsoft.CSharp.4.0.1", "ref", target);
            NuGetClientHelper.InstallPackages(new[] { p }, false, null, target);
            Assert.AreEqual(expectedFullPath, Environment.GetEnvironmentVariable("Microsoft_CSharp"), "Invalid path environment variable");
            Assert.AreEqual("4.0.1", Environment.GetEnvironmentVariable("Microsoft_CSharp_version"), "Invalid version environment variable");
            Assert.AreEqual(target, Environment.GetEnvironmentVariable("Microsoft_CSharp_framework"), "Invalid framework environment variable");
        }

        [TestCase("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json")]
        public void InstallPackage(string id, string version, string target, string source)
        {
            var p = new NuGetPackageInfo(id, version, source, NuGetPackageType.DotNet, GetNugetCachePath());
            var installed = NuGetClientHelper.InstallPackages(new[] { p }, false, null, target);
            Assert.AreEqual(1, installed.Count(), "Invalid number of installed packages");
        }

        /// <summary>
        /// A .NET package without code, used only to reference other dependencies
        /// </summary>
        [TestCase("Microsoft.AspNet.WebApi.OwinSelfHost", "5.2.7", "net45", "https://api.nuget.org/v3/index.json")]
        public void InstallCollectorPackage(string id, string version, string target, string source)
        {
            var p = new NuGetPackageInfo(id, version, source, NuGetPackageType.Custom, GetNugetCachePath());
            var installed = NuGetClientHelper.InstallPackages(new[] { p }, true, null, target);
            Assert.AreEqual(9, installed.Count(), "Invalid number of installed packages");
        }

        /// <summary>
        /// A .NET package without code, used only to reference other dependencies
        /// </summary>
        [TestCase("Ninject.Web.WebApi.OwinSelfHost", "3.3.1", "net45", "https://api.nuget.org/v3/index.json")]        
        public void InstallCollectorPackageAsDependency(string id, string version, string target, string source)
        {
            var p = new NuGetPackageInfo(id, version, source, NuGetPackageType.DotNet, GetNugetCachePath());
            var installed = NuGetClientHelper.InstallPackages(new[] { p }, true, null, target);
        }

        /// <summary>
        /// Package which do not follow the .NET package convention should be installed as well.
        /// Check that the "framework" can be used to set "any" folder structure used for the full path
        /// </summary>
        [TestCase("IronPython.StdLib", "2.7.7", "content", "https://api.nuget.org/v3/index.json")]
        [TestCase("IronPython.StdLib", "2.7.7", "OneToThree", "https://api.nuget.org/v3/index.json")]
        public void InstallNonDotNet(string id, string version, string content, string source)
        {
            var p = new NuGetPackageInfo(id, version, source, null, NuGetPackageType.Custom, GetNugetCachePath(), true, content);
            var installed = NuGetClientHelper.InstallPackages(new[] { p }, false, null, NuGet.Frameworks.NuGetFramework.AnyFramework).First();
            Assert.AreEqual(Path.Combine(GetNugetCachePath(), $"{id}.{version}", content), installed.FullPath, "Invalid package FullPath");
        }

        /// <summary>
        /// Test the isDotNetLib = false
        /// </summary>
        [Test]
        public void EnsureEnvironmentVariable_NonDotNetPackage()
        {
            var p = new NuGetPackageInfo("IronPython.StdLib", "2.7.7", "https://api.nuget.org/v3/index.json", null, NuGetPackageType.Custom, GetNugetCachePath(), true, "content\\Lib");
            var expectedFullPath = Path.Combine(GetNugetCachePath(), "IronPython.StdLib.2.7.7", "content", "Lib");
            NuGetClientHelper.InstallPackages(new[] { p }, false, null, NuGet.Frameworks.NuGetFramework.AnyFramework);
            Assert.AreEqual(expectedFullPath, Environment.GetEnvironmentVariable("IronPython_StdLib"), "Invalid path environment variable");
            Assert.AreEqual("2.7.7", Environment.GetEnvironmentVariable("IronPython_StdLib_version"), "Invalid version environment variable");
            Assert.AreEqual(null, Environment.GetEnvironmentVariable("IronPython_StdLib_framework"), "Invalid framework environment variable");
        }

        [Test]
        public void CheckInvalidFramework()
        {
            var p = new NuGetPackageInfo("Microsoft.Owin.Hosting", "2.0.2", "https://api.nuget.org/v3/index.json", NuGetPackageType.DotNet, GetNugetCachePath());
            var ex = Assert.Throws<Exceptions.PackageInstallationException>(() => NuGetClientHelper.InstallPackages(new[] { p }, false, null, "net5"));
            Assert.IsInstanceOf<Exceptions.TargetFrameworkNotFoundException>(ex.InnerException.InnerException);
        }

        [TestCase("NuGet.Common", "5.8.1", "net45", ExpectedResult = "net45")]
        [TestCase("NuGet.Common", "5.8.1", "net472", ExpectedResult = "net472")]
        [TestCase("NuGet.Common", "5.8.1", "net5", ExpectedResult = "netstandard2.0")]
        public string CheckFramework(string id, string version, string framework)
        {
            NuGetPackage installed = null;

            Assert.DoesNotThrow(() =>
               {
                   installed = NuGetClientHelper.InstallPackages(
                       new[] {
                        new NuGetPackageInfo(id, version, "https://api.nuget.org/v3/index.json", NuGetPackageType.DotNet, GetNugetCachePath())
                       }, false, null, framework).First();
               });
            return installed?.TargetFramework;
        }

        [TestCase("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json")]
        public void InstallPackageRecursively(string id, string version, string target, string source)
        {
            var p = new NuGetPackageInfo(id, version, source, NuGetPackageType.DotNet, GetNugetCachePath());
            var installed = NuGetClientHelper.InstallPackages(new[] { p }, true, null, target).ToList();
            Assert.AreEqual(4, installed.Count(),  "Invalid number of installed packages");

            NuGetClientHelper.CheckPackagesConsistency(installed);
        }

        [TestCase("Unity.Container", "5.11.10", "netstandard2.0", "https://api.nuget.org/v3/index.json", new[] { "{0}\\Unity.Container.dll", "{0}\\Unity.Container.pdb" })]
        public void CheckPackageContent(string id, string version, string target, string source, string[] content)
        {
            var p = new NuGetPackageInfo(id, version, source, NuGetPackageType.DotNet, GetNugetCachePath());
            var installed = NuGetClientHelper.InstallPackages(new[] { p }, false, null, target);
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
            var installed = new List<NuGetPackage>();
            var packages = new List<NuGetPackageInfo>();

            packages.Clear();
            packages.Add(new NuGetPackageInfo("CoreLib", "0.0.1", GetLocalTestRepository(), NuGetPackageType.DotNet, GetNugetCachePath()));
            packages.Add(new NuGetPackageInfo("CoreLib", "0.0.2", GetLocalTestRepository(), NuGetPackageType.DotNet, GetNugetCachePath()));
            installed = NuGetClientHelper.InstallPackages(packages, false, null, "netstandard2.0").ToList();
            //Should assert due to the duplicate packages dependency package
            Assert.Throws<Exceptions.MultiplePackagesFoundException>(() => NuGetClientHelper.CheckPackagesConsistency(installed));

            packages.Clear();
            packages.Add(new NuGetPackageInfo("TestLib1", "1.0.0", GetLocalTestRepository(), NuGetPackageType.DotNet, GetNugetCachePath()));
            packages.Add(new NuGetPackageInfo("TestLib2", "1.0.0", GetLocalTestRepository(), NuGetPackageType.DotNet, GetNugetCachePath()));
            installed = NuGetClientHelper.InstallPackages(packages, false, null, "netstandard2.0").ToList();
            //Should assert due to the missing dependency package
            Assert.Throws<Exceptions.DependencyNotFoundException>(() => NuGetClientHelper.CheckPackagesConsistency(installed));

            packages.Clear();
            packages.Add(new NuGetPackageInfo("TestLib1", "1.0.0", GetLocalTestRepository(), NuGetPackageType.DotNet, GetNugetCachePath()));
            packages.Add(new NuGetPackageInfo("TestLib2", "1.0.0", GetLocalTestRepository(), NuGetPackageType.DotNet, GetNugetCachePath()));
            packages.Add(new NuGetPackageInfo("CoreLib", "0.0.1", GetLocalTestRepository(), NuGetPackageType.DotNet, GetNugetCachePath()));
            packages.Add(new NuGetPackageInfo("CoreLib", "0.0.2", GetLocalTestRepository(), NuGetPackageType.DotNet, GetNugetCachePath()));
            installed = NuGetClientHelper.InstallPackages(packages, false, null, "netstandard2.0").ToList();
            //Should assert due to the different versions of the CoreLib checked as dependencies in the TestLib1 package
            Assert.Throws<Exceptions.MultipleDependenciesFoundException>(() => NuGetClientHelper.CheckPackagesConsistency(installed));

            packages.Clear();
            packages.Add(new NuGetPackageInfo("TestLib1", "1.0.0", GetLocalTestRepository(), NuGetPackageType.DotNet, GetNugetCachePath()));
            packages.Add(new NuGetPackageInfo("TestLib2", "1.0.0", GetLocalTestRepository(), NuGetPackageType.DotNet, GetNugetCachePath()));
            installed = NuGetClientHelper.InstallPackages(packages, true, null, "netstandard2.0").ToList();
            //Should assert due to the different versions of the CoreLib package
            Assert.Throws<Exceptions.MultipleDependenciesFoundException>(() => NuGetClientHelper.CheckPackagesConsistency(installed));

            packages.Clear();
            packages.Add(new NuGetPackageInfo("TestLib2", "1.0.0", GetLocalTestRepository(), NuGetPackageType.DotNet, GetNugetCachePath()));
            packages.Add(new NuGetPackageInfo("CoreLib", "0.0.1", GetLocalTestRepository(), NuGetPackageType.DotNet, GetNugetCachePath()));
            installed = NuGetClientHelper.InstallPackages(packages, false, null, "netstandard2.0").ToList();
            //Should assert due to the unsupported version of the CoreLib package
            Assert.Throws<Exceptions.InvalidDependencyFoundException>(() => NuGetClientHelper.CheckPackagesConsistency(installed));

            packages.Clear();
            packages.Add(new NuGetPackageInfo("TestLib2", "1.0.0", GetLocalTestRepository(), NuGetPackageType.DotNet, GetNugetCachePath()));
            packages.Add(new NuGetPackageInfo("CoreLib", "1.0.0", GetLocalTestRepository(), NuGetPackageType.DotNet, GetNugetCachePath()));
            installed = NuGetClientHelper.InstallPackages(packages, false, null, "netstandard2.0").ToList();
            //Although the Lib2 is build against the CoreLib;0.0.2 this test should pass.
            //This because Lib2 was built with version dependency CoreLib>=0.0.2
            NuGetClientHelper.CheckPackagesConsistency(installed);

            //With the flag set to true, this test should now fail, because the exact min version 0.0.2 is missing
            Assert.Throws<Exceptions.InvalidMinVersionDependencyFoundException>(() => NuGetClientHelper.CheckPackagesConsistency(installed, true));

            packages.Clear();
            packages.Add(new NuGetPackageInfo("TestLib2", "1.0.0", GetLocalTestRepository(), NuGetPackageType.DotNet, GetNugetCachePath(), false));
            packages.Add(new NuGetPackageInfo("CoreLib", "1.0.0", GetLocalTestRepository(), NuGetPackageType.DotNet, GetNugetCachePath()));
            installed = NuGetClientHelper.InstallPackages(packages, false, null, "netstandard2.0").ToList();
            //Although the Lib2 is build against the CoreLib;0.0.2 this test should pass.
            //This because the package TestLib2 is instantiated with the parameter dependencyForceMinVersion set to false.
            NuGetClientHelper.CheckPackagesConsistency(installed, true);
        }

        /// <summary>
        /// Unity has a reference to CommonServiceLocator version 1.3.0 => the version comes really as 1.3.0!
        /// CommonServiceLocator version 1.3.0 doesn't actually exists, the nuspec specifies the version 1.3!
        /// Check that the code creates the correct FullPath (checked in the NuGetClientHelper code)
        /// </summary>
        [Test]
        public void VersionConsistencyCheckOnDependency()
        {
            var packages = new List<NuGetPackageInfo>();
            packages.Add(new NuGetPackageInfo("Unity", "4.0.1", "https://api.nuget.org/v3/index.json", NuGetPackageType.DotNet, GetNugetCachePath()));
            packages.Add(new NuGetPackageInfo("CommonServiceLocator", "1.3", "https://api.nuget.org/v3/index.json", NuGetPackageType.DotNet, GetNugetCachePath()));
            var installed = NuGetClientHelper.InstallPackages(packages, true, null, "net45").ToList();
            NuGetClientHelper.CheckPackagesConsistency(installed);
        }

        /// <summary>
        /// Unity has a dependency to CommonServiceLocator version 1.3.0 => the version comes really as 1.3.0!
        /// CommonServiceLocator version 1.3.0 doesn't actually exists, the nuspec specifies the version 1.3!
        /// Check that the code creates the correct FullPath (checked in the NuGetClientHelper code) and that the 
        /// consistency check works, since the package 1.3 is in fact equal as the package 1.3.0
        /// </summary>
        [Test]
        public void VersionConsistencyCheckOnPackage()
        {
            var packages = new List<NuGetPackageInfo>();
            packages.Add(new NuGetPackageInfo("CommonServiceLocator", "1.3", "https://api.nuget.org/v3/index.json", NuGetPackageType.DotNet, GetNugetCachePath()));
            packages.Add(new NuGetPackageInfo("Unity", "4.0.1", "https://api.nuget.org/v3/index.json", NuGetPackageType.DotNet, GetNugetCachePath()));
            var installed = NuGetClientHelper.InstallPackages(packages, true, null, "net45").ToList();
            NuGetClientHelper.CheckPackagesConsistency(installed, true);
        }

        /// <summary>
        /// Check that the consistency-check works, since the package version 10.0.10586 is in fact equal as the package 10.0.10586.0 
        /// </summary>
        [Test]
        public void VersionConsistencyCheckOnPackageOnDependency()
        {
            var packages = new List<NuGetPackageInfo>();
            packages.Add(new NuGetPackageInfo("TestVersionConflict", "1.0.0",GetLocalTestRepository(), NuGetPackageType.DotNet, GetNugetCachePath()));
            packages.Add(new NuGetPackageInfo("System.Management.Automation.dll", "10.0.10586.0", "https://api.nuget.org/v3/index.json", NuGetPackageType.DotNet, GetNugetCachePath()));
            var installed = NuGetClientHelper.InstallPackages(packages, true, null, "net45").ToList();
            NuGetClientHelper.CheckPackagesConsistency(installed, true);
        }

        [TestCase("1.0.0", "net45")]
        [TestCase("2.0.0", "Any")]
        [TestCase("3.0.0", "Any")]
        [TestCase("3.0.0", "")]
        public void TestNearestFrameworkAny(string version, string targetFramework)
        {
            var packages = new List<NuGetPackageInfo>();
            packages.Add(new NuGetPackageInfo("TestFrameworkAny", version, GetLocalTestRepository(), NuGetPackageType.DotNet, GetNugetCachePath()));
            var installed = NuGetClientHelper.InstallPackages(packages, true, null, targetFramework).ToList();
            NuGetClientHelper.CheckPackagesConsistency(installed, true);
        }

        [TestCase("CommonServiceLocator", "1.3", "portable-net4+sl5+netcore45+wpa81+wp8", "https://api.nuget.org/v3/index.json")]
        public void TestSpeacialFramework(string id, string version, string target, string source)
        {
            var p = new NuGetPackageInfo(id, version, source, NuGetPackageType.DotNet, GetNugetCachePath());
            Assert.DoesNotThrow(() => NuGetClientHelper.InstallPackages(new[] { p }, false, null, target));
        }

        /// <summary>
        /// The provided test packages have both compileTime and implementation assemblies compatible with the requested framework.
        /// This test ensures that the implementation assemblies are selected.
        /// </summary>
        [TestCase("System.Buffers", "4.5.1", "net461")]
        [TestCase("System.Numerics.Vectors", "4.5.0", "net46")]
        [TestCase("System.Runtime.CompilerServices.Unsafe", "4.5.3", "net461")]
        public void EnsureImplementationAssembliesWhenAvailable(string id, string version, string framework)
        {
            var packages = new List<NuGetPackageInfo>();

            packages.Add(new NuGetPackageInfo(id, version, "https://api.nuget.org/v3/index.json", NuGetPackageType.DotNet, GetNugetCachePath()));
            var installed = NuGetClientHelper.InstallPackages(packages, false, null, framework).ToList();

            Assert.IsTrue(installed.First().PackageType == NuGetDotNetPackageType.DotNetImplementationAssembly);
        }

        /// <summary>
        /// System.Resources.Extensions is a compile time assembly, with dependencies to (directly, or indirectly):
        /// - System.Memory
        /// - System.Buffers
        /// - System.Numerics.Vectors
        /// - System.Runtime.CompilerServices.Unsafe
        /// Numerics.Vectors and CompilerServices.Unsafe have both compileTime and implementation assemblies compatible with the requested framework.
        /// This test ensures that the implementation assemblies are selected when the packages are installed as dependencies.
        /// </summary>
        [Test]
        public void EnsureImplementationAssembliesWhenAvailableOnDependency()
        {
            var packages = new List<NuGetPackageInfo>();

            packages.Add(new NuGetPackageInfo("System.Resources.Extensions", "5.0.0",  "https://api.nuget.org/v3/index.json", NuGetPackageType.DotNet, GetNugetCachePath()));
            var installed = NuGetClientHelper.InstallPackages(packages, true, null, "net461").ToList();

            Assert.IsTrue(installed.Any(x => x.Identity.Id == "System.Memory"));
            Assert.IsTrue(installed.Any(x => x.Identity.Id == "System.Buffers"));
            Assert.IsTrue(installed.Any(x => x.Identity.Id == "System.Numerics.Vectors"));
            Assert.IsTrue(installed.Any(x => x.Identity.Id == "System.Runtime.CompilerServices.Unsafe"));

            Assert.IsTrue(installed.Where(x => x.Identity.Id == "System.Numerics.Vectors").First().PackageType == NuGetDotNetPackageType.DotNetImplementationAssembly);
            Assert.IsTrue(installed.Where(x => x.Identity.Id == "System.Runtime.CompilerServices.Unsafe").First().PackageType == NuGetDotNetPackageType.DotNetImplementationAssembly);

            NuGetClientHelper.CheckPackagesConsistency(installed);
        }

        [TestCase("Newtonsoft.Json", "9.0.1", "netstandard1.0")]
        [TestCase("Newtonsoft.Json", "12.0.3", "netstandard2.0")]
        public void CompileTimeReferencesInstallOnDependency(string id, string version, string framework)
        {
            var packages = new List<NuGetPackageInfo>();

            packages.Add(new NuGetPackageInfo(id, version, "https://api.nuget.org/v3/index.json", NuGetPackageType.DotNet, GetNugetCachePath()));
            var installed = NuGetClientHelper.InstallPackages(packages, true, null, framework).ToList();
            NuGetClientHelper.CheckPackagesConsistency(installed);
        }

        [Test]
        public void CompileTimeReferencesInstallOnPackage()
        {
            var packages = new List<NuGetPackageInfo>();

            packages.Add(new NuGetPackageInfo("Microsoft.CSharp", "4.0.1", "https://api.nuget.org/v3/index.json", NuGetPackageType.DotNet, GetNugetCachePath()));
            var installed = NuGetClientHelper.InstallPackages(packages, true, null, "netstandard1.0").ToList();
            Assert.IsTrue(installed.Where(x => x.Identity.Id == "Microsoft.CSharp").First().PackageType == NuGetDotNetPackageType.DotNetCompileTimeAssembly);
            Assert.DoesNotThrow(() => NuGetClientHelper.CheckPackagesConsistency(installed));
            packages.Clear();
        }

        [Test]
        public void CompileTimeReferencesCheck()
        {
            var packages = new List<NuGetPackageInfo>();

            packages.Add(new NuGetPackageInfo("Newtonsoft.Json", "9.0.1", "https://api.nuget.org/v3/index.json", NuGetPackageType.DotNet, GetNugetCachePath()));
            var installed = NuGetClientHelper.InstallPackages(packages, false, null, "netstandard1.0").ToList();
            //Should fail because at least the dependency to Microsoft.CSharp is missing.
            Assert.Throws<Exceptions.DependencyNotFoundException>(() => NuGetClientHelper.CheckPackagesConsistency(installed));

            //Should not fail because the dependency check is inhibited. 
            NuGetClientHelper.CheckPackagesConsistency(installed, false, true);
        }


        [Test]
        public void CheckPackageLibraryContent()
        {
            var p = new NuGetPackageInfo("Unity", "4.0.1", "https://api.nuget.org/v3/index.json", NuGetPackageType.DotNet, GetNugetCachePath());
            var installed = NuGetClientHelper.InstallPackages(new[] { p }, false, null, "net45").First();

            Assert.AreEqual(6, installed.Libraries.Count());
        }

        [Test]
        public void CheckAdditionalDependencySources()
        {
            var packages = new List<NuGetPackageInfo>();

            packages.Clear();
            packages.Add(new NuGetPackageInfo("TestLib1", "1.0.0", GetLocalTestLibOnlyRepository(), NuGetPackageType.DotNet, GetNugetCachePath()));

            //Should fail due to the missing dependency package in the repository
            var ex = Assert.Throws<Exceptions.PackageInstallationException>(() => NuGetClientHelper.InstallPackages(packages, true, null, "netstandard2.0"));
            Assert.IsInstanceOf<Exceptions.DependencyNotFoundException>(ex.InnerException.InnerException);

            //This should work without exceptions
            packages.Clear();
            packages.Add(new NuGetPackageInfo("TestLib1", "1.0.0", GetLocalTestLibOnlyRepository(), new[] { GetLocalTestRepository() }, NuGetPackageType.DotNet, GetNugetCachePath(), true, null));
            Assert.DoesNotThrow(() => NuGetClientHelper.InstallPackages(packages, true, null, "netstandard2.0").ToList());
        }

        [Test]
        public void CheckDependencyConfusion()
        {
            var packages = new List<NuGetPackageInfo>();
            packages.Clear();
            packages.Add(new NuGetPackageInfo("CoreLib", "0.0.1", GetLocalTestLibOnlyRepository(), new[] { GetLocalTestRepository() }, NuGetPackageType.DotNet, GetNugetCachePath(), true, null));
            try
            {
                //Should fail because CoreLib is found in one of the dependency repositories, not in the source one.
                NuGetClientHelper.InstallPackages(packages, true, null, "netstandard2.0").ToList();
            }
            catch (Exceptions.PackageInstallationException e)
            {
                Assert.AreEqual(typeof(Exceptions.DependencyConfusionException), e.InnerException.InnerException.GetType(), "DependencyNotFoundException not found in test call.");
            }
        }
    }
}
