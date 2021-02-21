using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace NugetHelper
{
    public class Nuspec
    {
        Dictionary<string, List<string>> _files = new Dictionary<string, List<string>>();
        List<NugetPackage> _dependecies = new List<NugetPackage>();
        public Nuspec(string id, string version, string readmeFile, string additionalElements)
        {
            Id = id;
            Version = version;
            ReadmeFile = readmeFile;
            AdditionalElements = additionalElements;
        }

        public string FileName {  get { return string.Format("{0}.nuspec", Id); } }

        public string Id { get; private set; }

        public string Version { get; private set; }

        public string ReadmeFile { get; private set; }

        public string AdditionalElements { get; private set; }

        public ReadOnlyDictionary<string, List<string>> Files { get { return new ReadOnlyDictionary<string, List<string>>(_files); } }
        
        public ReadOnlyCollection<NugetPackage> Dependecies { get { return _dependecies.AsReadOnly(); } }

        public void AddLibraryFile(string framework, string file)
        {
            if (!_files.ContainsKey(framework))
            {
                _files[framework] = new List<string>();
            }
            if (!_files[framework].Contains(file))
            {
                _files[framework].Add(file);
            }
        }

        public void AddDependeciesPacket(NugetPackage p)
        {
            var idMatch = _dependecies.Where(x => x.Id == p.Id).FirstOrDefault();

            if (idMatch == null)
            {
                _dependecies.Add(p);
            }
            else
            {
                if (!idMatch.VersionRange.Satisfies(p.VersionRange.MinVersion)) //The current known dependency-package does not satisfy the proposed dependency
                {
                    if (p.VersionRange.Satisfies(idMatch.VersionRange.MinVersion)) //The proposed dependecy satisfies the known dependency-package
                    {
                        _dependecies.Remove(idMatch);
                        _dependecies.Add(p);
                    }
                    else
                    {
                        throw new Exception($"Dependecies mismatch found while creating the NuSpec of the package {Id};{Version}. The package with id {p.Id} is requested with two non-overlapping versions V={p.VersionRange.PrettyPrint()} and V={idMatch.VersionRange.PrettyPrint()}");
                    }
                }
            }
        }
    }
}
