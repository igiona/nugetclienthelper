using System;
using System.Collections.Generic;
using System.Text;

namespace NuGetClientHelper.Exceptions
{
    public class TargetFrameworkNotFoundException : Exception
    {
        public TargetFrameworkNotFoundException(string msg) : base(msg) { }
    }
}
