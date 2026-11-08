using System.Windows.Controls;

namespace GameBackup.Plugin.UI
{
    public partial class LocateView : UserControl
    {
        public LocateView()
        {
            InitializeComponent();
            ThemeBridge.Ensure(this);
        }
    }
}
