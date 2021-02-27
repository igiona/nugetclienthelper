using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using System.Reflection;
using NuGet.Frameworks;

namespace NugetHelper
{

    public class NuspecGenerator
    {
        #region Templates
        static readonly string _nuspecTemplate = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>{0}</id>
    <!-- Specifying dependencies and version ranges? https://docs.nuget.org/create/versioning#specifying-version-ranges-in-.nuspec-files -->
    <version>{1}</version>
    
    {2}

    <dependencies>
{3}
    </dependencies>
  </metadata>
</package>
";
        static readonly string _dependencyGroupFrameworkTemplate = "     <group targetFramework=\"{0}\">";
        static readonly string _dependencyGroupOpening = "     <group>";
        static readonly string _dependencyGroupClosing = "     </group>";
        static readonly string _dependencyTemplate = "        <dependency id = \"{0}\" version=\"{1}\" />";

        #endregion

        public static void Generate(string outDir, Nuspec spec)
        {
            if (Directory.Exists(outDir))
            {
                Directory.Delete(outDir, true);
            }
            Directory.CreateDirectory(outDir);

            /*
            This approach creates a lib/<framework> as necessary, depending on the target framework of each assembly.
            An issue will arise when referencing suc nuget package via SlnX, because as per today there only one
            targetFramework can be selected on each <package ... /> element.
            Another potential issue is caused by NuGet.exe (see https://docs.microsoft.com/en-us/nuget/reference/nuspec#dependencies-element).
            For each lib/<framework> there should be a 
                <group targetFramework="<framework>"> in the <dependencies> element.
            This code creates such a structure, but due to the nature of the Slnx nuget packages handling
            it cannot know weather a file (DLL) in a specific framwork does reference one of the listed packages.
            It fact, it is well possible that this code would generate invalid dependencies.
            e.g.
                 <group targetFramework="net45"> ---> Caused by a project present in the SlnX (no reference to NUinit here)
                    <dependency id = "NUnit" version="3.13.1" /> ---> Caused by another project in the SlnX
                 <group>
            For this reason, the code will now check for the "highest" compatible framwork, and create the package for that one only

            var dependencies = new StringBuilder();
            foreach (var frameworkItems in spec.Files)
            {
                var frameworkDir = Path.Combine(outDir, "lib", frameworkItems.Key);
                Directory.CreateDirectory(frameworkDir);
                foreach(var f in frameworkItems.Value)
                {
                    File.Copy(f, Path.Combine(frameworkDir, Path.GetFileName(f)), true);
                }

                if (string.IsNullOrEmpty(frameworkItems.Key))
                {
                    dependencies.Append(_dependencyGroupOpening);
                }
                else
                {
                    dependencies.AppendFormat(_dependencyGroupFrameworkTemplate, frameworkItems.Key);
                }
                dependencies.AppendLine();
                foreach (var d in spec.Dependecies)
                {
                    dependencies.AppendFormat(string.Format(_dependencyTemplate, d.Id, d.Version));
                    dependencies.AppendLine();
                }
                dependencies.Append(_dependencyGroupClosing);
                dependencies.AppendLine();
            }
            */

            var frameworkReducer = new FrameworkReducer();
            var highestFramework = frameworkReducer.ReduceUpwards(spec.Files.Select(x => NuGetFramework.Parse(x.Key))).ToList();
            if (highestFramework.Count != 1)
            {
                var required = string.Join(Environment.NewLine, highestFramework);
                throw new Exception($"For the required package {spec.Id} multiple .NET frameworks would be necessary. This is probably caused by a mixture of '.NET Framework' and '.NET Core' based projects.\nThis is not supported.\nRequired frameworks: {required}");
            }
            var highestFrameworkName = highestFramework.Single().GetShortFolderName();

            var packageVersion = spec.Version;

            if (packageVersion == null)
            {
                if (!spec.Files.ContainsKey(highestFrameworkName))
                {
                    throw new Exception($"The .NET version of the required package {spec.Id} has been resolved to {highestFrameworkName}. No file has been found under that framework.");
                }
                var assemblies = spec.Files[highestFrameworkName].Where(x => x.EndsWith(".dll"));
                if (assemblies.Count() == 0)
                {
                    throw new Exception("Error while loading the nuget information. No DLL found from which automatically retrieve the version information.");
                }

                var assemblyPath = assemblies.First();
                if (File.Exists(assemblyPath))
                {
                    var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
                    packageVersion = assemblyName.Version.ToString(3);
                }
                else
                {
                    throw new Exception(string.Format("Unable to automatically retrieve the package version. Assembly {0} not found", assemblyPath));
                }
            }

            foreach (var frameworkItems in spec.Files)
            {
                var frameworkDir = Path.Combine(outDir, "lib", highestFrameworkName);
                Directory.CreateDirectory(frameworkDir);
                foreach (var f in frameworkItems.Value)
                {
                    File.Copy(f, Path.Combine(frameworkDir, Path.GetFileName(f)), true);
                }
            }
            var dependencies = new StringBuilder();

            if (string.IsNullOrEmpty(highestFrameworkName))
            {
                dependencies.Append(_dependencyGroupOpening);
            }
            else
            {
                dependencies.AppendFormat(_dependencyGroupFrameworkTemplate, highestFrameworkName);
            }
            dependencies.AppendLine();
            foreach (var d in spec.Dependecies)
            {
                dependencies.AppendFormat(string.Format(_dependencyTemplate, d.Id, d.VersionRange.OriginalString));
                dependencies.AppendLine();
            }
            dependencies.Append(_dependencyGroupClosing);
            dependencies.AppendLine();

        
            string customElements = null;
            if (spec.AdditionalElements != null)
            {
                var lines = spec.AdditionalElements.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                customElements = string.Join($"{Environment.NewLine}    ", lines);
            }
            var content = string.Format(_nuspecTemplate, spec.Id, packageVersion, customElements, dependencies);
            Utilities.WriteAllText(Path.Combine(outDir, spec.FileName), content);
        }
    }
}
