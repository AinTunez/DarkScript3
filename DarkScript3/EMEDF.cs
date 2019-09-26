using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkScript3
{
    public class EMEDF
    {
        public ClassDoc this[int classIndex] => Classes.Find(c => c.Index == classIndex);

        [JsonProperty(PropertyName = "unknown")]
        private long UNK;

        [JsonProperty(PropertyName = "main_classes")]
        public List<ClassDoc> Classes;

        [JsonProperty(PropertyName = "enums")]
        public EnumDoc[] Enums;

        public static EMEDF Read(string path)
        {
            string input = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<EMEDF>(input);
        }

        public static EMEDF ReadStream(string resource)
        {
            resource = "DarkScript3.Resources." + resource;
            using (Stream stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd();
                    return JsonConvert.DeserializeObject<EMEDF>(result); ;
                }
            }
        }

        public class ClassDoc
        {
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "index")]
            public long Index { get; set; }

            [JsonProperty(PropertyName = "instrs")]
            public List<InstrDoc> Instructions { get; set; }

            public InstrDoc this[int instructionIndex] => Instructions.Find(ins => ins.Index == instructionIndex);
        }

        public class InstrDoc
        {
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "index")]
            public long Index { get; set; }

            [JsonProperty(PropertyName = "args")]
            public ArgDoc[] Arguments { get; set; }

            public ArgDoc this[uint i] => Arguments[i];
        }

        public class ArgDoc
        {
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "type")]
            public long Type { get; set; }

            [JsonProperty(PropertyName = "enum_name")]
            public string EnumName { get; set; }

            [JsonProperty(PropertyName = "default")]
            public long Default { get; set; }

            [JsonProperty(PropertyName = "min")]
            public long Min { get; set; }

            [JsonProperty(PropertyName = "max")]
            public long Max { get; set; }

            [JsonProperty(PropertyName = "increment")]
            public long Increment { get; set; }

            [JsonProperty(PropertyName = "format_string")]
            public string FormatString { get; set; }

            [JsonProperty(PropertyName = "unk1")]
            private long UNK1 { get; set; }

            [JsonProperty(PropertyName = "unk2")]
            private long UNK2 { get; set; }

            [JsonProperty(PropertyName = "unk3")]
            private long UNK3 { get; set; }

            [JsonProperty(PropertyName = "unk4")]
            private long UNK4 { get; set; }
        }

        public class EnumDoc
        {
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "values")]
            public Dictionary<string, string> Values { get; set; }
        }
    }
}
