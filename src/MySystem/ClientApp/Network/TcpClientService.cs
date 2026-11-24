using System;
using System.Net.Sockets;
using System.Text;

namespace ClientApp.Network
{
    /// <summary>
    /// Мережевий сервіс клієнта для обміну пакетами з TCP-сервером Smart House.
    /// </summary>
    public static class TcpClientService
    {
        private const string ServerIp = "127.0.0.1";
        private const int ServerPort = 8888;
        private const int TimeoutMs = 3000; // Таймаут очікування відповіді 3 секунди згідно з UML-схемою

        /// <summary>
        /// Надсилає текстовий пакет на сервер по TCP/IP та повертає рядок відповіді.
        /// </summary>
        public static string SendRequest(string packet)
        {
            using TcpClient client = new TcpClient();
            client.SendTimeout = TimeoutMs;
            client.ReceiveTimeout = TimeoutMs;

            // Встановлюємо з'єднання з обмеженням очікування 3 секунди
            var connectTask = client.ConnectAsync(ServerIp, ServerPort);
            if (!connectTask.Wait(TimeoutMs))
            {
                throw new TimeoutException("Перевищено час очікування підключення до сервера (3 сек). Переконайтеся, що ServerApp запущено.");
            }

            using NetworkStream stream = client.GetStream();

            byte[] requestBytes = Encoding.UTF8.GetBytes(packet);
            stream.Write(requestBytes, 0, requestBytes.Length);

            byte[] buffer = new byte[8192];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);

            if (bytesRead == 0)
            {
                throw new Exception("Сервер закрив з'єднання без відповіді.");
            }

            return Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
        }
    }
}