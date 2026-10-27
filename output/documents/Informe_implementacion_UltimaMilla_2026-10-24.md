# Informe de implementación de UltimaMilla

Grupo 5 · Taller de Sistemas de Información .NET · 24 de octubre de 2026

## Alcance del avance

Implementamos el modelo de dominio, la persistencia, el aislamiento por operador y cuenta comercial, el control de concurrencia, dos consultas con caché y una base de aplicación Web. El núcleo se puede verificar mediante pruebas unitarias y de integración contra PostgreSQL. El informe describe el código disponible en el repositorio y distingue las funcionalidades integradas de los componentes preparados para los siguientes casos de uso.

La aplicación Web permite consultar el seguimiento público por HTTP y demostrar la caché con endpoints de desarrollo. El alta cuenta con un handler y un repositorio compatibles con el modelo completo; su formulario todavía requiere completar datos operativos, preparación de tarifa y fechas, y contexto autenticado. El despliegue completo, la PWA y el procesamiento asíncrono permanecen pendientes.

## Tecnologías y organización

| Componente | Tecnología y uso actual |
| --- | --- |
| Plataforma | C# y .NET 10. Desarrollo con Rider y ejecución también desde la CLI de dotnet. |
| Persistencia | EF Core 10.0.12 y Npgsql 10.0.3 para mapear y consultar PostgreSQL. |
| Datos y caché | PostgreSQL 17 y Redis 7, con contenedores locales definidos mediante Docker Compose. |
| Web | ASP.NET Core, Razor Pages y Blazor con Interactive Server para el componente inicial de alta. |
| Caché distribuida | IDistributedCache con Microsoft.Extensions.Caching.StackExchangeRedis 10.0.13 y respaldo en memoria cuando no se configura Redis. |
| Pruebas | xUnit, EF Core InMemory para pruebas de materialización y PostgreSQL real para integración. |
| Automatización | PowerShell, dotnet-ef y scripts para iniciar servicios, aplicar migraciones y probar. |

La solución contiene cuatro proyectos de producción: `UltimaMilla.Domain`, `UltimaMilla.Application`, `UltimaMilla.Infrastructure` y `UltimaMilla.Web`. Domain contiene entidades y reglas y no depende de los otros proyectos ni de EF. Application depende de Domain y declara los contratos que necesitan los casos de uso. Infrastructure depende de Application y Domain e implementa persistencia y caché. Web registra los servicios y expone páginas, componentes y endpoints.

Esta dirección permite que un caso de uso utilice una interfaz de repositorio sin conocer el DbContext. El `DbContext` es la unidad de trabajo de EF: consulta entidades, conserva sus valores originales y coordina el guardado de cambios. Se incluyeron comprobaciones automatizadas de dependencias entre ensamblados; la suite prevista con NetArchTest sigue pendiente.

## Modelo de dominio y reglas operativas

Implementamos 31 entidades, agrupadas según los cuatro diagramas UML y las áreas funcionales.

| Área | Entidades principales |
| --- | --- |
| Operadores y comercios | Operador, Comercio y CuentaComercial. |
| Usuarios | Usuario y Repartidor. |
| Configuración | ReglasOperativas, PoliticaDevolucion, MotivoNoEntrega, PlazoServicio, CuadroTarifario, LineaTarifa, AjusteTarifario, ZonaCobertura y FranjaHoraria. |
| Envíos | Envio, Bulto, Destinatario, Direccion, EventoDeEnvio, IntentoEntrega y PruebaEntrega. |
| Despacho | Vehiculo, Ruta y Parada. |
| Depósito | EscaneoBulto, Discrepancia, Devolucion y Rendicion. |
| Avisos | SuscripcionAviso, EntregaAviso y Notificacion. |

Las entidades tienen identificadores UUID, setters privados y colecciones expuestas como solo lectura. Las operaciones se ejecutan mediante métodos que validan las condiciones antes de cambiar el estado. Los constructores privados de rehidratación permiten que EF reconstruya objetos persistidos sin repetir el alta ni sus efectos.

`CuentaComercial` representa la relación entre un comercio y un operador. Un mismo comercio puede trabajar con varios operadores y cada cuenta delimita sus envíos y suscripciones. El destinatario y su dirección pertenecen al envío correspondiente. El envío conserva una referencia a su parada activa; las paradas anteriores permanecen asociadas a sus rutas.

### Ciclo de vida del envío

La tabla declarativa `TransicionesEnvio` define qué eventos se admiten para cada estado. Los métodos de Envio, Ruta y Devolucion aplican las transiciones y generan un `EventoDeEnvio`: el registro de tipo de evento, responsable, origen, fecha, estados anterior y nuevo y datos asociados. Los estados Entregado, Devuelto y Extraviado no tienen transiciones de salida en el modelo actual.

- La admisión exige cuenta comercial, destinatario, bultos, zona, versiones de configuración, tarifa, fechas, programación y responsable. Genera el primer evento de seguimiento.
- La recepción en depósito requiere los escaneos de recepción de todos los bultos antes de confirmar el cambio de estado.
- La asignación comprueba operador, estado, ausencia de otra asignación activa, ventana horaria, máximo de paradas y capacidad de peso y volumen del vehículo.
- El despacho valida los envíos activos de la ruta y los pasa a reparto. La rendición requiere una ruta despachada y sin paradas activas.
- Un intento de entrega verifica el repartidor asignado, la cantidad de intentos, el plazo entre ellos, el motivo y las pruebas exigidas por la configuración conservada en el envío.
- Las devoluciones comprueban si corresponde reintentar o devolver. Su cierre exige escanear los bultos de la devolución activa.

### Programación y evidencia

`ProgramacionEntrega` es una instantánea de fecha, franja y extremos horarios concretos. Conserva las horas locales y su desplazamiento respecto de UTC; sus límites se consultan como instantes UTC. La franja debe pertenecer a la zona del envío. Al aplazar o reprogramar, el evento conserva las programaciones anterior y nueva y se mantiene la fecha comprometida original para poder medir atrasos.

Los escaneos distinguen recepción, carga y devolución. La carga exige una asignación activa a una ruta todavía no despachada; la devolución se vincula a su proceso específico. `PruebaEntrega` representa foto, firma o evidencia textual según el tipo exigido; `GeoPunto` conserva latitud y longitud y `ArchivoRef` representa la ubicación del archivo. La captura y transferencia de archivos desde el dispositivo todavía deben integrarse.

Las reglas operativas y el tarifario tienen versión y vigencia. Al utilizarse en un envío quedan marcados en uso y sus métodos de Domain impiden editarlos; los cambios deben prepararse como nuevas versiones. Falta reforzar esa inmutabilidad frente a modificaciones directas desde persistencia.

## Persistencia y migraciones

Los mapeos Fluent API definen tablas, relaciones, claves y acceso a campos privados. EF utiliza claves foráneas shadow: propiedades de almacenamiento administradas por el contexto, sin agregar dependencias de persistencia a Domain. Las 31 entidades se representan en 30 tablas porque Usuario y Repartidor comparten una tabla mediante un discriminador. Se agregan la tabla de asociación entre usuarios y cuentas y la de instantáneas de programación: 32 tablas de modelo, además del historial de migraciones de EF.

Se configuraron UUID, fechas de operación como `timestamp with time zone`, valores decimales como `numeric`, coordenadas como `jsonb` y conjuntos de enumeraciones como arrays de enteros. Los extremos locales de programación se serializan conservando su offset original. El borrado de relaciones es restrictivo para evitar eliminaciones en cascada.

Los índices únicos respaldan, entre otros, el seguimiento, las cuentas por operador y comercio, emails y matrículas normalizados, códigos de bulto por envío, números e identificadores de intento y entregas de avisos por suscripción y evento. Un índice filtrado permite como máximo una parada activa por envío.

| Migración | Cambio incorporado |
| --- | --- |
| InitialCreate | Esquema inicial del modelo completo, relaciones e índices. |
| OperatorIsolation | Propiedad de operador, recuperación de pertenencia para datos existentes y restricciones compuestas. |
| OptimisticConcurrency | Versiones lógicas de concurrencia en envíos y rutas, incluidas las filas existentes. |

Se conserva un SQL de instalación desde cero y una fábrica de diseño para dotnet-ef. Las migraciones del prototipo simplificado se archivaron fuera del ensamblado para mantener un único historial vigente. Las pruebas verifican la creación desde cero y actualizaciones del esquema con datos existentes.

## Aislamiento por operador y cuenta comercial

Aplicamos multitenancy por fila con una base PostgreSQL y un esquema compartidos, conforme al ADR-002. `TenantId` es la propiedad shadow que identifica al operador propietario de las filas privadas. El segundo nivel de aislamiento delimita los datos de una cuenta comercial dentro de ese operador.

`TenantContext` es el objeto inmutable que representa operador, usuario, rol, cuenta activa y origen de la operación. `ITenantActual` es el contrato que lo entrega al DbContext. Cada contexto captura su ámbito al construirse, evitando cambiar de operador durante una operación o compartir un estado mutable entre mensajes. Sin ámbito no se devuelven datos y el guardado se bloquea.

- Los filtros globales nombrados de EF restringen las consultas por operador y cuenta. Incluyen las entidades secundarias a través de sus relaciones.
- `TenantSaveChangesInterceptor` interviene en SaveChanges y SaveChangesAsync. Valida altas, modificaciones, bajas y referencias, comprobando la pertenencia original y la propuesta.
- Las claves foráneas compuestas por identificador y TenantId impiden relaciones entre operadores distintos en PostgreSQL. El aislamiento por cuenta se valida en la aplicación; no se configuró RLS, es decir, políticas de seguridad por fila ejecutadas por PostgreSQL.
- Los catálogos del operador pueden consultarse desde una cuenta para preparar un envío. Ese acceso no concede permiso de edición del catálogo.
- El resolvedor de claims verifica usuario activo, rol y pertenencia a cuenta en la base. ASP.NET debe autenticar previamente la cookie o el token; la conexión con el login sigue pendiente.
- `TenantMessageScope` transporta operador y cuenta opcional para el futuro Worker. El resolvedor valida esos identificadores y genera un ámbito independiente por mensaje; el consumidor y RabbitMQ todavía no están integrados.

El aislamiento de datos no reemplaza la autorización funcional de los endpoints. Los accesos SQL privilegiados, la desactivación de filtros y las operaciones masivas deben tratarse explícitamente al incorporar nuevos casos de uso.

## Concurrencia e idempotencia

Envios y Rutas tienen una propiedad shadow `RowVersion`, un UUID marcado como token de concurrencia de EF. Al consultar, EF conserva su versión original; al guardar, la aplicación genera otra y el UPDATE compara la versión leída con la persistida. Si otro proceso ya modificó la fila, se produce un conflicto.

`AggregateConcurrency` identifica las raíces afectadas por cambios en hijos operativos. Una parada participa en la versión del envío y de la ruta; un escaneo también comprueba los agregados correspondientes. Esto permite detectar tanto dos asignaciones del mismo envío como asignaciones de envíos diferentes que compiten por la capacidad de una ruta.

`ConflictoConcurrenciaException` comunica el conflicto mediante un contrato de Application. Se utiliza para los conflictos de versión y para la violación del índice de asignación activa. El guardado transaccional revierte el intento perdedor. No se reintenta automáticamente: el caso de uso debe descartar el contexto, consultar nuevamente y volver a validar la intención.

Los agregados y las relaciones necesarias deben cargarse con seguimiento en el mismo contexto. Recuperar una versión recién al guardar ocultaría que la decisión se tomó con datos anteriores. El mecanismo cubre las escrituras mediante SaveChanges; no actualiza versiones para SQL directo o escrituras masivas que lo omitan.

La idempotencia tiene una base diferenciada. `IdOperacion` es el identificador estable de un intento de entrega y debe mantenerse en los reenvíos. Domain devuelve el intento original si el identificador ya existe y rechaza reutilizarlo para otro resultado. Los índices únicos respaldan esa regla. Falta completar el tratamiento de carreras desde los casos de uso y la idempotencia del consumidor de mensajes.

## Consultas con caché

Implementamos cache-aside sobre `IDistributedCache`, el contrato de caché de .NET. Cada lectura busca primero en caché; ante un fallo de búsqueda consulta PostgreSQL y guarda el resultado. Los decoradores agregan esta conducta a las consultas originales sin modificar su implementación. Redis es el proveedor distribuido; sin conexión configurada se utiliza memoria para desarrollo y pruebas.

| Consulta | Política implementada |
| --- | --- |
| Tarifario vigente del operador | Duración máxima de 30 minutos, acotada a la fecha de entrada en vigor del siguiente cuadro ya cargado. Clave separada por operador. Invalidación explícita disponible. |
| Seguimiento público | Duración de 30 segundos, sin invalidación por evento en el estado actual. Clave construida con SHA-256 del token, evitando almacenar el token utilizable dentro del nombre de la clave. |

`ITarifarioVigente` declara la lectura del cuadro vigente. Su implementación selecciona el cuadro por fecha y versión, devuelve líneas y ajustes y calcula `ProximaVigencia`, la fecha de activación del próximo cuadro. Se consulta con un contexto acotado al operador y sin seguimiento de cambios. Esta consulta no calcula todavía el precio final del envío.

`IInvalidacionTarifario` permite quitar la entrada cacheada. El mecanismo y su endpoint de desarrollo están implementados; falta invocarlo desde los casos de uso que creen o activen una nueva versión. El alta también debe conectarse con la consulta de tarifario vigente.

`CacheSegura` centraliza lectura, escritura, invalidación y tratamiento de fallas. Si Redis falla, registra el error y utiliza la base. Durante 30 segundos omite nuevas lecturas de caché para evitar repetir esperas; la invalidación se sigue intentando. Los resultados inexistentes no se cachean y los tokens malformados no generan entradas. Las duraciones son configurables.

## Seguimiento público y aplicación Web

El token de seguimiento es aleatorio e independiente del identificador interno del envío. La consulta valida su formato, resuelve internamente el ámbito y crea un contexto de solo lectura restringido a ese envío. Devuelve `SeguimientoPublicoDto`, un objeto de respuesta que contiene estado, fecha comprometida y ventana horaria UTC, sin contactos, direcciones ni identificadores internos.

Web expone la consulta mediante HTTP. También contiene un listado inicial con Razor Pages que recupera destinatario y dirección y un formulario Blazor con Interactive Server. El formulario tiene campos básicos y control de errores; todavía no completa los datos requeridos para un alta operativa ni el contexto de un usuario autenticado. La pantalla pública de seguimiento en Blazor sigue pendiente.

`CrearEnvioHandler` coordina el alta usando `IAltaEnvioRepository`, el contrato que carga referencias y guarda el envío. `AltaEnvioRepository` implementa ese contrato con EF y respeta el ámbito autenticado. El handler utiliza el constructor completo de Domain, valida datos mínimos y toma la hora de admisión de `TimeProvider`, el proveedor de reloj que permite controlar el tiempo en pruebas. La preparación de tarifa, compromiso y programación debe resolverse en el servidor antes de ejecutar el alta.

| Método y ruta | Comportamiento disponible |
| --- | --- |
| GET /api/seguimiento/{token} | Devuelve el seguimiento cacheado o HTTP 404 si no existe. |
| POST /dev/datos-demo | Crea datos de prueba y devuelve operador y token de un envío. |
| GET /dev/tarifario/{operadorId} | Permite consultar el cuadro vigente con caché. |
| POST /dev/tarifario/{operadorId}/invalidar | Permite probar la invalidación explícita. |
| GET /dev/cache/metricas | Devuelve contadores y tasa de aciertos de cada caché. |

Las rutas /dev se registran exclusivamente en Development. Los datos de demostración permiten probar las consultas sin depender todavía del login y del formulario completo. La respuesta JSON representa las enumeraciones como texto para facilitar su lectura.

## Avisos e instrumentación disponibles

`SuscripcionAviso` almacena la cuenta comercial, los tipos de evento elegidos, la URL de destino y el secreto previsto para firmar mensajes. `EntregaAviso` vincula una suscripción con un evento y registra éxito, cantidad de intentos y fecha del último. Domain evita duplicar una entrega para la misma suscripción y evento; existe además un índice único. `Notificacion` representa el mensaje al destinatario y su fecha de envío.

Estas entidades, sus mapeos y pruebas están implementados. El envío HTTP, la firma, los reintentos automáticos, la cola de fallidos, los proveedores de notificación y el receptor de prueba requieren integración posterior.

La caché publica contadores mediante `System.Diagnostics.Metrics`. El `Meter` llamado UltimaMilla.Cache agrupa los instrumentos de aciertos, fallos y errores; se conserva también un resumen en memoria con la tasa de aciertos para el endpoint de desarrollo. Las fallas se registran mediante ILogger. Esta instrumentación queda disponible para conectar OpenTelemetry y un tablero; no constituye todavía la observabilidad completa del sistema.

## Entorno local y pruebas

PostgreSQL utiliza un volumen persistente y una comprobación de disponibilidad; escucha localmente en el puerto 55432. Redis escucha en el puerto 56379, tiene un límite de 128 MB y utiliza una política LRU, que expulsa las entradas usadas hace más tiempo cuando se alcanza ese límite. Su persistencia está desactivada porque los valores se reconstruyen desde PostgreSQL.

`Start-LocalPostgres.ps1` prepara las credenciales locales si faltan e inicia el contenedor. `Start-LocalRedis.ps1` inicia Redis. `Start-Web.ps1` configura temporalmente la conexión de PostgreSQL, aplica migraciones y arranca Web en el puerto 5243. `Test-LocalPersistence.ps1` configura la conexión de pruebas y ejecuta las pruebas de integración. Las credenciales generadas se guardan en un archivo local excluido de Git y los scripts restauran las variables de entorno que modifican temporalmente.

La validación del 24 de octubre de 2026 comprende compilación de toda la solución y pruebas automatizadas.

| Validación | Resultado |
| --- | --- |
| Compilación | Correcta con 0 errores y 0 advertencias, después de restaurar dependencias. |
| Pruebas de Domain | 58 satisfactorias. |
| Pruebas unitarias de Infrastructure | 33 satisfactorias, incluidas 7 de caché. |
| Integración con PostgreSQL | 38 satisfactorias, incluidas 2 de las consultas con caché. Verificación contra servidor local real. |
| Total | 129 pruebas automatizadas satisfactorias. |

Las pruebas cubren relaciones, transiciones, programación, capacidad, evidencia, escaneos, devoluciones, avisos, conversiones, índices, migraciones, rollback y aislamiento con operadores y cuentas distintos. También comprueban claims inválidos, revocación de pertenencia, contextos de mensajes independientes y concurrencia con dos DbContext.

Las pruebas de caché controlan aciertos, fallos, expiración, invalidación, separación entre operadores y fallas del proveedor mediante dobles e implementaciones en memoria. Las dos integraciones adicionales comprueban la consulta real del tarifario y la combinación de PostgreSQL con el decorador de seguimiento. Esas pruebas automáticas no usan Redis real como proveedor. Las bases de integración son temporales y se eliminan al finalizar; si falta su conexión, el ejecutor las omite explícitamente.

## Documentación y trabajo pendiente

El repositorio incluye README, descripción del modelo, persistencia, multitenancy, concurrencia, entorno local, SQL de instalación y bitácora de uso de IA. Están versionados el ADR-001 de estructura y el ADR-002 de multitenancy. El código y el README hacen referencia al ADR-004 de caché, cuyo archivo no está incorporado en la copia revisada; debe verificarse antes de considerarlo parte de la documentación entregada.

Para completar los flujos y la integración quedan pendientes:

- Cálculo automático de tarifa, fecha comprometida y programación, con resolución de la zona horaria del operador y uso de ITarifarioVigente en el alta.
- Completar formularios y casos de uso y conectar la invalidación con los cambios del tarifario.
- Login, permisos por funcionalidad y registro del TenantContext autenticado en Web; completar las protecciones del seguimiento público y las pantallas pendientes.
- Protección persistente del historial y de las versiones en uso, además de las restricciones ya aplicadas por Domain.
- Outbox transaccional, contratos y transporte de mensajes, Worker, idempotencia del consumidor, reintentos y cola de fallidos.
- PWA del repartidor, almacenamiento offline, captura de evidencia, sincronización y política documentada de conflictos.
- Envío efectivo de avisos y notificaciones, firma, receptor de prueba y pantallas de consulta y reenvío.
- SignalR, reportes, liquidaciones y demás flujos que todavía no tienen implementación completa.
- Suite de arquitectura con NetArchTest, observabilidad integral, Compose completo, CI/CD y despliegue mediante infraestructura como código.

