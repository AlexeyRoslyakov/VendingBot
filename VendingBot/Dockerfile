# Используем официальный образ .NET SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем исходный код
COPY . .

# Собираем приложение
RUN dotnet publish -c Release -o /app

# Используем официальный образ .NET Runtime для запуска
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Копируем собранное приложение
COPY --from=build /app .

# Указываем порт, который будет слушать приложение
EXPOSE 80

# Запускаем приложение
ENTRYPOINT ["dotnet", "VendingBot.dll"]