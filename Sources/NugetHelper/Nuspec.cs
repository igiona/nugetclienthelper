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
        public Nuspec(string id, Version version, string readmeFile, string additionalElements)
        {
            Id = id;
            Version = version;
            ReadmeFile = readmeFile;
            AdditionalElements = additionalElements;
        }

        public string FileName {  get { return string.Format("{0}.nuspec", Id); } }

        public string Id { get; private set; }

        public Version Version { get; private set; }

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
            else if (idMatch.Version != p.Version)
            {
                throw new Exception($"Dependecies mismatch found for the NuGet packet {Id};{Version} a dependecies. The dependencies to the package {p.Id} is set at least for V={p.Version} and V={idMatch.Version}");
            }
        }
    }
}
