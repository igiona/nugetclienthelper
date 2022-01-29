using System;
using System.Collections.Generic;
using System.Text;

namespace NuGetClientHelper
{
    /// <summary>
    /// Explicitly avoid using NuGet.Packaging.Core.PackageIdentity, to have better control on the "MinVersion" checks.
    /// </summary>
    public class NuGetPackageIdentity
    {
        private readonly int _hashCode;
        
        public NuGetPackageIdentity(string id, string version)
        {
            Id = id;
            VersionRange = NuGet.Versioning.VersionRange.Parse(System.Environment.ExpandEnvironmentVariables(version));
            MinVersion = VersionRange.ToNonSnapshotRange().MinVersion.ToString();

            unchecked
            {
                var hashCode = Id.ToUpper().GetHashCode();
                hashCode = (hashCode * 397) ^ VersionRange.GetHashCode();
                hashCode = (hashCode * 397) ^ MinVersion.GetHashCode();
                _hashCode = hashCode;
            }
        }

        public string Id { get; private set; }
        public string MinVersion { get; private set; }
        
        public NuGet.Versioning.VersionRange VersionRange { get; private set; }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as NuGetPackageIdentity);
        }

        public bool Equals(NuGetPackageIdentity p)
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

        public static bool operator ==(NuGetPackageIdentity lhs, NuGetPackageIdentity rhs)
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

        public static bool operator !=(NuGetPackageIdentity lhs, NuGetPackageIdentity rhs)
        {
            return !(lhs == rhs);
        }

        public override string ToString()
        {
            return $"{Id} V{MinVersion}";
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }
    }
}
