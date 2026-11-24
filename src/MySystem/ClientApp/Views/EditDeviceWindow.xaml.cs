using System;
using System.Text.RegularExpressions;
using System.Windows;
using ClientApp.Models;

namespace ClientApp.Views
{
    /// <summary>
    /// Логіка модального вікна редагування параметрів пристрою.
    /// </summary>
    public partial class EditDeviceWindow : Window
    {
        public SmartDevice Device { get; }
        public string UpdatedDetails { get; private set; } = string.Empty;

        public EditDeviceWindow(SmartDevice device)
        {
            InitializeComponent();
            Device = device;

            TxtDeviceTitle.Text = $"{Device.Name} ({Device.Type})";

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
                TxtValuePrompt.Text = "Введіть нове значення параметра:";
                TxtNewValue.Text = Device.StatusDetails;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string input = TxtNewValue.Text.Trim().Replace("|", "/").Replace("~", "-").Replace(";", ",");

                if (string.IsNullOrEmpty(input))
                {
                    MessageBox.Show("Будь ласка, введіть значення!", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (Device.Type == "Клімат")
                {
                    if (!double.TryParse(input, out double temp))
                    {
                        MessageBox.Show("Введіть коректне число для температури!", "Помилка введення", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (Device.Name.Contains("Кондиціонер", StringComparison.OrdinalIgnoreCase))
                    {
                        if (temp < 16 || temp > 30)
                        {
                            MessageBox.Show("Для кондиціонера дозволена температура від 16°C до 30°C!", "Перевищення меж", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                    else if (Device.Name.Contains("підлога", StringComparison.OrdinalIgnoreCase))
                    {
                        if (temp < 15 || temp > 35)
                        {
                            MessageBox.Show("Для теплої підлоги дозволена температура від 15°C до 35°C!", "Перевищення меж", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                    else if (temp < 15 || temp > 35)
                    {
                        MessageBox.Show("Дозволена температура для клімат-контролю від 15°C до 35°C!", "Перевищення меж", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    UpdatedDetails = $"{temp}°C";
                }
                else if (Device.Type == "Освітлення")
                {
                    if (!int.TryParse(input, out int brightness) || brightness < 0 || brightness > 100)
                    {
                        MessageBox.Show("Яскравість має бути числом від 0 до 100%!", "Помилка введення", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    UpdatedDetails = $"Яскравість {brightness}%";
                }
                else
                {
                    UpdatedDetails = input;
                }

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка перевірки даних: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}