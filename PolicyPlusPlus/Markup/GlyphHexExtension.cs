using System;
using System.Globalization;
using Microsoft.UI.Xaml.Markup;

namespace PolicyPlusPlus.Markup;

/// <summary>
/// XAML markup extension that converts a hexadecimal Unicode code point (for example, "E710" or "0xE710")
/// into a string containing the corresponding glyph character.
/// </summary>
/// <remarks>
/// - Accepts common prefixes/suffixes when copying codes (e.g., "0x", "#", "&", ";", and an optional leading 'x'/'X').
/// - Returns an empty string on invalid input to fail softly.
/// - This approach helps prevent mojibake (garbled characters) during automatic code formatting by tools such as CSharpier,
///   because glyphs are represented as hex codes rather than literal characters in source.
/// - Extension that allows specifying hex codes instead of literal characters to prevent garbled text during CSharpier formatting.
/// </remarks>
/// <example>
/// <!-- XAML -->
/// <TextBlock Text="{local:GlyphHex E710}" />
/// <TextBlock Text="{local:GlyphHex Code=0xE710}" />
/// </example>
[MarkupExtensionReturnType(ReturnType = typeof(string))]
public sealed partial class GlyphHexExtension : MarkupExtension
{
    // Accepts hex like "E710" or "0xE710". Returns a string containing the Unicode character.
    public string Code { get; set; } = string.Empty;

    public GlyphHexExtension() { }

    public GlyphHexExtension(string code)
    {
        Code = code;
    }

    // Converts the hex code to a string containing the corresponding Unicode scalar.
    protected override object ProvideValue()
    {
        var s = (Code ?? string.Empty).Trim();

        // Be forgiving with common prefixes/suffixes that may appear when copying codes.
        s = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s[2..] : s;
        s = s.Trim('#').Trim('&').Trim(';').TrimStart('x', 'X');

        // Parse as hexadecimal number; if invalid, return empty string to fail soft.
        return int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var cp)
            ? char.ConvertFromUtf32(cp)
            : string.Empty;
    }
}
