# PostgreSQL local para probar persistencia

Este entorno permite que Agustín pruebe EF y las migraciones mientras el equipo prepara
el despliegue completo. Solo incluye PostgreSQL; los demás servicios y la configuración
de producción quedan para el trabajo de despliegue del equipo.

## Levantar y probar

Requisitos: Docker Desktop abierto, motor de contenedores Linux y SDK .NET 10.
Desde la raíz del repositorio, en la terminal de Rider o PowerShell:

```powershell
./scripts/Start-LocalPostgres.ps1
./scripts/Test-LocalPersistence.ps1
```

El primer script crea `deploy/.env.postgres` si no existe, generando una contraseña aleatoria;
después ejecuta Compose y espera al healthcheck. No sobrescribe un archivo de credenciales
existente. El segundo configura la conexión solo durante su ejecución, aplica las migraciones
pendientes a la base de desarrollo y ejecuta las pruebas de integración. Es posible volver
a ejecutarlo; EF utiliza su historial para aplicar solo las migraciones pendientes.

La imagen oficial es `postgres:17-alpine`. El puerto del servidor dentro del contenedor es
5432 y se publica en el equipo como `127.0.0.1:55432`. Este puerto queda accesible localmente.
El proyecto Compose se llama `ultimamilla-local`; su volumen `postgres_data` conserva los
datos fuera del contenedor. Las credenciales se usan al inicializar el volumen por primera
vez: editar la contraseña del archivo después no cambia automáticamente la del servidor.

## Conectar desde Rider

Crear una fuente de datos PostgreSQL con estos valores:

| Campo | Valor predeterminado |
| --- | --- |
| Host | 127.0.0.1 |
| Puerto | 55432 |
| Base | ultimamilla |
| Usuario | ultimamilla |
| Contraseña | El valor de POSTGRES_PASSWORD en deploy/.env.postgres |

La migración crea 32 tablas del modelo y `__EFMigrationsHistory`. La base de desarrollo
queda vacía de datos del negocio hasta que se carguen desde la aplicación o casos de prueba.
El puerto, usuario y base pueden ajustarse en `.env.postgres` antes del primer inicio.

## Estado, logs y detener

```powershell
docker compose --env-file deploy/.env.postgres -f deploy/compose.postgres.yaml ps
docker compose --env-file deploy/.env.postgres -f deploy/compose.postgres.yaml logs --tail 50 postgres
docker compose --env-file deploy/.env.postgres -f deploy/compose.postgres.yaml stop
```

`stop` detiene PostgreSQL conservando el contenedor y el volumen. Para volver a iniciarlo,
ejecutar otra vez `Start-LocalPostgres.ps1`.

## Qué verifican las pruebas

Las 35 pruebas reales crean bases independientes llamadas `ultimamilla_test_<guid>`.
Cada una aplica las migraciones desde cero y elimina su propia base al terminar. La base
`ultimamilla` y sus datos no se limpian ni se usan para esas escrituras. Se requiere que
el usuario local pueda crear bases; el usuario inicial de la imagen oficial lo permite.

Se comprueban migraciones, datos y relaciones, conversiones de tipos, asignación, devolución,
reprogramación, avisos, duplicados, claves foráneas, borrado restringido y rollback.
Cuando no se configura `ULTIMAMILLA_TEST_CONNECTION_STRING`, estas pruebas se omiten
explícitamente, para que `dotnet test UltimaMilla.slnx` pueda usarse sin Docker. Usar el script
de persistencia para ejecutarlas con la conexión configurada.

El 14 de octubre de 2026 se aplicó InitialCreate y pasaron siete pruebas contra PostgreSQL
17.11. El 17 de octubre se aplicó OperatorIsolation y se amplió el contexto para cumplir
ambos niveles del ADR-002. Pasaron 26 pruebas: operador, cuenta comercial, claims, Worker,
seguimiento público y persistencia. No se requiere una tercera migración por los filtros
de cuenta. La tercera migración, OptimisticConcurrency, agrega versiones a envíos y rutas.
La conexión con el login y los servicios externos y el outbox continúan pendientes.

Referencias: [imagen oficial PostgreSQL](https://hub.docker.com/_/postgres) y
[healthchecks y espera de disponibilidad en Compose](https://docs.docker.com/compose/how-tos/startup-order/).
