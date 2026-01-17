namespace PolicyPlusCore.Core.Policies
{
    /// <summary>
    /// Evaluates whether a policy is supported based on product definitions.
    /// Extracted from PolicyProcessing.IsPolicySupported (ADR 0013 Phase 3c).
    /// </summary>
    internal static class PolicySupportChecker
    {
        /// <summary>
        /// Determines if a policy is supported by the given product set.
        /// </summary>
        /// <param name="policy">The policy to check.</param>
        /// <param name="products">List of products to check against.</param>
        /// <param name="alwaysUseAny">If true, treats AnyOf as default logic; if false, uses AllOf only when explicitly specified.</param>
        /// <param name="approveLiterals">Return value for policies with blank/missing support definitions or null products.</param>
        /// <returns>True if the policy is supported by at least one product configuration.</returns>
        public static bool IsSupported(
            PolicyPlusPolicy policy,
            List<PolicyPlusProduct> products,
            bool alwaysUseAny,
            bool approveLiterals
        )
        {
            if (
                policy.SupportedOn is null
                || policy.SupportedOn.RawSupport.Logic == AdmxSupportLogicType.Blank
            )
            {
                return approveLiterals;
            }

            var entriesSeen = new List<PolicyPlusSupport>();

            bool SupEntryMet(PolicyPlusSupportEntry supportEntry)
            {
                if (supportEntry.Product is null)
                    return approveLiterals;

                if (
                    products.Contains(supportEntry.Product) && !supportEntry.RawSupportEntry.IsRange
                )
                    return true;

                if (
                    supportEntry.Product.Children is null
                    || supportEntry.Product.Children.Count == 0
                )
                    return false;

                int rangeMin = supportEntry.RawSupportEntry.MinVersion ?? 0;
                int rangeMax =
                    supportEntry.RawSupportEntry.MaxVersion
                    ?? supportEntry.Product.Children.Max(p => p.RawProduct.Version);

                for (int v = rangeMin; v <= rangeMax; v++)
                {
                    int version = v;
                    var subproduct = supportEntry.Product.Children.FirstOrDefault(p =>
                        p.RawProduct.Version == version
                    );

                    if (subproduct is null)
                        continue;

                    if (products.Contains(subproduct))
                        return true;

                    if (
                        subproduct.Children is object
                        && subproduct.Children.Any(p => products.Contains(p))
                    )
                    {
                        return true;
                    }
                }

                return false;
            }

            bool SupDefMet(PolicyPlusSupport support)
            {
                // Cycle detection
                if (entriesSeen.Contains(support))
                    return false;
                entriesSeen.Add(support);

                bool requireAll = alwaysUseAny
                    ? support.RawSupport.Logic == AdmxSupportLogicType.AllOf
                    : false;

                // Evaluate direct support entries (no nested definition)
                foreach (var supElem in support.Elements.Where(e => e.SupportDefinition is null))
                {
                    bool isMet = SupEntryMet(supElem);
                    if (requireAll)
                    {
                        if (!isMet)
                            return false;
                    }
                    else if (isMet)
                    {
                        return true;
                    }
                }

                // Evaluate nested support definitions
                foreach (var subDef in support.Elements.Where(e => e.SupportDefinition is object))
                {
                    bool isMet =
                        subDef.SupportDefinition != null && SupDefMet(subDef.SupportDefinition);
                    if (requireAll)
                    {
                        if (!isMet)
                            return false;
                    }
                    else if (isMet)
                    {
                        return true;
                    }
                }

                return requireAll;
            }

            return SupDefMet(policy.SupportedOn);
        }
    }
}
