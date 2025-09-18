using System.Windows;

namespace LOLSummonerTiming
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenKeyBindings_Click(object sender, RoutedEventArgs e)
        {
            var w = new KeyBindingsWindow { Owner = this };
            w.ShowDialog();
        }

        private void OpenWorkWindow_Click(object sender, RoutedEventArgs e)
        {
            var w = new Work(this) { Owner = this };
            w.Show();
            this.Hide();
        }

        private void OpenTextSettings_Click(object sender, RoutedEventArgs e)
        {
            var w = new TextSettingsWindow { Owner = this };
            w.ShowDialog();
        }
    }
}