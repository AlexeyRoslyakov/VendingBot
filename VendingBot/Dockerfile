# Используем официальный образ .NET SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем файлы проекта в контейнер
COPY . .

# Собираем проект
RUN dotnet publish -c Release -o /app

# Используем runtime-образ для запуска приложения
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Указываем точку входа (команду для запуска приложения)
ENTRYPOINT ["dotnet", "VendingBot.dll"]