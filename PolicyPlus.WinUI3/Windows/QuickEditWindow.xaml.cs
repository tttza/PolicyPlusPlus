using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PolicyPlus.Core.Admx;
using PolicyPlus.Core.Core;
using PolicyPlus.Core.IO;
using PolicyPlus.WinUI3.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PolicyPlus.WinUI3.Windows
{
    public sealed class QuickEditWindow : Window
    {
        private QuickEditGridControl _grid = new();
        private TextBlock _headerCount = null!;
        private AdmxBundle _bundle = null!;
        private IPolicySource? _comp;
        private IPolicySource? _user;

        public QuickEditWindow()
        {
            Title = "Quick Edit";
            var root = new Grid { Padding = new Thickness(12), MinWidth = 1280, MinHeight = 650 };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            headerPanel.Children.Add(new TextBlock { Text = "Quick Edit", FontSize = 20, FontWeight = FontWeights.SemiBold });
            _headerCount = new TextBlock { Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center };
            headerPanel.Children.Add(_headerCount);
            root.Children.Add(headerPanel);
            Grid.SetRow(_grid, 1); root.Children.Add(_grid);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
            var close = new Button { Content = "Close" }; close.Click += (_, __) => Close(); buttons.Children.Add(close); Grid.SetRow(buttons, 2); root.Children.Add(buttons);
            Content = root;
        }

        public static IEnumerable<PolicyPlusPolicy> BuildSourcePolicies(IEnumerable<PolicyPlusPolicy> allVisible, IEnumerable<PolicyPlusPolicy> selected, IEnumerable<string> bookmarkIds, bool bookmarksOnly, int cap = 500)
        {
            var list = new List<PolicyPlusPolicy>();
            var sel = selected.ToList();
            if (sel.Count > 0) list.AddRange(sel);
            else if (bookmarksOnly || bookmarkIds.Any())
            {
                var set = new HashSet<string>(bookmarkIds, StringComparer.OrdinalIgnoreCase);
                list.AddRange(allVisible.Where(p => set.Contains(p.UniqueID)));
            }
            else list.AddRange(allVisible);
            return list.Distinct().Take(cap);
        }

        public void Initialize(AdmxBundle bundle, IPolicySource? comp, IPolicySource? user, IEnumerable<PolicyPlusPolicy> policies)
        {
            _bundle = bundle; _comp = comp; _user = user;
            _grid.Rows.Clear();
            foreach (var p in policies.OrderBy(p => p.DisplayName)) _grid.Rows.Add(new QuickEditRow(p, bundle, comp, user));
            _headerCount.Text = $"{_grid.Rows.Count} policies";
        }
    }
}
