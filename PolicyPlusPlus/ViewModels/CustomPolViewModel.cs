using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PolicyPlusPlus.Services;

namespace PolicyPlusPlus.ViewModels
{
    public sealed class CustomPolViewModel : INotifyPropertyChanged
    {
        private readonly SettingsService _settings;
        private bool _enableComputer;
        private bool _enableUser;
        private string? _computerPath;
        private string? _userPath;
        private bool _active;
        private bool _suppressNotify;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? StateChanged;
        public event EventHandler? Committed;

        public CustomPolViewModel(SettingsService settings, CustomPolSettings? initial)
        {
            _settings = settings;
            if (initial != null)
            {
                _enableComputer = initial.EnableComputer;
                _enableUser = initial.EnableUser;
                _computerPath = initial.ComputerPath;
                _userPath = initial.UserPath;
                _active = initial.Active && (initial.EnableComputer || initial.EnableUser);
            }
        }

        public bool EnableComputer
        {
            get => _enableComputer;
            set
            {
                if (_enableComputer == value)
                    return;
                _enableComputer = value;
                OnChanged();
                if (!value && !_enableUser)
                    Active = false;
            }
        }
        public bool EnableUser
        {
            get => _enableUser;
            set
            {
                if (_enableUser == value)
                    return;
                _enableUser = value;
                OnChanged();
                if (!value && !_enableComputer)
                    Active = false;
            }
        }
        public string? ComputerPath
        {
            get => _computerPath;
            set
            {
                if (_computerPath == value)
                    return;
                _computerPath = value;
                OnChanged();
            }
        }
        public string? UserPath
        {
            get => _userPath;
            set
            {
                if (_userPath == value)
                    return;
                _userPath = value;
                OnChanged();
            }
        }
        public bool Active
        {
            get => _active;
            set
            {
                if (_active == value)
                    return;
                _active = value && (_enableComputer || _enableUser);
                OnChanged();
            }
        }
        public bool IsDirty { get; private set; }

        public CustomPolSettings Snapshot() =>
            new()
            {
                EnableComputer = _enableComputer,
                EnableUser = _enableUser,
                ComputerPath = _enableComputer ? _computerPath : null,
                UserPath = _enableUser ? _userPath : null,
                Active = _active && (_enableComputer || _enableUser),
            };

        private void OnChanged([CallerMemberName] string? name = null)
        {
            if (_suppressNotify)
                return;
            IsDirty = true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Commit()
        {
            var snap = Snapshot();
            try
            {
                _settings.UpdateCustomPol(snap);
                IsDirty = false;
                Committed?.Invoke(this, EventArgs.Empty);
            }
            catch { }
        }

        public void ReplaceModel(CustomPolSettings model)
        {
            _suppressNotify = true;
            try
            {
                _enableComputer = model.EnableComputer;
                _enableUser = model.EnableUser;
                _computerPath = model.ComputerPath;
                _userPath = model.UserPath;
                _active = model.Active && (model.EnableComputer || model.EnableUser);
                IsDirty = false;
            }
            finally
            {
                _suppressNotify = false;
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }
}
