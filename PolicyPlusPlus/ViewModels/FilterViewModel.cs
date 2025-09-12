using PolicyPlusCore.Core;
using PolicyPlusPlus.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PolicyPlusPlus.ViewModels
{
    public sealed class FilterViewModel : INotifyPropertyChanged
    {
        private AdmxPolicySection _applies = AdmxPolicySection.Both;
        private bool _configuredOnly;
        private bool _bookmarksOnly;
        private bool _limitUnfilteredTo1000 = true;
        private bool _hideEmptyCategories = true;
        private bool _showDetails = true;
        private bool _loaded;

        public event PropertyChangedEventHandler? PropertyChanged;
        void Notify([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public FilterViewModel()
        {
            try
            {
                var s = SettingsService.Instance.LoadSettings();
                _configuredOnly = s.ConfiguredOnly ?? false;
                _bookmarksOnly = s.BookmarksOnly ?? false;
                _limitUnfilteredTo1000 = s.LimitUnfilteredTo1000 ?? true;
                _hideEmptyCategories = s.HideEmptyCategories ?? true;
                _showDetails = s.ShowDetails ?? true;
                _loaded = true;
            }
            catch { }
        }

        private void Persist()
        {
            if (!_loaded) return;
            try
            {
                SettingsService.Instance.UpdateConfiguredOnly(_configuredOnly);
                SettingsService.Instance.UpdateBookmarksOnly(_bookmarksOnly);
                SettingsService.Instance.UpdateLimitUnfilteredTo1000(_limitUnfilteredTo1000);
                SettingsService.Instance.UpdateHideEmptyCategories(_hideEmptyCategories);
                SettingsService.Instance.UpdateShowDetails(_showDetails);
            }
            catch { }
        }

        public AdmxPolicySection Applies
        {
            get => _applies;
            set { if (value == _applies) return; _applies = value; Notify(); }
        }
        public bool ConfiguredOnly
        {
            get => _configuredOnly;
            set { if (value == _configuredOnly) return; _configuredOnly = value; Notify(); Persist(); }
        }
        public bool BookmarksOnly
        {
            get => _bookmarksOnly;
            set { if (value == _bookmarksOnly) return; _bookmarksOnly = value; Notify(); Persist(); }
        }
        public bool LimitUnfilteredTo1000
        {
            get => _limitUnfilteredTo1000;
            set { if (value == _limitUnfilteredTo1000) return; _limitUnfilteredTo1000 = value; Notify(); Persist(); }
        }
        public bool HideEmptyCategories
        {
            get => _hideEmptyCategories;
            set { if (value == _hideEmptyCategories) return; _hideEmptyCategories = value; Notify(); Persist(); }
        }
        public bool ShowDetails
        {
            get => _showDetails;
            set { if (value == _showDetails) return; _showDetails = value; Notify(); Persist(); }
        }
    }
}
