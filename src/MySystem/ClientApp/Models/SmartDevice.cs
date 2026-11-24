using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClientApp.Models
{
    /// <summary>
    /// Модель розумного пристрою з підтримкою сповіщень про зміну властивостей (INotifyPropertyChanged).
    /// </summary>
    public class SmartDevice : INotifyPropertyChanged
    {
        private int _id;
        private string _name = string.Empty;
        private string _type = string.Empty;
        private bool _isOn;
        private string _statusDetails = string.Empty;
        private string _lastUpdated = DateTime.Now.ToString("dd:MM:yyyy HH:mm");

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        public bool IsOn
        {
            get => _isOn;
            set
            {
                _isOn = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public string StatusDetails
        {
            get => _statusDetails;
            set { _statusDetails = value; OnPropertyChanged(); }
        }

        public string StatusText => IsOn ? "Увімкнено / Відкрито" : "Вимкнено / Закрито";

        public string LastUpdated
        {
            get => _lastUpdated;
            set { _lastUpdated = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}