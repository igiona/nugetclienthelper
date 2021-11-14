using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace NuGetClientHelper
{
    public class Nuspec
    {
        Dictionary<string, List<string>> _files = new Dictionary<string, List<string>>();
        List<NuGetPackage> _dependecies = new List<NuGetPackage>();
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

        public ReadOnlyDictionary<string, List<string>> Elements { get { return new ReadOnlyDictionary<string, List<string>>(_files); } }
        
        public ReadOnlyCollection<NuGetPackage> Dependecies { get { return _dependecies.AsReadOnly(); } }

        public void AddLibraryElements(string framework, string file)
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
                    if (p.Identity.VersionRange.Satisfies(idMatch.Identity.VersionRange.MinVersion)) //The proposed dependecy satisfies the known dependency-package
                    {
                        _dependecies.Remove(idMatch);
                        _dependecies.Add(p);
                    }
                    else
                    {
                        throw new Exception($"Dependecies mismatch found while creating the NuSpec of the package {Id};{Version}. The package with id {p.Identity.Id} is requested with two non-overlapping versions V={p.Identity.VersionRange.PrettyPrint()} and V={idMatch.Identity.VersionRange.PrettyPrint()}");
                    }
                }
            }
        }
    }
}
