using System.Collections.Generic;

namespace PolicyPlusCore.Core
{
    // Compiled data, more object-oriented and display-worthy than raw data from ADMX files

    public interface IPolicyPlus
    {
        public string UniqueID { get; set; }
        public string DisplayName { get; set; }
    }

    public partial class BasePolicyPlus : IPolicyPlus
    {
    public string UniqueID { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    }

    public class PolicyPlusCategory : BasePolicyPlus
    {
    public PolicyPlusCategory? Parent;
        public List<PolicyPlusCategory> Children = new List<PolicyPlusCategory>();
    public string? DisplayExplanation;
    public List<PolicyPlusPolicy> Policies = new List<PolicyPlusPolicy>();
    public AdmxCategory RawCategory = null!;
    }

    public class PolicyPlusProduct : BasePolicyPlus
    {
    public PolicyPlusProduct? Parent;
        public List<PolicyPlusProduct> Children = new List<PolicyPlusProduct>();
    public AdmxProduct RawProduct = null!;
    }

    public class PolicyPlusSupport : BasePolicyPlus
    {
        public List<PolicyPlusSupportEntry> Elements = new List<PolicyPlusSupportEntry>();
    public AdmxSupportDefinition RawSupport = null!;
    }

    public class PolicyPlusSupportEntry
    {
    public PolicyPlusProduct? Product;
    public PolicyPlusSupport? SupportDefinition; // Only used if this entry actually points to another support definition
    public AdmxSupportEntry RawSupportEntry = null!;
    }

    public class PolicyPlusPolicy : BasePolicyPlus
    {
    public PolicyPlusCategory? Category;
    public string? DisplayExplanation;
    public PolicyPlusSupport? SupportedOn;
    public Presentation? Presentation;
    public AdmxPolicy RawPolicy = null!;
    }
}
