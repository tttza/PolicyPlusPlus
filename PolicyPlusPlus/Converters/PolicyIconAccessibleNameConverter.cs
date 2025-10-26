using System;
using Microsoft.UI.Xaml.Data;
using PolicyPlusPlus.Models;
using PolicyPlusPlus.Resources.Localization;

namespace PolicyPlusPlus.Converters
{
    internal enum PolicyIconAccessibleKind
    {
        Bookmark,
        CategoryIcon,
        UserState,
        ComputerState,
    }

    // Generates accessible names for policy list icon/state cells.
    // Accepts either an enum (recommended) or legacy string ConverterParameter for backward compatibility.
    public sealed partial class PolicyIconAccessibleNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var kind = parameter switch
            {
                PolicyIconAccessibleKind ek => ek,
                string s => ParseLegacyKind(s),
                _ => (PolicyIconAccessibleKind?)null,
            };
            if (kind == null)
                return string.Empty;

            switch (kind.Value)
            {
                case PolicyIconAccessibleKind.Bookmark:
                    return BookmarkName(value);
                case PolicyIconAccessibleKind.CategoryIcon:
                    return CategoryName(value);
                case PolicyIconAccessibleKind.UserState:
                    return UserStateName(value);
                case PolicyIconAccessibleKind.ComputerState:
                    return ComputerStateName(value);
                default:
                    return string.Empty;
            }
        }

        private static PolicyIconAccessibleKind? ParseLegacyKind(string s) =>
            s switch
            {
                "Bookmark" => PolicyIconAccessibleKind.Bookmark,
                "CategoryIcon" => PolicyIconAccessibleKind.CategoryIcon,
                "UserState" => PolicyIconAccessibleKind.UserState,
                "ComputerState" => PolicyIconAccessibleKind.ComputerState,
                _ => null,
            };

        private static string BookmarkName(object value)
        {
            // Prefer full row to allow category distinction.
            if (value is PolicyListRow row)
            {
                if (row.IsCategory)
                    return AccessibilityStrings.BookmarkNone;
                return row.IsBookmarked
                    ? AccessibilityStrings.BookmarkBookmarked
                    : AccessibilityStrings.BookmarkNotBookmarked;
            }
            if (value is bool b)
                return b
                    ? AccessibilityStrings.BookmarkBookmarked
                    : AccessibilityStrings.BookmarkNotBookmarked;
            return string.Empty;
        }

        private static string CategoryName(object value)
        {
            if (value is PolicyListRow row)
            {
                if (!row.IsCategory)
                    return AccessibilityStrings.CategoryNone;
                string display = string.IsNullOrWhiteSpace(row.DisplayName)
                    ? AccessibilityStrings.CategoryUnnamed
                    : row.DisplayName;
                return string.Format(AccessibilityStrings.CategoryWithNameFormat, display);
            }
            if (value is bool isCat)
                return isCat
                    ? AccessibilityStrings.CategoryFolder
                    : AccessibilityStrings.CategoryNone;
            return AccessibilityStrings.CategoryNone;
        }

        private static string UserStateName(object value)
        {
            if (value is PolicyListRow row)
            {
                if (string.IsNullOrWhiteSpace(row.UserStateText))
                    return AccessibilityStrings.UserPolicyPrefix
                        + AccessibilityStrings.StateNotConfigured;
                string baseState = AccessibilityStrings.UserPolicyPrefix + row.UserStateText;
                return row.UserPending
                    ? baseState + AccessibilityStrings.PendingSuffixWrapped
                    : baseState;
            }
            if (value is string s && !string.IsNullOrWhiteSpace(s))
                return AccessibilityStrings.UserPolicyPrefix + s;
            return string.Empty;
        }

        private static string ComputerStateName(object value)
        {
            if (value is PolicyListRow row)
            {
                if (string.IsNullOrWhiteSpace(row.ComputerStateText))
                    return AccessibilityStrings.ComputerPolicyPrefix
                        + AccessibilityStrings.StateNotConfigured;
                string baseState =
                    AccessibilityStrings.ComputerPolicyPrefix + row.ComputerStateText;
                return row.ComputerPending
                    ? baseState + AccessibilityStrings.PendingSuffixWrapped
                    : baseState;
            }
            if (value is string s && !string.IsNullOrWhiteSpace(s))
                return AccessibilityStrings.ComputerPolicyPrefix + s;
            return string.Empty;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            string language
        ) => throw new NotSupportedException();
    }
}
