using Microsoft.Win32;
using System;

namespace PolicyPlus
{
    public class ConfigurationStorage
    {
        private RegistryKey ConfigKey;

        public ConfigurationStorage(RegistryHive RegistryBase, string Subkey)
        {
            try
            {
                ConfigKey = RegistryKey.OpenBaseKey(RegistryBase, RegistryView.Default).CreateSubKey(Subkey);
            }
            catch (Exception ex)
            {
                // The key couldn't be created
            }
        }

        public object GetValue(string ValueName, object DefaultValue)
        {
            if (ConfigKey is object)
                return ConfigKey.GetValue(ValueName, DefaultValue);
            else
                return DefaultValue;
        }

        public void SetValue(string ValueName, object Data)
        {
            if (ConfigKey is object)
                ConfigKey.SetValue(ValueName, Data);
        }

        public bool HasValue(string ValueName)
        {
            return ConfigKey is object && ConfigKey.GetValue(ValueName) is object;
        }
    }
}