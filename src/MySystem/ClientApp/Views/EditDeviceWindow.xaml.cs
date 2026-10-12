using System;
using System.Text.RegularExpressions;
using System.Windows;
using ClientApp.Models;

namespace ClientApp.Views
{
    // Логіка модального вікна редагування параметрів пристрою
    public partial class EditDeviceWindow : Window
    {
        // Посилання на редагований пристрій
        public SmartDevice Device { get; }

        public EditDeviceWindow(SmartDevice device)
        {
            InitializeComponent();
            Device = device;

            // Відображаємо назву пристрою
            TxtDeviceTitle.Text = $"{Device.Name} ({Device.Type})";

            // Налаштовуємо підказку та витягуємо суто чисельне значення для редагування
            if (Device.Type == "Клімат")
            {
                TxtValuePrompt.Text = "Введіть температуру (°C):";
                TxtNewValue.Text = Regex.Match(Device.StatusDetails, @"-?\d+").Value;
            }
            else if (Device.Type == "Освітлення")
            {
                TxtValuePrompt.Text = "Введіть рівень яскравості (0-100%):";
                TxtNewValue.Text = Regex.Match(Device.StatusDetails, @"\d+").Value;
            }
            else
            {
                TxtValuePrompt.Text = "Введіть значення параметра:";
                TxtNewValue.Text = Device.StatusDetails;
            }
        }

        // Обробка збереження нових параметрів
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string input = TxtNewValue.Text.Trim();

                // Перевірка на порожнє значення
                if (string.IsNullOrEmpty(input))
                {
                    MessageBox.Show("Будь ласка, введіть значення!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 1. Валідація кліматичних пристроїв (граничні значення температури)
                if (Device.Type == "Клімат")
                {
                    if (!double.TryParse(input, out double temp))
                    {
                        MessageBox.Show("Введіть коректне число для температури!", "Помилка введення", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Перевірка меж для Кондиціонера (16-30°C)
                    if (Device.Name.Contains("Кондиціонер", StringComparison.OrdinalIgnoreCase))
                    {
                        if (temp < 16 || temp > 30)
                        {
                            MessageBox.Show("Для кондиціонера дозволена температура від 16°C до 30°C!", "Перевищення меж", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                    // Перевірка меж для Теплої підлоги (15-35°C)
                    else if (Device.Name.Contains("підлога", StringComparison.OrdinalIgnoreCase))
                    {
                        if (temp < 15 || temp > 35)
                        {
                            MessageBox.Show("Для теплої підлоги дозволена температура від 15°C до 35°C!", "Перевищення меж", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                    // Загальні межі для решти кліматичних приладів
                    else
                    {
                        if (temp < 15 || temp > 35)
                        {
                            MessageBox.Show("Дозволена температура для клімат-контролю від 15°C до 35°C!", "Перевищення меж", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }

                    Device.StatusDetails = $"{temp}°C";
                }
                // 2. Валідація освітлення (яскравість 0-100%)
                else if (Device.Type == "Освітлення")
                {
                    if (!int.TryParse(input, out int brightness) || brightness < 0 || brightness > 100)
                    {
                        MessageBox.Show("Яскравість має бути числом від 0 до 100%!", "Помилка введення", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    Device.StatusDetails = $"Яскравість {brightness}%";
                }
                else
                {
                    Device.StatusDetails = input;
                }

                // Фіксація часу зміни параметра
                Device.LastUpdated = DateTime.Now.ToString("HH:mm:ss");
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка збереження: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Обробка скасування дій
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}