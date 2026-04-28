using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace CSArp.Logic.Utilities
{
    public static class ApplicationSettings
    {
        private const string MajorDelimiter = "--------------------------------------------------------------";
        private const char MinorDelimiter = '$';
        private const string SettingsFileName = "dznetcut.config";
        private const string ShowLogPrefix = "show_log=";
        private static readonly string SettingsFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            SettingsFileName);

        public static string GetSavedClientNameFromMAC(string clientMacAddress)
        {
            if (string.IsNullOrWhiteSpace(clientMacAddress) || !File.Exists(SettingsFilePath))
            {
                return string.Empty;
            }

            try
            {
                var entries = GetMacToClientNameDictionary(File.ReadAllText(SettingsFilePath));
                return entries.TryGetValue(clientMacAddress, out var clientName) ? clientName : string.Empty;
            }
            catch (Exception ex)
            {
                Debug.Print("Exception in ApplicationSettings.GetSavedClientNameFromMAC\n" + ex.Message);
                return string.Empty;
            }
        }

        public static string? GetSavedPreferredInterfaceFriendlyName() => GetSettingsMetadata().InterfaceFriendlyName;

        public static bool? GetSavedShowLog() => GetSettingsMetadata().ShowLog;

        public static bool SaveSettings(ListView listView, string interfaceFriendlyName, bool showLog)
        {
            try
            {
                var entries = File.Exists(SettingsFilePath)
                    ? GetMacToClientNameDictionary(File.ReadAllText(SettingsFilePath))
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (ListViewItem item in listView.Items)
                {
                    var macAddress = item.SubItems[1].Text;
                    var clientName = item.SubItems[3].Text;
                    if (!string.IsNullOrWhiteSpace(clientName))
                    {
                        entries[macAddress] = clientName;
                    }
                }

                WriteToFile(interfaceFriendlyName, showLog, entries, SettingsFilePath);
                return true;
            }
            catch (Exception ex)
            {
                Debug.Print("Exception at ApplicationSettings.SaveSettings()\n" + ex.Message);
                return false;
            }
        }

        private static Dictionary<string, string> GetMacToClientNameDictionary(string settingsFileContents)
        {
            var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(settingsFileContents))
            {
                return entries;
            }

            try
            {
                var sections = settingsFileContents.Split(new[] { MajorDelimiter }, StringSplitOptions.None);
                if (sections.Length < 2)
                {
                    return entries;
                }

                var lines = sections[1].Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var separatorIndex = line.IndexOf(MinorDelimiter);
                    if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
                    {
                        continue;
                    }

                    var macAddress = line.Substring(0, separatorIndex);
                    var clientName = line.Substring(separatorIndex + 1);
                    entries[macAddress] = clientName;
                }
            }
            catch (Exception ex)
            {
                Debug.Print("Exception in ApplicationSettings.GetMacToClientNameDictionary()\n" + ex.Message);
            }

            return entries;
        }

        private static (string? InterfaceFriendlyName, bool? ShowLog) GetSettingsMetadata()
        {
            if (!File.Exists(SettingsFilePath))
            {
                return (null, null);
            }

            try
            {
                var lines = File.ReadAllLines(SettingsFilePath);
                if (lines.Length == 0)
                {
                    return (null, null);
                }

                var interfaceFriendlyName = string.IsNullOrWhiteSpace(lines[0]) ? null : lines[0];
                bool? showLog = null;

                if (lines.Length > 1 && lines[1].StartsWith(ShowLogPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (bool.TryParse(lines[1].Substring(ShowLogPrefix.Length), out var parsedValue))
                    {
                        showLog = parsedValue;
                    }
                }

                return (interfaceFriendlyName, showLog);
            }
            catch (Exception ex)
            {
                Debug.Print("Exception in ApplicationSettings.GetSettingsMetadata\n" + ex.Message);
                return (null, null);
            }
        }

        private static void WriteToFile(string interfaceFriendlyName, bool showLog, Dictionary<string, string> entries, string fileName)
        {
            var directoryPath = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var content = new StringBuilder();
            content.AppendLine(interfaceFriendlyName ?? string.Empty);
            content.AppendLine($"{ShowLogPrefix}{showLog}");
            content.AppendLine(MajorDelimiter);

            foreach (var entry in entries)
            {
                content.Append(entry.Key)
                    .Append(MinorDelimiter)
                    .AppendLine(entry.Value);
            }

            File.WriteAllText(fileName, content.ToString());
        }
    }
}
