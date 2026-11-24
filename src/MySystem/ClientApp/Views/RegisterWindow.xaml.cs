using System;
using System.Text.RegularExpressions;
using System.Windows;
using ClientApp.Network;

namespace ClientApp.Views
{
    /// <summary>
    /// Логіка вікна реєстрації користувача із записом нового акаунта в БД через TCP-сервер.
    /// </summary>
    public partial class RegisterWindow : Window
    {
        public RegisterWindow()
        {
            InitializeComponent();
        }

        private void BtnRegisterSubmit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string username = TxtRegisterUsername.Text.Trim().Replace("|", "");
                string email = TxtRegisterEmail.Text.Trim().Replace("|", "");
                string password = TxtRegisterPassword.Password.Replace("|", "");
                string passwordConfirm = TxtRegisterPasswordConfirm.Password.Replace("|", "");

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("Заповніть усі обов'язкові поля!", "Помилка введення",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (username.Length < 5)
                {
                    MessageBox.Show("Логін повинен містити мінімум 5 символів!", "Помилка введення",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
                if (!Regex.IsMatch(email, emailPattern))
                {
                    MessageBox.Show("Введіть коректну адресу електронної пошти (наприклад, user@gmail.com)!", "Помилка введення",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (password.Length < 5)
                {
                    MessageBox.Show("Пароль повинен містити мінімум 5 символів!", "Помилка введення",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (password != passwordConfirm)
                {
                    MessageBox.Show("Паролі не збігаються!", "Помилка введення",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string response = TcpClientService.SendRequest($"REGISTER|{username}|{email}|{password}");
                string[] parts = response.Split('|', 2);

                if (parts[0] == "OK")
                {
                    MessageBox.Show(parts.Length > 1 ? parts[1] : "Реєстрація успішна!", "Успіх",
                                    MessageBoxButton.OK, MessageBoxImage.Information);

                    LoginWindow login = new LoginWindow();
                    login.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show(parts.Length > 1 ? parts[1] : "Помилка реєстрації!", "Помилка реєстрації",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка мережі / Сервер недоступний:\n{ex.Message}", "Помилка підключення",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBackToLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoginWindow login = new LoginWindow();
                login.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка переходу: {ex.Message}", "Помилка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}