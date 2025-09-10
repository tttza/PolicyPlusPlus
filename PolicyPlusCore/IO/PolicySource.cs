using Microsoft.Win32;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PolicyPlus.Core.Utils;

namespace PolicyPlus.Core.IO
{
    public interface IPolicySource
    {
        bool ContainsValue(string Key, string Value);
    object? GetValue(string Key, string Value);
        bool WillDeleteValue(string Key, string Value);
        List<string> GetValueNames(string Key);
        void SetValue(string Key, string Value, object Data, RegistryValueKind DataType);
        void ForgetValue(string Key, string Value); // Stop keeping track of a value
        void DeleteValue(string Key, string Value); // Mark a value as queued for deletion
        void ClearKey(string Key); // Destroy all values in a key
        void ForgetKeyClearance(string Key); // Unmark a key as cleared
    }

    public class PolFile : IPolicySource
    {
        private readonly SortedDictionary<string, PolEntryData> Entries = new SortedDictionary<string, PolEntryData>();
        private readonly Dictionary<string, string> CasePreservation = new Dictionary<string, string>();

        private string GetDictKey(string Key, string Value)
        {
            string origCase = Key + @"\\" + Value;
            string lowerCase = origCase.ToLowerInvariant();
            if (!CasePreservation.ContainsKey(lowerCase))
                CasePreservation.Add(lowerCase, origCase);
            return lowerCase;
        }

        public static PolFile Load(string File)
        {
            using (var fPol = new FileStream(File, FileMode.Open, FileAccess.Read))
            using (var binary = new BinaryReader(fPol))
            {
                return Load(binary);
            }
        }

        public static PolFile Load(BinaryReader Stream)
        {
            var pol = new PolFile();
            if (Stream.ReadUInt32() != 0x67655250L)
                throw new InvalidDataException("Missing PReg signature");
            if (Stream.ReadUInt32() != 1L)
                throw new InvalidDataException("Unknown (newer) version of POL format");
            string readSz()
            {
                var sb = new System.Text.StringBuilder();
                do
                {
                    int charCode = Stream.ReadUInt16();
                    if (charCode == 0)
                        break;
                    sb.Append(char.ConvertFromUtf32(charCode));
                }
                while (true);
                return sb.ToString();
            };
            while (Stream.BaseStream.Position != Stream.BaseStream.Length)
            {
                var ped = new PolEntryData();
                Stream.BaseStream.Position += 2L;
                string key = readSz();
                Stream.BaseStream.Position += 2L;
                string value = readSz();
                if (Stream.ReadUInt16() != ';')
                    Stream.BaseStream.Position += 2L;
                ped.Kind = (RegistryValueKind)Stream.ReadInt32();
                Stream.BaseStream.Position += 2L;
                uint length = Stream.ReadUInt32();
                Stream.BaseStream.Position += 2L;
                var data = new byte[(int)(length - 1L + 1)];
                Stream.Read(data, 0, (int)length);
                ped.Data = data;
                Stream.BaseStream.Position += 2L;
                pol.Entries.Add(pol.GetDictKey(key, value), ped);
            }

            return pol;
        }

        public void Save(string File)
        {
            using (var fPol = new FileStream(File, FileMode.Create))
            using (var binary = new BinaryWriter(fPol, System.Text.Encoding.Unicode))
            {
                Save(binary);
            }
        }

        public void Save(BinaryWriter Writer)
        {
            void writeSz(string Text)
            {
                foreach (var c in Text)
                    Writer.Write(c);
                Writer.Write((short)0);
            };
            Writer.Write(0x67655250U);
            Writer.Write(1);
            foreach (var kv in Entries)
            {
                Writer.Write('[');
                var pathparts = CasePreservation[kv.Key].Split(new[] { "\\\\" }, 2, StringSplitOptions.None);
                writeSz(pathparts[0]);
                Writer.Write(';');
                writeSz(pathparts[1]);
                Writer.Write(';');
                Writer.Write((int)kv.Value.Kind);
                Writer.Write(';');
                Writer.Write(kv.Value.Data.Length);
                Writer.Write(';');
                Writer.Write(kv.Value.Data);
                Writer.Write(']');
            }
        }

        public void DeleteValue(string Key, string Value)
        {
            ForgetValue(Key, Value);
            if (!WillDeleteValue(Key, Value))
            {
                var ped = PolEntryData.FromDword(32U);
                Entries.Add(GetDictKey(Key, "**del." + Value), ped);
            }
        }

        public void ForgetValue(string Key, string Value)
        {
            string dictKey = GetDictKey(Key, Value);
            if (Entries.ContainsKey(dictKey))
                Entries.Remove(dictKey);
            string deleterKey = GetDictKey(Key, "**del." + Value);
            if (Entries.ContainsKey(deleterKey))
                Entries.Remove(deleterKey);
        }

    public void SetValue(string Key, string Value, object Data, RegistryValueKind DataType)
        {
            string dictKey = GetDictKey(Key, Value);
            if (Entries.ContainsKey(dictKey))
                Entries.Remove(dictKey);
            if (DataType == RegistryValueKind.MultiString)
            {
                if (Data is string[] arr)
                    Entries.Add(dictKey, PolEntryData.FromMultiString(arr));
                else if (Data is IEnumerable<string> lines)
                    Entries.Add(dictKey, PolEntryData.FromMultiString(lines.ToArray()));
        else if (Data != null)
            Entries.Add(dictKey, PolEntryData.FromMultiString(new[] { Data?.ToString() ?? string.Empty }));
                else
                    Entries.Add(dictKey, PolEntryData.FromMultiString(Array.Empty<string>()));
                return;
            }
            Entries.Add(dictKey, PolEntryData.FromArbitrary(Data, DataType));
        }

        public bool ContainsValue(string Key, string Value)
        {
            if (WillDeleteValue(Key, Value))
                return false;
            return Entries.ContainsKey(GetDictKey(Key, Value));
        }

    public object? GetValue(string Key, string Value)
        {
            if (!ContainsValue(Key, Value))
                return null;
            var entry = Entries[GetDictKey(Key, Value)];
            if (entry.Kind == RegistryValueKind.MultiString)
                return entry.AsMultiString();
            return entry.AsArbitrary();
        }

    public bool WillDeleteValue(string Key, string Value)
        {
            bool willDelete = false;
            string keyRoot = GetDictKey(Key, "");
            foreach (var kv in Entries.Where(e => e.Key.StartsWith(keyRoot)))
            {
                if ((kv.Key ?? "") == (GetDictKey(Key, "**del." + Value) ?? ""))
                {
                    willDelete = true;
                }
        else if ((kv.Key ?? string.Empty).StartsWith(GetDictKey(Key, "**delvals")))
                {
                    willDelete = true;
                }
                else if ((kv.Key ?? "") == (GetDictKey(Key, "**deletevalues") ?? ""))
                {
                    string lowerVal = Value.ToLowerInvariant();
                    var deletedValues = kv.Value.AsString().Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if (deletedValues.Any(s => (s.ToLowerInvariant() ?? "") == (lowerVal ?? "")))
                        willDelete = true;
                }
                else if ((kv.Key ?? "") == (GetDictKey(Key, Value) ?? ""))
                {
                    willDelete = false;
                }
            }

            return willDelete;
        }

        public List<string> GetValueNames(string Key)
        {
            return GetValueNames(Key, true);
        }

        public List<string> GetValueNames(string Key, bool OnlyValues)
        {
            string prefix = GetDictKey(Key, "");
            var valNames = new List<string>();
            foreach (var k in Entries.Keys)
            {
                if (k.StartsWith(prefix))
                {
                    string valName = CasePreservation[k].Split(new[] { "\\\\" }, 2, StringSplitOptions.None)[1];
                    if (!(OnlyValues & valName.StartsWith("**")))
                        valNames.Add(valName);
                }
            }

            return valNames;
        }

    public void ApplyDifference(PolFile? OldVersion, IPolicySource Target)
        {
            if (OldVersion is null)
                OldVersion = new PolFile();
            var oldEntries = OldVersion.Entries.Keys.Where(k => !k.Contains(@"\\**")).ToList();
            foreach (var kv in Entries)
            {
                var parts = kv.Key.Split(new[] { "\\\\" }, 2, StringSplitOptions.None);
                var casedParts = CasePreservation[kv.Key].Split(new[] { "\\\\" }, 2, StringSplitOptions.None);
                if (parts[1].StartsWith("**del."))
                {
                    Target.DeleteValue(parts[0], parts[1].Split(new[] { '.' }, 2)[1]);
                }
                else if (parts[1].StartsWith("**delvals"))
                {
                    Target.ClearKey(parts[0]);
                }
                else if (parts[1] == "**deletevalues")
                {
                    foreach (var entry in kv.Value.AsString().Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                        Target.DeleteValue(parts[0], entry);
                }
                else if (parts[1].StartsWith("**deletekeys"))
                {
                    foreach (var entry in kv.Value.AsString().Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                        Target.ClearKey(parts[0] + @"\" + entry);
                }
                else if (!string.IsNullOrEmpty(parts[1]) & !parts[1].StartsWith("**"))
                {
                    Target.SetValue(casedParts[0], casedParts[1], kv.Value.AsArbitrary(), kv.Value.Kind);
                    if (oldEntries.Contains(kv.Key))
                        oldEntries.Remove(kv.Key);
                }
            }

            foreach (var e in oldEntries.Where(RegistryPolicyProxy.IsPolicyKey))
            {
                var parts = e.Split(new[] { "\\\\" }, 2, StringSplitOptions.None);
                Target.ForgetValue(parts[0], parts[1]);
            }
        }

    public void Apply(IPolicySource Target)
        {
            ApplyDifference(null, Target);
        }

        public void ClearKey(string Key)
        {
            foreach (var value in GetValueNames(Key, false))
                ForgetValue(Key, value);
            var ped = PolEntryData.FromString(" ");
            Entries.Add(GetDictKey(Key, "**delvals."), ped);
        }

        public void ForgetKeyClearance(string Key)
        {
            string keyDeleter = GetDictKey(Key, "**delvals");
            foreach (var kv in Entries.Where(e => e.Key.StartsWith(keyDeleter)).ToList())
                Entries.Remove(kv.Key);
        }

        public List<string> GetKeyNames(string Key)
        {
            var subkeyNames = new List<string>();
            string prefix = string.IsNullOrEmpty(Key) ? "" : Key + @"\";
            foreach (var entry in Entries.Keys.Where(e => e.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase)))
            {
                if (entry.StartsWith(prefix + @"\", StringComparison.InvariantCultureIgnoreCase))
                    continue;
                string properCased = CasePreservation[entry].Split(new[] { "\\\\" }, 2, StringSplitOptions.None)[0];
                if (prefix.Length >= properCased.Length)
                    continue;
                string localKeyName = properCased.Substring(prefix.Length).Split(new[] { '\\' }, 2)[0];
                if (!subkeyNames.Contains(localKeyName, StringComparer.InvariantCultureIgnoreCase))
                    subkeyNames.Add(localKeyName);
            }

            return subkeyNames;
        }

        public RegistryValueKind GetValueKind(string Key, string Value)
        {
            return Entries[GetDictKey(Key, Value)].Kind;
        }

        public PolFile Duplicate()
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms, System.Text.Encoding.Unicode, true))
                {
                    Save(writer);
                }

                ms.Position = 0L;
                using (var reader = new BinaryReader(ms, System.Text.Encoding.Unicode))
                {
                    return Load(reader);
                }
            }
        }

        public static byte[] ObjectToBytes(object Data, RegistryValueKind Kind)
        {
            return PolEntryData.FromArbitrary(Data, Kind).Data;
        }

        public static object BytesToObject(byte[] Data, RegistryValueKind Kind)
        {
            return new PolEntryData() { Data = Data, Kind = Kind }.AsArbitrary();
        }

        private class PolEntryData
        {
            public RegistryValueKind Kind;
            public byte[] Data = Array.Empty<byte>();

            public string AsString()
            {
                var sb = new System.Text.StringBuilder();
                for (int x = 0, loopTo = Data.Length / 2 - 1; x <= loopTo; x++)
                {
                    int charCode = Data[x * 2] + (Data[x * 2 + 1] << 8);
                    if (charCode == 0)
                        break;
                    sb.Append(char.ConvertFromUtf32(charCode));
                }

                return sb.ToString();
            }

            public static PolEntryData FromString(string Text, bool Expand = false)
            {
                var ped = new PolEntryData() { Kind = RegistryValueKind.String };
                if (Expand)
                    ped.Kind = RegistryValueKind.ExpandString;
                if (Text is null) Text = "";
                var data = new byte[Text.Length * 2 + 1 + 1];
                for (int x = 0, loopTo = Text.Length - 1; x <= loopTo; x++)
                {
                    int charCode = Text[x];
                    data[x * 2] = (byte)(charCode & 0xFF);
                    data[x * 2 + 1] = (byte)(charCode >> 8);
                }

                ped.Data = data;
                return ped;
            }

            public uint AsDword()
            {
                return Data[0] + ((uint)Data[1] << 8) + ((uint)Data[2] << 16) + ((uint)Data[3] << 24);
            }

            public static PolEntryData FromDword(uint Dword)
            {
                var ped = new PolEntryData() { Kind = RegistryValueKind.DWord };
                var data = new byte[4];
                data[0] = (byte)(Dword & 0xFFU);
                data[1] = (byte)(Dword >> 8 & 0xFFU);
                data[2] = (byte)(Dword >> 16 & 0xFFU);
                data[3] = (byte)(Dword >> 24);
                ped.Data = data;
                return ped;
            }

            public ulong AsQword()
            {
                ulong value = 0UL;
                for (int n = 0; n <= 7; n++)
                    value += (ulong)Data[n] << n * 8;
                return value;
            }

            public static PolEntryData FromQword(ulong Qword)
            {
                var ped = new PolEntryData() { Kind = RegistryValueKind.QWord };
                var data = new byte[8];
                for (int n = 0; n <= 7; n++)
                    data[n] = (byte)(Qword >> n * 8 & 0xFFUL);
                ped.Data = data;
                return ped;
            }

            public string[] AsMultiString()
            {
                var strings = new List<string>();
                int i = 0;
                while (i + 1 < Data.Length)
                {
                    int start = i;
                    while (i + 1 < Data.Length && (Data[i] != 0 || Data[i + 1] != 0))
                        i += 2;
                    if (i > start)
                    {
                        var s = System.Text.Encoding.Unicode.GetString(Data, start, i - start);
                        strings.Add(s);
                    }
                    i += 2;
                }
                return strings.ToArray();
            }

            public static PolEntryData FromMultiString(string[] Strings)
            {
                var ped = new PolEntryData() { Kind = RegistryValueKind.MultiString };
                int totalLen = (Strings.Sum(s => s.Length) + Strings.Length + 1) * 2;
                var data = new byte[totalLen];
                int n = 0;
                foreach (var s in Strings)
                {
                    var bytes = System.Text.Encoding.Unicode.GetBytes(s);
                    Array.Copy(bytes, 0, data, n, bytes.Length);
                    n += bytes.Length;
                    data[n] = 0; data[n + 1] = 0;
                    n += 2;
                }
                data[n] = 0; data[n + 1] = 0;
                ped.Data = data;
                return ped;
            }

            public byte[] AsBinary()
            {
                return (byte[])Data.Clone();
            }

            public static PolEntryData FromBinary(byte[] Binary, RegistryValueKind Kind = RegistryValueKind.Binary)
            {
                var ped = new PolEntryData() { Kind = Kind };
                ped.Data = (byte[])Binary.Clone();
                return ped;
            }

            public object AsArbitrary()
            {
                switch (Kind)
                {
                    case RegistryValueKind.String:
                        return AsString();
                    case RegistryValueKind.DWord:
                        return AsDword();
                    case RegistryValueKind.ExpandString:
                        return AsString();
                    case RegistryValueKind.QWord:
                        return AsQword();
                    case RegistryValueKind.MultiString:
                        return AsMultiString();
                    default:
                        return AsBinary();
                }
            }

            public static PolEntryData FromArbitrary(object Data, RegistryValueKind Kind)
            {
                switch (Kind)
                {
                    case RegistryValueKind.String:
                        return FromString(Convert.ToString(Data) ?? string.Empty);
                    case RegistryValueKind.DWord:
                        return FromDword(Convert.ToUInt32(Data));
                    case RegistryValueKind.ExpandString:
                        return FromString(Convert.ToString(Data) ?? string.Empty, true);
                    case RegistryValueKind.QWord:
                        return FromQword(Convert.ToUInt64(Data));
                    case RegistryValueKind.MultiString:
                        return FromMultiString((string[])Data);
                    default:
                        return FromBinary((byte[])Data, Kind);
                }
            }
        }
    }

    public class RegistryPolicyProxy : IPolicySource
    {
        private RegistryKey RootKey = null!;

        public static RegistryPolicyProxy EncapsulateKey(RegistryKey Key)
            => new RegistryPolicyProxy() { RootKey = Key };

        public static RegistryPolicyProxy EncapsulateKey(RegistryHive Key)
            => EncapsulateKey(RegistryKey.OpenBaseKey(Key, RegistryView.Default));

        public RegistryKey EncapsulatedRegistry => RootKey;

        public void DeleteValue(string Key, string Value)
        {
            var isEmpty = false;
            if (string.IsNullOrEmpty(Key))
            {
                try
                {
                    RootKey.DeleteValue(Value, false);
                    using (var regKey = RootKey)
                    {
                        if (regKey.SubKeyCount == 0 & regKey.ValueCount == 0)
                            isEmpty = true;
                    }
                }
                catch { }
            }
            else
            {
                using (var regKey = RootKey.OpenSubKey(Key, true))
                {
                    if (regKey is null)
                        return;
                    regKey.DeleteValue(Value, false);

                    if (regKey.SubKeyCount == 0 & regKey.ValueCount == 0)
                        isEmpty = true;
                }
                if (isEmpty)
                {
                    RootKey.DeleteSubKey(Key, false);
                }
            }
        }

        public void ForgetValue(string Key, string Value) => DeleteValue(Key, Value);

        public void SetValue(string Key, string Value, object Data, RegistryValueKind DataType)
        {
            if (Data is uint u) Data = new ReinterpretableDword() { Unsigned = u }.Signed;
            else if (Data is ulong ul) Data = new ReinterpretableQword() { Unsigned = ul }.Signed;

            if (string.IsNullOrEmpty(Key))
            { RootKey.SetValue(Value, Data, DataType); return; }

            using (var regKey = RootKey.CreateSubKey(Key))
            { regKey.SetValue(Value, Data, DataType); }
        }

        public bool ContainsValue(string Key, string Value)
        {
            if (string.IsNullOrEmpty(Key))
            {
                var data = RootKey.GetValue(Value, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                return data != null || RootKey.GetValueNames().Any(s => s.Equals(Value, StringComparison.InvariantCultureIgnoreCase));
            }
            using (var regKey = RootKey.OpenSubKey(Key))
            {
                if (regKey is null) return false;
                if (string.IsNullOrEmpty(Value)) return true;
                return regKey.GetValueNames().Any(s => s.Equals(Value, StringComparison.InvariantCultureIgnoreCase));
            }
        }

        public object? GetValue(string Key, string Value)
        {
            if (string.IsNullOrEmpty(Key))
            {
                var data = RootKey.GetValue(Value, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (data is int i) return new ReinterpretableDword() { Signed = i }.Unsigned;
                if (data is long l) return new ReinterpretableQword() { Signed = l }.Unsigned;
                return data;
            }
            using (var regKey = RootKey.OpenSubKey(Key, false))
            {
                if (regKey is null) return null;
                var data = regKey.GetValue(Value, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (data is int i) return new ReinterpretableDword() { Signed = i }.Unsigned;
                if (data is long l) return new ReinterpretableQword() { Signed = l }.Unsigned;
                return data;
            }
        }

        public List<string> GetValueNames(string Key)
        {
            if (string.IsNullOrEmpty(Key)) return RootKey.GetValueNames().ToList();
            using (var regKey = RootKey.OpenSubKey(Key))
            { return regKey is null ? new List<string>() : regKey.GetValueNames().ToList(); }
        }

        public bool WillDeleteValue(string Key, string Value) => false;

        public static bool IsPolicyKey(string KeyPath)
        {
            return PolicyKeys.Any(pk => KeyPath.StartsWith(pk + @"\", StringComparison.InvariantCultureIgnoreCase)
                                     || KeyPath.Equals(pk, StringComparison.InvariantCultureIgnoreCase));
        }

        public void ClearKey(string Key)
        {
            if (string.IsNullOrEmpty(Key))
            { foreach (var value in RootKey.GetValueNames()) ForgetValue(string.Empty, value); return; }
            foreach (var value in GetValueNames(Key)) ForgetValue(Key, value);
        }

        public void ForgetKeyClearance(string Key) { }

        public static IEnumerable<string> PolicyKeys => new[]
        {
            @"software\policies",
            @"software\microsoft\windows\currentversion\policies",
            @"system\currentcontrolset\policies"
        };
    }
}
