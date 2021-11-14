using System;
using System.Collections.Generic;
using System.Text;

namespace NuGetClientHelper.Exceptions
{
    public class PackageNotFoundException : Exception
    {
        public PackageNotFoundException(NuGet.Packaging.Core.PackageIdentity package, IEnumerable<NuGet.Protocol.Core.Types.SourceRepository> repositories) 
            : base(
                  string.Format("Package {0} V{1} not found in any of the provided repositories: {2}. See the log for more error details.", 
                                package.Id, 
                                package.Version,
                                String.Join(", ", repositories))
                  )
        {
        }
    }
}
