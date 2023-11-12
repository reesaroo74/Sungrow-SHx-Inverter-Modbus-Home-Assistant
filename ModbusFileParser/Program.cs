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

            textParser.ModbusFileReverseNameAndId(rawFileName, newFileName);

            textParser.ModbusFileParse(newFileName);

            textParser.ModbusChangeNameAndId(@"C:\Development\Sungrow\Sungrow-SHx-Inverter-Modbus-Home-Assistant\modbus_sungrow_1.yaml",
                @"C:\Development\Sungrow\Sungrow-SHx-Inverter-Modbus-Home-Assistant\modbus_sungrow_garage.yaml",
                "Sgunit1",
                "Garage",
                "sgunit1",
                "garage");

            textParser.ModbusChangeNameAndId(@"C:\Development\Sungrow\Sungrow-SHx-Inverter-Modbus-Home-Assistant\modbus_sungrow_2.yaml",
                @"C:\Development\Sungrow\Sungrow-SHx-Inverter-Modbus-Home-Assistant\modbus_sungrow_shed.yaml",
                "Sgunit2",
                "Shed",
                "sgunit2",
                "shed");
        }
    }
}