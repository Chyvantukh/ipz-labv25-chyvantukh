using System;
using System.Collections.ObjectModel;
using System.Windows;
using ClientApp.Models;

namespace ClientApp.Views
{
    // Логіка головного вікна керування пристроями
    public partial class MainWindow : Window
    {
        // Колекція пристроїв із підтримкою автоматичного оновлення UI (ObservableCollection)
        public ObservableCollection<SmartDevice> Devices { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                // Початкова ініціалізація списку пристроїв
                Devices = new ObservableCollection<SmartDevice>
                {
                    new SmartDevice { Name = "Кондиціонер у вітальні", Type = "Клімат", IsOn = true, StatusDetails = "22°C" },
                    new SmartDevice { Name = "Освітлення кухні", Type = "Освітлення", IsOn = false, StatusDetails = "Яскравість 0%" },
                    new SmartDevice { Name = "Розумний замок", Type = "Безпека", IsOn = true, StatusDetails = "Зачинено" },
                    new SmartDevice { Name = "Тепла підлога", Type = "Клімат", IsOn = false, StatusDetails = "20°C" }
                };

                DgDevices.ItemsSource = Devices;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження пристроїв: {ex.Message}", "Помилка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Перемикання стану увімкнено/вимкнено
        private void BtnToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DgDevices.SelectedItem is SmartDevice selectedDevice)
                {
                    selectedDevice.IsOn = !selectedDevice.IsOn;
                    selectedDevice.LastUpdated = DateTime.Now.ToString("HH:mm:ss");
                }
                else
                {
                    MessageBox.Show("Будь ласка, виберіть пристрій зі списку!", "Попередження",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка керування пристроєм: {ex.Message}", "Помилка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Редагування параметра (замок змінюється прямо по кнопці, для числових — відкриваємо модальне вікно)
        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DgDevices.SelectedItem is SmartDevice selectedDevice)
                {
                    if (selectedDevice.Type == "Безпека")
                    {
                        selectedDevice.StatusDetails = selectedDevice.StatusDetails == "Зачинено" ? "Відчинено" : "Зачинено";
                        selectedDevice.LastUpdated = DateTime.Now.ToString("HH:mm:ss");
                    }
                    else
                    {
                        EditDeviceWindow editWindow = new EditDeviceWindow(selectedDevice);
                        editWindow.Owner = this;
                        editWindow.ShowDialog();
                    }
                }
                else
                {
                    MessageBox.Show("Будь ласка, виберіть пристрій зі списку!", "Попередження",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка оновлення параметра: {ex.Message}", "Помилка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Завершення сеансу та повернення до авторизації
        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show("Ви дійсно бажаєте вийти з системи?", "Підтвердження виходу",
                                             MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    LoginWindow loginWindow = new LoginWindow();
                    loginWindow.Show();
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка під час виходу: {ex.Message}", "Помилка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}