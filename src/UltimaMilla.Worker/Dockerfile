# Contexto: la raíz del repositorio (docker build -f src/UltimaMilla.Worker/Dockerfile .).
# Se copian primero los .csproj para que la restauración de paquetes quede en caché.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/UltimaMilla.Domain/UltimaMilla.Domain.csproj src/UltimaMilla.Domain/
COPY src/UltimaMilla.Application/UltimaMilla.Application.csproj src/UltimaMilla.Application/
COPY src/UltimaMilla.Infrastructure/UltimaMilla.Infrastructure.csproj src/UltimaMilla.Infrastructure/
COPY src/UltimaMilla.Worker/UltimaMilla.Worker.csproj src/UltimaMilla.Worker/
RUN dotnet restore src/UltimaMilla.Worker/UltimaMilla.Worker.csproj
COPY src/UltimaMilla.Domain src/UltimaMilla.Domain
COPY src/UltimaMilla.Application src/UltimaMilla.Application
COPY src/UltimaMilla.Infrastructure src/UltimaMilla.Infrastructure
COPY src/UltimaMilla.Worker src/UltimaMilla.Worker
RUN dotnet publish src/UltimaMilla.Worker/UltimaMilla.Worker.csproj -c Release -o /app --no-restore

# El Worker es un host genérico (sin ASP.NET): alcanza la imagen "runtime".
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
# Sin secretos en la imagen: la conexión a Postgres y la clave de RabbitMQ llegan por variables de entorno.
USER $APP_UID
ENTRYPOINT ["dotnet", "UltimaMilla.Worker.dll"]
