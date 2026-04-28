using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CSArp.View
{
    public static class ApplicationSettings
    {
        /* Settings are stored in CSArp_settings.ini file as follows:
            Wi-Fi
            --------------------------------------------------------------
            D8:K2:I9:U6:J1:I8$ClientName1
            4K:6U:1H:7N:0V:5J$ClientName2
            8H:3I:2N:9E:1G:7v$ClientName1
            --------------------------------------------------------------
            Note that the same MAC cannot have two clientnames but the same clientname can have two MACs
             */

        private static readonly string majorDelim = "--------------------------------------------------------------" + "\n";
        private static readonly string minorDelim = "$";

        /// <summary>
        /// Retrieves saved client name from a PhysicalAddress if present in CSArp_settings.ini file
        /// </summary>
        /// <param name="clientphysicaladdress"></param>
        /// <returns></returns>
        public static string GetSavedClientNameFromMAC(string clientMACaddress)
        {
            var retval = "";
            try
            {
                if (File.Exists("CSArp_settings.ini"))
                {
                    var filecontents = File.ReadAllText("CSArp_settings.ini");
                    var mactoclientnamedictionary = GetMACtoClientNameDictionary(filecontents);
                    if (mactoclientnamedictionary.ContainsKey(clientMACaddress))
                    {
                        retval = (from entry in mactoclientnamedictionary where entry.Key == clientMACaddress select entry.Value).ToList()[0];
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print("Exception in ApplicationSettingsClass.GetSavedClientNameFromMAC\n" + ex.Message);
            }
            return retval;
        }

        /// <summary>
        /// Returns the preferred network interface from file
        /// </summary>
        /// <returns> Returns the name of the saved network adapter name, or null. </returns>
        public static string GetSavedPreferredInterfaceFriendlyName()
        {
            try
            {
                if (File.Exists("CSArp_settings.ini"))
                {
                    var filecontents = File.ReadAllText("CSArp_settings.ini");
                    return filecontents.Split(new string[] { majorDelim }, StringSplitOptions.RemoveEmptyEntries)[0].Split('\n')[0];
                }
            }
            catch (Exception ex)
            {
                Debug.Print("Exception in ApplicationSettingsClass.GetSavedPreferredInterfaceFriendlyName\n" + ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Updates settings file
        /// </summary>
        /// <param name="listview"></param>
        /// <param name="interfacefriendlyname"></param>
        public static bool SaveSettings(ListView listview, string interfacefriendlyname)
        {
            var retval = false;
            try
            {
                #region Populate listviewdictionary with entries from listview

                var listviewdictionary = new Dictionary<string, string>();
                foreach (ListViewItem listviewitem in listview.Items)
                {
                    var macaddress = listviewitem.SubItems[1].Text;
                    var clientname = listviewitem.SubItems[3].Text;
                    if (clientname != "")
                    {
                        if (!listviewdictionary.Contains(new KeyValuePair<string, string>(macaddress, clientname))) //just in case listview has multiple entries though that shouldn't happen
                        {
                            listviewdictionary.Add(macaddress, clientname);
                        }
                    }
                }

                #endregion Populate listviewdictionary with entries from listview

                try
                {
                    if (File.Exists("CSArp_settings.ini"))
                    {
                        var filecontentstring = File.ReadAllText("CSArp_settings.ini");
                        var olddictionary = GetMACtoClientNameDictionary(filecontentstring);

                        #region Update olddictionary with entries from listviewdictionary

                        foreach (var entry in listviewdictionary)
                        {
                            var macaddress = entry.Key;
                            var clientname = entry.Value;

                            if (!olddictionary.Contains(new KeyValuePair<string, string>(macaddress, clientname))) //if doesn't already exist
                            {
                                if (olddictionary.ContainsKey(macaddress)) //if old dict contains the current mac address
                                {
                                    olddictionary[macaddress] = clientname; //update clientname(this is underpinned by the fact that one MAC address can't have two clientnames)
                                }
                                else
                                {
                                    olddictionary.Add(macaddress, clientname); //otherwise, no need to update, just add entry
                                }
                            }
                        }

                        #endregion Update olddictionary with entries from listviewdictionary

                        WriteToFile(interfacefriendlyname, olddictionary, "CSArp_settings.ini.new");
                        File.Delete("CSArp_settings.ini");
                        File.Move("CSArp_settings.ini.new", "CSArp_settings.ini");
                    }
                    else
                    {
                        WriteToFile(interfacefriendlyname, listviewdictionary, "CSArp_settings.ini");
                    }
                    retval = true; //success
                }
                catch (Exception ex)
                {
                    Debug.Print("Inner Exception at ApplicationSettingsClass.SaveSettings()\n" + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Debug.Print("Exception at ApplicationSettingsClass.SaveSettings()\n" + ex.Message);
            }
            return retval;
        }

        #region Private methods

        private static void WriteToFile(string interfacefriendlyname, Dictionary<string, string> macaddressclientnamedictionary, string filename)
        {
            var towrite = interfacefriendlyname + "\n" + majorDelim;
            foreach (var entry in macaddressclientnamedictionary)
            {
                towrite += entry.Key + minorDelim + entry.Value + "\n";
            }
            File.WriteAllText(filename, towrite);
        }

        /// <summary>
        /// Returns a Dictionary of ClientMACAddress:ClientName from the settings file contents
        /// </summary>
        /// <param name="settingsfilecontents"></param>
        /// <returns></returns>
        private static Dictionary<string, string> GetMACtoClientNameDictionary(string settingsfilecontents)
        {
            var retval = new Dictionary<string, string>();
            try
            {
                var fields = settingsfilecontents.Split(new string[] { majorDelim }, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length > 1)
                {
                    var secondfield = fields[1];
                    if (!string.IsNullOrEmpty(secondfield))
                    {
                        var macandclientnamearray = secondfield.Split('\n');
                        foreach (var entry in macandclientnamearray)
                        {
                            if (entry.Length > 10) //exclude any '\n'
                            {
                                retval.Add(entry.Split(new string[] { minorDelim }, StringSplitOptions.RemoveEmptyEntries)[0], entry.Split(new string[] { minorDelim }, StringSplitOptions.RemoveEmptyEntries)[1]);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print("Exception in ApplicationSettingsClass.GetMACtoClientNameDictionary()\n" + ex.Message);
            }

            return retval;
        }

        #endregion Private methods
    }
}