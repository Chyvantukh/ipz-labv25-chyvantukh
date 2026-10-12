using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ClientApp.Models;

namespace ClientApp.Views
{
    // Логіка вікна авторизації
    public partial class LoginWindow : Window
    {
        // Статичний список користувачів (імітація бази даних для Лабораторної №2)
        public static List<User> RegisteredUsers = new List<User>
        {
            new User("admin", "admin@smarthouse.com", "admin")
        };

        public LoginWindow()
        {
            InitializeComponent();
        }

        // Перевірка введених даних та перехід до головного вікна
        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string username = TxtUsername.Text.Trim();
                string password = TxtPassword.Password;

                // Перевірка на заповнення полів
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("Будь ласка, заповніть усі поля!", "Помилка введення",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Перевірка наявності користувача
                bool userExists = RegisteredUsers.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)
                                                         && u.Password == password);

                if (userExists)
                {
                    // Перехід до головного вікна без додаткових підтверджень
                    MainWindow mainWindow = new MainWindow();
                    mainWindow.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Невірний логін або пароль!", "Помилка авторизації",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Виникла помилка під час авторизації: {ex.Message}", "Помилка системи",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Перехід до вікна реєстрації
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