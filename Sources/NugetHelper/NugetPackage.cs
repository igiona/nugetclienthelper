using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetClientHelper
{
    public class NuGetPackage
    {
        public const string DotNetCompileTimeAssemblyPath = "ref";

        public const string DotNetImplementationAssemblyPath = "lib";

        private readonly Dictionary<NuGetPackageType, string> AssemblyFolderDictionary = new Dictionary<NuGetPackageType, string>() {
            { NuGetPackageType.DotNetCompileTimeAssembly, DotNetCompileTimeAssemblyPath},
            { NuGetPackageType.DotNetImplementationAssembly, DotNetImplementationAssemblyPath }
        };
        private readonly int _hashCode;
        private readonly NuGetPackageInfo _info;

        public NuGetPackage(NuGetPackageInfo info, string targetFramework)
        {
            Dependencies = new List<NuGetDependency>();
            Libraries = new List<string>();
            _info = info;

            TargetFramework = string.IsNullOrEmpty(targetFramework) ? "" : System.Environment.ExpandEnvironmentVariables(targetFramework);

            if (PackageType != NuGetPackageType.Other)
            {
                SetDotNetLibInformation(targetFramework);
            }
            else //Ignore the TargetFramework and eventually use the CustomContentPath value
            {
                FullPath = PackageRootPath;
                if (!string.IsNullOrEmpty(_info.CustomContentPath))
                {
                    FullPath = Path.Combine(FullPath, _info.CustomContentPath);
                }
            }
            EnvironmentVariableKeys = new List<string>();
            EnvironmentVariableKeys.Add(EscapeStringAsEnvironmentVariableAsKey(Identity.Id));
            EnvironmentVariableKeys.Add(GetVersionEnvironmentVariableKey(Identity.Id));
            EnvironmentVariableKeys.Add(GetFrameworkEnvironmentVariableKey(Identity.Id));

            //Always set the "default" key value
            Environment.SetEnvironmentVariable(EscapeStringAsEnvironmentVariableAsKey(Identity.Id), FullPath);
            Environment.SetEnvironmentVariable(GetVersionEnvironmentVariableKey(Identity.Id), Identity.MinVersion);
            Environment.SetEnvironmentVariable(GetFrameworkEnvironmentVariableKey(Identity.Id), TargetFramework);

            if (Directory.Exists(FullPath))
            {
                AddLibraries(Directory.GetFiles(FullPath));
            }
            else if (PackageType != NuGetPackageType.Other)
            {
                throw new Exceptions.InvalidAssemblyPathException($"Unable to load libraries. The library path of {this} doesn't exists ['{FullPath}']");
            }

            unchecked
            {
                var hashCode = Identity.GetHashCode();
                hashCode = (hashCode * 397) ^ TargetFramework.GetHashCode();
                hashCode = (hashCode * 397) ^ Source.GetHashCode();
                _hashCode = hashCode;
            }
        }

        public NuGetPackageIdentity Identity => _info.Identity;                

        public bool DependenciesForceMinVersion => _info.DependenciesForceMinVersion;

        public string TargetFramework { get; private set; }

        public bool CompileTimeTarget { get; private set; }

        public Uri Source => _info.Source;

        public List<Uri> DependencySources => _info.DependencySources;

        public List<string> EnvironmentVariableKeys { get; private set; }

        public string EnvironmentVariableAdditionalKey { get; private set; }

        /// <summary>
        /// Location of the package folder. Usually a "cache" folder.
        /// </summary>
        public string RootPath => _info.RootPath;

        /// <summary>
        /// Path pointing to the package root, where the package content is unpacked in.
        /// </summary>
        public string PackageRootPath => _info.PackageRootPath;

        /// <summary>
        /// Path pointing to the folder containing the assemblies. For example: {PackageRootPath}/lib/net5
        /// </summary>
        public string FullPath { get; private set; }

        public NuGetPackageType PackageType => _info.PackageType;
        
        public List<NuGetDependency> Dependencies { get; private set; }

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
            Dependencies.AddRange(dependencies.Select(x => new NuGetDependency(x, DependenciesForceMinVersion)));
        }

        public void AddLibraries(IEnumerable<string> libraries)
        {
            Libraries.AddRange(libraries);
        }

        public override string ToString()
        {
            return $"{Identity} {TargetFramework}";
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as NuGetPackage);
        }

        public bool Equals(NuGetPackage p)
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

            return (Identity == p.Identity);
        }

        public static bool operator ==(NuGetPackage lhs, NuGetPackage rhs)
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

        public static bool operator !=(NuGetPackage lhs, NuGetPackage rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        private void SetDotNetLibInformation(string targetFramework)
        {
            #region Framwework spacial cases resolver
            var frameworkResolver = new Dictionary<string, Func<string, string[]>>()
                {
                    {
                        "Any,Version=v0.0",
                        (framework) => new[]
                        {
                            Path.Combine(PackageRootPath, AssemblyFolderDictionary[PackageType], framework),
                            Path.Combine(PackageRootPath, AssemblyFolderDictionary[PackageType]),
                            PackageRootPath //This is assumed to always exists!                          
                        }
                    },
                    {
                        ".NETPortable,Version=v0.0,Profile=Profile328",
                        (framework) => new[]
                        {
                            Path.Combine(PackageRootPath, AssemblyFolderDictionary[PackageType], framework),
                            Path.Combine(PackageRootPath, AssemblyFolderDictionary[PackageType], "portable-net4+sl5+netcore45+wpa81+wp8"),
                        }
                    }
                };
            #endregion

            if (string.IsNullOrEmpty(TargetFramework))
            {
                throw new Exception($"The NuGet package {ToString()} is marked as .NET lib, but the TargetFramework is not specified.");
            }

            var parsedFramework = NuGet.Frameworks.NuGetFramework.ParseFolder(TargetFramework);
            var parsedFrameworkName = parsedFramework?.GetDotNetFrameworkName(NuGet.Frameworks.DefaultFrameworkNameProvider.Instance) ?? throw new Exceptions.TargetFrameworkNotFoundException($"Unable to parse {TargetFramework} in {this}");
            if (frameworkResolver.ContainsKey(parsedFrameworkName))
            {
                FullPath = frameworkResolver[parsedFrameworkName](TargetFramework).Where(x => Directory.Exists(x)).FirstOrDefault();
            }
            else
            {
                FullPath = Path.Combine(PackageRootPath, AssemblyFolderDictionary[PackageType], TargetFramework);
            }

            if (PackageType != NuGetPackageType.Other && (FullPath == null || !Directory.Exists(FullPath)))
            {
                throw new Exceptions.InvalidAssemblyPathException($"Unable to find the FullPath ['{FullPath}'] of the .NET package {this}");
            }
        }
    }
}
