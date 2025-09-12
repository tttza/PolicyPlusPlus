using PolicyPlusPlus.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PolicyPlusPlus.ViewModels
{
    public sealed class SearchOptionsViewModel : INotifyPropertyChanged
    {
        private bool _inName = true;
        private bool _inId = true;
        private bool _inRegistryKey = true;
        private bool _inRegistryValue = true;
        private bool _inDescription;
        private bool _inComments;
        private bool _loaded;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public SearchOptionsViewModel()
        {
            try
            {
                var s = SettingsService.Instance.LoadSettings().Search;
                if (s != null)
                {
                    _inName = s.InName;
                    _inId = s.InId;
                    _inRegistryKey = s.InRegistryKey;
                    _inRegistryValue = s.InRegistryValue;
                    _inDescription = s.InDescription;
                    _inComments = s.InComments;
                }
                _loaded = true;
            }
            catch { }
        }

        private void Persist()
        {
            if (!_loaded) return;
            try
            {
                SettingsService.Instance.UpdateSearchOptions(new SearchOptions
                {
                    InName = _inName,
                    InId = _inId,
                    InRegistryKey = _inRegistryKey,
                    InRegistryValue = _inRegistryValue,
                    InDescription = _inDescription,
                    InComments = _inComments
                });
            }
            catch { }
        }

        public bool InName { get => _inName; set { if (value == _inName) return; _inName = value; Notify(); Persist(); } }
        public bool InId { get => _inId; set { if (value == _inId) return; _inId = value; Notify(); Persist(); } }
        public bool InRegistryKey { get => _inRegistryKey; set { if (value == _inRegistryKey) return; _inRegistryKey = value; Notify(); Persist(); } }
        public bool InRegistryValue { get => _inRegistryValue; set { if (value == _inRegistryValue) return; _inRegistryValue = value; Notify(); Persist(); } }
        public bool InDescription { get => _inDescription; set { if (value == _inDescription) return; _inDescription = value; Notify(); Persist(); } }
        public bool InComments { get => _inComments; set { if (value == _inComments) return; _inComments = value; Notify(); Persist(); } }
    }
}
