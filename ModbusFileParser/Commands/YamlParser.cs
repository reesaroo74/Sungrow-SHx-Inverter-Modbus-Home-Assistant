using System.Dynamic;
using System.IO;
using YamlDotNet.Serialization;

namespace TextParse.Commands
{
    public static class YamlParser
    {
        public static dynamic Parse(string fileIn)
        {
            Deserializer deserializer = new Deserializer();

            return deserializer.Deserialize<ExpandoObject>(File.OpenText(fileIn));
        }
        
    }
}