# ���������� ����������� ����� .NET SDK ��� ������
#FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
#WORKDIR /src

# �������� �������� ���
#COPY . .

# �������� ����������
#RUN dotnet publish -c Release -o /app

# ���������� ����������� ����� .NET Runtime ��� �������
#FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
#WORKDIR /app

# �������� ��������� ����������
#COPY --from=build /app .

# ��������� ����, ������� ����� ������� ����������
#EXPOSE 80

# ��������� ����������
#ENTRYPOINT ["dotnet", "VendingBot.dll"]

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 80
ENTRYPOINT ["dotnet", "VendingBot.dll"]