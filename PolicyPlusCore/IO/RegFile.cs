// VB dependency removed: replaced Strings/Conversions helpers with .NET equivalents
using Microsoft.Win32;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PolicyPlus.Core.IO
{
    public class RegFile : IPolicySource
    {
        private const string RegSignature = "Windows Registry Editor Version 5.00";
    private string Prefix = string.Empty;
    private string SourceSubtree = string.Empty;
        public List<RegFileKey> Keys = new List<RegFileKey>();

        private static string EscapeValue(string Text)
        {
            if (Text is null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            for (int n = 0, loopTo = Text.Length - 1; n <= loopTo; n++)
            {
                char character = Text[n];
                if (character == '"' | character == '\\')
                    sb.Append('\\');
                sb.Append(character);
            }

            return sb.ToString();
        }

        private static string UnescapeValue(string Text)
        {
            var sb = new System.Text.StringBuilder();
            bool escaping = false;
            for (int n = 0, loopTo = Text.Length - 1; n <= loopTo; n++)
            {
                if (escaping)
                {
                    sb.Append(Text[n]);
                    escaping = false;
                }
                else if (Text[n] == '\\')
                {
                    escaping = true;
                }
                else
                {
                    sb.Append(Text[n]);
                }
            }

            return sb.ToString();
        }

        private static string? ReadNonCommentingLine(StreamReader Reader, char? StopAt = default)
        {
            do
            {
                if (Reader.EndOfStream)
                    return null;
                if (StopAt.HasValue && Reader.Peek() == StopAt.Value)
                    return null;
                string? line = Reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith(";"))
                    continue;
                return line;
            }
            while (true);
        }

        public static RegFile Load(StreamReader Reader, string Prefix)
        {
            if ((Reader.ReadLine() ?? "") != RegSignature)
                throw new InvalidDataException("Incorrect REG signature");
            var reg = new RegFile();
            reg.SetPrefix(Prefix);
            do
            {
                string? keyHeader = ReadNonCommentingLine(Reader);
                if (keyHeader is null)
                    break;
                string keyName = keyHeader!.Substring(1, keyHeader.Length - 2);
                if (keyName.StartsWith("-"))
                {
                    var deleterKey = new RegFileKey() { Name = keyName.Substring(1), IsDeleter = true };
                    reg.Keys.Add(deleterKey);
                }
                else
                {
                    var key = new RegFileKey() { Name = keyName };
                    do
                    {
                        string? valueLine = ReadNonCommentingLine(Reader, '[');
                        if (valueLine is null)
                            break;
                        string valueName = "";
                        string data;
                        if (valueLine!.StartsWith("@"))
                        {
                            data = valueLine.Substring(2);
                        }
                        else
                        {
                            var parts = valueLine.Split(new[] { "\"=" }, 2, StringSplitOptions.None);
                            valueName = UnescapeValue(parts[0].Substring(1));
                            data = parts[1];
                        }

                        var value = new RegFileValue() { Name = valueName };
                        if (data == "-")
                        {
                            value.IsDeleter = true;
                        }
                        else if (data.StartsWith("\""))
                        {
                            value.Kind = RegistryValueKind.String;
                            value.Data = UnescapeValue(data.Substring(1, data.Length - 2));
                        }
                        else if (data.StartsWith("dword:"))
                        {
                            value.Kind = RegistryValueKind.DWord;
                            value.Data = uint.Parse(data.Substring(6), System.Globalization.NumberStyles.HexNumber);
                        }
                        else if (data.StartsWith("hex"))
                        {
                            int indexOfClosingParen = data.IndexOf(')');
                            string? curHexLine;
                            if (indexOfClosingParen != -1)
                            {
                                value.Kind = (RegistryValueKind)int.Parse(data.Substring(4, indexOfClosingParen - 4), System.Globalization.NumberStyles.HexNumber);
                                curHexLine = data.Substring(indexOfClosingParen + 2);
                            }
                            else
                            {
                                value.Kind = RegistryValueKind.Binary;
                                curHexLine = data.Substring(4);
                            }

                            var allDehexedBytes = new List<byte>();
                            do
                            {
                                var safeLine = (curHexLine ?? string.Empty).Trim().TrimEnd('\\', ',');
                                var hexBytes = safeLine.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
                                foreach (var b in hexBytes)
                                    allDehexedBytes.Add(byte.Parse(b, System.Globalization.NumberStyles.HexNumber));
                                if (curHexLine != null && curHexLine.EndsWith(@"\"))
                                {
                                    curHexLine = Reader.ReadLine();
                                }
                                else
                                {
                                    break;
                                }
                            }
                            while (true);
                            value.Data = PolFile.BytesToObject(allDehexedBytes.ToArray(), value.Kind);
                        }

                        key.Values.Add(value);
                    }
                    while (true);
                    reg.Keys.Add(key);
                }
            }
            while (true);
            return reg;
        }

        public static RegFile Load(string File, string Prefix)
        {
            using (var reader = new StreamReader(File))
            {
                return Load(reader, Prefix);
            }
        }

    public void Save(TextWriter Writer, Dictionary<string, string>? casePreservation = null)
        {
            Writer.WriteLine(RegSignature);
            Writer.WriteLine();
            foreach (var key in Keys)
            {
                string keyName = CanonicalizeKeyName(key.Name);
                if (casePreservation != null && casePreservation.TryGetValue(keyName.ToLowerInvariant() + "\\", out var preservedKey))
                    keyName = preservedKey;
                if (key.IsDeleter)
                {
                    Writer.WriteLine("[-" + keyName + "]");
                }
                else
                {
                    Writer.WriteLine("[" + keyName + "]");
                    foreach (var value in key.Values)
                    {
                        int posInRow = 0;
                        string valueName = value.Name;
                        if (casePreservation != null && !string.IsNullOrEmpty(valueName))
                        {
                            string dictKey = keyName.ToLowerInvariant() + "\\" + valueName.ToLowerInvariant();
                            if (casePreservation.TryGetValue(dictKey, out var preservedValue))
                                valueName = preservedValue;
                        }
                        if (string.IsNullOrEmpty(valueName))
                        {
                            Writer.Write("@");
                            posInRow = 1;
                        }
                        else
                        {
                            string quotedName = "\"" + EscapeValue(valueName) + "\"";
                            Writer.Write(quotedName);
                            posInRow = quotedName.Length;
                        }
                        Writer.Write("=");
                        posInRow += 1;
                        if (value.IsDeleter)
                        {
                            Writer.WriteLine("-");
                        }
                        else
                        {
                            switch (value.Kind)
                            {
                                case RegistryValueKind.String:
                                    {
                                        Writer.Write("\"");
                                        Writer.Write(EscapeValue(Convert.ToString(value.Data) ?? string.Empty));
                                        Writer.WriteLine("\"");
                                        break;
                                    }
                                case RegistryValueKind.DWord:
                                    {
                                        Writer.Write("dword:");
                                        var dwordVal = Convert.ToUInt32(value.Data ?? 0);
                                        Writer.WriteLine(Convert.ToString(dwordVal, 16).PadLeft(8, '0'));
                                        break;
                                    }
                                default:
                                    {
                                        Writer.Write("hex");
                                        posInRow += 3;
                                        if (value.Kind != RegistryValueKind.Binary)
                                        {
                                            Writer.Write("(");
                                            Writer.Write(Convert.ToString((int)value.Kind, 16));
                                            Writer.Write(")");
                                            posInRow += 3;
                                        }
                                        Writer.Write(":");
                                        posInRow += 1;
                                        object safeData;
                                        switch (value.Kind)
                                        {
                                            case RegistryValueKind.MultiString:
                                                safeData = value.Data as string[] ?? Array.Empty<string>();
                                                break;
                                            case RegistryValueKind.QWord:
                                                safeData = Convert.ToUInt64(value.Data ?? 0UL);
                                                break;
                                            case RegistryValueKind.ExpandString:
                                            case RegistryValueKind.String:
                                                safeData = Convert.ToString(value.Data) ?? string.Empty;
                                                break;
                                            case RegistryValueKind.Binary:
                                            default:
                                                safeData = value.Data as byte[] ?? Array.Empty<byte>();
                                                break;
                                        }
                                        var bytes = PolFile.ObjectToBytes(safeData, value.Kind);
                                        for (int n = 0, loopTo = bytes.Length - 2; n <= loopTo; n++)
                                        {
                                            Writer.Write(Convert.ToString(bytes[n], 16).PadLeft(2, '0'));
                                            Writer.Write(",");
                                            posInRow += 3;
                                            if (posInRow >= 78)
                                            {
                                                Writer.WriteLine(@"\");
                                                Writer.Write("  ");
                                                posInRow = 2;
                                            }
                                        }
                                        if (bytes.Length > 0)
                                        {
                                            Writer.WriteLine(Convert.ToString(bytes[bytes.Length - 1], 16).PadLeft(2, '0'));
                                        }
                                        else
                                        {
                                            Writer.WriteLine();
                                        }
                                        break;
                                    }
                            }
                        }
                    }
                }
                Writer.WriteLine();
            }
        }

        private static string CanonicalizeKeyName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            // Trim accidental leading backslashes
            while (name.Length > 0 && name[0] == '\\') name = name.Substring(1);
            var parts = name.Split('\\');
            if (parts.Length == 0) return name;
            // Canonicalize hive
            if (parts[0].Equals("HKLM", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
                parts[0] = "HKEY_LOCAL_MACHINE";
            else if (parts[0].Equals("HKCU", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
                parts[0] = "HKEY_CURRENT_USER";
            else if (parts[0].Equals("HKU", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("HKEY_USERS", StringComparison.OrdinalIgnoreCase))
                parts[0] = "HKEY_USERS";
            // Canonicalize common first segments
            if (parts.Length > 1 && parts[1].Equals("software", StringComparison.OrdinalIgnoreCase)) parts[1] = "SOFTWARE";
            if (parts.Length > 1 && parts[1].Equals("system", StringComparison.OrdinalIgnoreCase)) parts[1] = "SYSTEM";
            if (parts.Length > 2 && parts[2].Equals("policies", StringComparison.OrdinalIgnoreCase)) parts[2] = "Policies";
            if (parts.Length > 2 && parts[2].Equals("microsoft", StringComparison.OrdinalIgnoreCase)) parts[2] = "Microsoft";
            if (parts.Length > 3 && parts[3].Equals("windows", StringComparison.OrdinalIgnoreCase)) parts[3] = "Windows";
            if (parts.Length > 4 && parts[4].Equals("currentversion", StringComparison.OrdinalIgnoreCase)) parts[4] = "CurrentVersion";
            return string.Join("\\", parts);
        }

        public void Save(string File)
        {
            using (var writer = new StreamWriter(File, false))
            {
                Save(writer);
            }
        }

        private string UnprefixKeyName(string Name)
        {
            if (!string.IsNullOrEmpty(Prefix) && Name.StartsWith(Prefix, StringComparison.InvariantCultureIgnoreCase))
                return Name.Substring(Prefix.Length);
            else
                return Name;
        }

        private string PrefixKeyName(string Name)
        {
            return Prefix + Name;
        }

    private RegFileKey? GetKey(string Name)
        {
            return Keys.FirstOrDefault(k => k.Name.Equals(Name, StringComparison.InvariantCultureIgnoreCase));
        }

    private RegFileKey? GetKeyByUnprefixedName(string Name)
        {
            return GetKey(PrefixKeyName(Name));
        }

    private RegFileKey GetOrCreateKey(string Name)
        {
            var key = GetKey(Name);
            if (key is null)
            {
                key = new RegFileKey() { Name = Name };
                Keys.Add(key);
            }

            return key;
        }

    private RegFileKey? GetNonDeleterKey(string Name)
        {
            return Keys.FirstOrDefault(k => !k.IsDeleter && k.Name.Equals(Name, StringComparison.InvariantCultureIgnoreCase));
        }

        private bool IsSourceKeyAcceptable(string Key)
        {
            return string.IsNullOrEmpty(SourceSubtree) || Key.Equals(SourceSubtree, StringComparison.InvariantCultureIgnoreCase) || Key.StartsWith(SourceSubtree + "\\", StringComparison.InvariantCultureIgnoreCase);
        }

        public bool ContainsValue(string Key, string Value)
        {
            if (!IsSourceKeyAcceptable(Key))
                return false;
            var key = GetNonDeleterKey(PrefixKeyName(Key));
            if (key is null)
                return false;
            if (string.IsNullOrEmpty(Value))
                return key.Values.Any(v => !v.IsDeleter && string.IsNullOrEmpty(v.Name));
            var val = key.GetValue(Value);
            return val != null && !val.IsDeleter;
        }
    public object? GetValue(string Key, string Value)
        {
            if (!IsSourceKeyAcceptable(Key))
                return null;
            var key = GetNonDeleterKey(PrefixKeyName(Key));
            var val = key?.GetValue(Value);
            if (val is null || val.IsDeleter)
                return null;
            return val.Data;
        }
        public bool WillDeleteValue(string Key, string Value)
        {
            if (!IsSourceKeyAcceptable(Key))
                return false;
            var fullKey = PrefixKeyName(Key);
            var key = GetKey(fullKey);
            if (key is null)
                return false;
            if (key.IsDeleter)
                return true;
            var val = key.GetValue(Value);
            return val != null && val.IsDeleter;
        }
        public List<string> GetValueNames(string Key)
        {
            var names = new List<string>();
            if (!IsSourceKeyAcceptable(Key))
                return names;
            var key = GetNonDeleterKey(PrefixKeyName(Key));
            if (key is null)
                return names;
            foreach (var v in key.Values)
            {
                if (!v.IsDeleter && v.Name != null)
                    names.Add(v.Name);
            }
            return names;
        }

        public void SetValue(string Key, string Value, object Data, RegistryValueKind DataType)
        {
            if (!IsSourceKeyAcceptable(Key))
                return;
            string fullKeyName = PrefixKeyName(Key);
            var keyRecord = GetNonDeleterKey(fullKeyName);
            if (keyRecord is null)
            {
                keyRecord = new RegFileKey() { Name = fullKeyName };
                Keys.Add(keyRecord);
            }
            var existing = keyRecord.GetValue(Value);
            if (existing != null)
                keyRecord.Values.Remove(existing);
            keyRecord.Values.Add(new RegFileValue() { Name = Value, Kind = DataType, Data = Data });
        }

        public void ForgetValue(string Key, string Value)
        {
            if (!IsSourceKeyAcceptable(Key))
                return;
            string fullKeyName = PrefixKeyName(Key);
            var keyRecord = GetKey(fullKeyName);
            if (keyRecord is null)
                return;
            var existing = keyRecord.GetValue(Value);
            if (existing != null)
                keyRecord.Values.Remove(existing);
            if (!keyRecord.IsDeleter && keyRecord.Values.Count == 0)
            {
                Keys.Remove(keyRecord);
            }
        }

        public void DeleteValue(string Key, string Value)
        {
            if (!IsSourceKeyAcceptable(Key))
                return;
            string fullKeyName = PrefixKeyName(Key);
            var keyRecord = GetOrCreateKey(fullKeyName);
            if (keyRecord.IsDeleter)
                return;
            var existing = keyRecord.GetValue(Value);
            if (existing != null)
                keyRecord.Values.Remove(existing);
            keyRecord.Values.Add(new RegFileValue() { Name = Value, IsDeleter = true });
        }

        public void ClearKey(string Key)
        {
            if (!IsSourceKeyAcceptable(Key))
                return;
            string fullName = PrefixKeyName(Key);
            var existing = GetKey(fullName);
            if (existing != null)
                Keys.Remove(existing);
            Keys.Add(new RegFileKey() { Name = fullName, IsDeleter = true });
        }

        public void ForgetKeyClearance(string Key)
        {
            if (!IsSourceKeyAcceptable(Key))
                return;
            var keyRecord = GetKeyByUnprefixedName(Key);
            if (keyRecord is null)
                return;
            if (keyRecord.IsDeleter)
                Keys.Remove(keyRecord);
        }

        public void Apply(IPolicySource Target)
        {
            foreach (var key in Keys)
            {
                string unprefixedKeyName = UnprefixKeyName(key.Name);
                unprefixedKeyName = StripHivePrefix(unprefixedKeyName);
                if (key.IsDeleter)
                {
                    Target.ClearKey(unprefixedKeyName);
                }
                else
                {
                    foreach (var value in key.Values)
                    {
                        if (value.IsDeleter)
                        {
                            Target.DeleteValue(unprefixedKeyName, value.Name);
                        }
                        else
                        {
                            object safeData;
                            switch (value.Kind)
                            {
                                case RegistryValueKind.MultiString:
                                    safeData = value.Data as string[] ?? Array.Empty<string>();
                                    break;
                                case RegistryValueKind.DWord:
                                    safeData = Convert.ToUInt32(value.Data ?? 0);
                                    break;
                                case RegistryValueKind.QWord:
                                    safeData = Convert.ToUInt64(value.Data ?? 0UL);
                                    break;
                                case RegistryValueKind.ExpandString:
                                case RegistryValueKind.String:
                                    safeData = Convert.ToString(value.Data) ?? string.Empty;
                                    break;
                                case RegistryValueKind.Binary:
                                default:
                                    safeData = value.Data as byte[] ?? Array.Empty<byte>();
                                    break;
                            }
                            Target.SetValue(unprefixedKeyName, value.Name, safeData, value.Kind);
                        }
                    }
                }
            }
        }

        private static string StripHivePrefix(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            string p = path;
            // Normalize to uniform form for startswith (case-insensitive)
            string Lower = p.ToLowerInvariant();
            static bool TryStrip(string lower, string original, string prefix, out string result)
            {
                if (lower.StartsWith(prefix, StringComparison.Ordinal))
                {
                    result = original.Substring(prefix.Length);
                    if (result.StartsWith("\\")) result = result.Substring(1);
                    return true;
                }
                result = original; return false;
            }
            if (TryStrip(Lower, p, "hkey_local_machine\\", out var r)) return r;
            if (TryStrip(Lower, p, "hkey_current_user\\", out r)) return r;
            if (TryStrip(Lower, p, "hkey_users\\", out r)) return r;
            if (TryStrip(Lower, p, "hklm\\", out r)) return r;
            if (TryStrip(Lower, p, "hkcu\\", out r)) return r;
            if (TryStrip(Lower, p, "hku\\", out r)) return r;
            return p;
        }

        public void SetPrefix(string Prefix)
        {
            if (string.IsNullOrEmpty(Prefix))
            {
                this.Prefix = string.Empty;
                return;
            }
            if (!Prefix.EndsWith("\\"))
                Prefix += "\\";
            this.Prefix = Prefix;
        }

        public void SetSourceBranch(string Branch)
        {
            if (Branch.EndsWith("\\"))
                Branch = Branch.TrimEnd('\\');
            SourceSubtree = Branch;
        }

        public string GuessPrefix()
        {
            if (Keys.Count == 0)
                return "HKEY_LOCAL_MACHINE\\";
            string firstKeyName = Keys[0].Name;
            if (firstKeyName.StartsWith("HKEY_USERS\\"))
            {
                int secondSlashPos = firstKeyName.IndexOf("\\", 11);
                return firstKeyName.Substring(0, secondSlashPos + 1);
            }
            else
            {
                int firstSlashPos = firstKeyName.IndexOf("\\");
                return firstKeyName.Substring(0, firstSlashPos + 1);
            }
        }

        public bool HasDefaultValues()
        {
            return Keys.Any(k => k.Values.Any(v => string.IsNullOrEmpty(v.Name)));
        }

        public class RegFileKey
        {
            public string Name = string.Empty;
            public bool IsDeleter;
            public List<RegFileValue> Values = new List<RegFileValue>();

            public RegFileValue? GetValue(string Value)
            {
                return Values.FirstOrDefault(v => v.Name.Equals(Value, StringComparison.InvariantCultureIgnoreCase));
            }
        }

        public class RegFileValue
        {
            public string Name = string.Empty;
            public object? Data;
            public RegistryValueKind Kind;
            public bool IsDeleter;
        }
    }
}
