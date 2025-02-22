using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.IO;

var token = Environment.GetEnvironmentVariable("TOKEN") ?? "1444286201:AAHZbygMp0byK1Ql7kTHNtcXh8HSLRkwuqA";


using var cts = new CancellationTokenSource();
var bot = new TelegramBotClient(token, cancellationToken: cts.Token);

var me = await bot.GetMe();
await bot.DeleteWebhook();
await bot.DropPendingUpdates();

// Словарь для хранения состояния пользователей
var userStates = new Dictionary<long, string>();

bot.OnError += OnError;
bot.OnMessage += OnMessage;

Console.WriteLine($"@{me.Username} is running... Press Escape to terminate");

//while (Console.ReadKey(true).Key != ConsoleKey.Escape) ;
while (true)
{
      await Task.Delay(1000); // Задержка 1 секунда
}
//cts.Cancel(); // stop the bot

async Task OnError(Exception exception, HandleErrorSource source)
{
    Console.WriteLine(exception);
    await Task.Delay(2000, cts.Token);
}

async Task OnMessage(Message msg, UpdateType type)
{
    if (msg.Text is not { } text)
        return;

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
                await bot.SendMessage(msg.Chat, "Пожалуйста, используйте команду /help для начала.");
                break;
        }
    }
}

async Task OnCommand(string command, Message msg)
{
    switch (command)
    {
        case "/start":
            await bot.SendMessage(msg.Chat, "Добро пожаловать! Используйте /help для получения списка команд.");
            break;
        case "/help":
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
        new[] { new KeyboardButton("Закончился напиток"), new KeyboardButton("Нет сдачи") },
        new[] { new KeyboardButton("Нет воды"), new KeyboardButton("Другое") }
    })
            {
                ResizeKeyboard = true, // Клавиатура автоматически подстраивается под размер экрана
                OneTimeKeyboard = true // Клавиатура скрывается после выбора
            };

            await bot.SendMessage(msg.Chat, "Выберите проблему:", replyMarkup: keyboard);

            // Устанавливаем состояние "ожидание выбора проблемы"
            userStates[msg.Chat.Id] = "waiting_for_problem";
            break;
    }
}

async Task HandleProblemSelection(Message msg)
{
    // Сохраняем выбранную проблему
    string problem = msg.Text??"default problem";
    LogUserChoice(msg.Chat.Id, problem);
    if (problem == "Другое")
    {
        // Просим пользователя описать проблему
        await bot.SendMessage(msg.Chat, "Пожалуйста, опишите проблему:", replyMarkup: new ReplyKeyboardRemove());

        // Устанавливаем состояние "ожидание описания проблемы"
        userStates[msg.Chat.Id] = "waiting_for_custom_problem";
    }
    else
    {
        // Переходим к выбору района
        await ShowDistrictKeyboard(msg.Chat);
        userStates[msg.Chat.Id] = "waiting_for_district";
    }

   
}
async Task HandleCustomProblem(Message msg)
{
    // Сохраняем пользовательское описание проблемы
    string customProblem = msg.Text??"default_problem";
    LogCustomProblem(msg.Chat.Id, customProblem);

    // Переходим к выбору района
    await ShowDistrictKeyboard(msg.Chat);
    userStates[msg.Chat.Id] = "waiting_for_district";
}
async Task ShowDistrictKeyboard(Chat chat)
{
    // Создаем клавиатуру для выбора района
    var districtKeyboard = new ReplyKeyboardMarkup(new[]
    {
        new[] { new KeyboardButton("Северный район"), new KeyboardButton("Южный район") },
        new[] { new KeyboardButton("Центральный район") }
    })
    {
        ResizeKeyboard = true,
        OneTimeKeyboard = true
    };

    await bot.SendMessage(chat, "Выберите район:", replyMarkup: districtKeyboard);
}


async Task HandleDistrictSelection(Message msg)
{
    // Сохраняем выбранный район
    string district = msg.Text?? "default_district";

    // Получаем сохраненную проблему
    string problem = GetUserChoice(msg.Chat.Id);

    // Если проблема была "Другое", получаем пользовательское описание
    string customProblem = problem == "Другое" ? GetCustomProblem(msg.Chat.Id) : null;

    // Формируем полное описание проблемы
    string fullProblem = problem == "Другое" ? $"Другое: {customProblem}" : problem;

    // Сохраняем всю информацию в лог
    LogComplaint(msg.Chat.Id, fullProblem, district);

    // Уведомляем оператора
    await NotifyOperator(msg.Chat.Id, fullProblem, district);
    await bot.SendMessage(msg.Chat, "Ваша жалоба зарегистрирована. Спасибо!", replyMarkup: new ReplyKeyboardRemove());

    // Сбрасываем состояние пользователя
    userStates.Remove(msg.Chat.Id);
}
void LogCustomProblem(long chatId, string customProblem)
{
    string path = $@"C:\Users\advsa\RiderProjects\TelegramBotF1\TelegramBotF1\LogFolder\{chatId}_log.txt";

    if (!File.Exists(path))
    {
        File.Create(path).Close();
    }

    File.AppendAllText(path, $"Описание проблемы: {customProblem}\n");
}
string GetCustomProblem(long chatId)
{
    string path = $@"C:\Users\advsa\RiderProjects\TelegramBotF1\TelegramBotF1\LogFolder\{chatId}_log.txt";

    if (!File.Exists(path))
    {
        return string.Empty;
    }

    string[] lines = File.ReadAllLines(path);
    foreach (var line in lines)
    {
        if (line.StartsWith("Описание проблемы:"))
        {
            return line.Replace("Описание проблемы: ", "");
        }
    }

    return string.Empty;
}

void LogUserChoice(long chatId, string choice)
{
    string path = $@"C:\Users\advsa\RiderProjects\TelegramBotF1\TelegramBotF1\LogFolder\{chatId}_log.txt";

    if (!File.Exists(path))
    {
        File.Create(path).Close();
    }

    File.AppendAllText(path, $"Проблема: {choice}\n");
}

void LogComplaint(long chatId, string problem, string district)
{
    string path = $@"C:\Users\advsa\RiderProjects\TelegramBotF1\TelegramBotF1\LogFolder\{chatId}_log.txt";

    if (!File.Exists(path))
    {
        File.Create(path).Close();
    }

    File.AppendAllText(path, $"Район: {district}\n");
    File.AppendAllText(path, $"Жалоба зарегистрирована: {DateTime.Now}\n");
    File.AppendAllText(path, new string('-', 30) + "\n\n");

    // Очищаем файл после регистрации жалобы
    File.WriteAllText(path, string.Empty);
}

string GetUserChoice(long chatId)
{
    string path = $@"C:\Users\advsa\RiderProjects\TelegramBotF1\TelegramBotF1\LogFolder\{chatId}_log.txt";

    if (!File.Exists(path))
    {
        return string.Empty;
    }

    string[] lines = File.ReadAllLines(path);
    return lines.Length > 0 ? lines[0].Replace("Проблема: ", "") : string.Empty;
}

async Task NotifyOperator(long chatId, string problem, string district)
{
    string operatorChatId = "142176914"; // Замени на реальный chat_id оператора
    string message = $"Новая жалоба!\nПроблема: {problem}\nРайон: {district}\nChat ID пользователя: {chatId}";

    await bot.SendMessage(operatorChatId, message);
}

//TODO Настроить логирование
//TODO Удалить лишние файлы