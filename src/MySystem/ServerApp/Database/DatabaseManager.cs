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
        private readonly object _dbLock = new();

        public string DatabaseFullPath { get; }

        /// <summary>
        /// Ініціалізує підключення до файлу БД та перевіряє наявність таблиць.
        /// </summary>
        public DatabaseManager(string dbPath = "smarthouse.db")
        {
            DatabaseFullPath = Path.GetFullPath(dbPath);
            _connectionString = $"Data Source={DatabaseFullPath}";
            InitializeDatabase();
        }

        /// <summary>
        /// Створює таблиці Users, Devices та EventLogs і заповнює БД стартовими даними
        /// для демонстрації роботи системи Smart House (варіант 25).
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

                var checkDevices = connection.CreateCommand();
                checkDevices.CommandText = "SELECT COUNT(*) FROM Devices;";
                long deviceCount = (long)(checkDevices.ExecuteScalar() ?? 0);

                if (deviceCount == 0)
                {
                    string now = DateTime.Now.ToString("HH:mm:ss");
                    var insertDevices = connection.CreateCommand();
                    insertDevices.CommandText = @"
                        INSERT INTO Devices (Name, Type, IsOn, StatusDetails, LastUpdated) VALUES
                        ('Кондиціонер у вітальні', 'Клімат', 1, '22°C', @now),
                        ('Освітлення кухні', 'Освітлення', 0, 'Яскравість 0%', @now),
                        ('Розумні вхідні двері', 'Безпека', 1, 'Зачинено', @now),
                        ('Віконні жалюзі', 'Автоматика', 1, 'Відкрито на 75%', @now),
                        ('Електрочайник', 'Побутові прилади', 0, 'Очікування (24°C)', @now),
                        ('Тепла підлога', 'Клімат', 0, '20°C', @now);
                    ";
                    insertDevices.Parameters.AddWithValue("@now", now);
                    insertDevices.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Перевіряє облікові дані користувача та реєструє вхід у таблиці EventLogs.
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
        /// Реєструє нового користувача в таблиці Users за умови унікальності логіна.
        /// </summary>
        public (bool Success, string Message) RegisterUser(string username, string email, string password)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM Users WHERE LOWER(Username) = LOWER(@username);";
                checkCmd.Parameters.AddWithValue("@username", username);

                long exists = (long)(checkCmd.ExecuteScalar() ?? 0);
                if (exists > 0)
                    return (false, "Даний логін уже зайнятий!");

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
        /// Фіксує вихід (деавторизацію) користувача в журналі подій EventLogs.
        /// </summary>
        public (bool Success, string Message) LogoutUser(string username)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                LogEventInternal(connection, null, username, "LOGOUT", "Вихід користувача із системи");
                return (true, "Вихід виконано");
            }
        }

        /// <summary>
        /// Зчитує всі пристрої з таблиці Devices і формує серіалізований список для передачі по TCP.
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
        /// Перемикає стан живлення пристрою (IsOn: 0 <-> 1) у таблиці Devices.
        /// </summary>
        public (bool Success, string Payload) ToggleDevice(int deviceId, string username)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var selectCmd = connection.CreateCommand();
                selectCmd.CommandText = "SELECT Name, IsOn FROM Devices WHERE Id = @id;";
                selectCmd.Parameters.AddWithValue("@id", deviceId);

                using var reader = selectCmd.ExecuteReader();
                if (!reader.Read())
                    return (false, $"Пристрій з ID={deviceId} не знайдено!");

                string name = reader.GetString(0);
                int currentIsOn = reader.GetInt32(1);
                reader.Close();

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
        /// Оновлює параметр пристрою (StatusDetails) та час зміни в таблиці Devices.
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
        /// Перевіряє відсутність дубліката за назвою та додає новий пристрій до таблиці Devices.
        /// </summary>
        public (bool Success, string Payload) AddDevice(string name, string type, bool isOn, string details, string username)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM Devices WHERE LOWER(Name) = LOWER(@name);";
                checkCmd.Parameters.AddWithValue("@name", name);

                long exists = (long)(checkCmd.ExecuteScalar() ?? 0);
                if (exists > 0)
                    return (false, $"Пристрій '{name}' вже існує в системі!");

                string updatedTime = DateTime.Now.ToString("HH:mm:ss");

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

                long newId = (long)(insertCmd.ExecuteScalar() ?? 0);
                LogEventInternal(connection, (int)newId, username, "ADD_DEVICE", $"Додано пристрій: {name} ({type})");

                return (true, $"{newId}|Пристрій '{name}' успішно додано");
            }
        }

        /// <summary>
        /// Видаляє пристрій із таблиці Devices за його ідентифікатором та фіксує дію в EventLogs.
        /// </summary>
        public (bool Success, string Payload) DeleteDevice(int deviceId, string username)
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

                var deleteCmd = connection.CreateCommand();
                deleteCmd.CommandText = "DELETE FROM Devices WHERE Id = @id;";
                deleteCmd.Parameters.AddWithValue("@id", deviceId);
                deleteCmd.ExecuteNonQuery();

                LogEventInternal(connection, deviceId, username, "DELETE_DEVICE", $"Видалено пристрій: {name}");
                return (true, $"Пристрій '{name}' успішно видалено");
            }
        }

        /// <summary>
        /// Записує подію аудиту в таблицю EventLogs у межах поточного з'єднання з БД.
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
            logCmd.Parameters.AddWithValue("@ts", DateTime.Now.ToString("HH:mm:ss"));
            logCmd.ExecuteNonQuery();
        }
    }
}