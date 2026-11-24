using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using ServerApp.Database;

namespace ServerApp.Network
{
    /// <summary>
    /// Клас мережевого TCP-сервера системи Smart House.
    /// Приймає вхідні сокет-з'єднання, маршрутизує команди протоколу та повертає відповіді клієнтам.
    /// </summary>
    public class TcpServerHandler
    {
        private readonly IPAddress _ipAddress;
        private readonly int _port;
        private readonly DatabaseManager _db;
        private int _connectionCounter = 0;

        public TcpServerHandler(string ip, int port, DatabaseManager db)
        {
            _ipAddress = IPAddress.Parse(ip);
            _port = port;
            _db = db;
        }

        /// <summary>
        /// Запускає прослуховування TCP-порту та обробляє кожного клієнта в окремому фоновому потоці.
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
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    int connId = ++_connectionCounter;
                    _ = Task.Run(() => ProcessingRequest(client, connId));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Помилка прийому підключення: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Зчитує вхідний TCP-пакет, викликає відповідний обробник команди та надсилає відповідь клієнту.
        /// </summary>
        public void ProcessingRequest(TcpClient client, int connId)
        {
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    Console.WriteLine("\n------------------------------------------------------------");
                    Console.WriteLine($"[CONN #{connId}] З'єднання з клієнтом встановлено ({client.Client.RemoteEndPoint})");

                    byte[] buffer = new byte[4096];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);

                    if (bytesRead == 0)
                    {
                        Console.WriteLine($"[CONN #{connId}] Отримано порожній пакет.");
                        return;
                    }

                    string request = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    Console.WriteLine($"[CONN #{connId}] Вхідний TCP-пакет: {request}");

                    byte[] responseBytes = RouteCommand(request, connId);
                    string responsePreview = Encoding.UTF8.GetString(responseBytes);

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
        /// Визначає команду за першим токеном пакета та передає її відповідному методу.
        /// </summary>
        private byte[] RouteCommand(string request, int connId)
        {
            string[] parts = request.Split('|');
            string command = parts[0].ToUpperInvariant();

            string response = command switch
            {
                "LOGIN" => HandleLogin(parts, connId),
                "REGISTER" => HandleRegister(parts, connId),
                "LOGOUT" => HandleLogout(parts, connId),
                "GET_DEVICES" => HandleGetDevices(connId),
                "TOGGLE_DEVICE" => HandleToggleDevice(parts, connId),
                "UPDATE_DEVICE" => HandleUpdateDevice(parts, connId),
                "ADD_DEVICE" => HandleAddDevice(parts, connId),
                "DELETE_DEVICE" => HandleDeleteDevice(parts, connId),
                _ => $"ERROR|Невідома команда: {command}"
            };

            return Encoding.UTF8.GetBytes(response);
        }

        private string HandleLogin(string[] parts, int connId)
        {
            if (parts.Length < 3)
                return "ERROR|Некоректний формат запиту LOGIN";

            Console.WriteLine($"[CONN #{connId}] -> БД: Перевірка користувача '{parts[1]}'...");
            var (success, message) = _db.ValidateUser(parts[1], parts[2]);
            return success ? $"OK|{message}" : $"ERROR|{message}";
        }

        private string HandleRegister(string[] parts, int connId)
        {
            if (parts.Length < 4)
                return "ERROR|Некоректний формат запиту REGISTER";

            Console.WriteLine($"[CONN #{connId}] -> БД: Реєстрація користувача '{parts[1]}' ({parts[2]})...");
            var (success, message) = _db.RegisterUser(parts[1], parts[2], parts[3]);
            return success ? $"OK|{message}" : $"ERROR|{message}";
        }

        private string HandleLogout(string[] parts, int connId)
        {
            string username = parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : "unknown";
            Console.WriteLine($"[CONN #{connId}] -> БД: Деавторизація користувача '{username}'...");
            var (success, message) = _db.LogoutUser(username);
            return success ? $"OK|{message}" : $"ERROR|{message}";
        }

        private string HandleGetDevices(int connId)
        {
            Console.WriteLine($"[CONN #{connId}] -> БД: Зчитування списку пристроїв...");
            var devices = _db.GetAllDevicesSerialized();
            return devices.Count > 0
                ? $"OK|{string.Join(";", devices)}"
                : "ERROR|Список пристроїв у БД порожній";
        }

        private string HandleToggleDevice(string[] parts, int connId)
        {
            if (parts.Length < 3 || !int.TryParse(parts[1], out int deviceId))
                return "ERROR|Некоректний формат запиту TOGGLE_DEVICE";

            Console.WriteLine($"[CONN #{connId}] -> БД: Перемикання стану пристрою ID={deviceId}...");
            var (success, payload) = _db.ToggleDevice(deviceId, parts[2]);
            return success ? $"OK|{payload}" : $"ERROR|{payload}";
        }

        private string HandleUpdateDevice(string[] parts, int connId)
        {
            if (parts.Length < 4 || !int.TryParse(parts[1], out int deviceId))
                return "ERROR|Некоректний формат запиту UPDATE_DEVICE";

            Console.WriteLine($"[CONN #{connId}] -> БД: Оновлення параметра пристрою ID={deviceId} -> '{parts[2]}'...");
            var (success, payload) = _db.UpdateDeviceDetails(deviceId, parts[2], parts[3]);
            return success ? $"OK|{payload}" : $"ERROR|{payload}";
        }

        private string HandleAddDevice(string[] parts, int connId)
        {
            if (parts.Length < 6)
                return "ERROR|Некоректний формат запиту ADD_DEVICE";

            bool isOn = parts[3] == "1" || parts[3].Equals("true", StringComparison.OrdinalIgnoreCase);
            Console.WriteLine($"[CONN #{connId}] -> БД: Додавання пристрою '{parts[1]}' ({parts[2]})...");
            var (success, payload) = _db.AddDevice(parts[1], parts[2], isOn, parts[4], parts[5]);
            return success ? $"OK|{payload}" : $"ERROR|{payload}";
        }

        private string HandleDeleteDevice(string[] parts, int connId)
        {
            if (parts.Length < 3 || !int.TryParse(parts[1], out int deviceId))
                return "ERROR|Некоректний формат запиту DELETE_DEVICE";

            Console.WriteLine($"[CONN #{connId}] -> БД: Видалення пристрою ID={deviceId}...");
            var (success, payload) = _db.DeleteDevice(deviceId, parts[2]);
            return success ? $"OK|{payload}" : $"ERROR|{payload}";
        }
    }
}