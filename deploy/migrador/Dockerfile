# Migrador: aplica las migraciones de EF Core y termina. Es un "migrations bundle" (ejecutable self-contained linux-x64),
# así que la imagen final no lleva el SDK ni el código fuente.
# Contexto: la raíz del repositorio (docker build -f deploy/migrador/Dockerfile .).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# La versión de dotnet-ef sale de dotnet-tools.json (fijada en el repositorio).
COPY dotnet-tools.json .
RUN dotnet tool restore
COPY src/UltimaMilla.Domain/UltimaMilla.Domain.csproj src/UltimaMilla.Domain/
COPY src/UltimaMilla.Application/UltimaMilla.Application.csproj src/UltimaMilla.Application/
COPY src/UltimaMilla.Infrastructure/UltimaMilla.Infrastructure.csproj src/UltimaMilla.Infrastructure/
RUN dotnet restore src/UltimaMilla.Infrastructure/UltimaMilla.Infrastructure.csproj -r linux-x64
COPY src/UltimaMilla.Domain src/UltimaMilla.Domain
COPY src/UltimaMilla.Application src/UltimaMilla.Application
COPY src/UltimaMilla.Infrastructure src/UltimaMilla.Infrastructure
RUN dotnet ef migrations bundle --project src/UltimaMilla.Infrastructure \
    --self-contained -r linux-x64 --output /out/migrador --force

# Solo las librerías nativas del sistema: el bundle trae su propio runtime.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
WORKDIR /app
# Npgsql intenta cargar GSSAPI (Kerberos) al conectar; sin esta librería imprime un "Error:" inofensivo pero confuso.
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
 && rm -rf /var/lib/apt/lists/*
COPY --from=build /out/migrador .
# Sin secretos en la imagen: la conexión llega por la variable ConnectionStrings__UltimaMilla (la lee AppDbContextFactory),
# nunca por argumentos de línea de comandos. El bundle solo aplica lo pendiente (__EFMigrationsHistory): es idempotente.
USER $APP_UID
ENTRYPOINT ["./migrador"]
