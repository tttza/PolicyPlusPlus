using PolicyPlusCore.Core;
using PolicyPlusCore.Utils;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace PolicyPlusCore.Admx
{
    public class AdmxFile
    {
    public string SourceFile = string.Empty;
    public string AdmxNamespace = string.Empty;
    public string SupersededAdm = string.Empty;
        public decimal MinAdmlVersion;
        public Dictionary<string, string> Prefixes = new Dictionary<string, string>();
        public List<AdmxProduct> Products = new List<AdmxProduct>();
        public List<AdmxSupportDefinition> SupportedOnDefinitions = new List<AdmxSupportDefinition>();
        public List<AdmxCategory> Categories = new List<AdmxCategory>();
        public List<AdmxPolicy> Policies = new List<AdmxPolicy>();

        public AdmxFile()
        {
        }

        public static AdmxFile Load(string File)
        {
            // ADMX documentation: https://technet.microsoft.com/en-us/library/cc772138(v=ws.10).aspx
            var admx = new AdmxFile();
            admx.SourceFile = File;
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(File);
            var policyDefinitionsList = xmlDoc.GetElementsByTagName("policyDefinitions");
            if (policyDefinitionsList.Count == 0)
                return admx;
            var policyDefinitions = policyDefinitionsList[0];
            if (policyDefinitions is null)
                return admx;
            foreach (XmlNode child in policyDefinitions.ChildNodes)
            {
                switch (child.LocalName ?? "")
                {
                    case "policyNamespaces": // Referenced namespaces and current namespace
                        {
                            foreach (XmlNode policyNamespace in child.ChildNodes)
                            {
                                var prefixAttr = policyNamespace.Attributes?["prefix"]?.Value;
                                var nsAttr = policyNamespace.Attributes?["namespace"]?.Value;
                                if (!string.IsNullOrEmpty(nsAttr) && policyNamespace.LocalName == "target")
                                    admx.AdmxNamespace = nsAttr;
                                if (!string.IsNullOrEmpty(prefixAttr) && !string.IsNullOrEmpty(nsAttr))
                                {
                                    if (!admx.Prefixes.ContainsKey(prefixAttr))
                                        admx.Prefixes.Add(prefixAttr, nsAttr);
                                    else
                                        admx.Prefixes[prefixAttr] = nsAttr;
                                }
                            }

                            break;
                        }

                    case "supersededAdm": // The ADM file that this ADMX supersedes
                        {
                            var fnAttr = child.Attributes?["fileName"]?.Value;
                            admx.SupersededAdm = fnAttr ?? string.Empty;
                            break;
                        }

                    case "resources": // Minimum required version
                        {
                            var minReq = child.Attributes?["minRequiredRevision"]?.Value;
                            if (!string.IsNullOrEmpty(minReq) && decimal.TryParse(minReq, NumberStyles.Number, CultureInfo.InvariantCulture, out var minRev))
                                admx.MinAdmlVersion = minRev;
                            break;
                        }

                    case "supportedOn": // Support definitions
                        {
                            foreach (XmlNode supportInfo in child.ChildNodes)
                            {
                                if (supportInfo.LocalName == "definitions")
                                {
                                    foreach (XmlNode supportDef in supportInfo.ChildNodes)
                                    {
                                        if (supportDef.LocalName != "definition")
                                            continue;
                                        var definition = new AdmxSupportDefinition();
                                        var defName = supportDef.Attributes?["name"]?.Value;
                                        var defDisp = supportDef.Attributes?["displayName"]?.Value;
                                        if (string.IsNullOrEmpty(defName) || string.IsNullOrEmpty(defDisp))
                                            continue;
                                        definition.ID = defName;
                                        definition.DisplayCode = defDisp;
                                        definition.Logic = AdmxSupportLogicType.Blank;
                                        foreach (XmlNode logicElement in supportDef.ChildNodes)
                                        {
                                            bool canLoad = true;
                                            if (logicElement.LocalName == "or")
                                            {
                                                definition.Logic = AdmxSupportLogicType.AnyOf;
                                            }
                                            else if (logicElement.LocalName == "and")
                                            {
                                                definition.Logic = AdmxSupportLogicType.AllOf;
                                            }
                                            else
                                            {
                                                canLoad = false;
                                            }

                                            if (canLoad)
                                            {
                                                definition.Entries = new List<AdmxSupportEntry>();
                                                foreach (XmlNode conditionElement in logicElement.ChildNodes)
                                                {
                                                    if (conditionElement.LocalName == "reference")
                                                    {
                                                        var product = conditionElement.Attributes?["ref"]?.Value;
                                                        if (!string.IsNullOrEmpty(product))
                                                            definition.Entries.Add(new AdmxSupportEntry() { ProductID = product, IsRange = false });
                                                    }
                                                    else if (conditionElement.LocalName == "range")
                                                    {
                                                        var entry = new AdmxSupportEntry() { IsRange = true };
                                                        entry.ProductID = conditionElement.Attributes?["ref"]?.Value ?? string.Empty;
                                                        var maxVerAttr = conditionElement.Attributes?["maxVersionIndex"]?.Value;
                                                        if (!string.IsNullOrEmpty(maxVerAttr) && int.TryParse(maxVerAttr, out var maxVer))
                                                            entry.MaxVersion = maxVer;
                                                        var minVerAttr = conditionElement.Attributes?["minVersionIndex"]?.Value;
                                                        if (!string.IsNullOrEmpty(minVerAttr) && int.TryParse(minVerAttr, out var minVer))
                                                            entry.MinVersion = minVer;
                                                        definition.Entries.Add(entry);
                                                    }
                                                }

                                                break;
                                            }
                                        }

                                        definition.DefinedIn = admx;
                                        admx.SupportedOnDefinitions.Add(definition);
                                    }
                                }
                                else if (supportInfo.LocalName == "products") // Product definitions
                                {
                                    void loadProducts(XmlNode Node, string ChildTagName, AdmxProduct? Parent)
                                    {
                                        foreach (XmlNode subproductElement in Node.ChildNodes)
                                        {
                                            if ((subproductElement.LocalName ?? "") != (ChildTagName ?? "")) continue;
                                            var product = new AdmxProduct();
                                            var pname = subproductElement.Attributes?["name"]?.Value;
                                            var pdisp = subproductElement.Attributes?["displayName"]?.Value;
                                            if (string.IsNullOrEmpty(pname) || string.IsNullOrEmpty(pdisp))
                                                continue;
                                            product.ID = pname; product.DisplayCode = pdisp;
                                            if (Parent is not null)
                                            {
                                                var verStr = subproductElement.Attributes?["versionIndex"]?.Value;
                                                if (!string.IsNullOrEmpty(verStr) && int.TryParse(verStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ver))
                                                    product.Version = ver;
                                            }
                                            product.Parent = Parent; product.DefinedIn = admx;
                                            admx.Products.Add(product);
                                            if (Parent is null)
                                            {
                                                product.Type = AdmxProductType.Product; loadProducts(subproductElement, "majorVersion", product);
                                            }
                                            else if (Parent.Parent is null)
                                            {
                                                product.Type = AdmxProductType.MajorRevision; loadProducts(subproductElement, "minorVersion", product);
                                            }
                                            else
                                            {
                                                product.Type = AdmxProductType.MinorRevision;
                                            }
                                        }
                                    }
                                    loadProducts(supportInfo, "product", null); // Start the recursive load
                                }
                            }

                            break;
                        }

                    case "categories": // Categories
                        {
                            foreach (XmlNode categoryElement in child.ChildNodes)
                            {
                                if (categoryElement.LocalName != "category")
                                    continue;
                                var category = new AdmxCategory();
                                var cname = categoryElement.Attributes?["name"]?.Value;
                                var cdisp = categoryElement.Attributes?["displayName"]?.Value;
                                if (string.IsNullOrEmpty(cname) || string.IsNullOrEmpty(cdisp))
                                    continue;
                                category.ID = cname;
                                category.DisplayCode = cdisp;
                                category.ExplainCode = categoryElement.AttributeOrNull("explainText");
                                if (categoryElement.HasChildNodes)
                                {
                                    var parentCatElement = categoryElement["parentCategory"];
                                    var pref = parentCatElement?.Attributes?["ref"]?.Value;
                                    if (!string.IsNullOrEmpty(pref))
                                        category.ParentID = pref;
                                }

                                category.DefinedIn = admx;
                                admx.Categories.Add(category);
                            }

                            break;
                        }

                    case "policies": // Policy settings
                        {
                            PolicyRegistryValue loadRegItem(XmlNode Node)
                            {
                                var regItem = new PolicyRegistryValue();
                                foreach (XmlNode subElement in Node.ChildNodes)
                                {
                                    if (subElement.LocalName == "delete")
                                    {
                                        regItem.RegistryType = PolicyRegistryValueType.Delete;
                                        break;
                                    }
                                    else if (subElement.LocalName == "decimal")
                                    {
                                        regItem.RegistryType = PolicyRegistryValueType.Numeric;
                                        var valStr = subElement.Attributes?["value"]?.Value;
                                        if (!string.IsNullOrEmpty(valStr))
                                            regItem.NumberValue = uint.Parse(valStr, CultureInfo.InvariantCulture);
                                        break;
                                    }
                                    else if (subElement.LocalName == "string")
                                    {
                                        regItem.RegistryType = PolicyRegistryValueType.Text;
                                        regItem.StringValue = subElement.InnerText ?? string.Empty;
                                        break;
                                    }
                                }

                                return regItem;
                            };
                            PolicyRegistrySingleList loadOneRegList(XmlNode Node)
                            {
                                var singleList = new PolicyRegistrySingleList();
                                singleList.DefaultRegistryKey = Node.AttributeOrNull("defaultKey");
                                singleList.AffectedValues = new List<PolicyRegistryListEntry>();
                                foreach (XmlNode itemElement in Node.ChildNodes)
                                {
                                    if (itemElement.LocalName != "item")
                                        continue;
                                    var listEntry = new PolicyRegistryListEntry();
                                    var valueName = itemElement.Attributes?["valueName"]?.Value;
                                    listEntry.RegistryValue = valueName ?? string.Empty;
                                    listEntry.RegistryKey = itemElement.AttributeOrNull("key");
                                    foreach (XmlNode valElement in itemElement.ChildNodes)
                                    {
                                        if (valElement.LocalName == "value")
                                        {
                                            listEntry.Value = loadRegItem(valElement);
                                            break;
                                        }
                                    }

                                    singleList.AffectedValues.Add(listEntry);
                                }

                                return singleList;
                            };
                            PolicyRegistryList loadOnOffValList(string OnValueName, string OffValueName, string OnListName, string OffListName, XmlNode Node)
                            {
                                var regList = new PolicyRegistryList();
                                foreach (XmlNode subElement in Node.ChildNodes)
                                {
                                    if ((subElement.Name ?? "") == (OnValueName ?? ""))
                                    {
                                        regList.OnValue = loadRegItem(subElement);
                                    }
                                    else if ((subElement.Name ?? "") == (OffValueName ?? ""))
                                    {
                                        regList.OffValue = loadRegItem(subElement);
                                    }
                                    else if ((subElement.Name ?? "") == (OnListName ?? ""))
                                    {
                                        regList.OnValueList = loadOneRegList(subElement);
                                    }
                                    else if ((subElement.Name ?? "") == (OffListName ?? ""))
                                    {
                                        regList.OffValueList = loadOneRegList(subElement);
                                    }
                                }

                                return regList;
                            };
                            foreach (XmlNode polElement in child.ChildNodes)
                            {
                                if (polElement.LocalName != "policy")
                                    continue;
                                var policy = new AdmxPolicy();
                                var polName = polElement.Attributes?["name"]?.Value;
                                if (string.IsNullOrEmpty(polName))
                                    continue;
                                policy.ID = polName;
                                policy.DefinedIn = admx;
                                policy.DisplayCode = polElement.Attributes?["displayName"]?.Value ?? string.Empty;
                                policy.RegistryKey = polElement.Attributes?["key"]?.Value ?? string.Empty;
                                string polClass = polElement.Attributes?["class"]?.Value ?? string.Empty;
                                switch (polClass ?? "")
                                {
                                    case "Machine":
                                        {
                                            policy.Section = AdmxPolicySection.Machine;
                                            break;
                                        }

                                    case "User":
                                        {
                                            policy.Section = AdmxPolicySection.User;
                                            break;
                                        }

                                    default:
                                        {
                                            policy.Section = AdmxPolicySection.Both;
                                            break;
                                        }
                                }

                                policy.ExplainCode = polElement.AttributeOrNull("explainText");
                                policy.PresentationID = polElement.AttributeOrNull("presentation");
                                policy.ClientExtension = polElement.AttributeOrNull("clientExtension") ?? string.Empty;
                                policy.RegistryValue = polElement.AttributeOrNull("valueName") ?? string.Empty;
                                policy.AffectedValues = loadOnOffValList("enabledValue", "disabledValue", "enabledList", "disabledList", polElement);
                                foreach (XmlNode polInfo in polElement.ChildNodes)
                                {
                                    switch (polInfo.LocalName ?? "")
                                    {
                                        case "parentCategory":
                                            {
                                                policy.CategoryID = polInfo.Attributes?["ref"]?.Value ?? string.Empty;
                                                break;
                                            }

                                        case "supportedOn":
                                            {
                                                policy.SupportedCode = polInfo.Attributes?["ref"]?.Value ?? string.Empty;
                                                break;
                                            }

                                        case "elements":
                                            {
                                                policy.Elements = new List<PolicyElement>();
                                                foreach (XmlNode uiElement in polInfo.ChildNodes)
                                                {
                                                    PolicyElement? entry = null;
                                                    switch (uiElement.LocalName ?? "")
                                                    {
                                                        case "decimal":
                                                            {
                                                                var decimalEntry = new DecimalPolicyElement();
                                                                decimalEntry.Minimum = Convert.ToUInt32(uiElement.AttributeOrDefault("minValue", 0), CultureInfo.InvariantCulture);
                                                                decimalEntry.Maximum = Convert.ToUInt32(uiElement.AttributeOrDefault("maxValue", uint.MaxValue), CultureInfo.InvariantCulture);
                                                                decimalEntry.NoOverwrite = Convert.ToBoolean(uiElement.AttributeOrDefault("soft", false));
                                                                decimalEntry.StoreAsText = Convert.ToBoolean(uiElement.AttributeOrDefault("storeAsText", false));
                                                                entry = decimalEntry;
                                                                break;
                                                            }

                                                        case "boolean":
                                                            {
                                                                var boolEntry = new BooleanPolicyElement();
                                                                boolEntry.AffectedRegistry = loadOnOffValList("trueValue", "falseValue", "trueList", "falseList", uiElement);
                                                                entry = boolEntry;
                                                                break;
                                                            }

                                                        case "text":
                                                            {
                                                                var textEntry = new TextPolicyElement();
                                                                textEntry.MaxLength = Convert.ToInt32(uiElement.AttributeOrDefault("maxLength", 255), CultureInfo.InvariantCulture);
                                                                textEntry.Required = Convert.ToBoolean(uiElement.AttributeOrDefault("required", false));
                                                                textEntry.RegExpandSz = Convert.ToBoolean(uiElement.AttributeOrDefault("expandable", false));
                                                                textEntry.NoOverwrite = Convert.ToBoolean(uiElement.AttributeOrDefault("soft", false));
                                                                entry = textEntry;
                                                                break;
                                                            }

                                                        case "list":
                                                            {
                                                                var listEntry = new ListPolicyElement();
                                                                listEntry.NoPurgeOthers = Convert.ToBoolean(uiElement.AttributeOrDefault("additive", false));
                                                                listEntry.RegExpandSz = Convert.ToBoolean(uiElement.AttributeOrDefault("expandable", false));
                                                                listEntry.UserProvidesNames = Convert.ToBoolean(uiElement.AttributeOrDefault("explicitValue", false));
                                                                listEntry.HasPrefix = uiElement.Attributes?["valuePrefix"] is object;
                                                                listEntry.RegistryValue = uiElement.AttributeOrNull("valuePrefix") ?? string.Empty;
                                                                entry = listEntry;
                                                                break;
                                                            }

                                                        case "enum":
                                                            {
                                                                var enumEntry = new EnumPolicyElement();
                                                                enumEntry.Required = Convert.ToBoolean(uiElement.AttributeOrDefault("required", false));
                                                                enumEntry.Items = new List<EnumPolicyElementItem>();
                                                                foreach (XmlNode itemElement in uiElement.ChildNodes)
                                                                {
                                                                    if (itemElement.LocalName == "item")
                                                                    {
                                                                        var enumItem = new EnumPolicyElementItem();
                                                                        enumItem.DisplayCode = itemElement.Attributes?["displayName"]?.Value ?? string.Empty;
                                                                        foreach (XmlNode valElement in itemElement.ChildNodes)
                                                                        {
                                                                            if (valElement.LocalName == "value")
                                                                            {
                                                                                enumItem.Value = loadRegItem(valElement);
                                                                            }
                                                                            else if (valElement.LocalName == "valueList")
                                                                            {
                                                                                enumItem.ValueList = loadOneRegList(valElement);
                                                                            }
                                                                        }

                                                                        enumEntry.Items.Add(enumItem);
                                                                    }
                                                                }

                                                                entry = enumEntry;
                                                                break;
                                                            }

                                                        case "multiText":
                                                            {
                                                                entry = new MultiTextPolicyElement();
                                                                break;
                                                            }
                                                    }

                                                    if (entry is object)
                                                    {
                                                        entry.ClientExtension = uiElement.AttributeOrNull("clientExtension") ?? string.Empty;
                                                        entry.RegistryKey = uiElement.AttributeOrNull("key") ?? string.Empty;
                                                        if (string.IsNullOrEmpty(entry.RegistryValue))
                                                            entry.RegistryValue = uiElement.AttributeOrNull("valueName") ?? entry.RegistryValue;
                                                        var idAttr = uiElement.Attributes?["id"]?.Value;
                                                        if (string.IsNullOrEmpty(idAttr))
                                                        {
                                                            // Skip elements without an ID to avoid malformed state
                                                            continue;
                                                        }
                                                        entry.ID = idAttr;
                                                        entry.ElementType = uiElement.LocalName ?? string.Empty;
                                                        policy.Elements.Add(entry);
                                                    }
                                                }

                                                break;
                                            }
                                    }
                                }

                                admx.Policies.Add(policy);
                            }

                            break;
                        }
                }
            }

            return admx;
        }
    }
}
