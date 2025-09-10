using PolicyPlus.Core.Admx;
using PolicyPlus.Core.Core;

using System;
using System.Collections.Generic;

namespace PolicyPlusPlus.Services
{
    internal static class CategoryIndex
    {
        public static Dictionary<string, PolicyPlusCategory> BuildIndex(AdmxBundle bundle)
        {
            var map = new Dictionary<string, PolicyPlusCategory>(StringComparer.OrdinalIgnoreCase);
            if (bundle == null || bundle.Categories == null) return map;
            foreach (var root in bundle.Categories.Values)
            {
                AddRecursive(root, map);
            }
            return map;
        }

        private static void AddRecursive(PolicyPlusCategory cat, Dictionary<string, PolicyPlusCategory> map)
        {
            if (cat == null || string.IsNullOrEmpty(cat.UniqueID)) return;
            map[cat.UniqueID] = cat;
            if (cat.Children != null)
            {
                foreach (var c in cat.Children)
                {
                    AddRecursive(c, map);
                }
            }
        }
    }
}
