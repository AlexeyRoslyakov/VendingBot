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
                var update = JsonSerializer.Deserialize<Update>(request.Body); // Используем System.Text.Json
                if (update?.Message != null)
                {
                    await Bot.SendTextMessageAsync(update.Message.Chat.Id, "Привет! Я работаю через AWS Lambda 🚀");
                }

                return new APIGatewayProxyResponse { StatusCode = 200, Body = "OK" };
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Ошибка: {ex.Message}");
                return new APIGatewayProxyResponse { StatusCode = 500, Body = "Error" };
            }
        }
    }
}
