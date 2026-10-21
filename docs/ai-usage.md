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
