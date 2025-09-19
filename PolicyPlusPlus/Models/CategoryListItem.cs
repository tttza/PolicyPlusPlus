using PolicyPlusCore.Core;

namespace PolicyPlusPlus.Models
{
    public sealed class CategoryListItem
    {
        public string Title { get; }
        public bool IsCategory { get; }
        public PolicyPlusPolicy? Policy { get; }
        public PolicyPlusCategory? Category { get; }

        private CategoryListItem(
            string title,
            bool isCategory,
            PolicyPlusPolicy? policy,
            PolicyPlusCategory? category
        )
        {
            Title = title;
            IsCategory = isCategory;
            Policy = policy;
            Category = category;
        }

        public static CategoryListItem FromPolicy(PolicyPlusPolicy p) =>
            new CategoryListItem(p.DisplayName, false, p, null);

        public static CategoryListItem FromCategory(PolicyPlusCategory c) =>
            new CategoryListItem(c.DisplayName, true, null, c);
    }
}
