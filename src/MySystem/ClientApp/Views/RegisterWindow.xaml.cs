using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using ClientApp.Models;

namespace ClientApp.Views
{
    // Логіка вікна реєстрації
    public partial class RegisterWindow : Window
    {
        public RegisterWindow()
        {
            InitializeComponent();
        }

        // Перевірка даних реєстрації та збереження користувача
        private void BtnRegisterSubmit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string username = TxtRegisterUsername.Text.Trim();
                string email = TxtRegisterEmail.Text.Trim();
                string password = TxtRegisterPassword.Password;
                string passwordConfirm = TxtRegisterPasswordConfirm.Password;

                // 1. Валідація заповнення обов'язкових полів
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("Заповніть усі обов'язкові поля!", "Помилка введення",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2. Перевірка довжини логіна (більше 5 символів)
                if (username.Length <= 5)
                {
                    MessageBox.Show("Логін повинен містити більше 5 символів!", "Помилка введення",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 3. Валідація формату E-mail (наявність @ та домену)
                string emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
                if (!Regex.IsMatch(email, emailPattern))
                {
                    MessageBox.Show("Введіть коректну адресу електронної пошти (наприклад, user@gmail.com)!", "Помилка введення",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 4. Перевірка довжини пароля (більше 5 символів)
                if (password.Length <= 5)
                {
                    MessageBox.Show("Пароль повинен містити більше 5 символів!", "Помилка введення",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 5. Перевірка відповідності паролів
                if (password != passwordConfirm)
                {
                    MessageBox.Show("Паролі не збігаються!", "Помилка введення",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 6. Перевірка унікальності логіна
                if (LoginWindow.RegisteredUsers.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Даний логін уже зайнятий!", "Помилка реєстрації",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Додавання нового користувача у список
                LoginWindow.RegisteredUsers.Add(new User(username, email, password));

                MessageBox.Show("Реєстрація успішна!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);

                // Повернення до авторизації
                LoginWindow login = new LoginWindow();
                login.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Виникла помилка під час реєстрації: {ex.Message}", "Помилка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Повернення до вікна входу
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