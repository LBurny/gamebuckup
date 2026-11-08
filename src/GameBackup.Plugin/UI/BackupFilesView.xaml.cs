using System.Windows.Controls;

namespace GameBackup.Plugin.UI
{
    public partial class BackupFilesView : UserControl
    {
        public BackupFilesView()
        {
            InitializeComponent();
            ThemeBridge.Ensure(this);
        }
    }
}
