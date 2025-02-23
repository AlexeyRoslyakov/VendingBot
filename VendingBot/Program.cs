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


// Сбрасываем вебхук и pending updates
await bot.DeleteWebhookAsync();
var updates = await bot.GetUpdatesAsync(offset: -1); // Получаем и игнорируем все pending updates
Console.WriteLine($"Сброшено {updates.Length} непрочитанных сообщений.");

// Словарь для хранения состояния пользователей (временное решение)
var userStates = new Dictionary<long, string>();
var userChoices = new Dictionary<long, string>();

app.MapPost("/webhook", async context =>
{
    Console.WriteLine("Получен запрос от Telegram");
    try
    {
        var update = await context.Request.ReadFromJsonAsync<Update>();
        if (update?.Message != null)
        {
            Console.WriteLine($"Получено сообщение: {update.Message.Text} от {update.Message.Chat.Id}");
            await OnMessage(update.Message); // Вызов логики бота
        }
        else
        {
            Console.WriteLine("Получен запрос без сообщения.");
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
        var photo = msg.Photo.Last();
        var file = await bot.GetFileAsync(photo.FileId);
        var fileUrl = $"https://api.telegram.org/file/bot{token}/{file.FilePath}";
        Console.WriteLine($"Получена фотография от {msg.Chat.Id}: {fileUrl}");
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

async Task HandlePhoto(Message msg)
{
    // Получаем фотографию с самым высоким разрешением
    var photo = msg.Photo.Last();

    // Получаем информацию о файле
    var file = await bot.GetFileAsync(photo.FileId);

    // Формируем URL для скачивания файла
    var fileUrl = $"https://api.telegram.org/file/bot{token}/{file.FilePath}";

    // Логируем информацию о фотографии
    Console.WriteLine($"Получена фотография от {msg.Chat.Id}: {fileUrl}");

    // Отправляем подтверждение пользователю
    await bot.SendMessage(msg.Chat.Id, "Фотография получена. Спасибо!");
}
async Task HandleDocument(Message msg)
{
    // Получаем информацию о документе
    var document = msg.Document;

    // Получаем информацию о файле
    var file = await bot.GetFileAsync(document.FileId);

    // Формируем URL для скачивания файла
    var fileUrl = $"https://api.telegram.org/file/bot{token}/{file.FilePath}";

    // Логируем информацию о документе
    Console.WriteLine($"Получен документ от {msg.Chat.Id}: {fileUrl}");

    // Отправляем подтверждение пользователю
    await bot.SendMessage(msg.Chat.Id, "Документ получен. Спасибо!");
}
async Task HandleCustomProblemWithMedia(Message msg)
{
    if (msg.Photo != null)
    {
        // Обработка фотографий
        var photo = msg.Photo.Last();
        var file = await bot.GetFileAsync(photo.FileId);
        var fileUrl = $"https://api.telegram.org/file/bot{token}/{file.FilePath}";

        // Сохраняем URL фотографии
        userChoices[msg.Chat.Id] = fileUrl;

        // Отправляем подтверждение пользователю
        await bot.SendMessage(msg.Chat.Id, "Фотография принята.");

        // Переходим к выбору района
        await ShowDistrictKeyboard(msg.Chat.Id);
        userStates[msg.Chat.Id] = "waiting_for_district";
    }
    else if (msg.Document != null)
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

//TODO LOG files