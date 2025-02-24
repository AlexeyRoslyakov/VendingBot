using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? throw new ArgumentNullException("TELEGRAM_BOT_TOKEN");
var bot = new TelegramBotClient(token);
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();


// Временное отключение вебхука
await bot.DeleteWebhookAsync();
Console.WriteLine("Вебхук временно отключен.");

// Сброс непрочитанных сообщений
var updates = await bot.GetUpdatesAsync(offset: -1); // Получаем и игнорируем все pending updates
Console.WriteLine($"Сброшено {updates.Length} непрочитанных сообщений.");

// Повторное включение вебхука
await bot.SetWebhookAsync("https://vendingbot.onrender.com/webhook");
Console.WriteLine("Вебхук успешно настроен.");


// Словарь для хранения состояния пользователей (временное решение)
var userStates = new Dictionary<long, string>();
var userChoices = new Dictionary<long, string>();

app.MapPost("/webhook", async context =>
{
    Console.WriteLine("Получен запрос от Telegram");
    try
    {
        // Log the raw request body
        using (var reader = new StreamReader(context.Request.Body))
        {
            var rawBody = await reader.ReadToEndAsync();
            Console.WriteLine($"Raw Telegram payload: {rawBody}");
        }
        context.Request.Body.Position = 0; // Reset stream position for deserialization

        var update = await context.Request.ReadFromJsonAsync<Update>();
        if (update?.Message != null)
        {
            Console.WriteLine($"Получено сообщение: {update.Message.Text} от {update.Message.Chat.Id}");
            await OnMessage(update.Message);
        }
        await context.Response.WriteAsync("OK");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка при обработке запроса: {ex.Message}");
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("Internal Server Error");
    }
});

app.Run("http://*:80"); // Слушаем порт 80

async Task OnMessage(Message msg)
{
    if (msg.Text is { } text && !string.IsNullOrEmpty(text))//обработка текстовых сообщений
    {

        // Получаем текущее состояние пользователя
        userStates.TryGetValue(msg.Chat.Id, out var state);

        if (text.StartsWith('/'))
        {
            var command = text.ToLower();
            await OnCommand(command, msg);
        }
        else
        {
            // Обработка текстовых сообщений в зависимости от состояния
            switch (state)
            {
                case "waiting_for_problem":
                    await HandleProblemSelection(msg);
                    break;
                case "waiting_for_district":
                    await HandleDistrictSelection(msg);
                    break;
                case "waiting_for_custom_problem":
                    await HandleCustomProblem(msg);
                    break;
                default:
                    await bot.SendMessage(msg.Chat.Id, "Пожалуйста, используйте команду /help для начала.");
                    break;
            }
        }
    }
    else if (msg.Photo != null || msg.Document != null)
    {
        // Обработка медиафайлов
        userStates.TryGetValue(msg.Chat.Id, out var state);

        if (state == "waiting_for_custom_problem")
        {
            // Если пользователь отправил фото/документ в состоянии "waiting_for_custom_problem"
            await HandleCustomProblemWithMedia(msg);
        }
        else
        {
            // Обработка медиафайлов в других состояниях
            await HandleMedia(msg);
        }
    }
    else
    {
        // Обработка других типов сообщений
        Console.WriteLine($"Получено некорректное сообщение от {msg.Chat.Id}.");
        await bot.SendMessage(msg.Chat.Id, "Я пока не умею обрабатывать этот тип сообщений.");
    }
}



async Task OnCommand(string command, Message msg)
{
    switch (command)
    {
        case "/start":
            await bot.SendMessage(msg.Chat.Id, "Добро пожаловать! Используйте /help для получения списка команд.");
            break;
        case "/help":
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
                new[] { new KeyboardButton("Закончился напиток"), new KeyboardButton("Нет сдачи") },
                new[] { new KeyboardButton("Нет воды"), new KeyboardButton("Другое") }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };

            await bot.SendMessage(
                chatId: msg.Chat.Id,
                text: "Выберите проблему:",
                replyMarkup: keyboard // Передаем клавиатуру
            );

            // Устанавливаем состояние "ожидание выбора проблемы"
            userStates[msg.Chat.Id] = "waiting_for_problem";
            break;
        case "/restart":
            await RestartBot();
            break;
    }
}

async Task HandleProblemSelection(Message msg)
{
    string problem = msg.Text ?? "default problem";
    LogUserChoice(msg.Chat.Id, problem);

    // Сохраняем выбранную проблему
    userChoices[msg.Chat.Id] = problem;


    // Проверяем, что введенный текст соответствует ожидаемым вариантам
    var validProblems = new[] { "Закончился напиток", "Нет сдачи", "Нет воды", "Другое" };
    if (!validProblems.Contains(problem))
    {
        await bot.SendMessage(msg.Chat.Id, "Пожалуйста, выберите вариант из клавиатуры.");
        return;
    }

    if (problem == "Другое")
    {
        await bot.SendMessage(msg.Chat.Id, "Пожалуйста, опишите проблему:", replyMarkup: new ReplyKeyboardRemove());
        userStates[msg.Chat.Id] = "waiting_for_custom_problem";
    }
    else
    {
        await ShowDistrictKeyboard(msg.Chat.Id);
        userStates[msg.Chat.Id] = "waiting_for_district";
    }
}



async Task HandleCustomProblem(Message msg)
{
    string customProblem = msg.Text ?? "default_problem";
    LogCustomProblem(msg.Chat.Id, customProblem);
    await ShowDistrictKeyboard(msg.Chat.Id);
    userStates[msg.Chat.Id] = "waiting_for_district";
    userChoices[msg.Chat.Id] = customProblem;
}

async Task ShowDistrictKeyboard(long chatId)
{
    var districtKeyboard = new ReplyKeyboardMarkup(new[]
    {
        new[] { new KeyboardButton("Северный район"), new KeyboardButton("Южный район") },
        new[] { new KeyboardButton("Центральный район") }
    })
    {
        ResizeKeyboard = true,
        OneTimeKeyboard = true
    };

    await bot.SendMessage(chatId, "Выберите район:", replyMarkup: districtKeyboard);
}

async Task HandleDistrictSelection(Message msg)
{
    string district = msg.Text ?? "default_district";
    string problem = GetUserChoice(msg.Chat.Id);
    string customProblem = problem == "Другое" ? GetCustomProblem(msg.Chat.Id) : null;
    string fullProblem = problem == "Другое" ? $"Другое: {customProblem}" : problem;

    LogComplaint(msg.Chat.Id, fullProblem, district);
    await NotifyOperator(msg.Chat.Id, fullProblem, district);
    await bot.SendMessage(msg.Chat.Id, "Ваша жалоба зарегистрирована. Спасибо!", replyMarkup: new ReplyKeyboardRemove());

    // Очищаем состояние и данные пользователя
    userStates.Remove(msg.Chat.Id);
    userChoices.Remove(msg.Chat.Id);
}
async Task HandleMedia(Message msg)
{
    if (msg.Photo != null)
    {
        // Обработка фотографий
        var fileId = msg.Photo.Last().FileId;
        var message = await bot.SendPhoto(msg.Chat.Id, fileId);
        //var filePath = 
        //var photo = msg.Photo.Last();
        //var file = await bot.GetFileAsync(photo.FileId);
        //var fileUrl = $"https://api.telegram.org/file/bot{token}/{message.FilePath}";
        Console.WriteLine($"Получена фотография от {msg.Chat.Id}:   file: {fileId}");
        await bot.SendMessage(msg.Chat.Id, "Фотография получена. Спасибо!");
    }
    else if (msg.Document != null)
    {
        // Обработка документов
        var document = msg.Document;
        var file = await bot.GetFileAsync(document.FileId);
        var fileUrl = $"https://api.telegram.org/file/bot{token}/{file.FilePath}";
        Console.WriteLine($"Получен документ от {msg.Chat.Id}: {fileUrl}");
        await bot.SendMessage(msg.Chat.Id, "Документ получен. Спасибо!");
    }
}

async Task HandleCustomProblemWithMedia(Message msg)
{
    if (msg.Photo != null && msg.Photo.Any())
    {
        try
        {

            await Task.Delay(5000); // Даем Telegram чуть больше времени на обработку

            var bestPhoto = msg.Photo.LastOrDefault(); // Берем самое большое фото
            if (bestPhoto != null && !string.IsNullOrEmpty(bestPhoto.FileId))
            {
                Console.WriteLine($"Используем самое большое фото: {bestPhoto.FileId}");
            }
            else
            {
                Console.WriteLine("Ошибка: file_id пустой даже после задержки.");
            }

            Console.WriteLine($"Получено {msg.Photo.Length} фотографий.");

            // Логируем информацию о каждой фотографии
            for (int i = 0; i < msg.Photo.Length; i++)
            {
                var photo = msg.Photo[i];
                Console.WriteLine($"Фотография {i + 1}: FileId = {photo.FileId}, Width = {photo.Width}, Height = {photo.Height}");
            }

            // Берем последний элемент массива (самый высокий размер)
            var photoToProcess = msg.Photo.Last();
            if (string.IsNullOrEmpty(photoToProcess.FileId))
            {
                Console.WriteLine("Ошибка: file_id пустой. Возможно, фотография слишком большая или не была сохранена на серверах Telegram.");
                await bot.SendMessage(msg.Chat.Id, "Не удалось обработать фотографию. Пожалуйста, попробуйте ещё раз или отправьте файл как документ.");
                return;
            }

            Console.WriteLine($"Обрабатываем фотографию с file_id: {photoToProcess.FileId}");

            // Получаем информацию о файле
            var fileInfo = await bot.GetFileAsync(photoToProcess.FileId);
            if (string.IsNullOrEmpty(fileInfo.FilePath))
            {
                Console.WriteLine("Ошибка: file_path пустой. Файл недоступен для скачивания.");
                await bot.SendMessage(msg.Chat.Id, "Не удалось обработать фотографию. Пожалуйста, попробуйте ещё раз или отправьте файл как документ.");
                return;
            }

            // Формируем URL для скачивания файла
            var fileUrl = $"https://api.telegram.org/file/bot{token}/{fileInfo.FilePath}";
            Console.WriteLine($"Сформирован URL файла: {fileUrl}");

            // Сохраняем URL фотографии
            userChoices[msg.Chat.Id] = fileUrl;

            // Отправляем подтверждение пользователю
            await bot.SendMessage(msg.Chat.Id, "Фотография принята.");

            // Переходим к выбору района
            await ShowDistrictKeyboard(msg.Chat.Id);
            userStates[msg.Chat.Id] = "waiting_for_district";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при обработке фотографии: {ex.Message}");
            await bot.SendMessage(msg.Chat.Id, "Не удалось обработать фотографию. Пожалуйста, попробуйте ещё раз.");
        }

    }
    else if (msg.Document != null)
    {
        try
        {
            // Обработка документов
            var document = msg.Document;
            var file = await bot.GetFileAsync(document.FileId);
            var fileUrl = $"https://api.telegram.org/file/bot{token}/{file.FilePath}";

            // Сохраняем URL документа
            userChoices[msg.Chat.Id] = fileUrl;

            // Отправляем подтверждение пользователю
            await bot.SendMessage(msg.Chat.Id, "Документ принят.");

            // Переходим к выбору района
            await ShowDistrictKeyboard(msg.Chat.Id);
            userStates[msg.Chat.Id] = "waiting_for_district";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при обработке документа: {ex.Message}");
            await bot.SendMessage(msg.Chat.Id, "Не удалось обработать документ. Пожалуйста, попробуйте ещё раз.");
        }
    }
    else
    {
        // Если сообщение не содержит фотографию или документ
        await bot.SendMessage(msg.Chat.Id, "Пожалуйста, отправьте фотографию или документ.");
    }
}
void LogUserChoice(long chatId, string choice)
{
    Console.WriteLine($"Проблема: {choice} для пользователя {chatId}");
}

void LogCustomProblem(long chatId, string customProblem)
{
    Console.WriteLine($"Описание проблемы: {customProblem} для пользователя {chatId}");
}

void LogComplaint(long chatId, string problem, string district)
{
    Console.WriteLine($"Район: {district}, Проблема: {problem} для пользователя {chatId}");
}

string GetUserChoice(long chatId)
{
    if (userChoices.TryGetValue(chatId, out var problem))
    {
        return problem;
    }
    return "default_problem";
}

string GetCustomProblem(long chatId)
{
    if (userChoices.TryGetValue(chatId, out var problem))
    {
        return problem;
    }
    return "default_custom_problem";
}

async Task NotifyOperator(long chatId, string problem, string district)
{
    string operatorChatId = "142176914"; // Замените на реальный chat_id оператора

    if (userChoices.TryGetValue(chatId, out var fileUrl) && Uri.IsWellFormedUriString(fileUrl, UriKind.Absolute))
    {
        // Если есть URL файла, отправляем его оператору
        await bot.SendPhoto(operatorChatId, fileUrl, caption: $"Новая жалоба!\nПроблема: default problem\nРайон: {district}\nChat ID пользователя: {chatId}");
    }
    else
    {
        // Если файла нет, отправляем только текст
        string message = $"Новая жалоба!\nПроблема: {problem}\nРайон: {district}\nChat ID пользователя: {chatId}";
        await bot.SendMessage(operatorChatId, message);
    }
}

async Task RestartBot()
{
    try
    {
        Console.WriteLine("Начало перезапуска бота...");

        // Удаляем вебхук
        await bot.DeleteWebhookAsync();
        Console.WriteLine("Вебхук успешно удален.");

        // Закрываем все сессии бота
        await bot.LogOutAsync();
        Console.WriteLine("Бот успешно вышел из облачного API Telegram.");

        // Закрываем текущий экземпляр бота
        await bot.CloseAsync();
        Console.WriteLine("Экземпляр бота успешно закрыт.");

        // Ждем 10 минут (если требуется)
        Console.WriteLine("Ожидание 10 минут перед перезапуском...");
        await Task.Delay(TimeSpan.FromMinutes(10));

        // Устанавливаем вебхук
        await bot.SetWebhookAsync("https://vendingbot.onrender.com/webhook");
        Console.WriteLine("Вебхук успешно настроен.");

        Console.WriteLine("Перезапуск бота завершен.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка при перезапуске бота: {ex.Message}");
    }
}
