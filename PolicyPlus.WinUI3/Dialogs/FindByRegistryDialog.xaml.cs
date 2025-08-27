using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace PolicyPlus.WinUI3.Dialogs
{
    public sealed partial class FindByRegistryDialog : ContentDialog
    {
        public string KeyPattern => KeyPatternBox.Text ?? string.Empty;
        public string ValuePattern => ValuePatternBox.Text ?? string.Empty;
        public Func<PolicyPlusPolicy, bool>? Searcher { get; private set; }

        public FindByRegistryDialog()
        {
            this.InitializeComponent();
            this.PrimaryButtonClick += FindByRegistryDialog_PrimaryButtonClick;
        }

        private void FindByRegistryDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var key = KeyPattern;
            var val = ValuePattern;
            if (string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(val))
            {
                args.Cancel = true;
                return;
            }
            Searcher = (p) => FindByRegistryWinUI.SearchRegistry(p, key.ToLowerInvariant(), val.ToLowerInvariant());
        }
    }
}
