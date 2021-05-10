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
        public ClassDoc this[int classIndex] => Classes.FirstOrDefault(c => c.Index == classIndex);

        [JsonProperty(PropertyName = "unknown")]
        private long UNK;

        [JsonProperty(PropertyName = "main_classes")]
        public List<ClassDoc> Classes { get; private set; }

        [JsonProperty(PropertyName = "enums")]
        public EnumDoc[] Enums { get; private set; }

        public static EMEDF ReadText(string input)
        {
            return JsonConvert.DeserializeObject<EMEDF>(input);
        }

        public static EMEDF ReadFile(string path)
        {
            string input = File.ReadAllText(path);
            return ReadText(input);
        }

        public static EMEDF ReadStream(string resource)
        {
            string input = Resource.Text(resource);
            return ReadText(input);
        }

        public class ClassDoc
        {
            [JsonProperty(PropertyName = "name")]
            public string Name { get; private set; }

            [JsonProperty(PropertyName = "index")]
            public long Index { get; private set; }

            [JsonProperty(PropertyName = "instrs")]
            public List<InstrDoc> Instructions { get; private set; }

            public InstrDoc this[int instructionIndex] => Instructions.Find(ins => ins.Index == instructionIndex);
        }

        public class InstrDoc
        {
            [JsonProperty(PropertyName = "name")]
            public string Name { get; private set; }

            [JsonProperty(PropertyName = "index")]
            public long Index { get; private set; }

            [JsonProperty(PropertyName = "args")]
            public ArgDoc[] Arguments { get; private set; }

            public ArgDoc this[uint i] => Arguments[i];

            // Calculated values

            public string DisplayName { get; set; }
        }

        public class ArgDoc
        {
            [JsonProperty(PropertyName = "name")]
            public string Name { get; private set; }

            [JsonProperty(PropertyName = "type")]
            public long Type { get; private set; }

            [JsonProperty(PropertyName = "enum_name")]
            public string EnumName { get; private set; }

            [JsonProperty(PropertyName = "default")]
            public long Default { get; private set; }

            [JsonProperty(PropertyName = "min")]
            public long Min { get; private set; }

            [JsonProperty(PropertyName = "max")]
            public long Max { get; private set; }

            [JsonProperty(PropertyName = "increment")]
            public long Increment { get; private set; }

            [JsonProperty(PropertyName = "format_string")]
            public string FormatString { get; private set; }

            [JsonProperty(PropertyName = "unk1")]
            private long UNK1;

            [JsonProperty(PropertyName = "unk2")]
            private long UNK2;

            [JsonProperty(PropertyName = "unk3")]
            private long UNK3;

            [JsonProperty(PropertyName = "unk4")]
            private long UNK4;

            // These fields are not present in the original EMEDF

            // If an argument at the end is optional. Used for reading and writing instructions.
            [JsonProperty(PropertyName = "optional")]
            public bool Optional { get; private set; }

            // If an argument may be repeated zero or multiple times. Only used for display/documentation for the moment.
            [JsonProperty(PropertyName = "vararg")]
            public bool Vararg { get; private set; }

            // Calculated values

            public string DisplayName { get; set; }

            public EnumDoc EnumDoc { get; set; }

            public object GetDisplayValue(object val) => EnumDoc == null ? val : EnumDoc.GetDisplayValue(val);
        }

        public class EnumDoc
        {
            [JsonProperty(PropertyName = "name")]
            public string Name { get; private set; }

            [JsonProperty(PropertyName = "values")]
            public Dictionary<string, string> Values { get; private set; }

            // Calculated values

            public string DisplayName { get; set; }

            public Dictionary<string, string> DisplayValues { get; set; }

            public object GetDisplayValue(object val) => DisplayValues.TryGetValue(val.ToString(), out string reval) ? reval : val;
        }
    }
}
