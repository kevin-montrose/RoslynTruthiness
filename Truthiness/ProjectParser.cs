using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roslyn.Compilers;
using System.Xml.XPath;
using System.IO;
using System.Xml;
using System.Reflection;

namespace Truthiness
{
    // Super cheesy way to parse a cspoj file, which is good enough for demonstration purposes
    class ProjectParser
    {
        private static List<string> GetReferences(XmlReader read)
        {
            var ret = new List<string>();

            while (read.ReadToFollowing("ItemGroup"))
            {
                while (read.ReadToFollowing("Reference"))
                {
                    ret.Add(read.GetAttribute("Include"));
                }
            }

            return ret;
        }

        private static List<string> GetCSharpFiles(XmlReader read)
        {
            var ret = new List<string>();

            while (read.ReadToFollowing("ItemGroup"))
            {
                while (read.ReadToFollowing("Compile"))
                {
                    var csFile = read.GetAttribute("Include");

                    if (csFile.EndsWith(".cs"))
                    {
                        ret.Add(csFile);
                    }
                }
            }

            return ret;
        }

        internal static bool ParseProject(string file, out List<string> csharpFiles, out List<AssemblyFileReference> fileReferences)
        {
            csharpFiles = new List<string>();
            fileReferences = new List<AssemblyFileReference>();

            try
            {
                var referenceNames = new List<string>();

                using(var stream = File.OpenRead(file))
                using (var read = XmlReader.Create(stream))
                {
                    referenceNames = GetReferences(read);
                }

                using (var stream = File.OpenRead(file))
                using (var read = XmlReader.Create(stream))
                {
                    csharpFiles = GetCSharpFiles(read);
                }

                foreach (var referenced in referenceNames)
                {
                    var asm = Assembly.LoadWithPartialName(referenced);
                    fileReferences.Add(new AssemblyFileReference(asm.Location));
                }

                return true;
            }
            catch (Exception)
            {
                csharpFiles = null;
                fileReferences = null;
                return false;
            }
        }
    }
}
