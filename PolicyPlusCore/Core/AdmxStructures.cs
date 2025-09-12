using PolicyPlusCore.Admx;

namespace PolicyPlusCore.Core
{
    // Raw data loaded from ADMX files
    public class AdmxProduct
    {
        public string ID = string.Empty;
        public string DisplayCode = string.Empty;
        public AdmxProductType Type;
        public int Version;
        public AdmxProduct? Parent;
        public AdmxFile DefinedIn = null!;
    }

    public enum AdmxProductType
    {
        Product,
        MajorRevision,
        MinorRevision
    }

    public class AdmxSupportDefinition
    {
        public string ID = string.Empty;
        public string DisplayCode = string.Empty;
        public AdmxSupportLogicType Logic;
        public List<AdmxSupportEntry> Entries = new List<AdmxSupportEntry>();
        public AdmxFile DefinedIn = null!;
    }

    public enum AdmxSupportLogicType
    {
        Blank,
        AllOf,
        AnyOf
    }

    public class AdmxSupportEntry
    {
        public string ProductID = string.Empty;
        public bool IsRange;
        public int? MinVersion;
        public int? MaxVersion;
    }

    public class AdmxCategory
    {
        public string ID = string.Empty;
        public string DisplayCode = string.Empty;
        public string? ExplainCode;
        public string? ParentID;
        public AdmxFile DefinedIn = null!;
    }

    public class AdmxPolicy
    {
        public string ID = string.Empty;
        public AdmxPolicySection Section;
        public string CategoryID = string.Empty;
        public string DisplayCode = string.Empty;
        public string? ExplainCode;
        public string SupportedCode = string.Empty;
        public string? PresentationID;
        public string ClientExtension = string.Empty;
        public string RegistryKey = string.Empty;
        public string RegistryValue = string.Empty;
        public PolicyRegistryList AffectedValues = new PolicyRegistryList();
        public List<PolicyElement> Elements = new List<PolicyElement>();
        public AdmxFile DefinedIn = null!;
    }

    public enum AdmxPolicySection
    {
        Machine = 1,
        User = 2,
        Both = 3
    }
}
