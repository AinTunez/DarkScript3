using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DarkScript3
{
    static class Resource
    {
        public static string Text(string s)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DarkScript3.Resources." + s))
            {
                if (stream == null)
                {
                    string[] allNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();
                    throw new Exception($"No resource named {s} exists (found {string.Join(", ", allNames)})");
                }
                using (StreamReader reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd();
                    return result;
                }
            }
        }
    }
}
