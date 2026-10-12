using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClientApp.Models
{
    // Модель розумного пристрою з підтримкою сповіщень про зміну властивостей (INotifyPropertyChanged)
    public class SmartDevice : INotifyPropertyChanged
    {
        // Приватні поля
        private string _name = string.Empty;
        private string _type = string.Empty;
        private bool _isOn;
        private string _statusDetails = string.Empty;
        private string _lastUpdated = DateTime.Now.ToString("HH:mm");

        // Назва пристрою
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        // Тип пристрою (Клімат, Освітлення, Безпека тощо)
        public string Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        // Стан пристрою (увімкнено/вимкнено) з оновленням залежної властивості StatusText
        public bool IsOn
        {
            get => _isOn;
            set
            {
                _isOn = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText)); // Оновлюємо статусний текст у UI
            }
        }

        // Деталі параметра (температура, яскравість тощо)
        public string StatusDetails
        {
            get => _statusDetails;
            set { _statusDetails = value; OnPropertyChanged(); }
        }

        // Обчислювана властивість для текстового відображення стану
        public string StatusText => IsOn ? "Увімкнено / Відкрито" : "Вимкнено / Закрито";

        // Час останнього оновлення пристрою
        public string LastUpdated
        {
            get => _lastUpdated;
            set { _lastUpdated = value; OnPropertyChanged(); }
        }

        // Подія для реалізації Data Binding у WPF
        public event PropertyChangedEventHandler? PropertyChanged;

        // Метод для виклику події оновлення інтерфейсу
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}