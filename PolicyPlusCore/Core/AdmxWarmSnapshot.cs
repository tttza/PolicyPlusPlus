using System;
using System.Collections.Generic;
using System.Linq;
using PolicyPlusCore.Admx;

namespace PolicyPlusCore.Core;

// Lightweight snapshot of ADMX-compiled structures sufficient for listing/searching.
public sealed class AdmxWarmSnapshot
{
    public int Version { get; set; } = 1;
    public string Language { get; set; } = string.Empty;
    public List<WarmCategory> Categories { get; set; } = new();
    public List<WarmPolicy> Policies { get; set; } = new();

    public sealed class WarmCategory
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ParentId { get; set; }
    }

    public sealed class WarmPolicy
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CategoryId { get; set; }
        public string? SupportedOn { get; set; }
    }

    // Build snapshot from a fully built AdmxBundle
    public static AdmxWarmSnapshot FromBundle(AdmxBundle b, string language)
    {
        var snap = new AdmxWarmSnapshot { Language = language };
        foreach (var cat in b.FlatCategories.Values)
        {
            string? parent = cat.Parent?.UniqueID;
            snap.Categories.Add(
                new WarmCategory
                {
                    Id = cat.UniqueID,
                    Name = cat.DisplayName ?? string.Empty,
                    ParentId = parent,
                }
            );
        }
        foreach (var p in b.Policies.Values)
        {
            snap.Policies.Add(
                new WarmPolicy
                {
                    Id = p.UniqueID,
                    Name = p.DisplayName ?? string.Empty,
                    Description = p.DisplayExplanation ?? string.Empty,
                    CategoryId = p.Category?.UniqueID,
                    SupportedOn = p.SupportedOn?.DisplayName,
                }
            );
        }
        return snap;
    }

    // Rehydrate minimal compiled structures for listing/searching.
    // RawPolicy / Presentation are not populated (lazy full load will replace later).
    public (
        IReadOnlyDictionary<string, PolicyPlusCategory> cats,
        IReadOnlyList<PolicyPlusPolicy> policies
    ) ToCompiled()
    {
        var cats = new Dictionary<string, PolicyPlusCategory>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in Categories)
        {
            cats[c.Id] = new PolicyPlusCategory
            {
                UniqueID = c.Id,
                DisplayName = c.Name ?? string.Empty,
                Parent = null,
                Children = new List<PolicyPlusCategory>(),
            };
        }
        // Link parent/children
        foreach (var c in Categories)
        {
            if (
                !string.IsNullOrEmpty(c.ParentId)
                && cats.TryGetValue(c.ParentId!, out var parent)
                && cats.TryGetValue(c.Id, out var child)
            )
            {
                child.Parent = parent;
                parent.Children.Add(child);
            }
        }

        var list = new List<PolicyPlusPolicy>(Policies.Count);
        foreach (var p in Policies)
        {
            var pol = new PolicyPlusPolicy
            {
                UniqueID = p.Id,
                DisplayName = p.Name ?? string.Empty,
                DisplayExplanation = p.Description ?? string.Empty,
                Category =
                    (p.CategoryId != null && cats.TryGetValue(p.CategoryId, out var catRef))
                        ? catRef
                        : null,
                SupportedOn = string.IsNullOrEmpty(p.SupportedOn)
                    ? null
                    : new PolicyPlusSupport { DisplayName = p.SupportedOn },
            };
            // Provide a dummy RawPolicy to satisfy non-null references; will be replaced on full load.
            pol.RawPolicy = new AdmxPolicy { ID = p.Id, DefinedIn = new AdmxFile() };
            list.Add(pol);
            if (pol.Category != null)
                pol.Category.Policies.Add(pol);
        }

        return (cats, list);
    }
}
