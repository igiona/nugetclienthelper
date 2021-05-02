using System;
using System.Collections.Generic;
using System.Text;

namespace NugetHelper.Exceptions
{
    public class PackageInstallationException : Exception
    {
        public PackageInstallationException(NugetPackage package, Exception innerException)
            : base($"Unable to install package {package} or one of its dependencies. See inner exception for more details", innerException)
        {
        }
    }
}
