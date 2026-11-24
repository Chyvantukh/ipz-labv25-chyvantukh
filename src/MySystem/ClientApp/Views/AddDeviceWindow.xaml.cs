using System;
using System.Windows;
using System.Windows.Controls;

namespace ClientApp.Views
{
    /// <summary>
    /// Логіка модального вікна додавання нового пристрою Smart House.
    /// </summary>
    public partial class AddDeviceWindow : Window
    {
        public string DeviceName { get; private set; } = string.Empty;
        public string DeviceType { get; private set; } = string.Empty;
        public string InitialDetails { get; private set; } = string.Empty;
        public bool IsDeviceOn { get; private set; }

        public AddDeviceWindow()
        {
            InitializeComponent();
        }

        private void BtnAddSubmit_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtDeviceName.Text.Trim().Replace("|", "/").Replace("~", "-").Replace(";", ",");
            string details = TxtInitialDetails.Text.Trim().Replace("|", "/").Replace("~", "-").Replace(";", ",");
            string type = (CmbDeviceType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Автоматика";

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(details))
            {
                MessageBox.Show("Будь ласка, заповніть назву та початковий параметр пристрою!", "Помилка введення",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DeviceName = name;
            DeviceType = type;
            InitialDetails = details;
            IsDeviceOn = ChkIsOn.IsChecked == true;

            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}