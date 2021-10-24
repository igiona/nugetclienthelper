using System;
using System.Collections.Generic;
using System.Text;

namespace NuGetClientHelper.Exceptions
{
    public class InvalidMinVersionDependencyFoundException : Exception
    {
        public InvalidMinVersionDependencyFoundException(string msg) : base(msg) { }
    }
}
