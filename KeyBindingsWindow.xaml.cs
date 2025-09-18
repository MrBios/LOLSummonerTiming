using System;
using System.Windows;
using System.Windows.Input;

namespace LOLSummonerTiming
{
    public partial class KeyBindingsWindow : Window
    {
        private enum CaptureTarget { None, Top, Jungle, Mid, Adc, Support, Send }
        private CaptureTarget _target = CaptureTarget.None;

        public KeyBindingsWindow()
        {
            InitializeComponent();
            DataContext = Config.Current;
        }

        private void BeginCapture(CaptureTarget target)
        
        {
            _target = target;
            CaptureHint.Visibility = Visibility.Visible;
            InfoText.Text = "Нажмите нужную клавишу... (Esc — отмена)";
        }

        private void EndCapture()
        {
            _target = CaptureTarget.None;
            CaptureHint.Visibility = Visibility.Collapsed;
            InfoText.Text = "Выберите, какую клавишу изменить, затем нажмите новую.";
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_target == CaptureTarget.None) return;

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape)
            {
                EndCapture();
                e.Handled = true;
                return;
            }
            if (key == Key.LeftShift || key == Key.RightShift || key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt)
            {
                // Не назначаем чистые модификаторы
                e.Handled = true;
                return;
            }

            switch (_target)
            {
                case CaptureTarget.Top: Config.Current.TopKey = key; break;
                case CaptureTarget.Jungle: Config.Current.JungleKey = key; break;
                case CaptureTarget.Mid: Config.Current.MidKey = key; break;
                case CaptureTarget.Adc: Config.Current.AdcKey = key; break;
                case CaptureTarget.Support: Config.Current.SupportKey = key; break;
                case CaptureTarget.Send: Config.Current.SendKey = key; break;
            }

            EndCapture();
            e.Handled = true;
        }

        private void SelectTop_Click(object sender, MouseButtonEventArgs e) => BeginCapture(CaptureTarget.Top);
        private void SelectJungle_Click(object sender, MouseButtonEventArgs e) => BeginCapture(CaptureTarget.Jungle);
        private void SelectMid_Click(object sender, MouseButtonEventArgs e) => BeginCapture(CaptureTarget.Mid);
        private void SelectAdc_Click(object sender, MouseButtonEventArgs e) => BeginCapture(CaptureTarget.Adc);
        private void SelectSupport_Click(object sender, MouseButtonEventArgs e) => BeginCapture(CaptureTarget.Support);
        private void SelectSend_Click(object sender, MouseButtonEventArgs e) => BeginCapture(CaptureTarget.Send);


        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
