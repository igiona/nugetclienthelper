using System;
using System.Collections.Generic;
using System.Text;

namespace NugetHelper.Exceptions
{
    public class InvalidDependencyFoundException : Exception
    {
        public InvalidDependencyFoundException(string msg) : base(msg) { }
    }
}
