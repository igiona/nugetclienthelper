using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace NuGetClientHelper
{
    public class Nuspec
    {
        Dictionary<string, List<string>> _libraryFiles = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> _genericFiles = new Dictionary<string, List<string>>();
        List<NuGetPackage> _dependecies = new List<NuGetPackage>();

        public Nuspec(string id, string version, string readmeFile, string additionalMetadataElements)
        {
            Id = id;
            Version = version;
            ReadmeFile = readmeFile;
            AdditionalMetadataElements = additionalMetadataElements;
        }

        public string FileName {  get { return string.Format("{0}.nuspec", Id); } }

        public string Id { get; private set; }

        public string Version { get; private set; }

        public string ReadmeFile { get; private set; }

        public string AdditionalMetadataElements { get; private set; }

        public ReadOnlyDictionary<string, List<string>> LibraryElements => new ReadOnlyDictionary<string, List<string>>(_libraryFiles);

        public ReadOnlyDictionary<string, List<string>> GenericElements => new ReadOnlyDictionary<string, List<string>>(_genericFiles);

        public ReadOnlyCollection<NuGetPackage> Dependecies { get { return _dependecies.AsReadOnly(); } }

        /// <summary>
        /// Append the provided sourceFile to the NuGet package under lib/<paramref name="framework"/>
        /// </summary>
        /// <param name="framework">Folder name of the target framework</param>
        /// <param name="sourcePath">Full path to the file/directory to be added</param>
        public void AddLibraryElement(string framework, string sourcePath)
        {
            if (framework == null) throw new ArgumentNullException(nameof(framework));
            if (string.IsNullOrEmpty(sourcePath?.Trim())) throw new ArgumentNullException(nameof(sourcePath));

            if (!_libraryFiles.ContainsKey(framework))
            {
                _libraryFiles[framework] = new List<string>();
            }
            if (!_libraryFiles[framework].Contains(sourcePath))
            {
                _libraryFiles[framework].Add(sourcePath);
            }
        }

        /// <summary>
        /// Append the provided sourceFile to the NuGet package to the targetPath
        /// </summary>
        /// <param name="targetDirectory">Directory relative to the NuGet package root</param>
        /// <param name="sourcePath">Full path to the file/directory to be added</param>
        public void AddGenericFile(string targetDirectory, string sourcePath)
        {
            if (targetDirectory == null) throw new ArgumentNullException(nameof(targetDirectory));
            if (string.IsNullOrEmpty(sourcePath?.Trim())) throw new ArgumentNullException(nameof(sourcePath));

            var canonicalTargetDirectory = targetDirectory.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var canonicalSourceFile = Path.GetFullPath(sourcePath);

            if (!_genericFiles.ContainsKey(canonicalTargetDirectory))
            {
                _genericFiles[canonicalTargetDirectory] = new List<string>();
            }
            if (!_genericFiles[canonicalTargetDirectory].Contains(canonicalSourceFile))
            {
                _genericFiles[canonicalTargetDirectory].Add(canonicalSourceFile);
            }
        }

        public void AddDependeciesPacket(NuGetPackage p)
        {
            var idMatch = _dependecies.Where(x => x.Identity.Id == p.Identity.Id).FirstOrDefault();

            if (idMatch == null)
            {
                _dependecies.Add(p);
            }
            else
            {
                if (!idMatch.Identity.VersionRange.Satisfies(p.Identity.VersionRange.MinVersion)) //The current known dependency-package does not satisfy the proposed dependency
                {
                    if (p.Identity.VersionRange.Satisfies(idMatch.Identity.VersionRange.MinVersion)) //The proposed dependency satisfies the known dependency-package
                    {
                        _dependecies.Remove(idMatch);
                        _dependecies.Add(p);
                    }
                    else
                    {
                        throw new Exception($"Dependencies mismatch found while creating the NuSpec of the package {Id};{Version}. The package with id {p.Identity.Id} is requested with two non-overlapping versions V={p.Identity.VersionRange.PrettyPrint()} and V={idMatch.Identity.VersionRange.PrettyPrint()}");
                    }
                }
            }
        }
    }
}
