# Persistencia con EF Core y PostgreSQL

## Estado de este paso

Se preparó EF Core 10.0.12 con Npgsql 10.0.3 y `dotnet-ef` local 10.0.12. Se generó
`InitialCreate`, su snapshot y `database-initial.sql`. Su generación funciona sin servidor.
Después se aplicaron InitialCreate, OperatorIsolation, OptimisticConcurrency y TransactionalOutbox a PostgreSQL local
en Docker y se validaron pruebas de integración contra el servidor real. Ver [entorno local](postgres-local.md).

Los mapeos están en `src/UltimaMilla.Infrastructure/Persistence/Configurations`, agrupados
por áreas. `AppDbContext` los aplica y establece borrado `Restrict` en las relaciones para
evitar eliminar dependencias e historial por cascada. Las entidades de Domain no referencian EF.

## Representación del modelo

Las 31 entidades del dominio se representan mediante 30 tablas: Usuario y Repartidor comparten
`Usuarios` con discriminador `TipoUsuario` (TPH). Se agregan `UsuariosCuentasComerciales` para
la asociación muchos a muchos, `ProgramacionesEntrega` para las instantáneas y `MensajesOutbox`
para los mensajes de integración pendientes. Son 33 tablas
del modelo, además de la tabla de historial que administra EF al aplicar migraciones.

| Elemento | Persistencia |
| --- | --- |
| Identidad de entidades | UUID generado por Domain; EF conserva el valor al insertar. |
| FK | Propiedades shadow internas llamadas `...Key`, con columnas `...Id`. Las propiedades públicas calculadas de Domain se ignoran en el modelo EF. |
| Colecciones de solo lectura | Navegaciones sobre los campos privados; EF no llama a los métodos de registro de Domain. |
| Fechas de operación | `timestamp with time zone`; los casos de uso deben proporcionar DateTime UTC. |
| Fecha de entrega y horas de franja | `date` y `time without time zone`. |
| Decimales | `numeric`, sin imponer redondeo monetario ni límites de precisión adicionales en este paso. |
| Enumeraciones | Enteros. No reordenar valores existentes sin una migración de datos. |
| Conjuntos de pruebas/eventos | `integer[]` con conversión a conjuntos inmutables y comparador por contenido. |
| GeoPunto opcional | `jsonb` mediante conversor, conservando latitud y longitud. |
| ArchivoRef y URL del webhook | Texto convertido a sus tipos de Domain. |
| Versión de envío y ruta | RowVersion shadow UUID; EF compara la versión original al modificar o borrar. |

Los objetos secundarios que pueden estar temporalmente sin dueño durante su preparación
(por ejemplo Bulto o PruebaEntrega) requieren su propietario al persistirse. Las relaciones
opcionales operativas permanecen opcionales: parada activa, devolución, motivo de no entrega
y referencias de ruta/devolución del escaneo según corresponda.

Los constructores de rehidratación evitan repetir los efectos de creación cuando EF recupera
el grafo. Los setters siguen siendo privados. El dominio determina el último intento por
su `Numero`, porque la posición dentro de una colección cargada no representa un orden SQL.

## Instantáneas de programación

`ProgramacionEntrega` sigue siendo un valor inmutable en Domain. EF le asigna un UUID shadow
en una tabla propia para que Envio y sus eventos puedan referenciar la misma instantánea.
No agrega identidad pública ni una colección histórica de paradas al envío.

`InicioLocal` y `FinLocal` se guardan como texto ISO 8601 de ida y vuelta, conservando fecha,
hora, fracciones y offset original. Guardarlos únicamente como timestamptz perdería ese offset.
`FechaEntrega` y la FK a FranjaHoraria se almacenan por separado. Los límites UTC continúan
calculándose desde la instantánea en Domain.

Estos conversores no traducen a SQL el acceso a las propiedades internas de GeoPunto ni
`VentanaDesde`/`VentanaHasta`. Las futuras consultas por intervalos UTC necesitarán un mapeo
adicional o una proyección diseñada para ellas. No se cargan todos los envíos para filtrar
ventanas en memoria como solución de producción.

## Relaciones y restricciones

Se configuraron explícitamente las relaciones del [modelo de dominio](domain-model.md),
incluidas las asociaciones uno a uno de destinatario/dirección, envío/destinatario,
reglas/política, envío/devolución y ruta/rendición.

Envio mantiene solamente su navegación a la parada activa. Cada Parada tiene su propio
Envio y Ruta; las anteriores permanecen en Ruta. Un índice único filtrado por `Activa`
permite como máximo una parada activa por envío. La FK de la navegación activa del envío
también es única y opcional. Un envío nuevo debe guardarse antes de crear su primera ruta
y asignación: insertar juntos un envío nuevo y su parada activa forma un ciclo de FK.

| Unicidad | Columnas lógicas |
| --- | --- |
| Cuenta comercial | Operador, Comercio |
| Usuario | Operador, email normalizado con lower |
| Vehículo | Operador, matrícula normalizada con lower |
| Bulto | Envío, código normalizado con lower |
| Versiones | Operador, Version en reglas y tarifario |
| Plazo | Reglas, ModalidadServicio |
| Seguimiento | CodigoSeguimiento |
| Parada | Ruta, Cod; además Envio cuando está activa |
| Intento | Envio, Numero; Envio, IdOperacion |
| Evento idempotente | Envio, IdOperacion cuando no es nulo |
| Entrega de aviso | Suscripcion, Evento |

Los índices respaldan unicidad. Las reglas de capacidad, motivos/evidencia, rangos tarifarios
no superpuestos y coherencia del grafo siguen verificándose en Domain. OperatorIsolation
agrega TenantId y FK compuestas, con validación adicional de pertenencia al guardar.
Ver [aislamiento por operador](multitenancy.md).

## Uso desde Rider o terminal

Abrir `UltimaMilla.slnx` y ejecutar desde su directorio:

```powershell
dotnet tool restore
dotnet build UltimaMilla.slnx
dotnet test UltimaMilla.slnx
dotnet ef migrations has-pending-model-changes --project src/UltimaMilla.Infrastructure
dotnet ef migrations script --project src/UltimaMilla.Infrastructure --output docs/database-initial.sql
```

`AppDbContextFactory` permite usar el proyecto Infrastructure como proyecto de inicio del
comando EF, sin un host Web todavía. Lee `ConnectionStrings__UltimaMilla`; si no está definida,
usa host localhost, puerto 5432, base y usuario ultimamilla, sin contraseña. Generar migraciones
y scripts no conecta al servidor. La cadena real deberá configurarse con secretos fuera del repo
al preparar cada entorno. `scripts/Test-LocalPersistence.ps1` configura temporalmente la
conexión local, ejecuta `database update` y restaura las variables del proceso al terminar.

Para consultas, usar las navegaciones (`e.CuentaComercial.Operador.Id`) o `EF.Property`
para las FK shadow; `e.OperadorId` es un getter calculado y no se traduce directamente a SQL.
No hay carga diferida. Los casos de uso que ejecuten reglas deben cargar el grafo necesario
completo, con seguimiento y sus colecciones; una colección parcial puede dar resultados
incorrectos en validaciones de capacidad, intentos o propiedad. El manejo de grafos desconectados
todavía no está implementado, especialmente para las FK y claves shadow.

## Verificación y pendientes

Hay 72 pruebas de Domain, cinco unitarias de Application y 37 unitarias de Infrastructure.
Las de Infrastructure comprueban el
modelo y SQL Npgsql, la correspondencia del snapshot con la migración, los conversores y la
rehidratación entre contextos mediante InMemory usando el modelo Npgsql. Cubren programación,
reintentos cargados en otro orden, evidencia, devolución, escaneos, discrepancias, rendición,
usuarios, cuentas y avisos. InMemory es una dependencia exclusiva del proyecto de pruebas.

También existen pruebas en `tests/Integration/UltimaMilla.Infrastructure.IntegrationTests`,
ejecutadas satisfactoriamente contra PostgreSQL 17.11 local. Cada prueba crea una base temporal
con nombre aleatorio, aplica las migraciones desde cero y elimina exclusivamente esa base
al terminar. Cubren esquema e historial, datos y tipos reales, asignación/intento/devolución,
reprogramación/reasignación, avisos, unicidad de cuenta/email/matrícula/bulto, FK, borrado
restringido y rollback. No modifican los datos de la base de desarrollo.

Se omiten cuando falta `ULTIMAMILLA_TEST_CONNECTION_STRING`; el script local la configura.
La conexión debe ser local y el usuario debe poder crear y eliminar las bases temporales.
Las pruebas comprueban aislamiento con dos operadores y dos cuentas de un mismo operador,
claims, Worker, seguimiento público, referencias falsificadas y migración con datos.
Las 54 pruebas de integración incluyen el alta mediante el contrato de Application,
conflictos de versión de envíos y rutas, asignaciones
simultáneas, capacidad compartida, hijos operativos, rollback, migración de versiones con datos,
caché, siete pruebas del registro transaccional del outbox y nueve de su publicador.
No demuestran todavía todas las condiciones de idempotencia.

Los filtros de operador/cuenta, TenantContext y el interceptor de validación de escrituras
ya están implementados. Envíos y rutas incorporan tokens de concurrencia de negocio y promoción
de sus cambios operativos; ver [Concurrencia](concurrency.md). Quedan pendientes la integración
con identidad/permisos, protección persistente del historial y de las versiones en uso y casos de uso.
El [outbox](outbox.md) guarda los mensajes de eventos en la misma transacción y tiene un componente
que coordina su publicación confirmada. Falta integrar el adaptador real de RabbitMQ y el host.
TenantId participa en el WHERE de escrituras, pero no reemplaza un token que detecte dos
actualizaciones simultáneas dentro del mismo operador.

Referencias de implementación: [constructores y materialización de EF Core](https://learn.microsoft.com/en-us/ef/core/modeling/constructors)
y [creación de DbContext en tiempo de diseño](https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation).
