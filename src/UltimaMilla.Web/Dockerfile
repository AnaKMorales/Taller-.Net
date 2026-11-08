# Contexto: la raíz del repositorio (docker build -f src/UltimaMilla.Web/Dockerfile .).
# Se copian primero los .csproj para que la restauración de paquetes quede en caché.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/UltimaMilla.Domain/UltimaMilla.Domain.csproj src/UltimaMilla.Domain/
COPY src/UltimaMilla.Application/UltimaMilla.Application.csproj src/UltimaMilla.Application/
COPY src/UltimaMilla.Infrastructure/UltimaMilla.Infrastructure.csproj src/UltimaMilla.Infrastructure/
COPY src/UltimaMilla.Web/UltimaMilla.Web.csproj src/UltimaMilla.Web/
COPY src/UltimaMilla.Repartidor/UltimaMilla.Repartidor.csproj src/UltimaMilla.Repartidor/
RUN dotnet restore src/UltimaMilla.Web/UltimaMilla.Web.csproj \
 && dotnet restore src/UltimaMilla.Repartidor/UltimaMilla.Repartidor.csproj
COPY src/UltimaMilla.Domain src/UltimaMilla.Domain
COPY src/UltimaMilla.Application src/UltimaMilla.Application
COPY src/UltimaMilla.Infrastructure src/UltimaMilla.Infrastructure
COPY src/UltimaMilla.Web src/UltimaMilla.Web
COPY src/UltimaMilla.Repartidor src/UltimaMilla.Repartidor
RUN dotnet publish src/UltimaMilla.Web/UltimaMilla.Web.csproj -c Release -o /app --no-restore
# La PWA se publica aparte (Web no la referencia): su salida estática queda en /pwa/wwwroot.
RUN dotnet publish src/UltimaMilla.Repartidor/UltimaMilla.Repartidor.csproj -c Release -o /pwa --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
# Npgsql intenta cargar GSSAPI (Kerberos) al conectar; sin esta librería el log muestra un "Error:" inofensivo pero confuso.
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
COPY --from=build /pwa/wwwroot ./wwwroot/repartidor
# Claves de Data Protection (cookies, antiforgery): montar un volumen en /claves para que sobrevivan a los reinicios.
RUN mkdir /claves && chown $APP_UID /claves
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DataProtection__RutaClaves=/claves
EXPOSE 8080
# Sin secretos en la imagen: Postgres, Redis y RabbitMQ llegan por variables de entorno.
USER $APP_UID
ENTRYPOINT ["dotnet", "UltimaMilla.Web.dll"]
