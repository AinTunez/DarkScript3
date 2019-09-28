using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkScript3
{
    static class Resource
    {
        public static string Text(string s)
        {
            using (Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("DarkScript3.Resources." + s))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd();
                    return result;
                }
            }
        }
    }
}
