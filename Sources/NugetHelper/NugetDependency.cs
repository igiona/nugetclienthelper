using System;
using System.Collections.Generic;
using System.Text;

namespace NuGetClientHelper
{
    public class NuGetDependency
    { 
        public NuGetDependency(NuGet.Packaging.Core.PackageDependency d, bool forceMinVersion)
        {
            PackageDependency = d;
            ForceMinVersion = forceMinVersion;
        }

        public bool ForceMinVersion{ get; private set; }

        public NuGet.Packaging.Core.PackageDependency PackageDependency { get; private set; }

        public override string ToString()
        {
            return PackageDependency.ToString();
        }
    }
}
