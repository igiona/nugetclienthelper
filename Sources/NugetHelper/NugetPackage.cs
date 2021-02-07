using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetHelper
{
    public class NugetPackage
    {
        public NugetPackage(string id, string version, string targetFramework, string source, string var, bool isDontNetLib, string packagesRoot)
        {
            Id = id;
            Version = new Version(System.Environment.ExpandEnvironmentVariables(version));
            if (!string.IsNullOrEmpty(targetFramework))
            {
                TargetFramework = System.Environment.ExpandEnvironmentVariables(targetFramework);
            }
            else
            {
                TargetFramework = "";
            }

            if (string.IsNullOrEmpty(source))
            {
                throw new Exception(string.Format("Invalid source for the package id {0};{1}. The source parameter is mandatory.", Id, Version));
            }
            Source = new Uri(System.Environment.ExpandEnvironmentVariables(source));
            
            RootPath = packagesRoot;
            FullPath = Path.Combine(RootPath, string.Format("{0}.{1}", Id, Version));
            IsDontNetLib = isDontNetLib;

            if (IsDontNetLib)
            {
                FullPath = Path.Combine(FullPath, "lib");
            }
            if (!string.IsNullOrEmpty(TargetFramework))
            {
                FullPath = Path.Combine(FullPath, TargetFramework);
            }
            EnvironmentVariableKey = EscapeStringAsEnvironmentVariableAsKey(Id);
            //Alaways set the "default" key value
            Environment.SetEnvironmentVariable(EnvironmentVariableKey, FullPath);

            if (!string.IsNullOrEmpty(var)) //If requested, set also the user specified value
            {
                EnvironmentVariableAdditionalKey = var;
                if (Environment.GetEnvironmentVariable(EnvironmentVariableAdditionalKey) == null)
                    Environment.SetEnvironmentVariable(EnvironmentVariableAdditionalKey, FullPath);
            }
        }

        public string Id { get; private set; }

        public Version Version { get; private set; }

        public string TargetFramework { get; private set; }

        public Uri Source { get; private set; }

        public string EnvironmentVariableKey { get; private set; }

        public string EnvironmentVariableAdditionalKey { get; private set; }

        public string RootPath { get; private set; }

        public string FullPath { get; private set; }

        public bool IsDontNetLib { get; private set; }

        public static string EscapeStringAsEnvironmentVariableAsKey(string key)
        {
            return key.Replace(".", "_"); //.Replace("-", "_");
        }

        public override string ToString()
        {
            return string.Format("{0} V{1}", Id, Version);
        }
    }
}
