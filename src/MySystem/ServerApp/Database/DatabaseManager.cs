using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace ServerApp.Database
{
    /// <summary>
    /// Клас для роботи з реляційною базою даних SQLite (smarthouse.db).
    /// Забезпечує створення таблиць, початкове наповнення та потокобезпечне виконання SQL-запитів.
    /// </summary>
    public class DatabaseManager
    {
        private readonly string _connectionString;
        // Об'єкт синхронізації, щоб два потоки не намагалися одночасно змінювати файл SQLite
        private readonly object _dbLock = new();

        public string DatabaseFullPath { get; }

        /// <summary>
        /// Формує рядок підключення до файлу БД та запускає перевірку/створення структури таблиць.
        /// </summary>
        public DatabaseManager(string dbPath = "smarthouse.db")
        {
            DatabaseFullPath = Path.GetFullPath(dbPath);
            _connectionString = $"Data Source={DatabaseFullPath}";
            InitializeDatabase();
        }

        /// <summary>
        /// Створює таблиці Users, Devices та EventLogs (якщо файл БД новий)
        /// і заповнює їх стартовими даними (адміністратор admin та 4 базові пристрої).
        /// </summary>
        private void InitializeDatabase()
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Username TEXT NOT NULL UNIQUE,
                        Email TEXT NOT NULL,
                        Password TEXT NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS Devices (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Type TEXT NOT NULL,
                        IsOn INTEGER NOT NULL,
                        StatusDetails TEXT NOT NULL,
                        LastUpdated TEXT NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS EventLogs (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        DeviceId INTEGER,
                        Username TEXT NOT NULL,
                        Action TEXT NOT NULL,
                        Details TEXT NOT NULL,
                        Timestamp TEXT NOT NULL
                    );
                ";
                cmd.ExecuteNonQuery();

                // Перевіряємо, чи є користувачі в таблиці; якщо порожня — додаємо admin
                var checkUsers = connection.CreateCommand();
                checkUsers.CommandText = "SELECT COUNT(*) FROM Users;";
                long userCount = (long)(checkUsers.ExecuteScalar() ?? 0);

                if (userCount == 0)
                {
                    var insertUser = connection.CreateCommand();
                    insertUser.CommandText = @"
                        INSERT INTO Users (Username, Email, Password)
                        VALUES ('admin', 'admin@gmail.com', 'admin');
                    ";
                    insertUser.ExecuteNonQuery();
                }

                // Перевіряємо, чи є пристрої в таблиці; якщо порожня — додаємо 4 стартові пристрої
                var checkDevices = connection.CreateCommand();
                checkDevices.CommandText = "SELECT COUNT(*) FROM Devices;";
                long deviceCount = (long)(checkDevices.ExecuteScalar() ?? 0);

                if (deviceCount == 0)
                {
                    string now = DateTime.Now.ToString("dd:MM:yyyy HH:mm");
                    var insertDevices = connection.CreateCommand();
                    insertDevices.CommandText = @"
                        INSERT INTO Devices (Name, Type, IsOn, StatusDetails, LastUpdated) VALUES
                        ('Кондиціонер у вітальні', 'Клімат', 1, '22°C', @now),
                        ('Освітлення кухні', 'Освітлення', 0, 'Яскравість 0%', @now),
                        ('Розумний замок', 'Безпека', 1, 'Зачинено', @now),
                        ('Тепла підлога', 'Клімат', 0, '20°C', @now);
                    ";
                    insertDevices.Parameters.AddWithValue("@now", now);
                    insertDevices.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Перевіряє логін і пароль користувача в таблиці Users.
        /// У разі успіху повертає (true, email) та записує подію входу в EventLogs.
        /// </summary>
        public (bool Success, string Message) ValidateUser(string username, string password)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT Email FROM Users 
                    WHERE LOWER(Username) = LOWER(@username) AND Password = @password;
                ";
                // Використовуємо параметри (@username, @password) для захисту від SQL-ін'єкцій
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@password", password);

                var email = cmd.ExecuteScalar()?.ToString();
                if (!string.IsNullOrEmpty(email))
                {
                    LogEventInternal(connection, null, username, "LOGIN", "Успішний вхід у систему");
                    return (true, email);
                }

                return (false, "Невірний логін або пароль!");
            }
        }

        /// <summary>
        /// Реєструє нового користувача в таблиці Users, якщо такий логін ще не зайнятий.
        /// </summary>
        public (bool Success, string Message) RegisterUser(string username, string email, string password)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                // Спочатку перевіряємо унікальність логіна
                var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM Users WHERE LOWER(Username) = LOWER(@username);";
                checkCmd.Parameters.AddWithValue("@username", username);

                long exists = (long)(checkCmd.ExecuteScalar() ?? 0);
                if (exists > 0)
                    return (false, "Даний логін уже зайнятий!");

                // Додаємо новий обліковий запис у таблицю Users
                var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO Users (Username, Email, Password)
                    VALUES (@username, @email, @password);
                ";
                insertCmd.Parameters.AddWithValue("@username", username);
                insertCmd.Parameters.AddWithValue("@email", email);
                insertCmd.Parameters.AddWithValue("@password", password);
                insertCmd.ExecuteNonQuery();

                LogEventInternal(connection, null, username, "REGISTER", $"Реєстрація акаунта ({email})");
                return (true, "Реєстрація успішна!");
            }
        }

        /// <summary>
        /// Зчитує всі записи з таблиці Devices та серіалізує кожен рядок у формат:
        /// id~name~type~isOn~details~updated для передачі по TCP-мережі.
        /// </summary>
        public List<string> GetAllDevicesSerialized()
        {
            lock (_dbLock)
            {
                var list = new List<string>();
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT Id, Name, Type, IsOn, StatusDetails, LastUpdated FROM Devices ORDER BY Id;";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    string name = reader.GetString(1);
                    string type = reader.GetString(2);
                    int isOn = reader.GetInt32(3);
                    string details = reader.GetString(4);
                    string updated = reader.GetString(5);

                    list.Add($"{id}~{name}~{type}~{isOn}~{details}~{updated}");
                }

                return list;
            }
        }

        /// <summary>
        /// Інвертує стан живлення пристрою (IsOn: 0 <-> 1) за його Id,
        /// оновлює час останньої зміни та фіксує дію в журналі EventLogs.
        /// </summary>
        public (bool Success, string Payload) ToggleDevice(int deviceId, string username)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                // 1. Отримуємо назву та поточний стан пристрою
                var selectCmd = connection.CreateCommand();
                selectCmd.CommandText = "SELECT Name, IsOn FROM Devices WHERE Id = @id;";
                selectCmd.Parameters.AddWithValue("@id", deviceId);

                using var reader = selectCmd.ExecuteReader();
                if (!reader.Read())
                    return (false, $"Пристрій з ID={deviceId} не знайдено!");

                string name = reader.GetString(0);
                int currentIsOn = reader.GetInt32(1);
                reader.Close(); // Закриваємо reader перед виконанням запиту UPDATE

                // 2. Обчислюємо новий стан та оновлюємо запис у таблиці Devices
                int newIsOn = currentIsOn == 1 ? 0 : 1;
                string updatedTime = DateTime.Now.ToString("HH:mm:ss");
                string statusText = newIsOn == 1 ? "Увімкнено / Відкрито" : "Вимкнено / Закрито";

                var updateCmd = connection.CreateCommand();
                updateCmd.CommandText = @"
                    UPDATE Devices 
                    SET IsOn = @isOn, LastUpdated = @updated 
                    WHERE Id = @id;
                ";
                updateCmd.Parameters.AddWithValue("@isOn", newIsOn);
                updateCmd.Parameters.AddWithValue("@updated", updatedTime);
                updateCmd.Parameters.AddWithValue("@id", deviceId);
                updateCmd.ExecuteNonQuery();

                LogEventInternal(connection, deviceId, username, "TOGGLE_DEVICE", $"{name}: {statusText}");
                return (true, $"{newIsOn}|{statusText}|{updatedTime}");
            }
        }

        /// <summary>
        /// Оновлює текстовий параметр пристрою (StatusDetails, наприклад температуру чи яскравість)
        /// у таблиці Devices за його ідентифікатором.
        /// </summary>
        public (bool Success, string Payload) UpdateDeviceDetails(int deviceId, string newDetails, string username)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var selectCmd = connection.CreateCommand();
                selectCmd.CommandText = "SELECT Name FROM Devices WHERE Id = @id;";
                selectCmd.Parameters.AddWithValue("@id", deviceId);

                var name = selectCmd.ExecuteScalar()?.ToString();
                if (string.IsNullOrEmpty(name))
                    return (false, $"Пристрій з ID={deviceId} не знайдено!");

                string updatedTime = DateTime.Now.ToString("HH:mm:ss");

                var updateCmd = connection.CreateCommand();
                updateCmd.CommandText = @"
                    UPDATE Devices 
                    SET StatusDetails = @details, LastUpdated = @updated 
                    WHERE Id = @id;
                ";
                updateCmd.Parameters.AddWithValue("@details", newDetails);
                updateCmd.Parameters.AddWithValue("@updated", updatedTime);
                updateCmd.Parameters.AddWithValue("@id", deviceId);
                updateCmd.ExecuteNonQuery();

                LogEventInternal(connection, deviceId, username, "UPDATE_DEVICE", $"{name} -> {newDetails}");
                return (true, $"{newDetails}|{updatedTime}");
            }
        }

        /// <summary>
        /// Додає новий пристрій до таблиці Devices та повертає згенерований автоінкрементний Id.
        /// </summary>
        public (bool Success, string Payload) AddDevice(string name, string type, bool isOn, string details, string username)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                string updatedTime = DateTime.Now.ToString("dd:MM:yyyy HH:mm");

                var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO Devices (Name, Type, IsOn, StatusDetails, LastUpdated)
                    VALUES (@name, @type, @isOn, @details, @updated);
                    SELECT last_insert_rowid();
                ";
                insertCmd.Parameters.AddWithValue("@name", name);
                insertCmd.Parameters.AddWithValue("@type", type);
                insertCmd.Parameters.AddWithValue("@isOn", isOn ? 1 : 0);
                insertCmd.Parameters.AddWithValue("@details", details);
                insertCmd.Parameters.AddWithValue("@updated", updatedTime);

                // Отримуємо Id щойно вставленого рядка
                long newId = (long)(insertCmd.ExecuteScalar() ?? 0);
                LogEventInternal(connection, (int)newId, username, "ADD_DEVICE", $"Додано пристрій: {name} ({type})");

                return (true, $"{newId}|Пристрій '{name}' успішно додано");
            }
        }

        /// <summary>
        /// Допоміжний метод для запису події аудиту в таблицю EventLogs
        /// у межах уже відкритого з'єднання з базою даних.
        /// </summary>
        private static void LogEventInternal(SqliteConnection connection, int? deviceId, string username, string action, string details)
        {
            var logCmd = connection.CreateCommand();
            logCmd.CommandText = @"
                INSERT INTO EventLogs (DeviceId, Username, Action, Details, Timestamp)
                VALUES (@deviceId, @username, @action, @details, @ts);
            ";
            logCmd.Parameters.AddWithValue("@deviceId", deviceId.HasValue ? deviceId.Value : DBNull.Value);
            logCmd.Parameters.AddWithValue("@username", username);
            logCmd.Parameters.AddWithValue("@action", action);
            logCmd.Parameters.AddWithValue("@details", details);
            logCmd.Parameters.AddWithValue("@ts", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"));
            logCmd.ExecuteNonQuery();
        }
    }
}