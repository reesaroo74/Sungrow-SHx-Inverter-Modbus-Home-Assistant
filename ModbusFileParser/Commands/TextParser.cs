using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TextParse.Commands
{
    public class TextParser
    {
        private const string AutomaticIdPrefix = "- id:";
        private const string AutomationIdNamePrefix = "automation_sungrow_";
        private const string ModbusHostIp = "sungrow_modbus_host_ip";

        private const string ModbusName = "SungrowSHx";
        private const string ModbusPort = "sungrow_modbus_port";
        private const string ModbusSlave = "sungrow_modbus_slave";

        private const string NamePrefix = "- name:";
        private const string SungrowIdPrefix = "sg_";
        private const string SungrowSetIdPrefix = "set_sg_";
        private const string UniqueIdPrefix = "unique_id:";

        private static readonly List<string> _directConverts = new List<string>
        {
            ModbusName,
            ModbusHostIp,
            ModbusPort,
            ModbusSlave
        };

        private static readonly List<string> _addSpaceIndexAtEndAfterPrefix = new List<string>
        {
            NamePrefix
        };

        private Dictionary<Sections, Dictionary<string, string>> _replacements;

        private void AddReplacement(string oldString, string newString, Sections section)
        {
            if (!_replacements.TryGetValue(section, out Dictionary<string, string> sectionReplacements))
            {
                sectionReplacements = new Dictionary<string, string>();
            }

            if (!sectionReplacements.ContainsKey(oldString))
            {
                sectionReplacements.Add(oldString, newString);
                _replacements[section] = sectionReplacements;
            }
        }

        private string GetAutomationId(string line)
        {
            string[] split = line.Split(new[] {AutomaticIdPrefix}, StringSplitOptions.None);
            
            return split.Length > 1 ? split[1].Trim().Trim('"') : string.Empty;
        }

        private static string GetId(string line)
        {
            string[] split = line.Split(new[] {UniqueIdPrefix}, StringSplitOptions.None);

            return split.Length > 1 ? split[1].Trim() : string.Empty;
        }

        private static string GetNewAutomationId(string id, int index)
        {
            if (id.StartsWith(AutomationIdNamePrefix))
            {
                string oldString = id.Substring(19).Trim();
                string newString = $"automation_{oldString}_sg_{index}";

                return newString;
            }

            return $"{id}_{index}";
        }

        private string GetNewId(string id, int index, Sections section)
        {
            if (id.StartsWith(SungrowIdPrefix))
            {
                string oldString = id.Substring(3).Trim();
                string newString = $"{oldString}_sg_{index}";
                AddReplacement(oldString, newString, section);

                return newString;
            }

            return $"{id}_{index}";
        }

        private static string GetNewSetId(string id, int index)
        {
            return $"set_{id.Substring(SungrowSetIdPrefix.Length)}_sg_{index}";
        }

        private static string GetSectionHeader(Sections section)
        {
            switch (section)
            {
                case Sections.Modbus:
                    return "modbus:";

                case Sections.Template:
                    return "template:";
                case Sections.TemplateBinarySensor:
                    return "  - binary_sensor:";
                case Sections.TemplateSensor:
                    return "  - sensor:";
                case Sections.InputNumber:
                    return "input_number:";
                case Sections.InputSelect:
                    return "input_select:";

                case Sections.Automation:
                    return "automation";
                case Sections.None:
                default:
                    throw new ArgumentOutOfRangeException(nameof(section), section, null);
            }
        }

        private string ParseLine(string line, int index, Sections section)
        {
            if (line == "        value_template: \"{{ not is_state('sensor.ems_mode_selection', 'unavailable') }}\"")
            {
                int i = 0;
                i++;
            }

            if (section == Sections.None)
            {
                return line;
            }

            string directConvert = TestLineContainsDirectConvert(line);

            if (!string.IsNullOrWhiteSpace(directConvert))
            {
                line = ReplaceAndAddToReplacements(line, section, directConvert, $"{directConvert}_{index}");
            }

            foreach (string prefix in _addSpaceIndexAtEndAfterPrefix)
            {
                // also test that this line doesn't include a direct convert!
                if (line.Trim().StartsWith(prefix) && string.IsNullOrWhiteSpace(TestLineContainsDirectConvert(line)))
                {
                    line = line.TrimEnd() + $" {index}";
                }
            }

            if (line.Trim().StartsWith(UniqueIdPrefix) || line.Trim().StartsWith($"- {UniqueIdPrefix}"))
            {
                string id = GetId(line);

                string newId = GetNewId(id, index, section);

                line = ReplaceAndAddToReplacements(line, section, id, newId);
            }

            switch (section)
            {
                case Sections.Modbus:
                    // should be no replacements
                    break;
                case Sections.Template:
                case Sections.TemplateBinarySensor:
                case Sections.TemplateSensor:
                    line = TestReplacements(line, Sections.Modbus | Sections.TemplateSensor | Sections.TemplateBinarySensor);

                    break;
                case Sections.InputNumber:
                case Sections.InputSelect:
                    if (line.StartsWith($"  {SungrowSetIdPrefix}"))
                    {
                        string id = line.Trim().TrimEnd(':');

                        string newId = GetNewSetId(id, index);

                        line = ReplaceAndAddToReplacements(line, section, id, newId);
                    }
                    else
                    {
                        line = TestReplacements(line, Sections.Modbus | Sections.TemplateSensor | Sections.TemplateBinarySensor | Sections.InputNumber);
                    }

                    break;
                case Sections.Automation:
                    if (line.Trim().StartsWith($"{AutomaticIdPrefix} \"{AutomationIdNamePrefix}") || line.Trim().StartsWith($"{AutomaticIdPrefix} {AutomationIdNamePrefix}"))
                    {
                        string id = GetAutomationId(line);
                        string newId = GetNewAutomationId(id, index);

                        line = ReplaceAndAddToReplacements(line, section, id, newId);
                    }
                    else
                    {
                        line = TestReplacements(line, Sections.Modbus | Sections.TemplateSensor | Sections.TemplateBinarySensor | Sections.InputNumber | Sections.InputSelect | Sections.Automation);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(section), section, null);
            }

            return line;
        }

        public void ParseModbusFile(string fileIn)
        {
            Sections currentSection = Sections.None;

            const int numberOfFiles = 2;

            for (int index = 1; index <= numberOfFiles; index++)
            {
                // reset the replacements

                Dictionary<string, string> modbusCustomSar = new Dictionary<string, string>
                {
                    {"sungrow_device_type_code", $"dev_code_sg_{index}"},
                    {"export_power_raw", $"battery_export_power_raw_sg_{index}"}
                };

                Dictionary<string, string> templateSensorCustomSar = new Dictionary<string, string>
                {
                        {"ems_mode_selection", $"ems_mode_selection_sg_{index}"},
                    {"battery_forced_charge_discharge_cmd", $"battery_forced_charge_discharge_cmd_raw_sg_{index}"},
                };

                _replacements = new Dictionary<Sections, Dictionary<string, string>>
                {
                    {Sections.Modbus, modbusCustomSar},
                    {Sections.TemplateSensor, templateSensorCustomSar},
                };

                string newFileName = Path.Combine(Path.GetDirectoryName(fileIn), Path.ChangeExtension($"modbus_sungrow_{index}", "yaml"));

                using (StreamReader streamReader = new StreamReader(fileIn))
                {
                    using (StreamWriter streamWriter = new StreamWriter(newFileName))
                    {
                        string line = streamReader.ReadLine();

                        while (line != null)
                        {
                            Sections section = TestForNewSection(currentSection, line);

                            if (section != Sections.None)
                            {
                                currentSection = section;
                            }

                            string newLine = ParseLine(line, index, currentSection);
                            streamWriter.WriteLine(newLine);

                            line = streamReader.ReadLine();
                        }
                    }
                }
            }
        }

        private string ReplaceAndAddToReplacements(string line, Sections section, string oldString, string newString)
        {
            AddReplacement(oldString, newString, section);

            return line.Replace(oldString, newString);
        }

        public bool ReverseNameAndId(string fileIn, string fileOut)
        {
            const string namePrefix = "name: ";
            const string uniqueIdPrefix = "unique_id: ";
            const string deviceAddressPrefix = "device_address: ";

            try
            {
                using (StreamReader streamReader = new StreamReader(fileIn))
                {
                    using (StreamWriter streamWriter = new StreamWriter(fileOut))
                    {
                        string line1 = streamReader.ReadLine();

                        while (line1 != null)
                        {
                            if (line1.Trim().StartsWith($"- {namePrefix}"))
                            {
                                string line2 = streamReader.ReadLine();

                                if (line2 == null)
                                {
                                    streamWriter.WriteLine(line1);

                                    break;
                                }

                                if (line2.Trim().StartsWith(uniqueIdPrefix))
                                {
                                    int indexOf = line1.IndexOf("-", StringComparison.Ordinal);
                                    string spaces = line1.Substring(0, indexOf);
                                    string id = $"{spaces}- {line2.Trim()}";
                                    string name = $"{spaces}  {line1.Substring(indexOf + 1).Trim()}";

                                    streamWriter.WriteLine(id);
                                    streamWriter.WriteLine(name);
                                }
                                else if (line2.Trim().StartsWith(deviceAddressPrefix))
                                {
                                    string line3 = streamReader.ReadLine();

                                    if (line3 == null)
                                    {
                                        streamWriter.WriteLine(line1);
                                        streamWriter.WriteLine(line2);

                                        break;
                                    }

                                    if (line3.Trim().StartsWith(uniqueIdPrefix))
                                    {
                                        int indexOf = line1.IndexOf("-", StringComparison.Ordinal);
                                        string spaces = line1.Substring(0, indexOf);
                                        string id = $"{spaces}- {line3.Trim()}";
                                        string name = $"{spaces}  {line1.Substring(indexOf + 1).Trim()}";

                                        streamWriter.WriteLine(id);
                                        streamWriter.WriteLine(name);
                                        streamWriter.WriteLine(line2);
                                    }
                                    else
                                    {
                                        streamWriter.WriteLine(line1);
                                        streamWriter.WriteLine(line2);
                                        streamWriter.WriteLine(line3);
                                    }
                                }
                                else
                                {
                                    streamWriter.WriteLine(line1);
                                    streamWriter.WriteLine(line2);
                                }
                            }
                            else
                            {
                                streamWriter.WriteLine(line1);
                            }

                            line1 = streamReader.ReadLine();
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Sections TestForNewSection(Sections currentSection, string line)
        {
            foreach (Sections section in EnumHelper<Sections>.GetValues().Where(s => s != Sections.None))
            {
                string header = GetSectionHeader(section);

                switch (section)
                {
                    case Sections.TemplateBinarySensor:
                    case Sections.TemplateSensor:
                        if (currentSection == Sections.Template)
                        {
                            if (line.StartsWith(header))
                            {
                                return section;
                            }
                        }

                        break;
                    default:
                        if (line.StartsWith(header))
                        {
                            return section;
                        }

                        break;
                }
            }

            return Sections.None;
        }

        private static string TestLineContainsDirectConvert(string line)
        {
            foreach (string directConvert in _directConverts)
            {
                if (line.Contains(directConvert))
                {
                    return directConvert;
                }
            }

            return string.Empty;
        }

        private string TestReplacements(string line, Sections sectionsToTest)
        {
            foreach (Sections section in EnumHelper<Sections>.GetValues())
            {
                if (sectionsToTest.HasFlag(section))
                {
                    if (_replacements.TryGetValue(section, out Dictionary<string, string> sectionReplacements))
                    {
                        string prefix = string.Empty;

                        switch (section)
                        {
                            case Sections.Modbus:
                                prefix = "sensor.";

                                break;
                            case Sections.TemplateBinarySensor:
                                prefix = "binary_sensor.";

                                break;
                            case Sections.TemplateSensor:
                                prefix = "sensor.";

                                break;
                            case Sections.InputNumber:
                                prefix = "input_number.";

                                break;
                            case Sections.InputSelect:
                                prefix = "input_select.";

                                break;
                            case Sections.Automation:
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        foreach (var kvp in sectionReplacements)
                        {
                            line = line.Replace($" {kvp.Key} ", $" {kvp.Value} ");

                            line = line.Replace($"'{prefix}{kvp.Key}'", $"'{prefix}{kvp.Value}'"); // eg {% if ((states('sensor.export_power_limit_mode_raw') | int(default=0)) == 0x00AA) %}

                            switch (section)
                            {
                                case Sections.Modbus:
                                case Sections.TemplateBinarySensor:
                                case Sections.TemplateSensor:
                                case Sections.InputNumber:
                                case Sections.InputSelect:
                                    // test for no quotes with nothing but white space afterwards (in automations)
                                    if (line.Split(new[] {$"{prefix}{kvp.Key}"}, StringSplitOptions.None).Last().Trim().Length == 0)
                                    {
                                        line = line.Replace($"{prefix}{kvp.Key}", $"{prefix}{kvp.Value}");
                                    }

                                    break;
                            }
                        }
                    }
                }
            }

            return line;
        }

        [Flags]
        private enum Sections
        {
            None = 0,
            Modbus = 1,
            Template = 2,
            TemplateBinarySensor = 4,
            TemplateSensor = 8,
            InputNumber = 16,
            InputSelect = 32,
            Automation = 64
        }
    }
}