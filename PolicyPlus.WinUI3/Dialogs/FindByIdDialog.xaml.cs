using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace PolicyPlus.WinUI3.Dialogs
{
    public sealed partial class FindByIdDialog : ContentDialog
    {
        public Func<PolicyPlusPolicy, bool>? Searcher { get; private set; }
        public FindByIdDialog()
        {
            InitializeComponent();
            this.PrimaryButtonClick += FindByIdDialog_PrimaryButtonClick;
        }

        private void FindByIdDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var text = IdText.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text)) { args.Cancel = true; return; }
            var low = text.ToLowerInvariant();
            bool partial = ChkPartial.IsChecked == true;
            Searcher = (p) =>
            {
                var id = p.UniqueID.ToLowerInvariant();
                return partial ? id.Contains(low, StringComparison.InvariantCultureIgnoreCase) : string.Equals(id, low, StringComparison.InvariantCultureIgnoreCase);
            };
        }
    }
}
