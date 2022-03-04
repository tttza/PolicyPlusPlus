using System;
using System.IO;
using System.Reflection;

namespace PolicyPlus
{
    static class VersionHolder
    {
        public static string Version
        {
            get
            {
                string gitVersion = String.Empty;
                using (Stream stream = Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream("PolicyPlus." + "version.txt"))
                using (StreamReader reader = new StreamReader(stream))
                {
                    gitVersion = reader.ReadToEnd();
                }
                return gitVersion;
            }
        }
    }
}