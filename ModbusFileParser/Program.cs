using System.IO;
using TextParse.Commands;

namespace ModbusFileParser
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            TextParser textParser = new TextParser();
            
            string rawFileName = @"C:\Development\Sungrow\Sungrow-SHx-Inverter-Modbus-Home-Assistant\modbus_sungrow.yaml";
            string newFileName = Path.Combine(Path.GetDirectoryName(rawFileName), Path.ChangeExtension($"{Path.GetFileNameWithoutExtension(rawFileName)}_reversedId", "yaml"));

            textParser.ReverseNameAndId(rawFileName, newFileName);
            
            textParser.ParseModbusFile(newFileName);
        }
    }
}
