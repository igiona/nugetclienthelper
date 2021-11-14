using System;
using System.Collections.Generic;
using System.Text;

namespace NuGetClientHelper.Exceptions
{
    public class InvalidDependencyFoundException : Exception
    {
        public InvalidDependencyFoundException(string msg) : base(msg) { }
    }
}
