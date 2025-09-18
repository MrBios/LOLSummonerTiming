using System.Windows;

namespace LOLSummonerTiming
{
    public partial class TextSettingsWindow : Window
    {
        public TextSettingsWindow()
        {
            InitializeComponent();
            DataContext = Config.Current;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
