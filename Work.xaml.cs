using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LOLSummonerTiming
{
    /// <summary>
    /// Логика взаимодействия для Work.xaml
    /// </summary>
    public partial class Work : Window
    {
        MainWindow main;
        GlobalKeyboard keyboard = new GlobalKeyboard();
        DateTime start;
        DateTime[] times;
        CancellationTokenSource token = new CancellationTokenSource();
        bool started = false;

        public Work(MainWindow main)
        {
            InitializeComponent();
            DataContext = Config.Current;
            this.main = main;
            keyboard.KeyDown += Keyboard_KeyDown;
            keyboard.Start();
        }

        private async Task showTime(CancellationToken cts)
        {
            while(!cts.IsCancellationRequested)
            {
                Application.Current.Dispatcher.Invoke(() => {
                    timeText.Content = (DateTime.Now - start).ToString(@"mm\:ss");
                    topLabel.Content = (times[0] - start).ToString(@"mm\:ss");
                    topLabel.Background = ((times[0] - start) > (DateTime.Now - start)) ? Brushes.LightPink : Brushes.LightGreen;
                    jungleLabel.Content = (times[1] - start).ToString(@"mm\:ss");
                    jungleLabel.Background = ((times[1] - start) > (DateTime.Now - start)) ? Brushes.LightPink : Brushes.LightGreen;
                    midLabel.Content = (times[2] - start).ToString(@"mm\:ss");
                    midLabel.Background = ((times[2] - start) > (DateTime.Now - start)) ? Brushes.LightPink : Brushes.LightGreen;
                    adcLabel.Content = (times[3] - start).ToString(@"mm\:ss");
                    adcLabel.Background = ((times[3] - start) > (DateTime.Now - start)) ? Brushes.LightPink : Brushes.LightGreen;
                    supportLabel.Content = (times[4] - start).ToString(@"mm\:ss");
                    supportLabel.Background = ((times[4] - start) > (DateTime.Now - start)) ? Brushes.LightPink : Brushes.LightGreen;
                });
                await Task.Delay(300, cts);
            }
        }

        private void Keyboard_KeyDown(object? sender, GlobalKeyEventArgs e)
        {
            if (!started)
            {
                if(e.Key == Config.Current.SendKey)
                {
                    started = true;
                    start = DateTime.Now;
                    times = new DateTime[5] { start, start, start, start, start };
                    showTime(token.Token);
                    startPanel.Visibility = Visibility.Collapsed;
                    mainGrid.Visibility = Visibility.Visible;
                }
            }
            else
            {
                switch (e.Key)
                {
                    case var key when key == Config.Current.TopKey:
                        times[0] = DateTime.Now.AddMinutes(5);
                        text.Text += "top " + (DateTime.Now - start).ToString(@"mm\:ss") + "\n";
                        break;
                    case var key when key == Config.Current.JungleKey:
                        times[1] = DateTime.Now.AddMinutes(5);
                        text.Text += "jungle " + (DateTime.Now - start).ToString(@"mm\:ss") + "\n";
                        break;
                    case var key when key == Config.Current.MidKey:
                        times[2] = DateTime.Now.AddMinutes(5);
                        text.Text += "mid " + (DateTime.Now - start).ToString(@"mm\:ss") + "\n";
                        break;
                    case var key when key == Config.Current.AdcKey:
                        times[3] = DateTime.Now.AddMinutes(5);
                        text.Text += "adc " + (DateTime.Now - start).ToString(@"mm\:ss") + "\n";
                        break;
                    case var key when key == Config.Current.SupportKey:
                        times[4] = DateTime.Now.AddMinutes(5);
                        text.Text += "support " + (DateTime.Now - start).ToString(@"mm\:ss") + "\n";
                        break;
                    case var key when key == Config.Current.SendKey:
                        send();
                        break;
                    default:
                        return;
                }
            }
        }

        private void send()
        {
            bool hasOne = false;
            string message = Config.Current.BeforeText;
            string[] positions = new string[5]
            {
                Config.Current.TopRole,
                Config.Current.JungleRole,
                Config.Current.MidRole,
                Config.Current.AdcRole,
                Config.Current.SupportRole
            };

            for (int i = 0; i < 5; i++)
            {
                if (times[i] > DateTime.Now)
                {
                    TimeSpan time = times[i] - start;
                    hasOne = true;
                    string timestr = time.Minutes.ToString("00") + ":" + time.Seconds.ToString("00");
                    message += " " + Config.Current.TextTemplate.Replace("{role}", positions[i]).Replace("{time}", timestr);
                }
            }

            if(hasOne)
            {
                keyboard.SendKeyPress(Key.Enter);
                Task.Delay(100).Wait();
                keyboard.TypeTextPhysical(message);
                Task.Delay(100).Wait();
                keyboard.SendKeyPress(Key.Enter);
                text.Text += "sent: " + message + "\n";
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            token.Cancel();
            keyboard.Stop();
            keyboard.Dispose();
            main.Show();
        }
    }
}
