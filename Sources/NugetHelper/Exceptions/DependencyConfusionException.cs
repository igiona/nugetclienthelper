using System;
using System.Collections.Generic;
using System.Text;

namespace NugetHelper.Exceptions
{
    public class DependencyConfusionException : Exception
    {
        public DependencyConfusionException(string msg) : base(msg) { }
    }
}
