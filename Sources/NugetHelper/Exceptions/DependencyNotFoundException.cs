using System;
using System.Collections.Generic;
using System.Text;

namespace NugetHelper.Exceptions
{
    public class DependencyNotFoundException : Exception
    {
        public DependencyNotFoundException(string msg) : base(msg) { }
    }
}
