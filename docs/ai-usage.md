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
