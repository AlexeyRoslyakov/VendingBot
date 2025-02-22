﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? throw new ArgumentNullException("TELEGRAM_BOT_TOKEN");
var bot = new TelegramBotClient(token);
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Словарь для хранения состояния пользователей (временное решение)
var userStates = new Dictionary<long, string>();

app.MapPost("/webhook", async context =>
{
    var update = await context.Request.ReadFromJsonAsync<Update>();
    // Обработка обновления
    await context.Response.WriteAsync("OK");
});

app.Run("http://*:80"); // Слушаем порт 80

async Task OnMessage(Message msg)
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
                await bot.SendMessage(msg.Chat.Id, "Пожалуйста, используйте команду /help для начала.");
                break;
        }
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

            await bot.SendMessage(msg.Chat.Id, "Выберите проблему:", replyMarkup: keyboard);

            // Устанавливаем состояние "ожидание выбора проблемы"
            userStates[msg.Chat.Id] = "waiting_for_problem";
            break;
    }
}

async Task HandleProblemSelection(Message msg)
{
    string problem = msg.Text ?? "default problem";
    LogUserChoice(msg.Chat.Id, problem);

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
    userStates.Remove(msg.Chat.Id);
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
    // Временное решение, замените на работу с базой данных
    return "default_problem";
}

string GetCustomProblem(long chatId)
{
    // Временное решение, замените на работу с базой данных
    return "default_custom_problem";
}

async Task NotifyOperator(long chatId, string problem, string district)
{
    string operatorChatId = "142176914"; // Замените на реальный chat_id оператора
    string message = $"Новая жалоба!\nПроблема: {problem}\nРайон: {district}\nChat ID пользователя: {chatId}";

    await bot.SendMessage(operatorChatId, message);
}