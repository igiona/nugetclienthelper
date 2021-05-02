using System;
using System.Collections.Generic;
using System.Text;

namespace NugetHelper.Exceptions
{
    public class DependencyNotFoundException : Exception
    {
        public DependencyNotFoundException(NuGet.Packaging.Core.PackageIdentity package,
                                           NuGet.Protocol.Core.Types.SourcePackageDependencyInfo parent,
                                           IEnumerable<NuGet.Protocol.Core.Types.SourceRepository> repositories)
            : base(
                  string.Format("Package {0} V{1} dependency of {2} V{3} not found in any of the provided repositories: {4}. See the log for more error details.",
                                 package.Id,
                                 package.Version,
                                 parent.Id,
                                 parent.Version,
                                 String.Join(", ", repositories))
                  )
        {
        }
        public DependencyNotFoundException(NugetDependency package,
                                           NugetPackage parent)
            : base($"The dependency {package} of the packet with id {parent.Id} is not present.")
        {
        }
    }
}
