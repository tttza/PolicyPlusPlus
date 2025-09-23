using System.Globalization;
using System.Xml;
using PolicyPlusCore.Core;
using PolicyPlusCore.Utils;

namespace PolicyPlusCore.Admx
{
    public class AdmlFile
    {
        public string SourceFile = string.Empty;
        public decimal Revision;
        public string DisplayName = string.Empty;
        public string Description = string.Empty;
        public Dictionary<string, string> StringTable = new Dictionary<string, string>();
        public Dictionary<string, Presentation> PresentationTable =
            new Dictionary<string, Presentation>();

        private AdmlFile() { }

        public static AdmlFile Load(string File)
        {
            // ADML documentation: https://technet.microsoft.com/en-us/library/cc772050(v=ws.10).aspx
            var adml = new AdmlFile();
            adml.SourceFile = File;

            var settings = new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
                DtdProcessing = DtdProcessing.Prohibit,
                CloseInput = true,
            };

            using var reader = XmlReader.Create(File, settings);
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                    continue;

                // metadata
                if (reader.LocalName == "policyDefinitionResources")
                {
                    var revAttr = reader.GetAttribute("revision");
                    if (
                        !string.IsNullOrEmpty(revAttr)
                        && decimal.TryParse(
                            revAttr,
                            NumberStyles.Number,
                            CultureInfo.InvariantCulture,
                            out var rev
                        )
                    )
                        adml.Revision = rev;

                    // Read children: displayName/description
                    using var metaSub = reader.ReadSubtree();
                    metaSub.Read();
                    while (metaSub.Read())
                    {
                        if (metaSub.NodeType != XmlNodeType.Element)
                            continue;
                        if (metaSub.LocalName == "displayName")
                        {
                            adml.DisplayName = metaSub.ReadElementContentAsString() ?? string.Empty;
                        }
                        else if (metaSub.LocalName == "description")
                        {
                            adml.Description = metaSub.ReadElementContentAsString() ?? string.Empty;
                        }
                        else if (metaSub.LocalName == "resources")
                        {
                            using var resSub = metaSub.ReadSubtree();
                            resSub.Read();
                            while (resSub.Read())
                            {
                                if (resSub.NodeType != XmlNodeType.Element)
                                    continue;
                                if (resSub.LocalName == "stringTable")
                                {
                                    using var st = resSub.ReadSubtree();
                                    st.Read();
                                    while (st.Read())
                                    {
                                        if (
                                            st.NodeType != XmlNodeType.Element
                                            || st.LocalName != "string"
                                        )
                                            continue;
                                        var key = st.GetAttribute("id");
                                        if (string.IsNullOrEmpty(key))
                                        {
                                            st.Skip();
                                            continue;
                                        }
                                        var value = st.ReadElementContentAsString() ?? string.Empty;
                                        adml.StringTable[key] = value;
                                    }
                                }
                                else if (resSub.LocalName == "presentationTable")
                                {
                                    using var pt = resSub.ReadSubtree();
                                    pt.Read();
                                    while (pt.Read())
                                    {
                                        if (
                                            pt.NodeType != XmlNodeType.Element
                                            || pt.LocalName != "presentation"
                                        )
                                            continue;
                                        var id = pt.GetAttribute("id");
                                        if (string.IsNullOrEmpty(id))
                                        {
                                            pt.Skip();
                                            continue;
                                        }
                                        var presentation = new Presentation { Name = id };
                                        using var presSub = pt.ReadSubtree();
                                        presSub.Read();
                                        while (presSub.Read())
                                        {
                                            if (presSub.NodeType != XmlNodeType.Element)
                                                continue;
                                            PresentationElement? presPart = null;
                                            string elemName = presSub.LocalName ?? string.Empty;
                                            // Cache refId before consuming inner content to avoid losing attribute after ReadElementContentAsString
                                            string refIdCached =
                                                presSub.GetAttribute("refId") ?? string.Empty;
                                            switch (elemName)
                                            {
                                                case "text":
                                                {
                                                    var textPart = new LabelPresentationElement();
                                                    textPart.Text =
                                                        presSub.ReadElementContentAsString()
                                                        ?? string.Empty;
                                                    presPart = textPart;
                                                    break;
                                                }
                                                case "decimalTextBox":
                                                {
                                                    var decTextPart =
                                                        new NumericBoxPresentationElement();
                                                    var defStr = presSub.GetAttribute(
                                                        "defaultValue"
                                                    );
                                                    var spinStr = presSub.GetAttribute("spin");
                                                    var stepStr = presSub.GetAttribute("spinStep");
                                                    if (
                                                        uint.TryParse(
                                                            defStr,
                                                            NumberStyles.Integer,
                                                            CultureInfo.InvariantCulture,
                                                            out var def
                                                        )
                                                    )
                                                        decTextPart.DefaultValue = def;
                                                    else
                                                        decTextPart.DefaultValue = 1;
                                                    decTextPart.HasSpinner = bool.TryParse(
                                                        spinStr,
                                                        out var sp
                                                    )
                                                        ? sp
                                                        : true;
                                                    if (
                                                        uint.TryParse(
                                                            stepStr,
                                                            NumberStyles.Integer,
                                                            CultureInfo.InvariantCulture,
                                                            out var step
                                                        )
                                                    )
                                                        decTextPart.SpinnerIncrement = step;
                                                    else
                                                        decTextPart.SpinnerIncrement = 1;
                                                    var label =
                                                        presSub.ReadElementContentAsString()
                                                        ?? string.Empty;
                                                    decTextPart.Label = label;
                                                    presPart = decTextPart;
                                                    break;
                                                }
                                                case "textBox":
                                                {
                                                    var textPart = new TextBoxPresentationElement();
                                                    using var tbSub = presSub.ReadSubtree();
                                                    tbSub.Read();
                                                    while (tbSub.Read())
                                                    {
                                                        if (tbSub.NodeType != XmlNodeType.Element)
                                                            continue;
                                                        if (tbSub.LocalName == "label")
                                                            textPart.Label =
                                                                tbSub.ReadElementContentAsString()
                                                                ?? string.Empty;
                                                        else if (tbSub.LocalName == "defaultValue")
                                                            textPart.DefaultValue =
                                                                tbSub.ReadElementContentAsString()
                                                                ?? string.Empty;
                                                    }
                                                    presPart = textPart;
                                                    break;
                                                }
                                                case "checkBox":
                                                {
                                                    var checkPart =
                                                        new CheckBoxPresentationElement();
                                                    var dc = presSub.GetAttribute("defaultChecked");
                                                    checkPart.DefaultState = bool.TryParse(
                                                        dc,
                                                        out var b
                                                    )
                                                        ? b
                                                        : false;
                                                    checkPart.Text =
                                                        presSub.ReadElementContentAsString()
                                                        ?? string.Empty;
                                                    presPart = checkPart;
                                                    break;
                                                }
                                                case "comboBox":
                                                {
                                                    var comboPart =
                                                        new ComboBoxPresentationElement();
                                                    var noSortStr = presSub.GetAttribute("noSort");
                                                    comboPart.NoSort = bool.TryParse(
                                                        noSortStr,
                                                        out var ns
                                                    )
                                                        ? ns
                                                        : false;
                                                    using var cbSub = presSub.ReadSubtree();
                                                    cbSub.Read();
                                                    while (cbSub.Read())
                                                    {
                                                        if (cbSub.NodeType != XmlNodeType.Element)
                                                            continue;
                                                        if (cbSub.LocalName == "label")
                                                            comboPart.Label =
                                                                cbSub.ReadElementContentAsString()
                                                                ?? string.Empty;
                                                        else if (cbSub.LocalName == "default")
                                                            comboPart.DefaultText =
                                                                cbSub.ReadElementContentAsString()
                                                                ?? string.Empty;
                                                        else if (cbSub.LocalName == "suggestion")
                                                        {
                                                            var s =
                                                                cbSub.ReadElementContentAsString()
                                                                ?? string.Empty;
                                                            comboPart.Suggestions.Add(s);
                                                        }
                                                    }
                                                    presPart = comboPart;
                                                    break;
                                                }
                                                case "dropdownList":
                                                {
                                                    var dropPart =
                                                        new DropDownPresentationElement();
                                                    var noSortStr = presSub.GetAttribute("noSort");
                                                    dropPart.NoSort = bool.TryParse(
                                                        noSortStr,
                                                        out var ns
                                                    )
                                                        ? ns
                                                        : false;
                                                    var defItem = presSub.GetAttribute(
                                                        "defaultItem"
                                                    );
                                                    if (
                                                        int.TryParse(
                                                            defItem,
                                                            NumberStyles.Integer,
                                                            CultureInfo.InvariantCulture,
                                                            out var di
                                                        )
                                                    )
                                                        dropPart.DefaultItemID = di;
                                                    dropPart.Label =
                                                        presSub.ReadElementContentAsString()
                                                        ?? string.Empty;
                                                    presPart = dropPart;
                                                    break;
                                                }
                                                case "listBox":
                                                {
                                                    var listPart = new ListPresentationElement();
                                                    // For empty elements like <listBox refId="..."/>, avoid ReadElementContentAsString which can disturb subtree iteration.
                                                    if (presSub.IsEmptyElement)
                                                    {
                                                        listPart.Label = string.Empty;
                                                        // Do not advance here; outer presSub.Read() will move to next sibling.
                                                    }
                                                    else
                                                    {
                                                        listPart.Label =
                                                            presSub.ReadElementContentAsString()
                                                            ?? string.Empty;
                                                    }
                                                    presPart = listPart;
                                                    break;
                                                }
                                                case "multiTextBox":
                                                {
                                                    var mt = new MultiTextPresentationElement();
                                                    mt.Label =
                                                        presSub.ReadElementContentAsString()
                                                        ?? string.Empty;
                                                    presPart = mt;
                                                    break;
                                                }
                                            }
                                            if (presPart is not null)
                                            {
                                                presPart.ID = refIdCached;
                                                presPart.ElementType = elemName;
                                                presentation.Elements.Add(presPart);
                                            }
                                        }
                                        adml.PresentationTable[presentation.Name] = presentation;
                                    }
                                }
                            }
                        }
                    }
                }
                // string table
                else if (reader.LocalName == "stringTable")
                {
                    using var st = reader.ReadSubtree();
                    st.Read();
                    while (st.Read())
                    {
                        if (st.NodeType != XmlNodeType.Element || st.LocalName != "string")
                            continue;
                        var key = st.GetAttribute("id");
                        if (string.IsNullOrEmpty(key))
                        {
                            st.Skip();
                            continue;
                        }
                        var value = st.ReadElementContentAsString() ?? string.Empty;
                        // Last one wins if duplicated
                        adml.StringTable[key] = value;
                    }
                }
                // presentations
                else if (reader.LocalName == "presentationTable")
                {
                    using var pt = reader.ReadSubtree();
                    pt.Read();
                    while (pt.Read())
                    {
                        if (pt.NodeType != XmlNodeType.Element || pt.LocalName != "presentation")
                            continue;
                        var id = pt.GetAttribute("id");
                        if (string.IsNullOrEmpty(id))
                        {
                            pt.Skip();
                            continue;
                        }
                        var presentation = new Presentation { Name = id };

                        using var presSub = pt.ReadSubtree();
                        presSub.Read();
                        while (presSub.Read())
                        {
                            if (presSub.NodeType != XmlNodeType.Element)
                                continue;

                            PresentationElement? presPart = null;
                            string elemName = presSub.LocalName ?? string.Empty;
                            // Cache refId before consuming inner content to avoid losing attribute after ReadElementContentAsString
                            string refIdCached = presSub.GetAttribute("refId") ?? string.Empty;

                            switch (elemName)
                            {
                                case "text":
                                {
                                    var textPart = new LabelPresentationElement();
                                    textPart.Text =
                                        presSub.ReadElementContentAsString() ?? string.Empty;
                                    presPart = textPart;
                                    break;
                                }

                                case "decimalTextBox":
                                {
                                    var decTextPart = new NumericBoxPresentationElement();
                                    var defStr = presSub.GetAttribute("defaultValue");
                                    var spinStr = presSub.GetAttribute("spin");
                                    var stepStr = presSub.GetAttribute("spinStep");
                                    if (
                                        uint.TryParse(
                                            defStr,
                                            NumberStyles.Integer,
                                            CultureInfo.InvariantCulture,
                                            out var def
                                        )
                                    )
                                        decTextPart.DefaultValue = def;
                                    else
                                        decTextPart.DefaultValue = 1;
                                    decTextPart.HasSpinner = bool.TryParse(spinStr, out var sp)
                                        ? sp
                                        : true;
                                    if (
                                        uint.TryParse(
                                            stepStr,
                                            NumberStyles.Integer,
                                            CultureInfo.InvariantCulture,
                                            out var step
                                        )
                                    )
                                        decTextPart.SpinnerIncrement = step;
                                    else
                                        decTextPart.SpinnerIncrement = 1;
                                    var label =
                                        presSub.ReadElementContentAsString() ?? string.Empty;
                                    decTextPart.Label = label;
                                    presPart = decTextPart;
                                    break;
                                }

                                case "textBox":
                                {
                                    var textPart = new TextBoxPresentationElement();
                                    using var tbSub = presSub.ReadSubtree();
                                    tbSub.Read();
                                    while (tbSub.Read())
                                    {
                                        if (tbSub.NodeType != XmlNodeType.Element)
                                            continue;
                                        if (tbSub.LocalName == "label")
                                        {
                                            textPart.Label =
                                                tbSub.ReadElementContentAsString() ?? string.Empty;
                                        }
                                        else if (tbSub.LocalName == "defaultValue")
                                        {
                                            textPart.DefaultValue =
                                                tbSub.ReadElementContentAsString() ?? string.Empty;
                                        }
                                    }
                                    presPart = textPart;
                                    break;
                                }

                                case "checkBox":
                                {
                                    var checkPart = new CheckBoxPresentationElement();
                                    var dc = presSub.GetAttribute("defaultChecked");
                                    checkPart.DefaultState = bool.TryParse(dc, out var b)
                                        ? b
                                        : false;
                                    checkPart.Text =
                                        presSub.ReadElementContentAsString() ?? string.Empty;
                                    presPart = checkPart;
                                    break;
                                }

                                case "comboBox":
                                {
                                    var comboPart = new ComboBoxPresentationElement();
                                    var noSortStr = presSub.GetAttribute("noSort");
                                    comboPart.NoSort = bool.TryParse(noSortStr, out var ns)
                                        ? ns
                                        : false;
                                    using var cbSub = presSub.ReadSubtree();
                                    cbSub.Read();
                                    while (cbSub.Read())
                                    {
                                        if (cbSub.NodeType != XmlNodeType.Element)
                                            continue;
                                        if (cbSub.LocalName == "label")
                                        {
                                            comboPart.Label =
                                                cbSub.ReadElementContentAsString() ?? string.Empty;
                                        }
                                        else if (cbSub.LocalName == "default")
                                        {
                                            comboPart.DefaultText =
                                                cbSub.ReadElementContentAsString() ?? string.Empty;
                                        }
                                        else if (cbSub.LocalName == "suggestion")
                                        {
                                            var s =
                                                cbSub.ReadElementContentAsString() ?? string.Empty;
                                            comboPart.Suggestions.Add(s);
                                        }
                                    }
                                    presPart = comboPart;
                                    break;
                                }

                                case "dropdownList":
                                {
                                    var dropPart = new DropDownPresentationElement();
                                    var noSortStr = presSub.GetAttribute("noSort");
                                    dropPart.NoSort = bool.TryParse(noSortStr, out var ns)
                                        ? ns
                                        : false;
                                    var defItem = presSub.GetAttribute("defaultItem");
                                    if (
                                        int.TryParse(
                                            defItem,
                                            NumberStyles.Integer,
                                            CultureInfo.InvariantCulture,
                                            out var di
                                        )
                                    )
                                        dropPart.DefaultItemID = di;
                                    dropPart.Label =
                                        presSub.ReadElementContentAsString() ?? string.Empty;
                                    presPart = dropPart;
                                    break;
                                }

                                case "listBox":
                                {
                                    var listPart = new ListPresentationElement();
                                    if (presSub.IsEmptyElement)
                                    {
                                        listPart.Label = string.Empty;
                                    }
                                    else
                                    {
                                        listPart.Label =
                                            presSub.ReadElementContentAsString() ?? string.Empty;
                                    }
                                    presPart = listPart;
                                    break;
                                }

                                case "multiTextBox":
                                {
                                    var mt = new MultiTextPresentationElement();
                                    mt.Label = presSub.ReadElementContentAsString() ?? string.Empty;
                                    presPart = mt;
                                    break;
                                }
                            }

                            if (presPart is not null)
                            {
                                presPart.ID = refIdCached;
                                presPart.ElementType = elemName;
                                presentation.Elements.Add(presPart);
                            }
                        }

                        adml.PresentationTable[presentation.Name] = presentation;
                    }
                }
            }

            // Merge DOM-parsed resources into the model to guarantee completeness even if streaming missed entries.
            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.Load(File);
                var policyDefinitionResourcesList = xmlDoc.GetElementsByTagName(
                    "policyDefinitionResources"
                );
                if (policyDefinitionResourcesList.Count > 0)
                {
                    var policyDefinitionResources = policyDefinitionResourcesList[0];
                    var revisionAttr = policyDefinitionResources?.Attributes?["revision"]?.Value;
                    if (
                        !string.IsNullOrEmpty(revisionAttr)
                        && decimal.TryParse(
                            revisionAttr,
                            NumberStyles.Number,
                            CultureInfo.InvariantCulture,
                            out var rev
                        )
                    )
                        adml.Revision = rev;
                    foreach (XmlNode child in policyDefinitionResources!.ChildNodes)
                    {
                        if (child.LocalName == "displayName")
                            adml.DisplayName = child.InnerText ?? string.Empty;
                        else if (child.LocalName == "description")
                            adml.Description = child.InnerText ?? string.Empty;
                        else if (child.LocalName == "resources")
                        {
                            var stringTableList = ((XmlElement)child).GetElementsByTagName(
                                "stringTable"
                            );
                            if (stringTableList.Count > 0)
                            {
                                foreach (XmlNode stringElement in stringTableList[0]!.ChildNodes)
                                {
                                    if (stringElement.LocalName != "string")
                                        continue;
                                    var idAttr = stringElement.Attributes?["id"];
                                    if (idAttr is null)
                                        continue;
                                    // Merge; keep existing if already set by streaming parser.
                                    if (!adml.StringTable.ContainsKey(idAttr.Value))
                                        adml.StringTable[idAttr.Value] =
                                            stringElement.InnerText ?? string.Empty;
                                }
                            }
                            var presTableList = ((XmlElement)child).GetElementsByTagName(
                                "presentationTable"
                            );
                            if (presTableList.Count > 0)
                            {
                                foreach (XmlNode presElement in presTableList[0]!.ChildNodes)
                                {
                                    if (presElement.LocalName != "presentation")
                                        continue;
                                    var presentation = new Presentation();
                                    var presIdAttr = presElement.Attributes?["id"];
                                    if (presIdAttr is null)
                                        continue;
                                    presentation.Name = presIdAttr.Value ?? string.Empty;
                                    Presentation targetPres;
                                    if (
                                        adml.PresentationTable.TryGetValue(
                                            presentation.Name,
                                            out var existingPres
                                        )
                                    )
                                    {
                                        // Merge missing parts into existing presentation parsed by streaming path.
                                        targetPres = existingPres;
                                    }
                                    else
                                    {
                                        targetPres = presentation;
                                    }

                                    foreach (XmlNode uiElement in presElement.ChildNodes)
                                    {
                                        PresentationElement? presPart = null;
                                        switch (uiElement.LocalName ?? string.Empty)
                                        {
                                            case "text":
                                            {
                                                var textPart = new LabelPresentationElement();
                                                textPart.Text = uiElement.InnerText ?? string.Empty;
                                                presPart = textPart;
                                                break;
                                            }
                                            case "decimalTextBox":
                                            {
                                                var dec = new NumericBoxPresentationElement();
                                                dec.DefaultValue = Convert.ToUInt32(
                                                    uiElement.AttributeOrDefault("defaultValue", 1),
                                                    CultureInfo.InvariantCulture
                                                );
                                                dec.HasSpinner = Convert.ToBoolean(
                                                    uiElement.AttributeOrDefault("spin", true)
                                                );
                                                dec.SpinnerIncrement = Convert.ToUInt32(
                                                    uiElement.AttributeOrDefault("spinStep", 1),
                                                    CultureInfo.InvariantCulture
                                                );
                                                dec.Label = uiElement.InnerText ?? string.Empty;
                                                presPart = dec;
                                                break;
                                            }
                                            case "textBox":
                                            {
                                                var tb = new TextBoxPresentationElement();
                                                foreach (
                                                    XmlNode textboxInfo in uiElement.ChildNodes
                                                )
                                                {
                                                    if (textboxInfo.LocalName == "label")
                                                        tb.Label =
                                                            textboxInfo.InnerText ?? string.Empty;
                                                    else if (
                                                        textboxInfo.LocalName == "defaultValue"
                                                    )
                                                        tb.DefaultValue =
                                                            textboxInfo.InnerText ?? string.Empty;
                                                }
                                                presPart = tb;
                                                break;
                                            }
                                            case "checkBox":
                                            {
                                                var cb = new CheckBoxPresentationElement();
                                                cb.DefaultState = Convert.ToBoolean(
                                                    uiElement.AttributeOrDefault(
                                                        "defaultChecked",
                                                        false
                                                    )
                                                );
                                                cb.Text = uiElement.InnerText ?? string.Empty;
                                                presPart = cb;
                                                break;
                                            }
                                            case "comboBox":
                                            {
                                                var combo = new ComboBoxPresentationElement();
                                                combo.NoSort = Convert.ToBoolean(
                                                    uiElement.AttributeOrDefault("noSort", false)
                                                );
                                                foreach (XmlNode comboInfo in uiElement.ChildNodes)
                                                {
                                                    if (comboInfo.LocalName == "label")
                                                        combo.Label =
                                                            comboInfo.InnerText ?? string.Empty;
                                                    else if (comboInfo.LocalName == "default")
                                                        combo.DefaultText =
                                                            comboInfo.InnerText ?? string.Empty;
                                                    else if (comboInfo.LocalName == "suggestion")
                                                        combo.Suggestions.Add(
                                                            comboInfo.InnerText ?? string.Empty
                                                        );
                                                }
                                                presPart = combo;
                                                break;
                                            }
                                            case "dropdownList":
                                            {
                                                var drop = new DropDownPresentationElement();
                                                drop.NoSort = Convert.ToBoolean(
                                                    uiElement.AttributeOrDefault("noSort", false)
                                                );
                                                drop.DefaultItemID = int.TryParse(
                                                    uiElement.AttributeOrNull("defaultItem"),
                                                    out int num
                                                )
                                                    ? num
                                                    : null;
                                                drop.Label = uiElement.InnerText ?? string.Empty;
                                                presPart = drop;
                                                break;
                                            }
                                            case "listBox":
                                            {
                                                var list = new ListPresentationElement();
                                                list.Label = uiElement.InnerText ?? string.Empty;
                                                presPart = list;
                                                break;
                                            }
                                            case "multiTextBox":
                                            {
                                                var mt = new MultiTextPresentationElement();
                                                mt.Label = uiElement.InnerText ?? string.Empty;
                                                presPart = mt;
                                                break;
                                            }
                                        }
                                        if (presPart is object)
                                        {
                                            if (
                                                uiElement.Attributes?[("refId")]
                                                is XmlAttribute refAttr
                                            )
                                                presPart.ID = refAttr.Value ?? string.Empty;
                                            presPart.ElementType =
                                                uiElement.LocalName ?? string.Empty;
                                            // Add only if not already present (by ID and type)
                                            if (
                                                !targetPres.Elements.Any(e =>
                                                    string.Equals(
                                                        e.ID,
                                                        presPart.ID,
                                                        StringComparison.OrdinalIgnoreCase
                                                    )
                                                    && string.Equals(
                                                        e.ElementType,
                                                        presPart.ElementType,
                                                        StringComparison.OrdinalIgnoreCase
                                                    )
                                                )
                                            )
                                            {
                                                targetPres.Elements.Add(presPart);
                                            }
                                        }
                                    }

                                    adml.PresentationTable[presentation.Name] = targetPres;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return adml;
        }
    }
}
