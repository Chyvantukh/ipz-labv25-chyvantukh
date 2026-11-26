using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using ClientApp.Models;
using ClientApp.Network;

namespace ClientApp.Views
{
    /// <summary>
    /// Логіка головного вікна дистанційного керування та моніторингу пристроїв Smart House.
    /// Усі операції синхронізуються з базою даних SQLite через TCP-сервер.
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<SmartDevice> Devices { get; set; } = new();
        private readonly string _currentUsername;
        private readonly DispatcherTimer _autoRefreshTimer;

        public MainWindow() : this("admin")
        {
        }

        public MainWindow(string username)
        {
            InitializeComponent();
            _currentUsername = username;
            TxtCurrentUser.Text = $"Користувач: {_currentUsername}";
            DgDevices.ItemsSource = Devices;

            LoadDevicesFromServer();

            // Налаштування фонового опитування сервера кожні 3 секунди для синхронізації між клієнтами
            _autoRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _autoRefreshTimer.Tick += AutoRefreshTimer_Tick;
            _autoRefreshTimer.Start();
        }

        private void AutoRefreshTimer_Tick(object? sender, EventArgs e)
        {
            LoadDevicesFromServer(isSilent: true);
        }

        protected override void OnClosed(EventArgs e)
        {
            _autoRefreshTimer.Stop();
            base.OnClosed(e);
        }

        /// <summary>
        /// Завантажує актуальний перелік пристроїв із БД через TCP-команду GET_DEVICES.
        /// </summary>
        private void LoadDevicesFromServer(bool isSilent = false)
        {
            try
            {
                // Запам'ятовуємо Id виділеного пристрою, щоб не скидати вибір користувача при фоновому оновленні
                int? selectedId = (DgDevices.SelectedItem as SmartDevice)?.Id;

                string response = TcpClientService.SendRequest("GET_DEVICES");
                string[] parts = response.Split('|', 2);

                if (parts[0] == "OK" && parts.Length > 1)
                {
                    Devices.Clear();
                    string[] items = parts[1].Split(';', StringSplitOptions.RemoveEmptyEntries);

                    foreach (string item in items)
                    {
                        string[] fields = item.Split('~');
                        if (fields.Length >= 6 && int.TryParse(fields[0], out int id))
                        {
                            var device = new SmartDevice
                            {
                                Id = id,
                                Name = fields[1],
                                Type = fields[2],
                                IsOn = fields[3] == "1",
                                StatusDetails = fields[4],
                                LastUpdated = fields[5]
                            };
                            Devices.Add(device);

                            if (selectedId.HasValue && device.Id == selectedId.Value)
                            {
                                DgDevices.SelectedItem = device;
                            }
                        }
                    }

                    TxtStatusBar.Text = $"Статус: Синхронізовано з сервером ({DateTime.Now:HH:mm:ss}) | Пристроїв: {Devices.Count}";
                }
                else
                {
                    string err = parts.Length > 1 ? parts[1] : "Не вдалося отримати дані";
                    TxtStatusBar.Text = $"Статус: {err}";
                }
            }
            catch (Exception ex)
            {
                TxtStatusBar.Text = "Статус: Помилка мережі / Сервер недоступний";
                if (!isSilent)
                {
                    MessageBox.Show($"Помилка мережі / Сервер недоступний:\n{ex.Message}", "Помилка моніторингу",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Перемикає стан увімкнено/вимкнено вибраного пристрою через команду TOGGLE_DEVICE.
        /// </summary>
        private void BtnToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DgDevices.SelectedItem is not SmartDevice selectedDevice)
                {
                    MessageBox.Show("Будь ласка, виберіть пристрій зі списку!", "Попередження",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string response = TcpClientService.SendRequest($"TOGGLE_DEVICE|{selectedDevice.Id}|{_currentUsername}");
                string[] parts = response.Split('|');

                if (parts[0] == "OK" && parts.Length >= 4)
                {
                    selectedDevice.IsOn = parts[1] == "1";
                    selectedDevice.LastUpdated = parts[3];
                    TxtStatusBar.Text = $"Команду виконано: '{selectedDevice.Name}' -> {selectedDevice.StatusText}";
                }
                else
                {
                    MessageBox.Show(parts.Length > 1 ? parts[1] : "Помилка виконання команди", "Помилка",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                TxtStatusBar.Text = "Статус: Помилка зв'язку із сервером";
                MessageBox.Show($"Помилка керування пристроєм:\n{ex.Message}", "Помилка мережі",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Оновлює робочий параметр пристрою через команду UPDATE_DEVICE.
        /// </summary>
        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DgDevices.SelectedItem is not SmartDevice selectedDevice)
                {
                    MessageBox.Show("Будь ласка, виберіть пристрій зі списку!", "Попередження",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string newDetails;
                if (selectedDevice.Type == "Безпека")
                {
                    newDetails = selectedDevice.StatusDetails == "Зачинено" ? "Відчинено" : "Зачинено";
                }
                else
                {
                    EditDeviceWindow editWindow = new EditDeviceWindow(selectedDevice) { Owner = this };
                    if (editWindow.ShowDialog() != true)
                        return;

                    newDetails = editWindow.UpdatedDetails;
                }

                string response = TcpClientService.SendRequest($"UPDATE_DEVICE|{selectedDevice.Id}|{newDetails}|{_currentUsername}");
                string[] parts = response.Split('|');

                if (parts[0] == "OK" && parts.Length >= 3)
                {
                    selectedDevice.StatusDetails = parts[1];
                    selectedDevice.LastUpdated = parts[2];
                    TxtStatusBar.Text = $"Команду виконано: '{selectedDevice.Name}' -> {selectedDevice.StatusDetails}";
                }
                else
                {
                    MessageBox.Show(parts.Length > 1 ? parts[1] : "Не вдалося оновити параметр", "Помилка",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                TxtStatusBar.Text = "Статус: Помилка зв'язку із сервером";
                MessageBox.Show($"Помилка оновлення параметра:\n{ex.Message}", "Помилка мережі",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Відкриває діалог додавання нового пристрою та зберігає його в БД через команду ADD_DEVICE.
        /// </summary>
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddDeviceWindow addWindow = new AddDeviceWindow { Owner = this };
                if (addWindow.ShowDialog() != true)
                    return;

                int isOnFlag = addWindow.IsDeviceOn ? 1 : 0;
                string packet = $"ADD_DEVICE|{addWindow.DeviceName}|{addWindow.DeviceType}|{isOnFlag}|{addWindow.InitialDetails}|{_currentUsername}";

                string response = TcpClientService.SendRequest(packet);
                string[] parts = response.Split('|');

                if (parts[0] == "OK")
                {
                    LoadDevicesFromServer();
                    TxtStatusBar.Text = $"Команду виконано: додано '{addWindow.DeviceName}'";
                    MessageBox.Show(parts.Length > 2 ? parts[2] : "Пристрій успішно додано!", "Успіх",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(parts.Length > 1 ? parts[1] : "Не вдалося додати пристрій", "Помилка додавання",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка додавання пристрою:\n{ex.Message}", "Помилка мережі",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Видаляє вибраний пристрій із бази даних через команду DELETE_DEVICE.
        /// </summary>
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DgDevices.SelectedItem is not SmartDevice selectedDevice)
                {
                    MessageBox.Show("Будь ласка, виберіть пристрій для видалення!", "Попередження",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var confirm = MessageBox.Show($"Ви дійсно бажаєте видалити пристрій '{selectedDevice.Name}'?",
                                              "Підтвердження видалення", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes)
                    return;

                string response = TcpClientService.SendRequest($"DELETE_DEVICE|{selectedDevice.Id}|{_currentUsername}");
                string[] parts = response.Split('|', 2);

                if (parts[0] == "OK")
                {
                    string removedName = selectedDevice.Name;
                    Devices.Remove(selectedDevice);
                    TxtStatusBar.Text = $"Команду виконано: видалено '{removedName}'";
                }
                else
                {
                    MessageBox.Show(parts.Length > 1 ? parts[1] : "Помилка видалення", "Помилка",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка видалення пристрою:\n{ex.Message}", "Помилка мережі",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Примусово оновлює список пристроїв із сервера.
        /// </summary>
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadDevicesFromServer();
        }

        /// <summary>
        /// Надсилає команду LOGOUT на сервер, очищає сесію та повертає у вікно входу.
        /// </summary>
        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show("Ви дійсно бажаєте вийти з системи?", "Підтвердження виходу",
                                             MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        TcpClientService.SendRequest($"LOGOUT|{_currentUsername}");
                    }
                    catch
                    {
                        // Якщо сервер тимчасово недоступний, виконуємо локальне закриття сесії згідно з UML
                    }

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