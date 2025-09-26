using System;
using System.Collections.Generic;
using System.Globalization;

namespace PolicyPlusCore.Utilities;

// Describes the semantic role of a culture in ordered preference.
public enum CultureRole
{
    Primary,
    Second,
    OSFallback,
    EnUsFallback,
    OtherFallback,
}

// Slot describing a culture plus whether it is a placeholder (e.g., duplicate primary used to retain index semantics when second disabled).
public readonly struct CultureSlot
{
    public string Name { get; }
    public CultureRole Role { get; }
    public bool IsPlaceholder { get; }

    public CultureSlot(string name, CultureRole role, bool isPlaceholder)
    {
        Name = name;
        Role = role;
        IsPlaceholder = isPlaceholder;
    }

    public override string ToString() =>
        Name + "(" + Role + (IsPlaceholder ? ":ph" : string.Empty) + ")";
}

// Builds ordered culture preference list with explicit placeholder handling.
public static class CulturePreference
{
    public sealed record BuildOptions(
        string Primary,
        string? Second,
        bool SecondEnabled,
        string? OsUiCulture,
        bool EnablePrimaryFallback
    );

    public static IReadOnlyList<CultureSlot> Build(BuildOptions opt)
    {
        var primary = Normalize(opt.Primary);
        var secondNormalized = string.IsNullOrWhiteSpace(opt.Second)
            ? null
            : Normalize(opt.Second!);
        var os = string.IsNullOrWhiteSpace(opt.OsUiCulture) ? null : Normalize(opt.OsUiCulture!);
        var list = new List<CultureSlot>(4);
        list.Add(new CultureSlot(primary, CultureRole.Primary, isPlaceholder: false));

        if (opt.SecondEnabled && secondNormalized != null)
        {
            if (string.Equals(primary, secondNormalized, StringComparison.OrdinalIgnoreCase))
            {
                // Placeholder second – mark explicitly so downstream does not treat as real second.
                list.Add(new CultureSlot(primary, CultureRole.Second, isPlaceholder: true));
            }
            else
            {
                list.Add(
                    new CultureSlot(secondNormalized, CultureRole.Second, isPlaceholder: false)
                );
            }
        }
        else if (opt.SecondEnabled && secondNormalized == null)
        {
            // Second enabled but unspecified -> insert placeholder duplicate to keep index semantics if caller expects slot[1].
            list.Add(new CultureSlot(primary, CultureRole.Second, isPlaceholder: true));
        }

        if (os != null && !ContainsName(list, os))
            list.Add(new CultureSlot(os, CultureRole.OSFallback, isPlaceholder: false));

        if (opt.EnablePrimaryFallback && !ContainsName(list, "en-US"))
            list.Add(new CultureSlot("en-US", CultureRole.EnUsFallback, isPlaceholder: false));

        return list;
    }

    public static List<string> FlattenNames(IReadOnlyList<CultureSlot> slots)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var res = new List<string>(slots.Count);
        foreach (var s in slots)
        {
            if (seen.Add(s.Name))
                res.Add(s.Name);
        }
        return res;
    }

    private static bool ContainsName(List<CultureSlot> slots, string name)
    {
        foreach (var s in slots)
            if (string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static string Normalize(string culture)
    {
        try
        {
            return CultureInfo.GetCultureInfo(culture).Name;
        }
        catch
        {
            return culture;
        }
    }
}
