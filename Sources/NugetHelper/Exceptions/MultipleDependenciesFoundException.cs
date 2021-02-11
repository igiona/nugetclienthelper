using System;
using System.Collections.Generic;
using System.Text;

namespace NugetHelper.Exceptions
{
    public class MultipleDependenciesFoundException : Exception
    {
        public MultipleDependenciesFoundException(string msg) : base(msg) { }
    }
}
