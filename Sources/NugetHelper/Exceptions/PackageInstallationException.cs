using System;
using System.Collections.Generic;
using System.Text;

namespace NuGetClientHelper.Exceptions
{
    public class PackageInstallationException : Exception
    {
        public PackageInstallationException(NuGetPackage package, Exception innerException)
            : base($"Unable to install package {package} or one of its dependencies. See inner exception for more details", innerException)
        {
        }
    }
}
