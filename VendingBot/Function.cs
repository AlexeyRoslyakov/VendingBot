using System;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Telegram.Bot;
using Telegram.Bot.Types;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace VendingBot
{
    public class Function
    {
        private static readonly string BotToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        private static readonly TelegramBotClient Bot = new(BotToken);

        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                // Десериализация входящего запроса от Telegram
                var update = JsonSerializer.Deserialize<Update>(request.Body);
                if (update?.Message != null)
                {
                    // Отправка ответа пользователю
                    await Bot.SendMessage(update.Message.Chat.Id, "Привет! Я работаю через вебхук 🚀");
                }

                // Возвращаем успешный ответ
                return new APIGatewayProxyResponse { StatusCode = 200, Body = "OK" };
            }
            catch (Exception ex)
            {
                // Логирование ошибки
                context.Logger.LogError($"Ошибка: {ex.Message}");
                return new APIGatewayProxyResponse { StatusCode = 500, Body = "Error" };
            }
        }
    }
}