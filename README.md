# UltimaMilla

Este repositorio está organizado siguiendo la estructura indicada en la documentación del proyecto.

## Estado actual

La solución usa .NET 10 y se trabaja con Rider. Actualmente existen Domain, Application,
Infrastructure, pruebas unitarias y pruebas de integración de persistencia. Web, Repartidor, Worker,
los otros tipos de pruebas y el despliegue que se enumeran abajo son la estructura prevista.

Domain contiene las entidades y relaciones de los cuatro diagramas UML, agrupadas por
área funcional. El detalle, las convenciones adoptadas y los límites de esta implementación
están en [Modelo de dominio](docs/domain-model.md).

Para compilar y ejecutar las pruebas desde la raíz del repositorio:

```powershell
dotnet build UltimaMilla.slnx
dotnet test UltimaMilla.slnx
```

En Rider se abre `UltimaMilla.slnx`; las pruebas se pueden ejecutar desde su explorador de tests.

## Programación y escaneos en Domain

- El alta del envío recibe una `ProgramacionEntrega`: fecha, franja de su zona y ventana
  concreta. Las horas locales se conservan con su desplazamiento y los límites se consultan
  en UTC. Application debe resolver la zona horaria del operador; Domain no usa la del equipo.
- `Reprogramar` y `Aplazar` reciben una nueva programación, conservan la fecha comprometida
  original y registran la programación anterior y la nueva en el evento del envío.
- Los escaneos se crean con `RegistrarRecepcion`, `RegistrarCarga` o `RegistrarDevolucion`.
  La recepción no necesita ruta, la carga exige asignación activa a una ruta sin despachar,
  y la devolución se vincula a su proceso activo, con ruta de origen opcional.
- `RecibirEnDeposito` confirma el ingreso después de escanear todos los bultos.

Estos comportamientos se prueban en memoria. Infrastructure ya contiene los mapeos de
EF Core y la migración inicial para PostgreSQL. La migración y siete pruebas de integración
se validaron contra PostgreSQL local en Docker. La concurrencia y los filtros automáticos
de tenant siguen pendientes.

## Persistencia

Se configuraron EF Core 10, Npgsql y los mapeos de las 31 entidades. Se conservan los setters
privados de Domain; los constructores de rehidratación permiten recuperar objetos sin volver
a ejecutar sus reglas de alta. La migración `InitialCreate` está generada y se aplicó a la
instancia local de desarrollo; cada integrante debe aplicarla en su propia base.

Estos comandos funcionan sin PostgreSQL ejecutándose:

```powershell
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/UltimaMilla.Infrastructure
dotnet ef migrations script --project src/UltimaMilla.Infrastructure --output docs/database-initial.sql
```

El manifiesto `dotnet-tools.json` fija la versión de `dotnet-ef`. La fábrica de diseño usa
`ConnectionStrings__UltimaMilla` si está definida; su valor predeterminado solo identifica
una base local, sin contraseña. Generar migraciones o SQL no abre una conexión.

El detalle de relaciones, índices, tipos y límites está en [Persistencia](docs/persistence.md).
También se conserva el [SQL inicial para revisión](docs/database-initial.sql).

## PostgreSQL local para desarrollo

Con Docker Desktop ejecutándose, desde la raíz del repositorio:

```powershell
./scripts/Start-LocalPostgres.ps1
./scripts/Test-LocalPersistence.ps1
```

El primer script levanta PostgreSQL 17 en `127.0.0.1:55432`, crea credenciales aleatorias
en `deploy/.env.postgres` (excluido de Git) y espera a que esté listo. El segundo aplica
las migraciones y ejecuta las siete pruebas de integración en bases temporales separadas.
La base de desarrollo conserva sus datos en un volumen de Docker.

Este Compose es el apoyo local para probar persistencia. El despliegue completo con los
demás servicios sigue pendiente. Ver [comandos y conexión desde Rider](docs/postgres-local.md).
`dotnet test UltimaMilla.slnx` sigue funcionando sin Docker: omite las siete pruebas de
integración cuando no se configura su conexión; el segundo script sí las ejecuta.

## Estructura

- `src/`
  - `UltimaMilla.Domain/` - entidades, reglas y máquina de estados.
  - `UltimaMilla.Application/` - casos de uso y contratos.
  - `UltimaMilla.Infrastructure/` - EF Core, Redis, RabbitMQ, outbox, Identity.
  - `UltimaMilla.Web/` - API, Razor Pages, Blazor y SignalR.
  - `UltimaMilla.Repartidor/` - Blazor WebAssembly PWA.
  - `UltimaMilla.Worker/` - consumidores y tareas en segundo plano.
- `tests/`
  - `Unit/`
  - `Integration/`
  - `Architecture/`
  - `Tenancy/`
- `deploy/` - Docker Compose y Terraform.
- `docs/`
  - `adr/` - registros de decisión de arquitectura.
- `scripts/` - scripts de automatización y soporte.

## Recomendaciones

1. Mantener la capa `Domain` libre de dependencias externas.
2. Usar `Application` para orquestar casos de uso.
3. Centralizar infraestructura y acceso a datos en `Infrastructure`.
4. Mantener pruebas separadas del código de producción.
5. Mantener la nomenclatura del dominio consistente con `UltimaMilla`.
