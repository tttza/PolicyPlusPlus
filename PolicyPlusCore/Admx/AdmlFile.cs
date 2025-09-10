using PolicyPlus.Core.Core;
using PolicyPlus.Core.Utils;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace PolicyPlus.Core.Admx
{
    public class AdmlFile
    {
    public string SourceFile = string.Empty;
        public decimal Revision;
    public string DisplayName = string.Empty;
    public string Description = string.Empty;
        public Dictionary<string, string> StringTable = new Dictionary<string, string>();
        public Dictionary<string, Presentation> PresentationTable = new Dictionary<string, Presentation>();

        private AdmlFile()
        {
        }

        public static AdmlFile Load(string File)
        {
            // ADML documentation: https://technet.microsoft.com/en-us/library/cc772050(v=ws.10).aspx
            var adml = new AdmlFile();
            adml.SourceFile = File;
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(File);
            // Load ADML metadata
            var policyDefinitionResourcesList = xmlDoc.GetElementsByTagName("policyDefinitionResources");
            if (policyDefinitionResourcesList.Count == 0)
                return adml;
            var policyDefinitionResources = policyDefinitionResourcesList[0];
            if (policyDefinitionResources is null)
                return adml;
            var revisionAttr = policyDefinitionResources.Attributes?["revision"]?.Value;
            if (!string.IsNullOrEmpty(revisionAttr) && decimal.TryParse(revisionAttr, NumberStyles.Number, CultureInfo.InvariantCulture, out var rev))
                adml.Revision = rev;
            foreach (XmlNode child in policyDefinitionResources.ChildNodes)
            {
                switch (child.LocalName ?? "")
                {
                    case "displayName":
                        {
                            adml.DisplayName = child.InnerText ?? string.Empty;
                            break;
                        }

                    case "description":
                        {
                            adml.Description = child.InnerText ?? string.Empty;
                            break;
                        }
                }
            }
            // Load localized strings
            var stringTableList = xmlDoc.GetElementsByTagName("stringTable");
            if (stringTableList.Count > 0)
            {
                var stringTable = stringTableList[0];
                if (stringTable is not null)
                {
                    foreach (XmlNode stringElement in stringTable.ChildNodes)
                    {
                    if (stringElement.LocalName != "string")
                        continue;
                    var idAttr = stringElement.Attributes?["id"]; if (idAttr is null) continue;
                    string key = idAttr.Value;
                    string value = stringElement.InnerText ?? string.Empty;
                    adml.StringTable.Add(key, value);
                    }
                }
            }
            // Load presentations (UI arrangements)
            var presTableList = xmlDoc.GetElementsByTagName("presentationTable");
            if (presTableList.Count > 0)
            {
                var presTable = presTableList[0];
                if (presTable is not null)
                {
                    foreach (XmlNode presElement in presTable.ChildNodes)
                    {
                    if (presElement.LocalName != "presentation")
                        continue;
                    var presentation = new Presentation();
                    var presIdAttr = presElement.Attributes?["id"]; if (presIdAttr is null) continue;
                    presentation.Name = presIdAttr.Value ?? string.Empty;
                    foreach (XmlNode uiElement in presElement.ChildNodes)
                    {
                        PresentationElement? presPart = null;
                        switch (uiElement.LocalName ?? "")
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
                                    var decTextPart = new NumericBoxPresentationElement();
                                    decTextPart.DefaultValue = Convert.ToUInt32(uiElement.AttributeOrDefault("defaultValue", 1), CultureInfo.InvariantCulture);
                                    decTextPart.HasSpinner = Convert.ToBoolean(uiElement.AttributeOrDefault("spin", true));
                                    decTextPart.SpinnerIncrement = Convert.ToUInt32(uiElement.AttributeOrDefault("spinStep", 1), CultureInfo.InvariantCulture);
                                    decTextPart.Label = uiElement.InnerText ?? string.Empty;
                                    presPart = decTextPart;
                                    break;
                                }

                            case "textBox":
                                {
                                    var textPart = new TextBoxPresentationElement();
                                    foreach (XmlNode textboxInfo in uiElement.ChildNodes)
                                    {
                                        switch (textboxInfo.LocalName ?? "")
                                        {
                                            case "label":
                                                {
                                                    textPart.Label = textboxInfo.InnerText ?? string.Empty;
                                                    break;
                                                }

                                            case "defaultValue":
                                                {
                                                    textPart.DefaultValue = textboxInfo.InnerText ?? string.Empty;
                                                    break;
                                                }
                                        }
                                    }

                                    presPart = textPart;
                                    break;
                                }

                            case "checkBox":
                                {
                                    var checkPart = new CheckBoxPresentationElement();
                                    checkPart.DefaultState = Convert.ToBoolean(uiElement.AttributeOrDefault("defaultChecked", false));
                                    checkPart.Text = uiElement.InnerText ?? string.Empty;
                                    presPart = checkPart;
                                    break;
                                }

                            case "comboBox":
                                {
                                    var comboPart = new ComboBoxPresentationElement();
                                    comboPart.NoSort = Convert.ToBoolean(uiElement.AttributeOrDefault("noSort", false));
                                    foreach (XmlNode comboInfo in uiElement.ChildNodes)
                                    {
                                        switch (comboInfo.LocalName ?? "")
                                        {
                                            case "label":
                                                {
                                                    comboPart.Label = comboInfo.InnerText ?? string.Empty;
                                                    break;
                                                }

                                            case "default":
                                                {
                                                    comboPart.DefaultText = comboInfo.InnerText ?? string.Empty;
                                                    break;
                                                }

                                            case "suggestion":
                                                {
                                                    comboPart.Suggestions.Add(comboInfo.InnerText ?? string.Empty);
                                                    break;
                                                }
                                        }
                                    }

                                    presPart = comboPart;
                                    break;
                                }

                            case "dropdownList":
                                {
                                    var dropPart = new DropDownPresentationElement();
                                    dropPart.NoSort = Convert.ToBoolean(uiElement.AttributeOrDefault("noSort", false));
                                    dropPart.DefaultItemID = int.TryParse(uiElement.AttributeOrNull("defaultItem"), out int num) ? num : null;
                                    dropPart.Label = uiElement.InnerText ?? string.Empty;
                                    presPart = dropPart;
                                    break;
                                }

                            case "listBox":
                                {
                                    var listPart = new ListPresentationElement();
                                    listPart.Label = uiElement.InnerText ?? string.Empty;
                                    presPart = listPart;
                                    break;
                                }

                            case "multiTextBox":
                                {
                                    var multiTextPart = new MultiTextPresentationElement();
                                    multiTextPart.Label = uiElement.InnerText ?? string.Empty;
                                    presPart = multiTextPart;
                                    break;
                                }
                        }

                        if (presPart is object)
                        {
                            if (uiElement.Attributes?["refId"] is XmlAttribute refAttr)
                                presPart.ID = refAttr.Value ?? string.Empty;
                            presPart.ElementType = uiElement.LocalName ?? string.Empty;
                            presentation.Elements.Add(presPart);
                        }
                    }

                        adml.PresentationTable.Add(presentation.Name, presentation);
                    }
                }
            }

            return adml;
        }
    }
}
