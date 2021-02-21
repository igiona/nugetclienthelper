using System;
using System.Collections.Generic;
using System.Text;

namespace NugetHelper.Exceptions
{
    public class TargetFrameworkNotFoundException : Exception
    {
        public TargetFrameworkNotFoundException(string msg) : base(msg) { }
    }
}
