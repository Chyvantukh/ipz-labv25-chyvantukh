namespace ClientApp.Models
{
    // Модель користувача системи
    public class User
    {
        // Приватні поля
        private string _username = string.Empty;
        private string _email = string.Empty;
        private string _password = string.Empty;

        // Логін користувача з методами get/set
        public string Username
        {
            get => _username;
            set => _username = value;
        }

        // Email користувача з методами get/set
        public string Email
        {
            get => _email;
            set => _email = value;
        }

        // Пароль користувача з методами get/set
        public string Password
        {
            get => _password;
            set => _password = value;
        }

        // Конструктор за замовчуванням
        public User() { }

        // Конструктор із параметрами
        public User(string username, string email, string password)
        {
            _username = username;
            _email = email;
            _password = password;
        }
    }
}