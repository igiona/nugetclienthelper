using System;
using System.Collections.Generic;
using System.Text;

namespace NuGetClientHelper
{
    public enum NuGetPackageType
    {
        /// <summary>
        /// To be used for packages that follow the NuGet package conventions.
        /// <see href="https://docs.microsoft.com/en-us/nuget/create-packages/creating-a-package#from-a-convention-based-working-directory" />
        /// </summary>
        DotNet,

        /// <summary>
        /// For all packages that do not follow the official conventions.
        /// <see href="https://docs.microsoft.com/en-us/nuget/create-packages/creating-a-package#from-a-convention-based-working-directory" />
        /// </summary>
        Custom
    }
}
