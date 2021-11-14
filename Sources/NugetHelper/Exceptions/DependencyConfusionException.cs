using System;
using System.Collections.Generic;
using System.Text;

namespace NuGetClientHelper.Exceptions
{
    public class DependencyConfusionException : Exception
    {
        public DependencyConfusionException(string msg) : base(msg) { }
    }
}
