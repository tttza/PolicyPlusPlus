using System.Globalization;
using PolicyPlusCore.Core;

namespace PolicyPlusCore.Admx
{
    public class AdmxBundle
    {
        // Controls whether language fallback (OS UI culture, en-US, en, generic scan) is attempted
        public bool EnableLanguageFallback { get; set; } = true;

        private Dictionary<AdmxFile, AdmlFile> SourceFiles = new Dictionary<AdmxFile, AdmlFile>();
        private Dictionary<string, AdmxFile> Namespaces = new Dictionary<string, AdmxFile>();

        // Cache for last-resort external ADML string lookups (path -> string table)
        private readonly Dictionary<string, Dictionary<string, string>> _externalAdmlStringCache =
            new(StringComparer.OrdinalIgnoreCase);

        // Caches to avoid repeated directory enumeration and File.Exists calls during batch loads.
        private readonly Dictionary<string, string[]> _dirSubdirsCache = new(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly Dictionary<string, bool> _fileExistsCache = new(
            StringComparer.OrdinalIgnoreCase
        );

        private string[] GetSubdirectories(string? dir)
        {
            if (string.IsNullOrEmpty(dir))
                return Array.Empty<string>();
            if (_dirSubdirsCache.TryGetValue(dir, out var cached))
                return cached;
            string[] subs;
            try
            {
                subs = Directory.EnumerateDirectories(dir).ToArray();
            }
            catch
            {
                subs = Array.Empty<string>();
            }
            _dirSubdirsCache[dir] = subs;
            return subs;
        }

        private bool CachedFileExists(string path)
        {
            if (_fileExistsCache.TryGetValue(path, out var exists))
                return exists;
            bool e;
            try
            {
                e = File.Exists(path);
            }
            catch
            {
                e = false;
            }
            _fileExistsCache[path] = e;
            return e;
        }

        // Temporary lists from ADMX files that haven't been integrated yet
        private List<AdmxCategory> RawCategories = new List<AdmxCategory>();
        private List<AdmxProduct> RawProducts = new List<AdmxProduct>();
        private List<AdmxPolicy> RawPolicies = new List<AdmxPolicy>();
        private List<AdmxSupportDefinition> RawSupport = new List<AdmxSupportDefinition>();

        // Lists that include all items, even those that are children of others
        public Dictionary<string, PolicyPlusCategory> FlatCategories =
            new Dictionary<string, PolicyPlusCategory>();
        public Dictionary<string, PolicyPlusProduct> FlatProducts =
            new Dictionary<string, PolicyPlusProduct>();

        // Lists of top-level items only
        public Dictionary<string, PolicyPlusCategory> Categories =
            new Dictionary<string, PolicyPlusCategory>();
        public Dictionary<string, PolicyPlusProduct> Products =
            new Dictionary<string, PolicyPlusProduct>();
        public Dictionary<string, PolicyPlusPolicy> Policies =
            new Dictionary<string, PolicyPlusPolicy>();
        public Dictionary<string, PolicyPlusSupport> SupportDefinitions =
            new Dictionary<string, PolicyPlusSupport>();

        public IEnumerable<AdmxLoadFailure> LoadFolder(string Path, string LanguageCode)
        {
            var fails = new List<AdmxLoadFailure>();
            foreach (var file in Directory.EnumerateFiles(Path))
            {
                if (file.ToLowerInvariant().EndsWith(".admx"))
                {
                    var fail = AddSingleAdmx(file, LanguageCode);
                    if (fail is object)
                        fails.Add(fail);
                }
            }

            BuildStructures();
            return fails;
        }

        public IEnumerable<AdmxLoadFailure> LoadFile(string Path, string LanguageCode)
        {
            var fail = AddSingleAdmx(Path, LanguageCode);
            BuildStructures();
            return fail is null ? Array.Empty<AdmxLoadFailure>() : new[] { fail };
        }

        private AdmxLoadFailure? AddSingleAdmx(string AdmxPath, string LanguageCode)
        {
            // Load ADMX file
            AdmxFile admx;
            AdmlFile adml;
            try
            {
                admx = AdmxFile.Load(AdmxPath);
            }
            catch (System.Xml.XmlException ex)
            {
                return new AdmxLoadFailure(AdmxLoadFailType.BadAdmxParse, AdmxPath, ex.Message);
            }
            catch (Exception ex)
            {
                return new AdmxLoadFailure(AdmxLoadFailType.BadAdmx, AdmxPath, ex.Message);
            }

            if (Namespaces.ContainsKey(admx.AdmxNamespace))
                return new AdmxLoadFailure(
                    AdmxLoadFailType.DuplicateNamespace,
                    AdmxPath,
                    admx.AdmxNamespace
                );
            // Find the ADML file
            string fileTitle = Path.GetFileName(AdmxPath);
            string admlPath = Path.ChangeExtension(
                AdmxPath.Replace(fileTitle, LanguageCode + @"\" + fileTitle),
                "adml"
            );
            // Build ordered candidate list and use cached existence checks to short-circuit quickly.
            var candidates = new List<string>(8);
            candidates.Add(admlPath);
            var admxDir = Path.GetDirectoryName(AdmxPath);
            // Base language match inside sibling culture folders (e.g., fr-CA -> fr-FR)
            try
            {
                string language = LanguageCode.Split('-')[0];
                if (!string.IsNullOrEmpty(language) && !string.IsNullOrEmpty(admxDir))
                {
                    var subs = GetSubdirectories(admxDir);
                    for (int i = 0; i < subs.Length; i++)
                    {
                        var langSubdirTitle = Path.GetFileName(subs[i]);
                        if ((langSubdirTitle?.Split('-')[0] ?? "") == language)
                        {
                            string similarLanguagePath = Path.ChangeExtension(
                                Path.Combine(subs[i], fileTitle),
                                "adml"
                            );
                            candidates.Add(similarLanguagePath);
                        }
                    }
                }
            }
            catch { }

            if (EnableLanguageFallback)
            {
                // OS UI culture fallback (full name then its base) before en-US/en.
                try
                {
                    var osFull = CultureInfo.CurrentUICulture.Name; // e.g. ja-JP
                    if (
                        !string.IsNullOrEmpty(osFull)
                        && !osFull.Equals(LanguageCode, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        string osFullPath = Path.ChangeExtension(
                            AdmxPath.Replace(fileTitle, osFull + @"\" + fileTitle),
                            "adml"
                        );
                        candidates.Add(osFullPath);
                    }
                }
                catch { }

                try
                {
                    var osBase = CultureInfo.CurrentUICulture.Name.Split('-')[0];
                    var specifiedBase = LanguageCode.Split('-')[0];
                    if (
                        !string.IsNullOrEmpty(osBase)
                        && !osBase.Equals(specifiedBase, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrEmpty(admxDir)
                    )
                    {
                        var subs = GetSubdirectories(admxDir);
                        for (int i = 0; i < subs.Length; i++)
                        {
                            var title = Path.GetFileName(subs[i]);
                            if ((title?.Split('-')[0] ?? "") == osBase)
                            {
                                var baseCandidate = Path.ChangeExtension(
                                    Path.Combine(subs[i], fileTitle),
                                    "adml"
                                );
                                candidates.Add(baseCandidate);
                            }
                        }
                    }
                }
                catch { }

                candidates.Add(
                    Path.ChangeExtension(AdmxPath.Replace(fileTitle, @"en-US\" + fileTitle), "adml")
                );

                try
                {
                    if (!string.IsNullOrEmpty(admxDir))
                    {
                        var enDir = Path.Combine(admxDir, "en");
                        if (Directory.Exists(enDir))
                        {
                            candidates.Add(
                                Path.ChangeExtension(Path.Combine(enDir, fileTitle), "adml")
                            );
                        }
                    }
                }
                catch { }

                try
                {
                    if (!string.IsNullOrEmpty(admxDir))
                    {
                        var subs = GetSubdirectories(admxDir);
                        for (int i = 0; i < subs.Length; i++)
                        {
                            var candidate = Path.ChangeExtension(
                                Path.Combine(subs[i], fileTitle),
                                "adml"
                            );
                            candidates.Add(candidate);
                        }
                    }
                }
                catch { }
            }

            // Choose first existing path from candidates
            admlPath = candidates.FirstOrDefault(CachedFileExists) ?? admlPath;

            if (!CachedFileExists(admlPath))
                return new AdmxLoadFailure(AdmxLoadFailType.NoAdml, AdmxPath);
            // Load the ADML
            try
            {
                adml = AdmlFile.Load(admlPath);
            }
            catch (System.Xml.XmlException ex)
            {
                return new AdmxLoadFailure(AdmxLoadFailType.BadAdmlParse, AdmxPath, ex.Message);
            }
            catch (Exception ex)
            {
                return new AdmxLoadFailure(AdmxLoadFailType.BadAdml, AdmxPath, ex.Message);
            }
            // Stage the raw ADMX info for BuildStructures
            RawCategories.AddRange(admx.Categories);
            RawProducts.AddRange(admx.Products);
            RawPolicies.AddRange(admx.Policies);
            RawSupport.AddRange(admx.SupportedOnDefinitions);
            SourceFiles.Add(admx, adml);
            Namespaces.Add(admx.AdmxNamespace, admx);
            return null;
        }

        private void BuildStructures()
        {
            var catIds = new Dictionary<string, PolicyPlusCategory>();
            var productIds = new Dictionary<string, PolicyPlusProduct>();
            var supIds = new Dictionary<string, PolicyPlusSupport>();
            var polIds = new Dictionary<string, PolicyPlusPolicy>();
            PolicyPlusCategory findCatById(string UID)
            {
                return FindInTempOrFlat(UID, catIds, FlatCategories)!;
            }

            PolicyPlusSupport findSupById(string UID)
            {
                return FindInTempOrFlat(UID, supIds, SupportDefinitions)!;
            }

            PolicyPlusProduct findProductById(string UID)
            {
                return FindInTempOrFlat(UID, productIds, FlatProducts)!;
            }
            // First pass: Build the structures without resolving references
            foreach (var rawCat in RawCategories)
            {
                var cat = new PolicyPlusCategory();
                cat.DisplayName = ResolveString(rawCat.DisplayCode, rawCat.DefinedIn);
                cat.DisplayExplanation = ResolveString(rawCat.ExplainCode, rawCat.DefinedIn);
                cat.UniqueID = QualifyName(rawCat.ID, rawCat.DefinedIn);
                cat.RawCategory = rawCat;
                catIds.Add(cat.UniqueID, cat);
            }

            foreach (var rawProduct in RawProducts)
            {
                var product = new PolicyPlusProduct();
                product.DisplayName = ResolveString(rawProduct.DisplayCode, rawProduct.DefinedIn);
                product.UniqueID = QualifyName(rawProduct.ID, rawProduct.DefinedIn);
                product.RawProduct = rawProduct;
                productIds.Add(product.UniqueID, product);
            }

            foreach (var rawSup in RawSupport)
            {
                var sup = new PolicyPlusSupport();
                sup.DisplayName = ResolveString(rawSup.DisplayCode, rawSup.DefinedIn);
                sup.UniqueID = QualifyName(rawSup.ID, rawSup.DefinedIn);
                if (rawSup.Entries is object)
                {
                    foreach (var rawSupEntry in rawSup.Entries)
                    {
                        var supEntry = new PolicyPlusSupportEntry();
                        supEntry.RawSupportEntry = rawSupEntry;
                        sup.Elements.Add(supEntry);
                    }
                }

                sup.RawSupport = rawSup;
                supIds.Add(sup.UniqueID, sup);
            }

            foreach (var rawPol in RawPolicies)
            {
                var pol = new PolicyPlusPolicy();
                pol.DisplayExplanation = ResolveString(rawPol.ExplainCode, rawPol.DefinedIn);
                pol.DisplayName = ResolveString(rawPol.DisplayCode, rawPol.DefinedIn);
                if (!string.IsNullOrEmpty(rawPol.PresentationID))
                    pol.Presentation = ResolvePresentation(rawPol.PresentationID, rawPol.DefinedIn);
                pol.UniqueID = QualifyName(rawPol.ID, rawPol.DefinedIn);
                pol.RawPolicy = rawPol;
                polIds.Add(pol.UniqueID, pol);
            }
            // Second pass: Resolve references and link structures
            foreach (var cat in catIds.Values)
            {
                if (!string.IsNullOrEmpty(cat.RawCategory.ParentID))
                {
                    string parentCatName = ResolveRef(
                        cat.RawCategory.ParentID,
                        cat.RawCategory.DefinedIn
                    );
                    var parentCat = findCatById(parentCatName);
                    if (parentCat is null)
                        continue; // In case the parent category doesn't exist
                    parentCat.Children.Add(cat);
                    cat.Parent = parentCat;
                }
            }

            foreach (var product in productIds.Values)
            {
                if (product.RawProduct.Parent is object)
                {
                    string parentProductId = QualifyName(
                        product.RawProduct.Parent.ID,
                        product.RawProduct.DefinedIn
                    ); // Child products can't be defined in other files
                    var parentProduct = findProductById(parentProductId);
                    parentProduct.Children.Add(product);
                    product.Parent = parentProduct;
                }
            }

            foreach (var sup in supIds.Values)
            {
                foreach (var supEntry in sup.Elements)
                {
                    string targetId = ResolveRef(
                        supEntry.RawSupportEntry.ProductID,
                        sup.RawSupport.DefinedIn
                    ); // Support or product
                    supEntry.Product = findProductById(targetId);
                    if (supEntry.Product is null)
                        supEntry.SupportDefinition = findSupById(targetId);
                }
            }

            foreach (var pol in polIds.Values)
            {
                string catId = ResolveRef(pol.RawPolicy.CategoryID, pol.RawPolicy.DefinedIn);
                var ownerCat = findCatById(catId);
                if (ownerCat is object)
                {
                    ownerCat.Policies.Add(pol);
                    pol.Category = ownerCat;
                }

                string supportId = ResolveRef(pol.RawPolicy.SupportedCode, pol.RawPolicy.DefinedIn);
                pol.SupportedOn = findSupById(supportId);
            }
            // Third pass: Add items to the final lists
            foreach (var cat in catIds)
            {
                FlatCategories.Add(cat.Key, cat.Value);
                if (cat.Value.Parent is null)
                    Categories.Add(cat.Key, cat.Value);
            }

            foreach (var product in productIds)
            {
                FlatProducts.Add(product.Key, product.Value);
                if (product.Value.Parent is null)
                    Products.Add(product.Key, product.Value);
            }

            foreach (var pol in polIds)
                Policies.Add(pol.Key, pol.Value);
            foreach (var sup in supIds)
                SupportDefinitions.Add(sup.Key, sup.Value);
            // Purge the temporary partially-constructed items
            RawCategories.Clear();
            RawProducts.Clear();
            RawSupport.Clear();
            RawPolicies.Clear();
        }

        private T? FindInTempOrFlat<T>(
            string UniqueID,
            Dictionary<string, T> TempDict,
            Dictionary<string, T> FlatDict
        )
            where T : class
        {
            // Get the best available structure for an ID
            if (TempDict.TryGetValue(UniqueID, out var tmp))
                return tmp;
            if (FlatDict is object && FlatDict.TryGetValue(UniqueID, out var flat))
                return flat;
            return default;
        }

        public string ResolveString(string? DisplayCode, AdmxFile Admx)
        {
            // Find a localized string from a display code
            if (string.IsNullOrEmpty(DisplayCode))
                return "";
            if (!DisplayCode.StartsWith("$(string."))
                return DisplayCode;
            string stringId = DisplayCode.Substring(9, DisplayCode.Length - 10);
            var admlObj = SourceFiles[Admx];
            var dict = admlObj.StringTable;
            if (dict.TryGetValue(stringId, out var val))
                return val;
            // Attempt to lazily merge strings from the actual ADML file on disk if not already present.
            try
            {
                var path = admlObj.SourceFile;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    if (!_externalAdmlStringCache.TryGetValue(path, out var mapSelf))
                    {
                        mapSelf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        try
                        {
                            var xml = new System.Xml.XmlDocument();
                            xml.Load(path);
                            var list = xml.GetElementsByTagName("stringTable");
                            if (list.Count > 0)
                            {
                                foreach (System.Xml.XmlNode n in list[0]!.ChildNodes)
                                {
                                    if (n.LocalName == "string")
                                    {
                                        var id = n.Attributes?["id"]?.Value;
                                        if (!string.IsNullOrEmpty(id) && !mapSelf.ContainsKey(id))
                                            mapSelf[id] = n.InnerText ?? string.Empty;
                                    }
                                }
                            }
                        }
                        catch { }
                        _externalAdmlStringCache[path] = mapSelf;
                    }
                    foreach (var kvp in mapSelf)
                    {
                        if (!dict.ContainsKey(kvp.Key))
                            dict[kvp.Key] = kvp.Value;
                    }
                    if (dict.TryGetValue(stringId, out var fromSelf))
                        return fromSelf;
                }
            }
            catch { }
            // Fallback: search other loaded ADMLs for the same string key. This is a resilience aid in case of partial ADML loads.
            foreach (var kv in SourceFiles)
            {
                if (ReferenceEquals(kv.Key, Admx))
                    continue;
                var table = kv.Value.StringTable;
                if (table.TryGetValue(stringId, out var v2))
                    return v2;
            }
            // Last resort: try to load en-US ADML adjacent to the ADMX and resolve from there (single file, cached by path).
            try
            {
                var admxDir = Path.GetDirectoryName(Admx.SourceFile);
                var admxName = Path.GetFileName(Admx.SourceFile);
                if (!string.IsNullOrEmpty(admxDir) && !string.IsNullOrEmpty(admxName))
                {
                    var enUsPath = Path.ChangeExtension(
                        Path.Combine(admxDir, "en-US", admxName),
                        "adml"
                    );
                    if (File.Exists(enUsPath))
                    {
                        if (!_externalAdmlStringCache.TryGetValue(enUsPath, out var map))
                        {
                            map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            try
                            {
                                var xml = new System.Xml.XmlDocument();
                                xml.Load(enUsPath);
                                var list = xml.GetElementsByTagName("stringTable");
                                if (list.Count > 0)
                                {
                                    foreach (System.Xml.XmlNode n in list[0]!.ChildNodes)
                                    {
                                        if (n.LocalName == "string")
                                        {
                                            var id = n.Attributes?["id"]?.Value;
                                            if (!string.IsNullOrEmpty(id) && !map.ContainsKey(id))
                                                map[id] = n.InnerText ?? string.Empty;
                                        }
                                    }
                                }
                            }
                            catch { }
                            _externalAdmlStringCache[enUsPath] = map;
                        }
                        if (map.TryGetValue(stringId, out var v3))
                            return v3;
                    }
                }
            }
            catch { }
            return DisplayCode;
        }

        public Presentation? ResolvePresentation(string DisplayCode, AdmxFile Admx)
        {
            // Find a presentation from a code
            if (!DisplayCode.StartsWith("$(presentation."))
                return null;
            string presId = DisplayCode.Substring(15, DisplayCode.Length - 16);
            var dict = SourceFiles[Admx].PresentationTable;
            return dict.TryGetValue(presId, out var pres) ? pres : null;
        }

        private string QualifyName(string ID, AdmxFile Admx)
        {
            return Admx.AdmxNamespace + ":" + ID;
        }

        private string ResolveRef(string Ref, AdmxFile Admx)
        {
            // Get a fully qualified name from a code and the current scope
            if (Ref.Contains(":"))
            {
                var parts = Ref.Split(new[] { ':' }, 2);
                if (Admx.Prefixes.ContainsKey(parts[0]))
                {
                    string srcNamespace = Admx.Prefixes[parts[0]];
                    return srcNamespace + ":" + parts[1];
                }
                else
                {
                    return Ref;
                } // Assume a literal
            }
            else
            {
                return QualifyName(Ref, Admx);
            }
        }

        public IReadOnlyDictionary<AdmxFile, AdmlFile> Sources => SourceFiles;
    }

    public enum AdmxLoadFailType
    {
        BadAdmxParse,
        BadAdmx,
        NoAdml,
        BadAdmlParse,
        BadAdml,
        DuplicateNamespace,
    }

    public class AdmxLoadFailure
    {
        public AdmxLoadFailType FailType;
        public string AdmxPath;
        public string Info;

        public AdmxLoadFailure(AdmxLoadFailType FailType, string AdmxPath, string Info)
        {
            this.FailType = FailType;
            this.AdmxPath = AdmxPath;
            this.Info = Info;
        }

        public AdmxLoadFailure(AdmxLoadFailType FailType, string AdmxPath)
            : this(FailType, AdmxPath, "") { }

        public override string ToString()
        {
            string failMsg = "Couldn't load " + AdmxPath + ": " + GetFailMessage(FailType, Info);
            if (!failMsg.EndsWith("."))
                failMsg += ".";
            return failMsg;
        }

        private static string GetFailMessage(AdmxLoadFailType FailType, string Info)
        {
            switch (FailType)
            {
                case AdmxLoadFailType.BadAdmxParse:
                {
                    return "The ADMX XML couldn't be parsed: " + Info;
                }

                case AdmxLoadFailType.BadAdmx:
                {
                    return "The ADMX is invalid: " + Info;
                }

                case AdmxLoadFailType.NoAdml:
                {
                    return "The corresponding ADML is missing";
                }

                case AdmxLoadFailType.BadAdmlParse:
                {
                    return "The ADML XML couldn't be parsed: " + Info;
                }

                case AdmxLoadFailType.BadAdml:
                {
                    return "The ADML is invalid: " + Info;
                }

                case AdmxLoadFailType.DuplicateNamespace:
                {
                    return "The " + Info + " namespace is already owned by a different ADMX file";
                }
            }

            return string.IsNullOrEmpty(Info) ? "An unknown error occurred" : Info;
        }
    }
}
