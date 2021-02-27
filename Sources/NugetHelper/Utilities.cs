using System;
using System.Collections.Generic;
using System.IO;

namespace NugetHelper
{
    static class Utilities
    {
        /// <summary>
        /// File.WriteAllText writes to the filesystem cache.
        /// The file is written to the disk by the OS at a later stage.
        /// This can be a problem, if the generated output is required by some other SW.
        /// This method makes use of the StreamWriter, which behaves differently.
        /// </summary>
        public static void WriteAllText(string path, string text)
        {
            using (var f = new StreamWriter(path))
            {
                f.Write(text);
            }
        }
    }
}
