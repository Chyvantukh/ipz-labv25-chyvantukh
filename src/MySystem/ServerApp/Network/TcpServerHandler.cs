using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using ServerApp.Database;

namespace ServerApp.Network
{
    /// <summary>
    /// Клас мережевого TCP-сервера.
    /// Відповідає за прийом вхідних сокет-з'єднань, розбір команд протоколу та формування відповідей.
    /// </summary>
    public class TcpServerHandler
    {
        private readonly IPAddress _ipAddress;
        private readonly int _port;
        private readonly DatabaseManager _db;
        private int _connectionCounter = 0; // Лічильник підключень для зручного логування

        /// <summary>
        /// Ініціалізує параметри TCP-сервера (IP-адресу, порт та посилання на менеджер БД).
        /// </summary>
        public TcpServerHandler(string ip, int port, DatabaseManager db)
        {
            _ipAddress = IPAddress.Parse(ip);
            _port = port;
            _db = db;
        }

        /// <summary>
        /// Запускає прослуховування порту та в асинхронному циклі приймає клієнтів.
        /// Кожне нове підключення передається в окремий фоновий потік (Task.Run).
        /// </summary>
        public async Task StartAsync()
        {
            TcpListener listener = new(_ipAddress, _port);
            listener.Start();

            Console.WriteLine("============================================================");
            Console.WriteLine($"[SERVER] Smart House TCP Server запущено на {_ipAddress}:{_port}");
            Console.WriteLine("[SERVER] Очікування з'єднання від клієнтів...");
            Console.WriteLine("============================================================");

            while (true)
            {
                try
                {
                    // Очікуємо підключення клієнта без блокування головного потоку
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    int connId = ++_connectionCounter;

                    // Запускаємо обробку клієнта паралельно, щоб сервер міг одразу приймати інших
                    _ = Task.Run(() => ProcessingRequest(client, connId));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Помилка прийому підключення: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Головна функція обробки одного клієнтського TCP-запиту.
        /// Зчитує пакет із мережевого потоку, викликає маршрутизатор команд і відправляє відповідь.
        /// </summary>
        public void ProcessingRequest(TcpClient client, int connId)
        {
            try
            {
                // Конструкція using автоматично закриє сокет і потік після завершення роботи
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    Console.WriteLine("\n------------------------------------------------------------");
                    Console.WriteLine($"[CONN #{connId}] З'єднання з клієнтом встановлено ({client.Client.RemoteEndPoint})");

                    // Виділяємо буфер на 4 КБ і зчитуємо вхідні байти від клієнта
                    byte[] buffer = new byte[4096];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);

                    if (bytesRead == 0)
                    {
                        Console.WriteLine($"[CONN #{connId}] Отримано порожній пакет.");
                        return;
                    }

                    // Перетворюємо рівно bytesRead отриманих байтів у рядок UTF-8
                    string request = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    Console.WriteLine($"[CONN #{connId}] Вхідний TCP-пакет: {request}");

                    // Обробляємо команду та отримуємо масив байтів відповіді
                    byte[] responseBytes = RouteCommand(request, connId);
                    string responsePreview = Encoding.UTF8.GetString(responseBytes);

                    // Надсилаємо відповідь клієнту через той самий мережевий потік
                    stream.Write(responseBytes, 0, responseBytes.Length);
                    Console.WriteLine($"[CONN #{connId}] Надіслано відповідь: {responsePreview}");
                    Console.WriteLine($"[CONN #{connId}] З'єднання закрито.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CONN #{connId}] Помилка обробки запиту: {ex.Message}");
            }
        }

        /// <summary>
        /// Розбирає вхідний текстовий пакет за роздільником '|' і спрямовує його
        /// у відповідний метод-обробник залежно від назви команди (parts[0]).
        /// </summary>
        private byte[] RouteCommand(string request, int connId)
        {
            string[] parts = request.Split('|');
            string command = parts[0].ToUpperInvariant();

            string response = command switch
            {
                "LOGIN" => HandleLogin(parts, connId),
                "REGISTER" => HandleRegister(parts, connId),
                "GET_DEVICES" => HandleGetDevices(connId),
                "TOGGLE_DEVICE" => HandleToggleDevice(parts, connId),
                "UPDATE_DEVICE" => HandleUpdateDevice(parts, connId),
                "ADD_DEVICE" => HandleAddDevice(parts, connId),
                _ => $"ERROR|Невідома команда: {command}"
            };

            return Encoding.UTF8.GetBytes(response);
        }

        /// <summary>
        /// Обробляє команду авторизації: LOGIN|username|password.
        /// Повертає OK|email або ERROR|причина.
        /// </summary>
        private string HandleLogin(string[] parts, int connId)
        {
            if (parts.Length < 3)
                return "ERROR|Некоректний формат запиту LOGIN";

            Console.WriteLine($"[CONN #{connId}] -> БД: Перевірка користувача '{parts[1]}'...");
            var (success, message) = _db.ValidateUser(parts[1], parts[2]);
            return success ? $"OK|{message}" : $"ERROR|{message}";
        }

        /// <summary>
        /// Обробляє команду реєстрації нового користувача: REGISTER|username|email|password.
        /// </summary>
        private string HandleRegister(string[] parts, int connId)
        {
            if (parts.Length < 4)
                return "ERROR|Некоректний формат запиту REGISTER";

            Console.WriteLine($"[CONN #{connId}] -> БД: Реєстрація користувача '{parts[1]}' ({parts[2]})...");
            var (success, message) = _db.RegisterUser(parts[1], parts[2], parts[3]);
            return success ? $"OK|{message}" : $"ERROR|{message}";
        }

        /// <summary>
        /// Обробляє запит GET_DEVICES — зчитує всі пристрої з БД та об'єднує їх через ';'.
        /// </summary>
        private string HandleGetDevices(int connId)
        {
            Console.WriteLine($"[CONN #{connId}] -> БД: Зчитування списку пристроїв...");
            var devices = _db.GetAllDevicesSerialized();
            return devices.Count > 0
                ? $"OK|{string.Join(";", devices)}"
                : "ERROR|Список пристроїв у БД порожній";
        }

        /// <summary>
        /// Обробляє команду ввімкнення/вимкнення пристрою: TOGGLE_DEVICE|deviceId|username.
        /// </summary>
        private string HandleToggleDevice(string[] parts, int connId)
        {
            if (parts.Length < 3 || !int.TryParse(parts[1], out int deviceId))
                return "ERROR|Некоректний формат запиту TOGGLE_DEVICE";

            Console.WriteLine($"[CONN #{connId}] -> БД: Перемикання стану пристрою ID={deviceId}...");
            var (success, payload) = _db.ToggleDevice(deviceId, parts[2]);
            return success ? $"OK|{payload}" : $"ERROR|{payload}";
        }

        /// <summary>
        /// Обробляє команду зміни параметра пристрою: UPDATE_DEVICE|deviceId|newDetails|username.
        /// </summary>
        private string HandleUpdateDevice(string[] parts, int connId)
        {
            if (parts.Length < 4 || !int.TryParse(parts[1], out int deviceId))
                return "ERROR|Некоректний формат запиту UPDATE_DEVICE";

            Console.WriteLine($"[CONN #{connId}] -> БД: Оновлення параметра пристрою ID={deviceId} -> '{parts[2]}'...");
            var (success, payload) = _db.UpdateDeviceDetails(deviceId, parts[2], parts[3]);
            return success ? $"OK|{payload}" : $"ERROR|{payload}";
        }

        /// <summary>
        /// Обробляє команду додавання нового пристрою: ADD_DEVICE|name|type|isOn|details|username.
        /// </summary>
        private string HandleAddDevice(string[] parts, int connId)
        {
            if (parts.Length < 6)
                return "ERROR|Некоректний формат запиту ADD_DEVICE";

            bool isOn = parts[3] == "1" || parts[3].Equals("true", StringComparison.OrdinalIgnoreCase);
            Console.WriteLine($"[CONN #{connId}] -> БД: Додавання пристрою '{parts[1]}' ({parts[2]})...");
            var (success, payload) = _db.AddDevice(parts[1], parts[2], isOn, parts[4], parts[5]);
            return success ? $"OK|{payload}" : $"ERROR|{payload}";
        }
    }
}