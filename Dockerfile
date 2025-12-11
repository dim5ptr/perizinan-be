# Gunakan image resmi .NET 8 SDK untuk build
FROM mcr.microsoft.com/dotnet/sdk:8.0.413 AS build
WORKDIR /app

# Salin file proyek dan restore dependensi
COPY *.csproj ./
RUN dotnet restore

# Salin sisa file dan build
COPY . ./
RUN dotnet publish -c Release -o out

# Gunakan image runtime .NET 8 untuk menjalankan aplikasi
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "PresensiQRBackend.dll"]