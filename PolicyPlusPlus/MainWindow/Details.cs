using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using PolicyPlusCore.Core;
using PolicyPlusPlus.Models;

namespace PolicyPlusPlus
{
    public sealed partial class MainWindow
    {
        // Only details content update helpers live here (visibility handled in Preferences.cs existing implementation).

        private static void SetRtbText(RichTextBlock? rtb, string? text)
        {
            try
            {
                if (rtb == null)
                    return;
                rtb.Blocks.Clear();
                var p = new Paragraph();
                p.Inlines.Add(new Run { Text = text ?? string.Empty });
                rtb.Blocks.Add(p);
            }
            catch { }
        }

        private void SetDetails(PolicyPlusPolicy? policy)
        {
            try
            {
                if (DetailPlaceholder == null || DetailTitle == null)
                    return;
                if (policy == null)
                {
                    DetailPlaceholder.Visibility = Visibility.Visible;
                    SetRtbText(DetailTitle, string.Empty);
                    SetRtbText(DetailId, string.Empty);
                    SetRtbText(DetailCategory, string.Empty);
                    SetRtbText(DetailApplies, string.Empty);
                    SetRtbText(DetailSupported, string.Empty);
                    SetRtbText(DetailExplain, string.Empty);
                    return;
                }

                DetailPlaceholder.Visibility = Visibility.Collapsed;
                SetRtbText(DetailTitle, policy.DisplayName ?? policy.UniqueID);
                SetRtbText(DetailId, policy.UniqueID);
                try
                {
                    var cat = policy.Category;
                    var path = string.Empty;
                    if (cat != null)
                    {
                        System.Collections.Generic.Stack<string> stack = new();
                        while (cat != null)
                        {
                            stack.Push(cat.DisplayName ?? string.Empty);
                            cat = cat.Parent;
                        }
                        path = string.Join(" / ", stack);
                    }
                    SetRtbText(DetailCategory, path);
                }
                catch
                {
                    SetRtbText(DetailCategory, string.Empty);
                }
                SetRtbText(
                    DetailApplies,
                    policy.RawPolicy.Section switch
                    {
                        AdmxPolicySection.Machine => "Computer",
                        AdmxPolicySection.User => "User",
                        _ => "Both",
                    }
                );
                SetRtbText(DetailSupported, policy.SupportedOn?.DisplayName ?? string.Empty);
                SetRtbText(DetailExplain, policy.DisplayExplanation);
            }
            catch { }
        }

        private void UpdateDetailsFromSelection()
        {
            try
            {
                if (PolicyList?.SelectedItem is PolicyListRow row && row.Policy != null)
                    SetDetails(row.Policy);
                else
                    SetDetails(null);
            }
            catch { }
        }
    }
}
