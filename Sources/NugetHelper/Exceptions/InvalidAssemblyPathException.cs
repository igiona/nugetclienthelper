using System;
using System.Collections.Generic;
using System.Text;

namespace NuGetClientHelper.Exceptions
{
    public class InvalidAssemblyPathException : Exception
    {
        public InvalidAssemblyPathException(string msg) : base(msg) { }
    }
}
