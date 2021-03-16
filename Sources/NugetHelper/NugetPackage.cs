﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetHelper
{
    public class NugetPackage
    {
        public const string DotNetCompileTimeAssemblyPath = "ref";

        public const string DotNetImplementationAssemblyPath = "lib";

        public NugetPackage(string id, string version, string targetFramework, string source, string var, NugetPackageType packageType, string packagesRoot)
        {
            Dependencies = new List<NuGet.Packaging.Core.PackageDependency>();
            Libraries = new List<string>();
            Id = id;
            VersionRange = NuGet.Versioning.VersionRange.Parse(System.Environment.ExpandEnvironmentVariables(version));
            MinVersion = VersionRange.ToNonSnapshotRange().MinVersion.ToString();
            
            if (string.IsNullOrEmpty(source))
            {
                throw new Exception(string.Format("Invalid source for the package id {0};{1}. The source parameter is mandatory.", Id, MinVersion));
            }
            Source = new Uri(System.Environment.ExpandEnvironmentVariables(source));
            
            RootPath = packagesRoot;

            SetDotNetLibInformation(targetFramework, packageType);

            EnvironmentVariableKeys = new List<string>();
            EnvironmentVariableKeys.Add(EscapeStringAsEnvironmentVariableAsKey(Id));
            EnvironmentVariableKeys.Add(GetVersionEnvironmentVariableKey(Id));
            EnvironmentVariableKeys.Add(GetFrameworkEnvironmentVariableKey(Id));

            //Alaways set the "default" key value
            Environment.SetEnvironmentVariable(EscapeStringAsEnvironmentVariableAsKey(Id), FullPath);
            Environment.SetEnvironmentVariable(GetVersionEnvironmentVariableKey(Id), MinVersion);
            Environment.SetEnvironmentVariable(GetFrameworkEnvironmentVariableKey(Id), TargetFramework);

            if (!string.IsNullOrEmpty(var)) //If requested, set also the user specified value
            {
                EnvironmentVariableAdditionalKey = var;
                if (Environment.GetEnvironmentVariable(EnvironmentVariableAdditionalKey) == null)
                    Environment.SetEnvironmentVariable(EnvironmentVariableAdditionalKey, FullPath);
            }
        }

        public string Id { get; private set; }

        public string MinVersion { get; private set; }

        public NuGet.Versioning.VersionRange VersionRange { get; private set; }

        public string TargetFramework { get; private set; }

        public bool CompileTimeTarget { get; private set; }

        public Uri Source { get; private set; }

        public List<string> EnvironmentVariableKeys { get; private set; }

        public string EnvironmentVariableAdditionalKey { get; private set; }

        public string RootPath { get; private set; }

        public string FullPath { get; private set; }

        public NugetPackageType PackageType { get; private set; }
        
        public List<NuGet.Packaging.Core.PackageDependency> Dependencies { get; private set; }

        public List<string> Libraries { get; private set; }

        public static string EscapeStringAsEnvironmentVariableAsKey(string id)
        {
            return id.Replace(".", "_"); //.Replace("-", "_");
        }

        public static string GetVersionEnvironmentVariableKey(string id)
        {
            return string.Format("{0}_version", EscapeStringAsEnvironmentVariableAsKey(id));
        }

        public static string GetDebugEnvironmentVariableKey(string id)
        {
            return string.Format("{0}_debug", EscapeStringAsEnvironmentVariableAsKey(id));
        }

        public static string GetFrameworkEnvironmentVariableKey(string id)
        {
            return string.Format("{0}_framework", EscapeStringAsEnvironmentVariableAsKey(id));
        }

        public void AddDependencies(IEnumerable<NuGet.Packaging.Core.PackageDependency> dependencies)
        {
            Dependencies.AddRange(dependencies);
        }

        public void AddLibraries(IEnumerable<string> libraries)
        {
            Libraries.AddRange(libraries);
        }

        public override string ToString()
        {
            return string.Format("{0} V{1}", Id, MinVersion);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as NugetPackage);
        }

        public bool Equals(NugetPackage p)
        {
            // If parameter is null, return false.
            if (Object.ReferenceEquals(p, null))
            {
                return false;
            }

            // Optimization for a common success case.
            if (Object.ReferenceEquals(this, p))
            {
                return true;
            }

            // If run-time types are not exactly the same, return false.
            if (this.GetType() != p.GetType())
            {
                return false;
            }

            return (Id == p.Id) && (MinVersion == p.MinVersion);
        }

        public static bool operator ==(NugetPackage lhs, NugetPackage rhs)
        {
            // Check for null on left side.
            if (Object.ReferenceEquals(lhs, null))
            {
                if (Object.ReferenceEquals(rhs, null))
                {
                    // null == null = true.
                    return true;
                }

                // Only the left side is null.
                return false;
            }
            // Equals handles case of null on right side.
            return lhs.Equals(rhs);
        }

        public static bool operator !=(NugetPackage lhs, NugetPackage rhs)
        {
            return !(lhs == rhs);
        }

        private void SetDotNetLibInformation(string targetFramework, NugetPackageType t)
        {
            PackageType = t;
            TargetFramework = "";
            var assemblyFolderDict = new Dictionary<NugetPackageType, string>() {
                { NugetPackageType.DotNetCompileTimeAssembly, DotNetCompileTimeAssemblyPath},
                { NugetPackageType.DotNetImplementationAssembly, DotNetImplementationAssemblyPath }
            };

            if (!string.IsNullOrEmpty(targetFramework))
            {
                TargetFramework = System.Environment.ExpandEnvironmentVariables(targetFramework);
            }
            
            var basePath = Path.Combine(RootPath, string.Format("{0}.{1}", Id, MinVersion));

            if (t == NugetPackageType.DotNetImplementationAssembly || t == NugetPackageType.DotNetCompileTimeAssembly)
            {
                if (string.IsNullOrEmpty(TargetFramework))
                {
                    throw new Exception($"The NuGet package {ToString()} is marked as .NET lib, but the TargetFramework is not specified.");
                }
                FullPath = Path.Combine(basePath, assemblyFolderDict[t], TargetFramework);
            }
            else
            {
                FullPath = basePath;
                if (!string.IsNullOrEmpty(TargetFramework))
                {
                    FullPath = Path.Combine(FullPath, TargetFramework);
                }
            }
        }

        /// <summary>
        /// Loads in the <see cref="Libraries"/> property all the files present in the FullPath.
        /// </summary>
        /// <exception cref="Exceptions.InvalidAssemblyPathException">Thrown when the FullPath doesn't exists, or cannot be resolved (e.g. Any framework)</exception>
        public void LoadLibraries()
        {
            var validFullPath = true;
            if (!Directory.Exists(FullPath))
            {
                validFullPath = false;
                if (NuGet.Frameworks.NuGetFramework.ParseFolder(TargetFramework) == NuGet.Frameworks.NuGetFramework.AnyFramework)
                {
                    //In case of Any framework, try to exclude the framework name from the path
                    FullPath = Path.GetDirectoryName(FullPath);
                    validFullPath = Directory.Exists(FullPath);
                }
            }

            if (validFullPath)
            {
                AddLibraries(Directory.GetFiles(FullPath));
            }
            else
            {
                throw new Exceptions.InvalidAssemblyPathException($"The installed package {this} has an invalid library path {FullPath}");
            }
        }
    }
}
