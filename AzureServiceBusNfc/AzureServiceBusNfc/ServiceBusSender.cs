using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System.Configuration;

namespace NfcReadAndSendToQueue
{
    public class ServiceBusSender
    {
        private static QueueClient sendClient;

        public static async Task SendMessagesAsync(string _loginId)
        {
            sendClient = QueueClient.CreateFromConnectionString(ConfigurationManager.AppSettings["Microsoft.ServiceBus.ConnectionString"]);

            dynamic data = new { loginId = _loginId };

            var message = new BrokeredMessage(new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data))))
            {
                ContentType = "application/json",
                Label = "ClockInAndOut",
                MessageId = Guid.NewGuid().ToString(),
                TimeToLive = TimeSpan.FromMinutes(2)
            };

            await sendClient.SendAsync(message);
            lock (Console.Out)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("UID {0} successfully sent at {1}", _loginId, DateTime.Now);
                Console.ResetColor();
            }

            await sendClient.CloseAsync();
        }
    }
}