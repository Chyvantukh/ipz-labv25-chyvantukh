using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using ServerApp.Database;
using ServerApp.Network;

namespace ServerApp
{
    /// <summary>
    /// Головний клас консольного застосунку Smart House Server.
    /// Відповідає за запуск TCP-сервера або вбудованого тестового клієнта.
    /// </summary>
    internal class Program
    {
        // Локальна IP-адреса (localhost) та порт, на якому сервер слухає підключення
        private const string ServerIp = "127.0.0.1";
        private const int ServerPort = 8888;

        /// <summary>
        /// Точка входу в програму. Налаштовує кодування консолі,
        /// ініціалізує базу даних SQLite та запускає прослуховування TCP-порту.
        /// </summary>
        static async Task Main(string[] args)
        {
            // Вмикаємо UTF-8 у консолі, щоб коректно відображалася кирилиця
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            // Якщо програму запущено з параметром "--test", працюємо в режимі тестового клієнта
            if (args.Length > 0 && args[0] == "--test")
            {
                RunTestClient();
                return;
            }

            try
            {
                // 1. Підключаємо або створюємо файл БД smarthouse.db та перевіряємо наявність таблиць
                var dbManager = new DatabaseManager("smarthouse.db");
                Console.WriteLine($"[INIT] Базу даних SQLite підключено: {dbManager.DatabaseFullPath}");

                // 2. Створюємо мережевий обробник і запускаємо нескінченний цикл прийому клієнтів
                var server = new TcpServerHandler(ServerIp, ServerPort, dbManager);
                await server.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CRITICAL] Помилка запуску сервера: {ex.Message}");
            }
        }

        /// <summary>
        /// Вбудований тестовий TCP-клієнт для перевірки працездатності сервера.
        /// По черзі надсилає набір контрольних пакетів і виводить відповіді сервера в консоль.
        /// </summary>
        private static void RunTestClient()
        {
            Console.WriteLine("=== ТЕСТОВИЙ TCP-КЛІЄНТ SMART HOUSE ===");

            // Набір тестових команд у форматі протоколу: КОМАНДА|параметр1|параметр2...
            string[] testPackets =
            {
                "LOGIN|admin|admin",
                "REGISTER|vadym_ki303|vadym@lpnu.ua|12345",
                "GET_DEVICES",
                "TOGGLE_DEVICE|2|vadym_ki303",
                "UPDATE_DEVICE|1|24°C|vadym_ki303",
                "ADD_DEVICE|Віконні жалюзі|Автоматика|1|Відкрито на 50%|vadym_ki303",
                "GET_DEVICES"
            };

            foreach (string packet in testPackets)
            {
                try
                {
                    // Відкриваємо нове TCP-з'єднання з сервером для кожного пакета
                    using TcpClient client = new(ServerIp, ServerPort);
                    using NetworkStream stream = client.GetStream();

                    // Перетворюємо текстову команду в байти UTF-8 та відправляємо на сервер
                    byte[] data = Encoding.UTF8.GetBytes(packet);
                    stream.Write(data, 0, data.Length);
                    Console.WriteLine($"\n[CLIENT -> SERVER] {packet}");

                    // Зчитуємо масив байтів відповіді від сервера та декодуємо назад у рядок
                    byte[] buffer = new byte[4096];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"[SERVER -> CLIENT] {response}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Не вдалося підключитися до сервера: {ex.Message}");
                }
            }
        }
    }
}