using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace NugetHelper
{
    public class ConfigGenerator
    {
        #region Templates
        static readonly string _configTemplate = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>{0}
  </packageSources>
</configuration>";
        static readonly string _sourceTemplate = "{0}<add key=\"{1}\" value=\"{2}\" />";
        #endregion

        const string NugetConfigName = "nuget.config";
        public static void Generate(string outDir, IEnumerable<NugetPackage> packages)
        {
            int local = 0;
            var sources = new StringBuilder();
            if (packages.Count() > 0)
            {
                var sourceList = new List<string>();
                foreach(var p in packages)
                {
                    var src = p.Source.ToString();
                    if (!sourceList.Contains(src))
                    {
                        sourceList.Add(src);
                        sources.AppendFormat(string.Format(_sourceTemplate, $"{Environment.NewLine}    ", GetHost(p.Source, ref local), src));
                    }
                }
            }
            if (sources.Length > 0)
            {
                var content = string.Format(_configTemplate, sources.ToString());
                Utilities.WriteAllText(Path.Combine(outDir, NugetConfigName), content);
            }
        }

        private static string GetHost(Uri source, ref int i)
        {
            if (string.IsNullOrEmpty(source.Host))
            {
                return string.Format("Local.{0}", i++);
            }
            return source.Host;
        }
    }
}
