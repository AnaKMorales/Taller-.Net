# UltimaMilla

Este repositorio está organizado siguiendo la estructura indicada en la documentación del proyecto.

## Estado actual

La solución usa .NET 10 y se trabaja con Rider. Actualmente existen Domain, Application,
Infrastructure, una base Web con Razor Pages y Blazor, pruebas unitarias y pruebas de integración
de persistencia. Repartidor, Worker, los otros tipos de pruebas y el despliegue que se enumeran
abajo son la estructura prevista.

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
EF Core y las migraciones para PostgreSQL. Se validaron pruebas de integración contra
PostgreSQL local, incluidas las de aislamiento automático por operador. Envíos y rutas ya
cuentan con concurrencia optimista. La conexión del login y el outbox siguen pendientes.

## Persistencia

Se configuraron EF Core 10, Npgsql y los mapeos de las 31 entidades. Se conservan los setters
privados de Domain; los constructores de rehidratación permiten recuperar objetos sin volver
a ejecutar sus reglas de alta. La migración `InitialCreate` está generada y se aplicó a la
instancia local de desarrollo. La migración posterior `OperatorIsolation` incorpora el tenant
y sus restricciones; cada integrante debe aplicar las migraciones pendientes en su propia base.

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
También se conserva el [SQL de instalación desde cero para revisión](docs/database-initial.sql),
que incluye las tres migraciones.

## Concurrencia de envíos y rutas

`RowVersion` detecta modificaciones basadas en una versión anterior del envío o la ruta.
También se comprueban cambios en hijos, como paradas y escaneos. Dos asignaciones que
compiten por el mismo envío o la capacidad de una ruta no pueden confirmar ambas con
la misma versión. El intento perdedor se revierte y produce `ConflictoConcurrenciaException`;
debe consultarse el estado actual y revalidar la operación, sin reintentos automáticos.

La migración `OptimisticConcurrency` agrega las versiones a filas nuevas y existentes.
Los casos de uso deben cargar los agregados completos con seguimiento y guardar su unidad
de trabajo de forma atómica. Ver [funcionamiento, integración y límites](docs/concurrency.md).

## Multitenancy por operador y cuenta comercial

Se sigue la estrategia por fila de la sección 11 y el ADR-002 del documento inicial.
Cada AppDbContext captura un TenantContext inmutable mediante ITenantActual: operador,
usuario/rol y cuenta activa para el perfil Comercio. Las consultas se filtran por operador
más cuenta; los usuarios internos mantienen su ámbito de operador según los permisos de API.
Sin contexto no se leen datos y el guardado se bloquea.

TenantSaveChangesInterceptor valida altas, modificaciones, bajas y referencias en ambos
niveles. TenantResolver resuelve y verifica claims autenticados y contextos de mensajes Worker.
ISeguimientoPublico resuelve un token aleatorio y devuelve un DTO limitado, en contexto de solo
lectura. TenantCacheKey separa las claves por operador/cuenta y perfil. Estos componentes no
implementan el login, el consumidor RabbitMQ ni el cliente Redis: sus proyectos deben conectarlos.

Comercio conserva su identidad compartida y las cuentas delimitan sus operaciones.
Los detalles, ejemplos de integración y límites están en [Multitenancy](docs/multitenancy.md)
y [ADR-002](docs/adr/002-multitenancy-por-fila.md).
Se validaron 120 pruebas: 58 de Domain, 26 unitarias de Infrastructure y 36 contra PostgreSQL,
incluidas nueve pruebas de concurrencia y migración de sus versiones.

## PostgreSQL local para desarrollo

Con Docker Desktop ejecutándose, desde la raíz del repositorio:

```powershell
./scripts/Start-LocalPostgres.ps1
./scripts/Test-LocalPersistence.ps1
```

El primer script levanta PostgreSQL 17 en `127.0.0.1:55432`, crea credenciales aleatorias
en `deploy/.env.postgres` (excluido de Git) y espera a que esté listo. El segundo aplica
las migraciones y ejecuta las pruebas de integración en bases temporales separadas.
La base de desarrollo conserva sus datos en un volumen de Docker.

Este Compose es el apoyo local para probar persistencia. El despliegue completo con los
demás servicios sigue pendiente. Ver [comandos y conexión desde Rider](docs/postgres-local.md).
`dotnet test UltimaMilla.slnx` sigue funcionando sin Docker: omite las pruebas de
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

El alta Web inicial todavía debe completar cuenta, configuración, bultos y preparación de
tarifa/fechas, y conectarse con el contexto autenticado. `CrearEnvioHandler` usa el contrato
`IAltaEnvioRepository`, implementado en Infrastructure, para respetar la dirección de dependencias.
No se utiliza un operador vacío ni se omiten las reglas de Domain para guardar ese formulario.

Las migraciones del prototipo simplificado de Envio se conservaron en
[`docs/archive/envio-prototype-migrations`](docs/archive/envio-prototype-migrations/README.md).
EF solo utiliza el historial del modelo completo en `Infrastructure/Persistence/Migrations`.

1. Mantener la capa `Domain` libre de dependencias externas.
2. Usar `Application` para orquestar casos de uso.
3. Centralizar infraestructura y acceso a datos en `Infrastructure`.
4. Mantener pruebas separadas del código de producción.
5. Mantener la nomenclatura del dominio consistente con `UltimaMilla`.
