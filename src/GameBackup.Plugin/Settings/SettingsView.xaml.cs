using System.Windows;
using System.Windows.Controls;
using GameBackup.Plugin.UI;

namespace GameBackup.Plugin.Settings
{
    public partial class SettingsView : UserControl
    {
        private bool _syncingPassword;

        public SettingsView()
        {
            InitializeComponent();
            ThemeBridge.Ensure(this);
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is SettingsViewModel viewModel)
            {
                _syncingPassword = true;
                ApiKeyBox.Password = viewModel.ApiKey ?? string.Empty;
                _syncingPassword = false;
            }
        }

        private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_syncingPassword)
            {
                return;
            }

            if (DataContext is SettingsViewModel viewModel)
            {
                viewModel.ApiKey = ApiKeyBox.Password;
            }
        }
    }
}
