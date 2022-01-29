using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetClientHelper
{
    public struct NuGetPackageInfo
    {
        private readonly int _hashCode;

        public NuGetPackageInfo(string id, string version, string source, NuGetPackageType packageType, string packagesRoot, bool dependeciesForceMinVersion = true)
            : this(id, version, source, null, packageType, packagesRoot, dependeciesForceMinVersion, null)
        {
        }

        /// <summary>
        /// </summary>
        /// <param name="id">Id of the NuGet package</param>
        /// <param name="version">Version of the NuGet package</param>
        /// <param name="source">URL in which the tool will look first to find the package. Can be also a local folder.</param>
        /// <param name="dependenciesSources">List of sources in which the tool will look for dependencies. Can be <see langword="null"/></param>
        /// <param name="packageType">Define weather the package is a .NET package or not. Used to properly create the content path</param>
        /// <param name="packagesRoot">Directory in which the package will be extracted</param>
        /// <param name="dependeciesForceMinVersion">Instruct the tool weather to enforce full match on the package dependencies of this package</param>
        /// <param name="customContentPath">In case of <paramref name="packageType"/> set to Other, this folder will be appended to the <paramref name="packagesRoot"/>. Can be <see langword="null"/></param>
        /// <exception cref="Exception"></exception>
        public NuGetPackageInfo(string id, string version, string source, string[] dependenciesSources, NuGetPackageType packageType, string packagesRoot, bool dependeciesForceMinVersion, string customContentPath)
        {
            Identity = new NuGetPackageIdentity(id, version);
            DependenciesForceMinVersion = dependeciesForceMinVersion;

            if (string.IsNullOrEmpty(source))
            {
                throw new Exception($"Invalid source for the package {Identity}. The source parameter is mandatory.");
            }

            RootPath = packagesRoot;
            PackageType = packageType;
            Source = new Uri("http://localhost");
            DependencySources = new List<Uri>();
            CustomContentPath = customContentPath;

            unchecked
            {
                var hashCode = Identity.GetHashCode();
                hashCode = (hashCode * 397) ^ Source.GetHashCode();
                _hashCode = hashCode;
            }
            
            if (dependenciesSources != null)
            {
                foreach (var src in dependenciesSources)
                {
                    DependencySources.Add(TryGetUri(src));
                }
            }
            Source = TryGetUri(source);
        }

        public NuGetPackageIdentity Identity { get; private set; }

        public bool DependenciesForceMinVersion { get; private set; }

        public Uri Source { get; private set; }

        public List<Uri> DependencySources { get; private set; }

        /// <summary>
        /// Location of the package folder. Usually a "cache" folder.
        /// </summary>
        public string RootPath { get; private set; }

        /// <summary>
        /// Path pointing to the package root, where the package content is unpacked in.
        /// </summary>
        public string PackageRootPath => Path.Combine(RootPath, $"{Identity.Id}.{Identity.MinVersion}");

        public NuGetPackageType PackageType { get; private set; }

        public string CustomContentPath { get; private set; }

        public void SetSource(Uri source)
        {
            Source = source;
        }

        public override string ToString()
        {
            return $"{Identity}";
        }

        public override bool Equals(object obj)
        {
            return this.Equals((NuGetPackageInfo)obj);
        }

        public bool Equals(NuGetPackageInfo p)
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

        public static bool operator ==(NuGetPackageInfo lhs, NuGetPackageInfo rhs)
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

        public static bool operator !=(NuGetPackageInfo lhs, NuGetPackageInfo rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        private Uri TryGetUri(string uriString)
        {
            var expandedString = System.Environment.ExpandEnvironmentVariables(uriString).Trim();
            try
            {
                return new Uri(expandedString);
            }
            catch (UriFormatException)
            {
                throw new UriFormatException($"The specified URL of the package {this} is invalid, the expanded value is: {expandedString}");
            }
        }
    }
}
