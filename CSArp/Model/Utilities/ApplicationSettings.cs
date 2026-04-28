using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace CSArp.View
{
    public static class ApplicationSettings
    {
        private const string SettingsFile = "CSArp_settings.ini";
        private const string MajorDelimiter = "--------------------------------------------------------------";
        private const char MinorDelimiter = '$';

        public static string GetSavedClientNameFromMAC(string clientMacAddress)
        {
            if (string.IsNullOrWhiteSpace(clientMacAddress) || !File.Exists(SettingsFile))
            {
                return string.Empty;
            }

            try
            {
                var entries = GetMacToClientNameDictionary(File.ReadAllText(SettingsFile));
                return entries.TryGetValue(clientMacAddress, out var clientName) ? clientName : string.Empty;
            }
            catch (Exception ex)
            {
                Debug.Print("Exception in ApplicationSettings.GetSavedClientNameFromMAC\n" + ex.Message);
                return string.Empty;
            }
        }

        public static string GetSavedPreferredInterfaceFriendlyName()
        {
            if (!File.Exists(SettingsFile))
            {
                return null;
            }

            try
            {
                using (var reader = File.OpenText(SettingsFile))
                {
                    return reader.ReadLine();
                }
            }
            catch (Exception ex)
            {
                Debug.Print("Exception in ApplicationSettings.GetSavedPreferredInterfaceFriendlyName\n" + ex.Message);
                return null;
            }
        }

        public static bool SaveSettings(ListView listView, string interfaceFriendlyName)
        {
            try
            {
                var entries = File.Exists(SettingsFile)
                    ? GetMacToClientNameDictionary(File.ReadAllText(SettingsFile))
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

                WriteToFile(interfaceFriendlyName, entries, SettingsFile);
                return true;
            }
            catch (Exception ex)
            {
                Debug.Print("Exception at ApplicationSettings.SaveSettings()\n" + ex.Message);
                return false;
            }
        }

        private static void WriteToFile(string interfaceFriendlyName, Dictionary<string, string> entries, string fileName)
        {
            var content = new StringBuilder();
            content.AppendLine(interfaceFriendlyName ?? string.Empty);
            content.AppendLine(MajorDelimiter);

            foreach (var entry in entries)
            {
                content.Append(entry.Key)
                    .Append(MinorDelimiter)
                    .AppendLine(entry.Value);
            }

            File.WriteAllText(fileName, content.ToString());
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
    }
}
