using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace TextParse.Commands
{
    public class TextParser
    {
        private const string AliasPrefix = "alias:";
        private const string AutomaticIdPrefix = "id:";
        private const string AutomationIdNamePrefix = "automation_sungrow_";
        private const string ModbusHostIp = "sungrow_modbus_host_ip";

        private const string ModbusName = "SungrowSHx";
        private const string ModbusPort = "sungrow_modbus_port";
        private const string ModbusSlave = "sungrow_modbus_slave";

        private const string NamePrefix = "name:";

        private const string SungrowIdPrefix = "sg_";
        private const string SungrowSetIdPrefix = "set_sg_";
        private const string UniqueIdPrefix = "unique_id:";

        private const string UnitIdentifier = "sgunit";

        private static readonly List<string> _directConverts = new List<string>
        {
            ModbusName,
            ModbusHostIp,
            ModbusPort,
            ModbusSlave
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

        private string FixName(string line, string currentId, YamlReader yamlReader)
        {
            long currentPosition = yamlReader.Position;

            if (string.IsNullOrEmpty(currentId))
            {
                string nextLine = yamlReader.ReadLine();

                while (nextLine != null)
                {
                    if (nextLine.Trim().StartsWith($"- {UniqueIdPrefix}"))
                    {
                        currentId = GetId(line);
                    }
                    else if (line.Trim().StartsWith($"- {AutomaticIdPrefix}"))
                    {
                        currentId = GetAutomationId(line);
                    }
                    else if (line.Trim().StartsWith("- "))
                    {
                        yamlReader.Seek(currentPosition, SeekOrigin.Begin);

                        return line;
                    }

                    nextLine = yamlReader.ReadLine();
                }

                // reset to the previous byte position
                yamlReader.Seek(currentPosition, SeekOrigin.Begin);
            }

            if (string.IsNullOrEmpty(currentId))
            {
                yamlReader.Seek(currentPosition, SeekOrigin.Begin);

                return line;
            }

            string prefix;

            if (line.Trim().StartsWith(NamePrefix) || line.Trim().StartsWith($"- {NamePrefix}"))
            {
                prefix = NamePrefix;
            }
            else if (line.Trim().StartsWith(AliasPrefix) || line.Trim().StartsWith($"- {AliasPrefix}"))
            {
                prefix = AliasPrefix;
            }
            else
            {
                return line;
            }

            string[] split = line.Split(new[] {prefix}, StringSplitOptions.None);

            string newName = currentId.Replace("_", " ");

            if (newName.StartsWith("automation "))
            {
                newName = newName.Substring("automation ".Length);
            }

            TextInfo textInfo = new CultureInfo("en-AU", false).TextInfo;

            newName = textInfo.ToTitleCase(newName);

            return $"{split[0]}{prefix} {newName}";
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
                string newString = $"automation_{oldString}_{UnitIdentifier}{index}";

                return newString;
            }

            return $"{id}_{index}";
        }

        private string GetNewId(string id, int index, Sections section)
        {
            // test custom replacements
            if (_replacements.TryGetValue(section, out Dictionary<string, string> replacements))
            {
                if (replacements.TryGetValue(id, out string replacement))
                {
                    return replacement;
                }
            }

            if (id.StartsWith(SungrowIdPrefix))
            {
                string oldString = id.Substring(3).Trim();
                string newString = $"{oldString}_{UnitIdentifier}{index}";
                AddReplacement(oldString, newString, section);

                return newString;
            }

            return $"{id}_{index}";
        }

        private static string GetNewSetId(string id, int index)
        {
            return $"set_{id.Substring(SungrowSetIdPrefix.Length)}_{UnitIdentifier}{index}";
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

        public void ModbusChangeNameAndId(string fileIn, string fileOut, string nameToReplace, string replacementName, string idToReplace, string replacementId)
        {
            using (YamlReader yamlReader = new YamlReader(fileIn))
            {
                using (StreamWriter streamWriter = new StreamWriter(fileOut))
                {
                    string line = yamlReader.ReadLine();

                    while (line != null)
                    {
                        line = line.Replace(nameToReplace, replacementName).Replace(idToReplace, replacementId);

                        streamWriter.WriteLine(line);

                        line = yamlReader.ReadLine();
                    }
                }
            }
        }

        public void ModbusFileFixNames(string fileIn)
        {
            string newFileName = Path.GetTempFileName();

            using (YamlReader yamlReader = new YamlReader(fileIn))
            {
                string currentId = string.Empty;

                using (StreamWriter streamWriter = new StreamWriter(newFileName))
                {
                    string line = yamlReader.ReadLine();

                    while (line != null)
                    {
                        if (line.Trim().StartsWith($"- {UniqueIdPrefix}"))
                        {
                            currentId = GetId(line);
                        }
                        else if (line.Trim().StartsWith($"- {AutomaticIdPrefix}"))
                        {
                            currentId = GetAutomationId(line);
                        }
                        else if (line.Trim().StartsWith("- "))
                        {
                            currentId = string.Empty;
                        }

                        if (line.Trim().StartsWith(NamePrefix) || line.Trim().StartsWith($"- {NamePrefix}") || line.Trim().StartsWith(AliasPrefix) || line.Trim().StartsWith($"- {AliasPrefix}"))
                        {
                            line = FixName(line, currentId, yamlReader);
                        }

                        streamWriter.WriteLine(line);

                        line = yamlReader.ReadLine();
                    }
                }
            }

            if (File.Exists(newFileName))
            {
                File.Delete(fileIn);
                File.Copy(newFileName, fileIn);
            }
        }

        public void ModbusFileParse(string fileIn)
        {
            Sections currentSection = Sections.None;

            const int numberOfFiles = 2;

            for (int index = 1; index <= numberOfFiles; index++)
            {
                // reset the replacements

                Dictionary<string, string> modbusCustomSar = new Dictionary<string, string>
                {
                    {"sungrow_device_type_code", $"dev_code_{UnitIdentifier}{index}"},
                    {"export_power_raw", $"battery_export_power_raw_{UnitIdentifier}{index}"}
                };

                Dictionary<string, string> templateSensorCustomSar = new Dictionary<string, string>
                {
                    {"ems_mode_selection", $"ems_mode_selection_{UnitIdentifier}{index}"},
                    {"battery_forced_charge_discharge_cmd", $"battery_forced_charge_discharge_cmd_raw_{UnitIdentifier}{index}"},
                    {"sg_battery_level_nom", $"battery_level_nominal_{UnitIdentifier}{index}"},
                    {"battery_level_nominal", $"battery_level_nominal_{UnitIdentifier}{index}"},
                    {"sg_battery_charge_nom", $"battery_charge_nominal_{UnitIdentifier}{index}"},
                    {"uid_daily_consumed_energy", $"daily_consumed_energy_{UnitIdentifier}{index}"},
                    {"uid_total_consumed_energy", $"total_consumed_energy_{UnitIdentifier}{index}"},
                    {"export_power_limit_mode", $"export_power_limit_mode_{UnitIdentifier}{index}"},
                    {"sungrow_inverter_state", $"inverter_state_{UnitIdentifier}{index}"},
                };

                Dictionary<string, string> templateBinarySensorCustomSar = new Dictionary<string, string>
                {
                    {"sg_inverter_state", $"inverter_state_{UnitIdentifier}{index}"},
                };

                _replacements = new Dictionary<Sections, Dictionary<string, string>>
                {
                    {Sections.Modbus, modbusCustomSar},
                    {Sections.TemplateSensor, templateSensorCustomSar},
                    {Sections.TemplateBinarySensor, templateBinarySensorCustomSar},
                };

                string newFileName = Path.Combine(Path.GetDirectoryName(fileIn), Path.ChangeExtension($"modbus_sungrow_{index}", "yaml"));

                using (YamlReader yamlReader = new YamlReader(fileIn))
                {
                    using (StreamWriter streamWriter = new StreamWriter(newFileName))
                    {
                        string line = yamlReader.ReadLine();

                        while (line != null)
                        {
                            Sections section = TestForNewSection(currentSection, line);

                            if (section != Sections.None)
                            {
                                currentSection = section;
                            }

                            line = ParseLine(line, index, currentSection);

                            streamWriter.WriteLine(line);

                            line = yamlReader.ReadLine();
                        }
                    }
                }

                ModbusFileFixNames(newFileName);
            }
        }

        public bool ModbusFileReverseNameAndId(string fileIn, string fileOut)
        {
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
                            if (line1.Trim().StartsWith($"- {NamePrefix}"))
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

        private string ParseLine(string line, int index, Sections section)
        {
            if (line == "          - sensor.sungrow_inverter_state")
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
                    if (line.Trim().StartsWith($"- {AutomaticIdPrefix} \"{AutomationIdNamePrefix}") || line.Trim().StartsWith($"- {AutomaticIdPrefix} {AutomationIdNamePrefix}"))
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

        private string ReplaceAndAddToReplacements(string line, Sections section, string oldString, string newString)
        {
            AddReplacement(oldString, newString, section);

            return line.Replace(oldString, newString);
        }

        private static Sections TestForNewSection(Sections currentSection, string line)
        {
            foreach (Sections section in EnumHelper<Sections>.GetValues().Where(s => s != Sections.None))
            {
                string header = GetSectionHeader(section);

                switch (section)
                {
                    case Sections.TemplateBinarySensor:
                        if (currentSection == Sections.Template)
                        {
                            if (line.StartsWith(header))
                            {
                                return section;
                            }
                        }

                        break;
                    case Sections.TemplateSensor:
                        if (currentSection == Sections.TemplateBinarySensor)
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