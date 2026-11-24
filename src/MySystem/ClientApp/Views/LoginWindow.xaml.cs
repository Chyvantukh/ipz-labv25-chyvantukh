using System;
using System.Windows;
using ClientApp.Network;

namespace ClientApp.Views
{
    /// <summary>
    /// Логіка вікна авторизації з перевіркою облікових даних через TCP-сервер.
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string username = TxtUsername.Text.Trim().Replace("|", "");
                string password = TxtPassword.Password.Replace("|", "");

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("Будь ласка, заповніть усі поля!", "Помилка введення",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string response = TcpClientService.SendRequest($"LOGIN|{username}|{password}");
                string[] parts = response.Split('|', 2);

                if (parts[0] == "OK")
                {
                    MainWindow mainWindow = new MainWindow(username);
                    mainWindow.Show();
                    this.Close();
                }
                else
                {
                    string errorMsg = parts.Length > 1 ? parts[1] : "Невірний логін або пароль!";
                    MessageBox.Show(errorMsg, "Помилка авторизації",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка мережі / Сервер недоступний:\n{ex.Message}", "Помилка підключення",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOpenRegister_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RegisterWindow registerWindow = new RegisterWindow();
                registerWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка відкриття вікна: {ex.Message}", "Помилка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}