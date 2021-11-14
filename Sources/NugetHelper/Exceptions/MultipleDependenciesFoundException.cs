using System;
using System.Collections.Generic;
using System.Text;

namespace NuGetClientHelper.Exceptions
{
    public class MultipleDependenciesFoundException : Exception
    {
        public MultipleDependenciesFoundException(string msg) : base(msg) { }
    }
}
