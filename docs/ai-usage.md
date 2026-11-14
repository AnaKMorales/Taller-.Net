# Bitácora de asistencia de IA

## 14 de octubre de 2026

Área: núcleo y datos, a cargo de Agustín Rapetti según el reparto del equipo.

Se utilizó Codex para generar y revisar la implementación de las entidades de Domain a partir
de los cuatro diagramas UML, la consigna y la entrega inicial. También se generaron pruebas
xUnit y documentación del modelo. Esto incluye la reorganización de la carpeta Envio a Envios
y la sustitución del modelo inicial de Envio por relaciones entre objetos.

Las adaptaciones respecto del dibujo están detalladas en `domain-model.md`: CuentaComercial,
unidades, referencias a versiones, horas con TimeOnly, evidencia textual y URL del receptor,
entre otras. Se ignoraron las notas rojas de dudas por indicación del usuario.

La validación automatizada incluyó compilación de Domain con advertencias como errores y
39 pruebas unitarias satisfactorias. La revisión y aceptación del código por el integrante
responsable, y cualquier modificación o descarte que haga, deben registrarse después de
esa revisión. No se atribuye al integrante una revisión que todavía no se realizó.

### Correcciones de programación y escaneos

Tras contrastar el código con los diagramas y la letra, se reprodujeron tres diferencias:
la recepción exigía una ruta, la reprogramación no cambiaba la ventana y el alta permitía
ventanas incompatibles con la franja de la zona. El usuario autorizó corregirlas y pidió
conservar el modelo sin agregar la colección Envio.Paradas.

Se agregó el valor inmutable ProgramacionEntrega, la referencia a la franja, el historial
de programación en los eventos y las fábricas de escaneo por tipo. También se adaptaron
las pruebas existentes al requisito de recepción completa y se agregaron pruebas de regresión.
La ejecución posterior pasó 58 pruebas de Domain con advertencias tratadas como errores.

### Preparación de EF Core antes de Docker

El usuario autorizó preparar la persistencia antes de disponer de PostgreSQL. Se agregaron
Npgsql, EF Design, el manifiesto local de dotnet-ef, los mapeos Fluent API, constructores de
rehidratación y la fábrica de diseño. Se generaron InitialCreate, el snapshot y el SQL inicial;
no se aplicó la migración ni se conectó a una base.

Al revisar la rehidratación se corrigió la elección del último intento para usar su número,
sin depender del orden de carga de las filas. Una prueba carga los intentos en orden inverso
y comprueba las decisiones de reintento y devolución. Se documentaron las FK shadow, las
instantáneas de programación, los índices y las limitaciones pendientes.

La validación pasó 58 pruebas de Domain y 15 de Infrastructure. Estas últimas usan el modelo
Npgsql para generar SQL y almacenamiento InMemory para rehidratar grafos. No equivalen a
pruebas de integración contra PostgreSQL. Continúa pendiente la revisión humana del código.

### PostgreSQL local y pruebas reales

El usuario autorizó preparar Docker local para probar persistencia aunque el despliegue
corresponda inicialmente a otra integrante. Se agregó un Compose independiente para
PostgreSQL 17, publicado solo en localhost:55432, con volumen persistente, healthcheck y
credenciales generadas en un archivo excluido de Git. Se agregaron scripts para levantar,
migrar y probar sin mostrar las credenciales ni conservar variables del proceso modificadas.

Se levantó el contenedor, se aplicó InitialCreate a la base local y pasaron siete pruebas
de integración contra PostgreSQL 17.11. Estas crean y eliminan exclusivamente bases temporales
con nombres aleatorios; prueban esquema, materialización, flujos, restricciones y rollback.
Se mantienen pendientes aislamiento automático por tenant, concurrencia, protección del
historial y outbox. Este apoyo local no representa la entrega del despliegue completo.

## 17 de octubre de 2026: multitenancy por operador

Después de subir la base anterior a su rama, el usuario autorizó implementar multitenancy.
Se agregaron el contrato IOperadorActual, su valor inmutable para procesos internos, una
excepción de aislamiento y la captura del ámbito al crear AppDbContext. Los filtros globales
cubren todas las raíces y la asociación de usuarios/cuentas; sin operador no hay lectura
de datos ni guardado. Se mantuvo Domain sin dependencias de EF.

Infrastructure agrega TenantId shadow en las tablas privadas, FK compuestas, índices y
validación antes de SaveChanges/SaveChangesAsync. Se comprueba el propietario persistido
de objetos y referencias, incluso ante tenant falsificado. Comercio sigue compartido y
su modificación/eliminación global se bloquea desde un contexto de operador.

OperatorIsolation recupera el tenant de las relaciones existentes y se aplicó a la base local.
Las 16 pruebas de integración contra PostgreSQL pasaron, incluidas nueve nuevas de aislamiento
y actualización del esquema con datos existentes. También pasaron las 58 pruebas de Domain
y las 20 unitarias de Infrastructure. La integración con identidad, permisos, concurrencia
de negocio y outbox queda pendiente. Las decisiones y límites se documentaron en multitenancy.md.

### Completar el ADR-002 y el segundo nivel

El usuario señaló que la estrategia estaba decidida en el Word. Se volvió a leer la sección
11 y el ADR-002: la decisión por fila coincidía, pero la primera implementación había dejado
incompleto el contexto y el aislamiento por cuenta comercial. El usuario autorizó completarlos.

Se reemplazó IOperadorActual por ITenantActual y TenantContext inmutable con operador,
usuario, rol, cuenta activa y origen. Se agregaron filtros por cuenta a datos y referencias,
validación de escrituras en un SaveChangesInterceptor y protección de catálogos/instantáneas.
El alta de un envío propio puede congelar sus versiones de reglas/tarifa sin permitir editar
el resto de su configuración. Domain conserva sus dependencias y relaciones.

Se agregaron el resolvedor de claims con verificación de usuario/rol/estado/membresía,
resolución de mensajes Worker con ámbito nuevo por mensaje, helper de claves de caché y
seguimiento por token aleatorio con DTO público y contexto de solo lectura. Son componentes
reutilizables: no se implementaron el login HTTP, el consumidor RabbitMQ ni el cliente Redis.

Pasaron las 26 pruebas de integración contra PostgreSQL, incluidas cuentas del mismo operador,
claims inválidos, revocación, cuentas múltiples, escrituras propias/ajenas, Worker y seguimiento.
El segundo nivel usa relaciones existentes y no requirió una tercera migración. Se actualizó
la documentación y se registró la decisión ya existente del equipo como ADR-002 en el repo.

### Concurrencia optimista — 21/10/2026

El usuario autorizó implementar concurrencia como siguiente paso. Se contrastó con la
sección 5.3 y la prueba de asignación simultánea del Word inicial. Se incorporaron versiones
lógicas UUID shadow a Envio y Ruta, manteniendo Domain libre de dependencias de persistencia.
El guardado renueva versiones y también incluye raíces afectadas por cambios en hijos.
Se conservan la unicidad de asignación activa y las validaciones de ambos ámbitos de tenancy.

Los conflictos de versión o del índice de asignación activa se expresan mediante una
excepción de Application. No se fuerzan escrituras ni se reintentan decisiones antiguas.
Se generó y aplicó la migración OptimisticConcurrency y se actualizó el SQL de instalación.
Pasaron 58 pruebas de Domain, 23 unitarias de Infrastructure y 35 contra PostgreSQL, incluidas
nueve nuevas de concurrencia/migración. Los límites e integración futura se documentan en
concurrency.md. Siguen pendientes outbox, tratamiento completo de idempotencia, protección
persistente del historial y la integración de los casos de uso con Web/Worker.

### Corrección de incompatibilidades del merge

El usuario informó definiciones duplicadas de EstadoEnvio y símbolos de tenancy sin resolver.
Se retiró la entidad simplificada duplicada y se restauraron las referencias: Application
depende de Domain e Infrastructure depende de Application y Domain. El handler incorporado
en el merge pasó a usar un contrato de Application implementado por AltaEnvioRepository,
sin acceso directo a EF ni operador vacío. Usa el constructor completo y la hora del servidor.

El comando contempla los datos operativos necesarios y una preparación de tarifa/fechas
que debe resolver el servidor. El formulario inicial aún no completa esa preparación,
los bultos ni su contexto autenticado; se rechaza el alta incompleta. No se implementó el
cálculo automático ni el login como parte de esta corrección. Se adaptó el listado a las
navegaciones de destinatario/dirección y se registraron los servicios requeridos.

También se identificaron una migración y snapshot anteriores del prototipo para el mismo
contexto. Se conservaron sin cambios en docs/archive/envio-prototype-migrations, fuera del
ensamblado, para mantener un único historial vigente; no se borraron datos ni se reescribieron
las tres migraciones del modelo completo. La migración de una base del prototipo requiere
una revisión independiente de sus datos.

Pasaron 58 pruebas de Domain, 26 unitarias de Infrastructure y 36 contra PostgreSQL.
Las pruebas nuevas comprueban las dependencias entre ensamblados, rechazo de altas incompletas,
rechazo sin contexto y alta con el grafo completo. La comprobación de dependencias añadida
no sustituye todavía la suite de arquitectura con NetArchTest prevista por el equipo.

### 27/10/2026: primera etapa del outbox transaccional

Con autorización del usuario se agregó el registro automático de un mensaje pendiente
por cada EventoDeEnvio nuevo guardado con EF Core. El contrato versionado está en
Application y la persistencia en Infrastructure; Domain mantiene sus dependencias.
El interceptor prepara los mensajes antes de validar tenancy y concurrencia y los guarda
en la misma transacción del negocio. Se conserva el aislamiento por operador y cuenta.

Se generó y aplicó TransactionalOutbox a PostgreSQL local y se actualizó el SQL de instalación.
La migración no emite mensajes de eventos históricos. Se documentó el alcance en outbox.md
y ADR-006. No se implementaron publicador, transporte RabbitMQ, marcado, reintentos ni
consumidor idempotente en esta etapa.

Se verificaron 58 pruebas de Domain, 33 unitarias de Infrastructure y 45 de integración
contra PostgreSQL. Las siete nuevas cubren contrato, guardado síncrono, ausencia de duplicados
en guardados sin cambios, rollback, falla real del insert, concurrencia, aislamiento y migración.
Una prueba se corrigió para usar una transición válida desde EnDeposito y se volvió a ejecutar.

### 27/10/2026: componente publicador del outbox, limitado a núcleo y datos

El usuario autorizó continuar únicamente con su responsabilidad. Se contrastó el reparto
con el documento inicial: outbox corresponde a Agustín; Worker, reintentos de procesamiento,
consumidores y avisos corresponden a Aldana. Se agregaron IPublicadorOutbox e ITransporteMensajes
en Application y PublicadorOutbox en Infrastructure, sin implementar un adaptador de RabbitMQ,
un proceso ejecutable, pantallas ni infraestructura de despliegue.

El componente usa un contexto interno dedicado por operador. Selecciona una fila pendiente
por transacción con FOR UPDATE SKIP LOCKED, llama al contrato de transporte y guarda PublicadoEn
después de su confirmación. Un fallo o cancelación deja el mensaje pendiente; si el transporte
ya confirmó y falla el marcado, la siguiente ejecución conserva el identificador. Los mensajes
anteriores confirmados en el lote no se revierten. No se garantiza orden estricto ni entrega
exactamente una vez. El adaptador deberá respetar la cancelación y confirmar aceptación y ruta.

Se agregó registro de dependencias opcional y documentación de integración en outbox.md y ADR-006.
No se modificó el esquema: siguen siendo cuatro migraciones. Pasaron las 58 pruebas de Domain,
38 unitarias de Infrastructure y las 54 de integración en una ejecución completa contra PostgreSQL.
Las nuevas son cinco unitarias y nueve de integración del publicador. El transporte controlado
existe exclusivamente en las pruebas; no se afirma haber probado RabbitMQ real.

### 27/10/2026: pruebas de arquitectura con NetArchTest

Con autorización del usuario se creó UltimaMilla.Architecture.Tests en tests/Architecture
y se incorporó a la solución. Se usaron xUnit y NetArchTest.Rules 1.3.2 para analizar los
ensamblados actuales y verificar la separación de Domain, Application, Infrastructure y Web.
Se complementó con lectura de los .csproj para detectar referencias declaradas sin uso y
con un inventario que exige revisar las reglas cuando se incorpora un proyecto productivo.

Se verificó la detección de un ejemplo con EF y la aceptación de uno válido, exclusivamente
en el ensamblado de pruebas. Se reemplazó la comprobación básica de referencias que estaba
en IntegracionCapasTests, conservando sus pruebas de integración de contratos.
No se modificó código productivo ni se agregó un pipeline de CI. Se documentó cómo ejecutar
y actualizar las reglas en architecture-tests.md, README y ADR-001.

Pasaron 11 pruebas de arquitectura. La ejecución de la solución pasó 58 pruebas de Domain,
37 unitarias de Infrastructure y las 11 de arquitectura, sin fallos; omitió las 54 de PostgreSQL
porque no se configuró su conexión en esa ejecución. Las 54 se habían verificado antes de este
cambio de pruebas/documentación y no se repitieron al no haber cambios productivos.

### 27/10/2026: calculador aislado de tarifas

El usuario autorizó una funcionalidad adicional de su área que pudiera servir al resto del
equipo sin depender de sus implementaciones. Se revisaron la consigna PDF, el documento
inicial, las convenciones del modelo y los DTO actuales de tarifas. Se agregó la política
de cálculo en Domain y un contrato/adaptador en Application compatible con TarifarioDto,
sin modificar las consultas ni la caché de Ana, Web o el esquema de persistencia.

El cálculo suma los bultos, selecciona zona/modalidad/peso y volumen máximo, aplica ajustes
sobre la base y devuelve el desglose y la versión. Se documentaron las decisiones de
porcentajes aditivos y redondeo final, que la consigna no fija, y el límite del modelo actual
sin bandas de precio por volumen. Sigue pendiente la integración al alta y el cálculo de fechas.

Se creó UltimaMilla.Application.Tests y se incorporó a la solución. Pasaron los 14 casos nuevos
de Domain y cinco de Application. La ejecución local completa pasó 125 pruebas: 72 Domain,
cinco Application, 37 Infrastructure y 11 de arquitectura. Omitió las 54 de PostgreSQL al no
configurar su conexión. Las pruebas de metadatos confirman que no hay cambios de modelo pendientes.

### 30/10/2026: actualización de las pruebas del historial de migraciones

El usuario autorizó adaptar las pruebas tras incorporar CredencialesLogin, la quinta migración.
Se retiró la expectativa fija de cuatro: la prueba del modelo exige migraciones registradas
y conserva la comprobación de que no haya cambios sin migrar en el modelo. La integración
compara los identificadores de todas las migraciones del proyecto con los realmente aplicados,
manteniendo las comprobaciones de pendientes y del esquema.

Pasaron las 37 pruebas unitarias de Infrastructure y la prueba específica de migración desde
cero contra PostgreSQL. Esta última utilizó una base temporal; no se modificó la base de desarrollo
ni se agregaron migraciones o cambios de código productivo.

### 30/10/2026: gestión administrativa de configuración versionada

El usuario autorizó implementar su backend de reglas y tarifas, conservando para sus
compañeras la conexión con las pantallas. Application define IConfiguracionVersionada
y sus DTO. Infrastructure implementa consultas, selección de vigencia, catálogo de zonas
y creación de nuevas versiones completas reutilizando las validaciones de Domain.

Se verifica administrador activo del operador, vigencia UTC inmediata o futura y última
versión esperada. Las creaciones se serializan mediante bloqueo de la fila del operador
en una transacción; un conflicto requiere recargar. Después del commit tarifario se llama
al contrato de invalidación ya existente. Si este falla, se informa en el resultado sin
presentar la versión confirmada como un guardado fallido.

ConfiguracionWriteGuard protege versiones completas e hijos ya guardados desde SaveChanges
síncrono/asíncrono, permitiendo el marcado EnUso ligado al alta. No cambia SQL directo ni
otros catálogos. No se agregaron tablas, migraciones, pantallas, endpoints o código de Worker.
El registro de servicios es optativo y la integración se documentó en configuracion-versionada.md.

Pasaron 125 pruebas locales. La primera suite PostgreSQL pasó sus 58 casos existentes y
detectó dos fallos nuevos por una navegación de zona sin cargar; se corrigió la consulta.
También se corrigió el escenario de primera versión para no precargar accidentalmente
la configuración mediante el grafo de prueba. La repetición de los diez casos nuevos pasó
completa; las once pruebas de arquitectura volvieron a pasar con los contratos finales.

La revisión automática inicialmente rechazó la ejecución con la base de desarrollo como
conexión de control. Se inspeccionó el aislamiento de la suite y se ejecutó una alternativa
con una base de control temporal explícita y bases temporales por prueba, eliminadas al
terminar. La base de desarrollo no se modificó durante estas verificaciones.

### 01/11/2026: seguimiento del cambio y prueba del flujo de entrega

Se creó CAMBIOS-REQUERIMIENTOS.md junto al README, contrastado con el PDF del 30/10:
alcance de tres integrantes, requisitos 31–36, responsabilidades, orden de trabajo,
decisiones abiertas y aclaraciones de caché y banderas. Se enlazó desde README.

Se agregó FlujoEntregaTests, una prueba de integración del núcleo con contextos independientes
para recepción, asignación, carga, despacho, entrega, repetición de operación y rendición.
Verifica persistencia, historial, evidencias, mensajes pendientes e invisibilidad para otro operador.
No implementa pantallas, PWA, Worker ni entrega parcial. La admisión usa el escenario de Domain
persistido; no es una prueba completa desde HTTP ni del alta automática de tarifa y fechas.

Pasaron 125 pruebas sin conexión y la nueva prueba contra PostgreSQL real con base de control
temporal y base temporal por prueba, eliminadas al finalizar. Las otras 68 integraciones se
omitieron en la ejecución sin conexión; no se reportan como ejecutadas en esta sesión.
El primer escenario falló al crear vehículo y asignación simultáneos por propagación de TenantId.
Con la flota registrada previamente pasó. El caso simultáneo queda documentado como pendiente,
sin modificar ni debilitar las validaciones de producción. No se aplicaron migraciones a desarrollo.

### 01/11/2026: corrección de asignación con vehículo nuevo (letra original)

El usuario autorizó corregir el fallo de persistencia dentro de su responsabilidad de núcleo/datos.
Dos regresiones reprodujeron el error de propagación de Envio.TenantId, con guardado síncrono
y asíncrono. AppDbContext ahora estampa el operador capturado al empezar a seguir entidades
Added con TenantId vacío, antes de que la detección de relaciones propague sus claves compuestas.
No sobrescribe tenants explícitos ni filas leídas/adjuntadas como existentes. Se mantienen los
guards de guardado, filtros, claves compuestas y validaciones de cuenta y concurrencia.

Se corrigió también una expectativa del escenario de prueba: ya existía un vehículo en los datos
iniciales y debían comprobarse ambos IDs, no una única fila. No cambió el modelo de tablas.
No se modificaron Web, PWA, Worker, contratos de entrega parcial ni migraciones.
La explicación técnica se agregó a multitenancy.md; el seguimiento del PDF nuevo remite allí
para distinguir esta corrección de los requisitos 31–36. Compilación Release sin advertencias.

Verificación final: pasaron las 196 pruebas en una ejecución de la solución, incluidas las
71 integraciones contra PostgreSQL real (sin omisiones). La base de control y las bases
temporales por prueba se eliminaron al terminar. Incluye ambas regresiones nuevas y las
protecciones existentes frente a referencias cruzadas, propietarios falsificados, escrituras
de otra cuenta, concurrencia y outbox. No se modificó la base de desarrollo.

### 01/11/2026: configuración versionada de entrega parcial (requisito nuevo 33)

Se implementó una etapa acotada del nuevo documento dentro de núcleo/datos:
ReglasOperativas agrega permiso de entrega parcial y AccionBultosPendientes, con
las tres alternativas de la consigna. Los argumentos opcionales false/0 conservan
las llamadas existentes y Domain rechaza acciones desconocidas antes de registrar la versión.
DatosReglasDto y el backend de configuración reciben y devuelven los campos; no se
implementaron formularios, PWA ni Worker. La acción puede quedar preconfigurada aunque
el permiso esté desactivado. No se definió un importe ni una fórmula de recargo.

Se generó 20261101185636_ReglasEntregaParcial con dos columnas, defaults para datos previos
y CHECK de valores válidos. Se actualizó el SQL de instalación y se comprobó que no quedan
cambios del modelo sin migración. No se aplicó la migración a la base de desarrollo.
Cinco casos de Domain y tres integraciones cubren defaults, políticas, históricos, migración,
aislamiento, inmutabilidad y acciones inválidas incluso mediante SQL directo.
Se documentó en CAMBIOS-REQUERIMIENTOS.md como avance parcial, con campos y valores para
integración del equipo y la necesidad de reenviarlos al crear versiones sucesivas.
Quedan pendientes los estados y resultados por bulto y ejecutar la política en el flujo real.

Verificación final: las 204 pruebas pasaron en la ejecución completa, incluidas 74 integraciones
contra PostgreSQL real sin omisiones. Se eliminaron las bases temporales al finalizar.
Compilación Release con advertencias como errores: cero errores y advertencias.

### 01/11/2026: estados de bultos y derivación del envío (base del requisito 31)

El usuario autorizó avanzar solo con núcleo/datos. Se agregó EstadoBulto y una tabla pura
de transiciones declarativas; Bulto conserva setter privado y solo Envio modifica su estado.
Recepción, asignación, rechazo, aplazamiento, despacho, intento completo, reintento,
devolución y extravío validan primero todas las transiciones. El resumen persistido de Envio
se calcula desde los resultados de sus bultos y se contrasta con su transición declarativa.

EstadoConjuntoEnvio define conjuntos homogéneos y mixtos, y se agregó EntregadoParcialmente
al final del enum sin renumerar valores previos. Las prioridades de casos mixtos son una
decisión de diseño documentada en el seguimiento y ADR-007 en curso. No se implementó un
método que registre resultados distintos por bulto: la entrega mixta operativa, evidencias,
asignación de pendientes y eventos nuevos quedan para las siguientes etapas. No se presenta
el requisito 31 como completamente terminado. No se modificaron Web, PWA ni Worker.

La migración EstadosPorBulto agrega Estado, infiere cada valor anterior desde Envio uniendo
también TenantId y agrega CHECK de valores 0–9. No crea eventos ni evidencias anteriores.
Se actualizó el SQL de instalación y el modelo coincide con las migraciones; no se aplicaron
a desarrollo. Se ampliaron las comprobaciones del flujo persistido y se agregaron doce
casos de Domain y tres integraciones (backfill por operador, concurrencia y CHECK).

La primera ejecución detectó un fallo de arquitectura por un tipo auxiliar generado por
el compilador para el array constante de prioridades. Se cambió a una lista de solo lectura,
sin alterar las reglas de arquitectura, y las once pruebas volvieron a pasar.

La ejecución final completa pasó las 219 pruebas (89 Domain, 5 Application, 37 Infrastructure,
11 arquitectura y 77 PostgreSQL), sin omisiones. Release compiló sin errores ni advertencias.
La migración se verificó en bases temporales eliminadas al finalizar; desarrollo no se modificó.

### 01/11/2026: resultados y evidencias por bulto (requisito nuevo 32)

Se implementó RegistrarIntentoPorBulto en Domain, sin Web/PWA/Worker. El resultado por bulto
incluye motivo activo de la versión del envío para cada fallo y las evidencias exigidas.
Una prueba puede acreditar varios entregados mediante PruebaBulto, pero no se comparte entre
entregados y fallidos. DetalleIntentoBulto conserva resultado y motivo; IntentoEntrega conserva
su resumen. Las validaciones preceden toda mutación; al confirmar se actualizan los bultos,
el resumen del envío, el historial y se libera la parada. La firma previa sigue disponible y
genera detalles para intentos nuevos, sin alterar el formato V1 ni reconstruir historial previo.

La operación nueva exige la parada de origen para rechazar trabajo de otra visita, UTC e
IdOperacion estable. Una huella SHA-256 compara contenido y asociaciones sin depender del
orden ni de nuevos Id de evidencia al reconstruir el reenvío. Igual contenido devuelve el
intento existente; otro contenido se rechaza. El llamador debe resolver identidad/catálogo
y cargar el agregado con seguimiento. Orquestación de Application y endpoint aún pendientes.

Se generó ResultadosEntregaPorBulto: dos tablas, huella nullable, FK de envío/intento/tenant,
índices y CHECK. TenantModel conserva los componentes adicionales de las FK; sus guards leen
la posición del Id principal en claves de más de dos columnas. Se agregaron caminos de cuenta,
concurrencia por padres e inmutabilidad de intentos/evidencias en SaveChanges. El outbox nuevo
usa EventoEnvioV2 para el método por bulto; el transporte permanece abstracto y Worker debe
integrar las versiones y la compatibilidad de receptores. No se aplicaron migraciones a desarrollo.

Las primeras integraciones detectaron supuestos de fixture de un solo bulto y una navegación
sin cargar en una comprobación de cuenta; se corrigieron los escenarios. También se detectó
una carrera real en la que PostgreSQL rechaza el número de intento antes de actualizar RowVersion:
se tradujeron los índices de número/IdOperacion de intento y de IdOperacion de evento a conflicto
de concurrencia, conservando el rollback y sin presentar una colisión como éxito idempotente.

Se agregaron 18 casos de Domain y seis integraciones de persistencia, repetición, outbox V2,
concurrencia, rollback, FK incluso dentro de un operador, cuenta, historial y migración.
El detalle de integración quedó en intentos-por-bulto.md y el seguimiento compartido y ADR-007.
Las nuevas visitas, devolución de pendientes y recargos siguen pendientes de los requisitos 33/34.

La ejecución final completa pasó 243 pruebas: 107 de Domain, 5 de Application, 37 de
Infrastructure, 11 de arquitectura y 83 de PostgreSQL real, sin omisiones. Release compiló
con advertencias como errores sin errores ni advertencias, y EF confirmó que no hay cambios
de modelo pendientes de migración. Se eliminaron las bases temporales; desarrollo no se modificó.

### 03/11/2026: caso de uso Application de registro por bulto

El usuario autorizó conectar identidad, carga del envío, versión esperada y guardado, dentro
de núcleo/datos. Se agregaron RegistrarIntentoCommand, DTO de respuesta, handler y puertos
de una operación de repositorio desechable. La solicitud no contiene operador, usuario ni rol;
define referencias locales de evidencia para construir una prueba compartida sin recibir entidades.
El handler copia/valida la solicitud, resuelve motivos de la versión del envío y delega en Domain.

RegistroIntentoRepository usa el tenant autenticado capturado y verifica un repartidor real,
activo y asignado al envío, o autor de la operación histórica que se solicita repetir.
Cada ejecución usa un DbContext dedicado. La carga dividida tiene un snapshot RepeatableRead
y termina su transacción de lectura antes del trabajo de Domain. Una operación nueva exige
la versión esperada; un reenvío exige igual huella y no guarda, aunque su versión sea anterior.
Se revalida el repartidor antes de SaveChanges. La concurrencia del guardado y el outbox existentes
se mantienen y no se reintentan decisiones automáticamente. Siempre se dispone el contexto.

El registro de servicios es optativo; no se modificaron Program.cs, endpoints, Web, PWA o Worker.
No cambió el modelo de base ni se generaron/aplicaron migraciones. Se actualizaron el seguimiento
compartido, README, ADR-007 y documentos de integración para distinguir el caso de uso del endpoint.
Se agregaron diez casos unitarios y cinco integraciones: identidad/rol/operador/asignación,
revocación incluso entre carga y guardado, versiones, reenvíos, colisión de contenido, guardado
V2, rollback y dos casos de uso que cargan la misma versión y solo uno confirma.

Verificación final: pasaron las 258 pruebas de la solución (107 Domain, 15 Application,
37 Infrastructure, 11 arquitectura y 88 contra PostgreSQL real), sin omisiones. Release
compiló con advertencias como errores sin errores ni advertencias. EF confirmó el modelo
sin cambios pendientes de migración. Las bases temporales fueron eliminadas; desarrollo no se modificó.

### 03/11/2026: asignación y capacidad de pendientes (requisito nuevo 34)

El usuario autorizó asignar solo pendientes a otra ruta y calcular su peso/volumen, dentro
de núcleo/datos. Se agregó BultoParada como composición histórica con medidas capturadas.
Ruta.AgregarParada conserva su firma y selecciona todos los pendientes preparados; la
capacidad suma snapshots de paradas activas. Se conserva una sola asignación activa por envío,
sin agregar una colección histórica a Envio. Los escaneos de carga exigen pertenencia a la visita.

Reprogramación, asignación, despacho y entrega solo modifican pendientes; los entregados
anteriores conservan sus resultados y evidencias. El resumen parcial persiste hasta completar
los restantes. El registro por bulto y su caso de uso admiten la selección actual y una visita
con otro repartidor. Se soporta la nueva visita sin recargo, respetando intentos, plazos,
motivos y programación. Recargo/devolución parcial se rechazan hasta implementar su tratamiento.

La migración BultosPorParada crea la tabla, medidas positivas, unicidad y FK compartiendo
envío/tenant. Se agregaron aislamiento, concurrencia de Envio/Ruta e inmutabilidad del
contenido persistido. El backfill incluye todos los bultos de cada parada anterior: el modelo
previo solo permitía visitas completas. Las medidas se inicializan con valores actuales,
sin pretender reconstruir medidas históricas que no se almacenaban. No se generan eventos.

Los eventos administrativos parciales se guardan en V2 con IdOperacion nullable y Bultos
vacío; los intentos V2 conservan identificador y detalles de la visita. Así V1 no recibe
nuevo vocabulario de estados parciales ni se fabrican resultados de intento desde estados posteriores.
Se actualizaron los loaders del núcleo y sus pruebas. No se modificaron Web, PWA o Worker.
El contrato y sus límites se documentaron en rutas-pendientes.md, seguimiento compartido y ADR-007.

Diez casos nuevos de Domain y cuatro integraciones verifican capacidad, varias visitas,
escaneos/resultados ajenos, políticas, históricos, migración, cuenta/FK/inmutabilidad y
competencia por una asignación. En la primera ejecución faltaba cargar la navegación Envio
en la comprobación de cuenta de una prueba; se corrigió el escenario. La prueba de recorrido
se amplió con otro repartidor y reconocimiento de los reenvíos de ambas visitas.

Verificación final: las 272 pruebas pasaron (117 Domain, 15 Application, 37 Infrastructure,
11 arquitectura y 92 PostgreSQL real), sin omisiones. Release sin errores ni advertencias
y modelo sin cambios pendientes de migración. Se actualizó el SQL de instalación; no se
aplicó a desarrollo. Las bases temporales se eliminaron al finalizar.

### 03/11/2026: política de pendientes y recargo fijo (requisito nuevo 33)

El usuario autorizó completar el último punto de la tabla y eligió explícitamente importe
fijo configurable por nueva visita. Se agregó ImporteRecargoNuevaVisita a reglas y DTO,
con valor positivo de hasta dos decimales para la política con recargo y null para las otras.
Se captura en la parada y se registra al despachar, una vez por visita, separado de la
tarifa original. RecargoVisita representa devengo, no pago ni integración de facturación.
Planificar, retirar y reasignar antes del despacho no devenga cargos.

La devolución parcial captura pendientes mediante BultoDevolucion y exige sus escaneos.
El cierre comprueba la composición completa para no aceptar una carga filtrada. Entregados
previos no cambian y el resumen de entregados más devueltos sigue parcial. La opción de
devolución impide reprogramar; se conservan las reglas de agotamiento y motivos existentes.

Se extendieron mapeos, filtros de cuenta/operador, FK compartiendo envío/tenant, promoción de
concurrencia e inmutabilidad de composición/cargos/importe previsto. La migración nueva
recupera devoluciones anteriores completas, no inventa cargos y deja importe null en reglas
antiguas. Las políticas antiguas con recargo sin precio bloquean la nueva visita y requieren
una decisión explícita; no se modifican retroactivamente reglas en uso.

Se agregaron casos de Domain y seis integraciones de configuración, persistencia, aislamiento,
unicidad, rollback, despacho concurrente, devolución parcial y migración. La primera ejecución
completa detectó que el índice único de cargo podía fallar antes del UPDATE de la raíz:
se agregó su traducción al conflicto de concurrencia de Application. Se amplió la devolución
para verificar que una carga filtrada no permita cerrar bultos sin escanear.

Se actualizaron seguimiento compartido, documentación de integración, README y ADR-007,
que sigue en curso. No se modificaron Web, PWA ni Worker ni se aplicó la migración a desarrollo.

Verificación final: pasaron 287 pruebas (126 Domain, 15 Application, 37 Infrastructure,
11 arquitectura y 98 contra PostgreSQL real), sin omisiones. La prueba ampliada confirmó
el rechazo de una composición de devolución filtrada y el despacho competidor se tradujo
a conflicto sin duplicar cargos. Release compiló con advertencias como errores sin errores
ni advertencias, EF confirmó el modelo alineado y se regeneró el SQL de instalación.
Las bases temporales se eliminaron; desarrollo no se modificó.

### 03/11/2026: backend de monitoreos anteriores al 12/11

El usuario autorizó completar su parte pendiente de los monitoreos anteriores, excluyendo
el 12/11 (opcionales y ensayo) y tareas de sus compañeras. Se implementó IHojasRuta con
DTO de lectura en Application y consultas dedicadas en Infrastructure: solo Repartidor
auténtico/activo, rutas propias despachadas/no rendidas y paradas activas, ordenadas por
Cod ascendente como secuencia manual del despachador. El detalle usa RepeatableRead para
descargar versiones, composición por visita, dirección, ventana y requisitos de evidencia
en un snapshot coherente. No se inventa una fecha local de ruta ni se usa el día del servidor.

Program registra hojas de ruta y configuración versionada. La Web expone la descarga y
consultas/creación de versiones administrativas, sin lógica de negocio en los endpoints.
Se agrega antiforgery a las escrituras con cookie y un endpoint autenticado que entrega
el token. Un marcador limita las respuestas 401/403 a estas API, conservando el comportamiento
previo de las páginas. Se mantienen comprobaciones de tenant/rol, versión e invalidación
del tarifario del servicio existente. No hay cambios de modelo ni migraciones.

Se agregaron seis pruebas HTTP con WebApplicationFactory sobre el Program real y bases
PostgreSQL temporales, usando un autenticador sintético solo en el servidor de pruebas y
el resolver real de usuarios/operador. Comprueban rutas no despachadas/rendidas, orden,
versiones, composición de pendientes, aislamiento, roles, revocación, antiforgery, edición
en caliente, conflicto y conservación de reglas/tarifa históricas. La primera ejecución
detectó redirecciones 302 en denegaciones de API y una navegación no cargada en una prueba;
se corrigieron ambos puntos. La comprobación de caché se amplió para poblarla antes del
POST y comprobar la nueva versión y el aislamiento después, usando caché de desarrollo.

No se implementaron pantallas, emisión de tokens/login móvil, PWA, Worker, adaptador de
broker, endpoint de intentos, cobertura, opcionales o ensayo. Se documentaron contratos,
integraciones de sus responsables y la diferencia entre backend listo y demostración
completa en api-monitoreos.md, README y seguimiento compartido.

Verificación final: pasaron 293 pruebas (126 Domain, 15 Application, 37 Infrastructure,
11 arquitectura y 104 PostgreSQL real), sin omisiones. Los seis casos HTTP pasaron después
de las correcciones y confirmaron la invalidación de la caché poblada. Release compiló
con advertencias como errores sin errores ni advertencias; EF confirmó el modelo alineado
con las migraciones existentes. Se eliminaron las bases temporales; desarrollo no se modificó.

### 07/11/2026: observabilidad, punto 4 de la organización del equipo

El usuario autorizó completar todos los pendientes de este punto y probar el proyecto.
Se incorporaron Serilog con JSON en consola y exportación OTLP, instrumentación HTTP de
OpenTelemetry en Web y Worker, métricas operativas y el recorrido correlacionado desde
la PWA. La PWA guarda el contexto con cada acción y lo conserva al reenviar; el outbox
lo persiste como metadatos separados de V1/V2, RabbitMQ lo transporta y el consumidor
lo recupera sin heredar una actividad ajena. La migración TraceContextOutbox agrega dos
columnas opcionales y se actualizaron snapshot y SQL de instalación.

Se agregaron health checks /live, /ready y /health; los dos últimos comprueban PostgreSQL.
El Worker toma muestras de outbox, avisos fallidos y profundidad de colas cada 30 segundos.
La PWA reporta cantidades pendientes/rechazadas a un endpoint autenticado. Las muestras
vencen en cinco minutos y sus identificadores no se exportan como etiquetas. Los fallos
del reporte auxiliar no bloquean la sincronización de entregas.

Un compose local separado provisiona colector, Grafana, Prometheus, Tempo y Loki, con
13 paneles y consulta de trazas/logs. Se documentaron comandos, métricas, límites y
conexión del despliegue en README y observabilidad.md. La configuración de AWS corresponde
a la integración de infraestructura del equipo.

La verificación completa pasó con 484 pruebas; después pasaron seis comprobaciones
finales que incluyen un caso adicional del nuevo endpoint. Los TRX confirman 485 casos
distintos satisfactorios: 126 Domain, 31 Application, 105 Infrastructure, 21 Worker,
41 PWA, 13 arquitectura y 148 integración. No hubo omisiones en esa verificación.
Se usaron PostgreSQL y RabbitMQ temporales y un colector real; se consultaron trazas
en Tempo, logs correlacionados en Loki, métricas y las consultas del tablero en Prometheus.
También se publicó la PWA en Release y se arrancó el Worker real con una base temporal:
declaró su topología, permaneció activo sin errores y sus logs llegaron por OTLP a Loki.
Los servicios temporales se eliminaron y el tablero local quedó disponible.

### 13/11/2026: cobertura e importación masiva idempotente

El usuario autorizó mejorar la cobertura y después implementar el backend de importación,
sin modificar las pantallas de las compañeras. Se propusieron y aceptaron resultados por
fila y una referencia estable por operador/cuenta, sin depender de la identidad del lote.
Se añadieron pruebas unitarias de alta, reprogramación y procesamiento del lote; un puerto
en Application; persistencia técnica en Infrastructure; bloqueo transaccional por referencia,
índice único, huella SHA-256 y guardado conjunto del alta/outbox/registro. Se reutilizó el
alta del portal y se registraron los servicios en Web, sin exponer otro endpoint.

Se ajustó la propuesta para normalizar la escala decimal de la huella y revalidar el usuario
antes de recuperar resultados existentes. No se añadió lector de archivos ni interfaz.
Se generaron migración, SQL y documentación de integración. Las pruebas propias de
PostgreSQL incluyen simultaneidad, rollback e independencia entre cuentas y operadores.
La última selección de ocho pruebas pasó. La regresión amplia detectó una expectativa
genérica de tabla no vacía, corregida y comprobada, y dos timeouts de datos demo pendientes
de investigar. No se presenta esa corrida completa como satisfactoria.
La compilación final no tuvo advertencias ni errores; cobertura Domain 85,97 % y
Application 74,12 %. No se aplicaron migraciones a desarrollo ni se hicieron commits.

### 13/11/2026: backend de preparación y despacho de rutas

El usuario autorizó el caso de uso y la API de despacho dentro del backend, dejando el
botón del backoffice a su responsable. Se implementaron consulta de versión/carga y POST
con permisos, antiforgery y concurrencia, reutilizando Ruta.Despachar y el outbox existente.
La carga se comprueba sobre los bultos de la visita activa, con escaneos de esa ruta y
posteriores a la última asignación. La lectura coherente del grafo usa una unidad propia;
el guardado conserva las versiones originales para competir por ruta y envíos.

Se aceptó la propuesta sin añadir API de escaneo/carga, pantalla ni migración. El alcance
restante quedó explícito en docs/despacho-operativo.md y en el seguimiento de pendientes.
Se probaron cinco escenarios con PostgreSQL temporal (API, falta de carga, rollback,
concurrencia y pendientes), todos aprobados. Pasaron 200 casos de Domain/Application y
13 de arquitectura. Compilación estricta sin advertencias; cobertura Domain 85,97 % y
Application 75,65 %. No se repitió la suite completa de PostgreSQL; los dos timeouts de
datos demo observados en la etapa previa siguen por investigar. Sin commits ni despliegue.

### 13/11/2026: registro de carga por código

El usuario autorizó continuar con el backend de carga. Se reutilizó la unidad de lectura
de rutas, conservando separados los permisos de carga y despacho: depósito puede cargar;
el repartidor usa Bearer y solo opera sobre su ruta; despachar continúa reservado al
administrador/despachador. Se compartió el cálculo de bultos cargados entre ambos casos.

Se aceptó el reconocimiento de cargas repetidas por ruta/bulto/asignación, sin otra
migración, y la desambiguación explícita de códigos compartidos mediante BultoId. No se
implementaron interfaces, lectura de cámara ni sincronización offline de carga. Los
escaneos no cambian el estado del envío; ese cambio sigue ocurriendo al despachar.
Se documentaron contrato, errores, permisos y límites en docs/carga-rutas.md.

Se corrigieron datos de preparación de las pruebas (referencias a grafos desconectados
y correos duplicados) y una expectativa de 401 que debía ser 403 para el acceso revocado.
No se cambió la autenticación para acomodar las pruebas. Quedaron verificados diez casos
de integración de carga/despacho con PostgreSQL, en una selección de nueve aprobados y
la repetición aprobada del caso Bearer corregido. Pasaron 206 casos de Domain/Application
y 13 de arquitectura. Cobertura final Domain 85,97 % y Application 77,26 %.
La solución compila con advertencias como errores. Sin cambios de base de desarrollo,
commits o despliegue; se eliminaron los contenedores temporales de prueba.

### 13/11/2026: dependencias de pruebas en GitHub Actions

El usuario cambió la prioridad para ejecutar las pruebas omitidas antes del resto del
circuito de depósito. Se añadieron servicios efímeros PostgreSQL, RabbitMQ y Redis al
runner, variables de integración y el compose existente de observabilidad con espera de
disponibilidad. Se reutilizaron los puertos y la configuración de la prueba del colector.
No se requieren credenciales ni infraestructura AWS para este job.

Se agregó control de omisiones leyendo resultados TRX y publicación de dichos archivos.
Se comprobó localmente el rechazo de un TRX con NotExecuted y la aceptación de los 13
resultados reales de arquitectura. No se ejecutó el workflow completo localmente ni se
afirma que haya pasado en Actions: requiere subir los cambios y revisar su primera corrida.
Los dos timeouts de datos demo siguen pendientes de investigar; no se alteraron pruebas
de negocio ni se desactivaron para hacer pasar CI. Sin commit, push ni despliegue.

### 13/11/2026: investigación de la prueba de logs en Loki

El usuario proporcionó los logs del CI: 579 casos aprobados y uno fallido al consultar
el log correlacionado de la PWA en Loki. Se revisaron exportación, consulta y ciclo de
vida del host. La prueba original pasó aislada contra PostgreSQL, RabbitMQ y LGTM reales;
se comprobó además el registro en Loki con el trace_id exacto. No se pudo reproducir
localmente el fallo de esa ejecución completa de GitHub.

Se detectó que ForceFlush cubría métricas y trazas, pero no el sink asíncrono de Serilog.
Se cerró explícitamente el host tras generar/exportar la telemetría y antes de consultar
Loki para vaciar ese sink. Se preservó la aserción original y su tiempo de espera, y se
añadió la traza al mensaje de error. El pipeline ahora conserva en su salida los últimos
logs del colector antes de detenerlo. Es una corrección de sincronización; su relación
causal con el fallo intermitente del CI requiere confirmarse en la siguiente corrida.

La prueba ajustada y la prueba de logging aprobaron juntas con los servicios reales.
Compilación estricta correcta. Se eliminaron los contenedores temporales y se conservó
el LGTM local que ya estaba iniciado. No se modificó código de producción ni se hizo push.

### 13/11/2026: persistencia del fallo del colector en CI

El usuario aportó otra corrida con la misma falla después de vaciar Serilog. Eso descarta
dar por suficiente el ajuste previo: la causa exacta del fallo en la suite remota sigue
sin confirmarse. El log del contenedor mostraba `logging=false` para Loki y el colector.
Se inspeccionaron los scripts de la imagen local para verificar los nombres de las variables
de diagnóstico; se habilitan solo en CI mediante OBSERVABILIDAD_LOGS_INTERNOS.

Se añadió una captura local de eventos Serilog para distinguir emisión de exportación,
SelfLog para informar errores internos del sink y diagnóstico en la aserción de Loki.
Se separó la prueba del colector en otro proceso dentro del mismo job, sin reintento,
con su TRX y validación obligatoria de resultados no vacíos/no omitidos. La selección
principal excluye únicamente esa prueba y E2E, ambas ejecutadas en sus pasos dedicados.
La hipótesis de interferencia entre hosts/proveedores requiere confirmación en GitHub.

La compilación estricta pasó y la prueba de colector con diagnóstico pasó localmente con
PostgreSQL, RabbitMQ, Redis y LGTM reales. Se eliminaron los tres servicios temporales y
se dejó el LGTM previo. No se desactivaron aserciones ni se ampliaron esperas. Sin push.

### 14/11/2026: rechazo de escrituras del WAL de Loki en CI

Los nuevos logs aportados por el usuario muestran reintentos del colector hacia Loki
con HTTP 503 e `Ingester is shutting down` mientras las consultas continúan respondiendo.
El fallo persiste aun ejecutando el caso en un proceso aislado. Se consultaron fuentes
primarias de Loki: la incidencia https://github.com/grafana/loki/issues/23769 y el PR
https://github.com/grafana/loki/pull/24750 describen ese mensaje engañoso cuando el WAL
supera su umbral de uso de disco. No se dispone del porcentaje de disco de aquella
ejecución remota, por lo que esa causa concreta sigue pendiente de confirmación en CI.

Se reprodujo el mecanismo sin llenar el disco: en un Loki temporal con la misma imagen
se redujo el umbral del WAL al 0,1 %. Apareció la advertencia de throttling y una escritura
válida devolvió HTTP 500 con el mismo mensaje `Ingester is shutting down`. Con un tmpfs
de 512 MiB y el WAL trasladado a él, manteniendo el mismo umbral reducido, la escritura
respondió 204. La diferencia 500/503 corresponde a la entrada directa usada en esa
reproducción frente a la exportación OTLP del log remoto; el rechazo del ingester coincide.

Se agregó un override de compose exclusivo de CI para datos y WAL de Loki en ese tmpfs.
La configuración normal conserva WAL habilitado y su umbral del 90 %. El workflow usa
el override en arranque, diagnóstico y cierre, e imprime uso de disco del runner y Loki.
Se documentó el alcance en docs/observabilidad.md. No cambia el compose de desarrollo,
la configuración AWS ni código de negocio o aserciones.

Validación: compose combinado válido, git diff sin errores de espacios y un caso del
colector completo aprobado (cero omitidos) contra PostgreSQL, RabbitMQ, Redis y un LGTM
temporal levantado con ambos archivos compose. Se comprobó que /data/loki es tmpfs.
La ejecución de GitHub con estos cambios todavía está pendiente. Los recursos temporales
de la revisión se eliminaron y se conservó el LGTM local anterior. Sin commit ni push.

### 14/11/2026: configuración de compilación en las migraciones del job E2E

El usuario confirmó build-test aprobado en GitHub; E2E falló antes de ejecutar pruebas
porque EF buscaba Infrastructure.deps.json en bin/Debug. El comando usaba `-c Release`:
en dotnet ef esa opción selecciona el DbContext, mientras que la configuración de
compilación requiere `--configuration Release`. Se comprobó en la ayuda de la herramienta
local 10.0.12 y en https://learn.microsoft.com/en-us/ef/core/cli/dotnet.

Se corrigió únicamente ese argumento del paso Migraciones, conservando --no-build y la
compilación Release del job. El comando corregido encontró las migraciones sin conexión
y aplicó las 14 a un PostgreSQL temporal desde cero, terminando en Done; se confirmó el
conteo en __EFMigrationsHistory y se eliminó el contenedor temporal. No se ejecutó la
suite E2E completa ni se modificaron sus pruebas; falta validar su corrida en GitHub.
Sin cambios en la base de desarrollo, commit ni push.

### 14/11/2026: backend de recepción en depósito con discrepancias

Tras confirmar ambos jobs de CI aprobados, el usuario autorizó retomar el primer pendiente:
recepción en depósito, requisito 10 de la letra original (5.2). Se contrastó el texto de
Laboratorio__NET_2026.pdf: escaneo, verificación de lo declarado y registro de discrepancias.
Se conservó la regla del dominio que exige recibir todos los bultos antes del ingreso.

Se agregó RecepcionDepositoHandler en Application, contrato de unidad de operación,
repositorio EF en Infrastructure y cuatro endpoints en Web: consulta de manifiesto,
escaneo de código, discrepancia y confirmación. El contexto usa identidad autenticada,
permisos de administrador/despachador/operario, lectura coherente y versión del envío;
los POST exigen antiforgery. Se revalida usuario activo/rol contra la base. Escaneo y
discrepancia idénticos se reconocen mientras la recepción permanezca abierta, sin otra
escritura; no se añadió una clave idempotente global.

El operario contrasta medidas/peso declarados devueltos en la consulta e informa valores
declarado/recibido en la discrepancia; no se automatizan balanza o tolerancias, ni se
sobrescriben medidas o tarifa. Un código sobrante no crea un bulto. Anotar un faltante
no sustituye el escaneo y no permite confirmar un manifiesto incompleto. La confirmación
reutiliza Domain, cambia los estados y guarda evento/outbox en una única transacción.
No hay cambios de esquema ni migración. No se implementaron pantallas, cámara, offline,
asignación regular o cierre/rendición de rutas, que quedan fuera de este avance.

Durante la preparación de pruebas se corrigió el uso de un atributo Theory inexistente
y se usaron sentencias SQL fijas para las restricciones temporales, respetando el analizador
de seguridad. Compilación Release con advertencias como errores aprobada. Pasaron siete
pruebas unitarias nuevas y ocho integraciones nuevas con PostgreSQL temporal: API/CSRF,
aislamiento, identidad, conservación de declaración, reenvíos, concurrencia y rollback
de escaneos, discrepancias y confirmación/outbox. La regresión pasó 402 casos unitarios/
arquitectura y 12 integraciones anteriores (carga, despacho y flujo crítico), sin omisiones
en los casos seleccionados. No se ejecutó la suite completa de integración ni E2E.

Cobertura Domain 85,97 % (1213/1411 líneas) y Application 80,06 % (534/667).
Se documentó el contrato en docs/recepcion-deposito.md y se enlazó desde README,
los límites de carga/despacho y el seguimiento de pendientes. Se eliminó el PostgreSQL
temporal; no se alteró la base de desarrollo. Sin commit, push ni despliegue.

### 14/11/2026: asignación regular de envíos a rutas

El usuario autorizó el segundo pendiente del backend, asignación regular de la letra
original (5.3, requisitos 13–16), conservando el avance anterior de recepción sin commit.
Se agregó AsignarEnvioRutaHandler y su puerto de unidad de operación en Application;
el repositorio en Infrastructure carga envío/ruta con lectura coherente y versiones.
Web expone consultas de envíos preparados, rutas abiertas con ocupación/capacidad,
recursos del operador y POST de asignación. Se reutiliza el POST existente de creación
de rutas, conservando su contrato para no romper a los consumidores de pendientes.

La identidad se obtiene de la sesión, se revalida contra la base y solo administrador/
despachador pueden planificar. POST exige antiforgery. Domain valida estado, ventana,
capacidad por composición, máximo de paradas y orden manual único; se conservan tarifa
y programación. Reprogramados preparados se reasignan sin otra reprogramación y las
entregas parciales siguen usando su circuito. Una transacción de SaveChanges guarda
parada, composición, estados, evento y outbox; los conflictos no dejan cambios parciales.

La prueba de carrera con dos envíos y el mismo número de parada reveló que la base ya
rechazaba el duplicado, pero el índice IX_Paradas_RutaId_Cod no se traducía al contrato
de concurrencia. Se añadió ese índice a la traducción existente en AppDbContext, sin
cambiar restricciones ni esquema. El caso ahora genera ConflictoConcurrenciaException
y la API lo presenta como 409. También se corrigió una expectativa de prueba que suponía
flota ajena vacía: el escenario persiste flota de ambos operadores; se comprobó que cada
uno reciba sus propios identificadores, sin mezclarlos. No se debilitó el aislamiento.

Compilación Release estricta aprobada. Pasaron 15 pruebas unitarias nuevas y las diez
integraciones nuevas contra PostgreSQL temporal tras las correcciones, incluida la
repetición aprobada de permisos. Verifican flujo recepción/asignación/carga/despacho/hoja,
roles, CSRF, versiones, capacidades, ventanas, conservación de tarifa/programación,
reprogramados, rechazo de parciales, rollback de outbox y carreras por envío, ruta y orden.
La regresión final pasó 417 casos unitarios/arquitectura y 24 integraciones anteriores
(recepción, carga, despacho, parciales, flujo crítico y restricciones), sin omisiones en
la selección. No se ejecutó toda la integración ni E2E en esta etapa.

Cobertura Domain 85,97 % (1213/1411) y Application 79,57 % (557/700). No requiere migración;
no se aplicó nada a la base de desarrollo. Se documentó el contrato en
docs/asignacion-regular.md y se actualizaron README, enlaces y seguimiento de pendientes.
Sin cambios de pantallas, PWA o Worker. El cierre de devoluciones/rendición sigue pendiente.
Se eliminó el PostgreSQL temporal de esta etapa. Sin commit, push ni despliegue.

### 14/11/2026: devolución y rendición operativa en depósito

El usuario autorizó el tercer pendiente, cierre de devoluciones y rendición del requisito
22 de la letra original. Se conservaron sin commit los avances anteriores de recepción y
asignación. Se añadieron dos casos de uso de Application y sus puertos, un repositorio
de Infrastructure con unidades dedicadas y ocho endpoints en Web: consulta/listado,
inicio, escaneo y cierre de devolución, y listado/consulta/rendición de rutas.

Los roles administrador, despachador y operario de depósito se resuelven desde sesión
y se revalidan activos contra la base; POST exige antiforgery. El inicio reutiliza las
políticas del envío. Escaneo y cierre respetan la composición de la devolución, incluyendo
parciales sin alterar entregados. Un reenvío de escaneo ya confirmado se reconoce mientras
la devolución siga abierta. El cierre guarda estado, fecha, evento y outbox juntos.
La rendición reutiliza Ruta.Rendir: ruta despachada y sin paradas activas. No completa
devoluciones automáticamente ni agrega cobros, liquidaciones o cierre diario del Worker.
La ruta y su rendición se guardan juntas; rendir no genera un evento de envío artificial.

Las pruebas de dos inicios de devolución y dos rendiciones simultáneos mostraron que los
índices existentes impedían duplicados antes del UPDATE de versión, pero faltaba traducir
esa excepción al contrato de concurrencia. Se añadieron los nombres conocidos de los
índices de Devoluciones/Envio y Rendiciones/Ruta (simples y con TenantId) a la traducción
de AppDbContext, sin cambiar esquema. Las dos pruebas detectaron primero un fallo y
pasaron después del ajuste, con un solo registro/evento y conflicto para el perdedor.
Durante la preparación se corrigió un nombre de enum oculto por una propiedad de la
fixture y una aserción conforme al analizador xUnit; sin cambios de reglas del dominio.

Compilación Release con advertencias como errores aprobada. Pasaron diez pruebas unitarias
nuevas y trece integraciones nuevas contra PostgreSQL temporal, tras repetir aprobadas
las dos carreras corregidas. Verifican API, roles/CSRF/aislamiento, usuario revocado,
políticas de reintento, devolución parcial/outbox V2, faltantes, reenvíos, cierres y carreras,
rollback de inicio/escaneo/outbox/rendición y la independencia entre rendir y completar
una devolución. La regresión final pasó 427 casos unitarios/arquitectura y 35 integraciones
anteriores sin omisiones en la selección. No se ejecutó toda la integración ni E2E.

Cobertura Domain 85,97 % (1213/1411) y Application 81,30 % (648/797). Sin migración ni
cambios en la base de desarrollo. Se agregó docs/cierre-deposito.md y se actualizaron
README, límites de las otras APIs y el seguimiento de pendientes. Las pantallas y acciones
de PWA continúan a cargo de sus responsables; no se modificaron Web UI, PWA o Worker.
Se eliminó el contenedor PostgreSQL temporal. Sin commit, push ni despliegue.
