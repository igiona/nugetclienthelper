using System;
using System.Collections.Generic;
using System.Text;

namespace NugetHelper.Exceptions
{
    public class InvalidAssemblyPathException : Exception
    {
        public InvalidAssemblyPathException(string msg) : base(msg) { }
    }
}
