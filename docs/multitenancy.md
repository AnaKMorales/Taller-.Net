# Multitenancy por fila y cuenta comercial

## Decisión del equipo

Se implementa la estrategia de la sección 11 y el ADR-002 del documento inicial:
una base PostgreSQL, un esquema compartido y aislamiento lógico por fila. El operador
es el tenant principal y CuentaComercial es el segundo ámbito para los usuarios de comercio.
No se creó una base ni un esquema por operador.

## Contexto de cada operación

Application define TenantContext, un valor inmutable con:

- OperadorId obligatorio.
- UsuarioId y Rol para usuarios autenticados.
- CuentaComercialId obligatorio para el perfil Comercio; ausente en perfiles internos.
- Origen: usuario autenticado, mensaje Worker, seguimiento público o proceso interno.
- EnvioPublicoId para restringir el seguimiento a un solo envío.

ITenantActual entrega ese valor a AppDbContext. TenantActual es su implementación fija.
El contexto lo captura una vez al construirse; cambiar el proveedor no modifica el ámbito
de un DbContext existente. Un usuario con varias cuentas trabaja con una sola cuenta activa
por contexto. No se utiliza estado global ni AsyncLocal compartido entre mensajes.

```csharp
var scope = await resolver.DesdeClaimsAsync(principalAutenticado);
await using var db = new AppDbContext(options, new TenantActual(scope));
```

Para bootstrap o trabajos internos previamente autorizados se dispone de
TenantActual.ParaOperador(id). Esto crea un contexto de proceso interno, sin cuenta activa;
no debe utilizarse como atajo para solicitudes de usuarios de comercio.
Sin contexto las lecturas devuelven cero registros y SaveChanges lanza
AislamientoOperadorException. La fábrica de diseño sigue funcionando para migraciones.

## Resolución desde identidad

TenantResolver.DesdeClaimsAsync recibe un ClaimsPrincipal previamente autenticado por
ASP.NET. Exige una sola identidad autenticada y claims no contradictorios:

| Claim | Significado |
| --- | --- |
| operador_id | Operador de la identidad. |
| NameIdentifier o sub | Id del Usuario de Domain. Si están ambos, deben coincidir. |
| Role o role | Uno de los perfiles de RolUsuario. |
| cuenta_comercial_id | Cuenta activa para el perfil Comercio. |
| comercio_id | Alternativa del ADR: identidad global del comercio, traducida a su cuenta del operador. |

El resolvedor comprueba en PostgreSQL que el usuario exista en ese operador, esté activo,
que el rol coincida con el persistido y que pertenezca a la cuenta indicada. Si se indican
cuenta y comercio, ambos deben corresponder. Los perfiles internos no aceptan una cuenta
o comercio activo ambiguos. Un usuario de comercio debe indicar una selección válida.
Las identidades anónimas, IDs vacíos/malformados, roles inventados y claims contradictorios
se rechazan. La pertenencia a la cuenta y el estado del usuario se vuelven a comprobar al guardar.

Este servicio no verifica firmas JWT ni crea cookies. Web debe autenticar primero con
Identity/cookies/JWT, resolver el contexto y registrar ITenantActual y AppDbContext por petición.
No recibir un TenantContext del body ni decidir el operador con un header arbitrario.
No utilizar AddDbContextPool: el constructor captura el ámbito y el reciclado de instancias
no vuelve a ejecutar ese constructor para la siguiente identidad.

## Resolución del Worker

TenantMessageScope transporta OperadorId y CuentaComercialId cuando corresponda.
DesdeMensajeAsync comprueba que el operador exista y que la cuenta sea de ese operador,
y devuelve un contexto nuevo por mensaje. Se probó el procesamiento concurrente de mensajes
A, B y nuevamente A con DbContext independientes, sin contaminación entre ellos.

El consumidor debe autenticar primero el origen del mensaje mediante el transporte o su
mecanismo de firma; este resolvedor valida el ámbito de los datos, no la autenticidad de RabbitMQ.
La conexión del consumidor RabbitMQ pertenece al Worker y todavía no existe en esta solución.

## Filtros de lectura

TenantModel agrega el filtro nombrado AislamientoOperador a todas las raíces del modelo.
Las 30 tablas privadas almacenan una propiedad shadow TenantId, equivalente al operador
propietario, sin dependencias de EF en Domain. Operador se filtra por su Id; Comercio por
una CuentaComercial del operador. Repartidor hereda los filtros de Usuario.

CuentaModel agrega AislamientoCuenta, sin agregar otra columna de propietario redundante:

- CuentaComercial, Envio y SuscripcionAviso se filtran directamente por la cuenta activa.
- Bultos, destinatarios, direcciones, eventos, intentos, pruebas, paradas, escaneos, discrepancias,
  devoluciones, entregas de avisos y notificaciones se filtran siguiendo sus relaciones hasta esa cuenta.
- ProgramacionEntrega permite ver la programación actual y las instantáneas históricas de sus envíos.
- Un usuario de comercio ve su propio Usuario y asociación de cuenta. No ve usuarios internos,
  rutas agregadas, vehículos ni rendiciones que puedan contener información de otros comercios.
- Los catálogos compartidos del operador (zonas, franjas, versiones de reglas/tarifas) se pueden
  leer desde una cuenta para preparar un envío; no se habilita su edición por ese motivo.
- Un perfil interno conserva el alcance del operador. Sus permisos concretos por funcionalidad
  deben verificarse también en los casos de uso/API.

Los filtros cubren consultas directas, búsqueda por ID, proyecciones e Includes. Cuando una
navegación requerida apunta a datos internos no visibles, incluirla puede quitar resultados:
los DTO de comercio deben seleccionar sus campos necesarios y no cargar un grafo interno
completo con usuarios responsables, rutas o vehículos. Domain no usa carga diferida.

## Escrituras e integridad

AppDbContext registra automáticamente TenantSaveChangesInterceptor, sin estado de petición.
El interceptor se ejecuta tanto en SaveChanges como en SaveChangesAsync y llama a:

1. TenantWriteGuard: exige operador, estampa las altas, contrasta propietarios persistidos
   y valida todas las referencias, incluso de objetos desconectados con tenant falsificado.
2. CuentaWriteGuard: en contextos de cuenta, verifica la cuenta/identidad, el propietario original
   y el propuesto y las referencias de cada alta, modificación o baja.

Un comercio no puede mover un envío a otra cuenta, adjuntarle un destinatario/programación
ajenos, crear bultos para el envío de otro comercio, editar catálogos o modificar membresías.
Puede crear sus propios envíos y suscripciones. El alta puede marcar EnUso en las versiones
utilizadas, exclusivamente como efecto de crear un envío propio; no autoriza otros cambios
en esas versiones. Las instantáneas existentes no se modifican desde una cuenta.
Los contextos de seguimiento público rechazan cualquier guardado.

Las lecturas de validación internas contrastan las filas reales y pueden omitir filtros
para ese fin específico; no devuelven esas entidades al cliente. Se utiliza una caché por
guardado para evitar repetir búsquedas. Los contextos se descartan tras un fallo de validación.

PostgreSQL respalda el primer nivel con FK compuestas (Id relacionado, TenantId), claves
alternativas e índices. En tablas con OperadorId se exige su coincidencia con TenantId.
El segundo nivel se valida mediante filtros y el interceptor; no se configuró RLS ni nuevas
FK de cuenta en todas las tablas. La concurrencia optimista de negocio sigue pendiente.

## Comercio compartido

Comercio mantiene sus datos comunes. Puede tener cuentas con varios operadores; cada
usuario ve solo las cuentas permitidas por su contexto. Un operador puede crear un comercio
junto a una cuenta propia, pero no modificar/eliminar ese registro global desde este contexto.
El proceso de mantenimiento de datos comunes y la provisión de cuentas necesita sus casos
administrativos específicos; el contexto no incorpora un bypass automático.

## Seguimiento público

ISeguimientoPublico.ConsultarAsync recibe únicamente el token. Se reutiliza CodigoSeguimiento,
un GUID aleatorio independiente del Id del envío. Un token malformado o desconocido devuelve null.
El acceso inicial sin filtros es un lookup exacto por token, limitado a obtener el ámbito internamente.
Luego se crea un contexto público de operador/cuenta/envío y se consulta un único envío.

El resultado es SeguimientoPublicoDto: estado, fecha comprometida y ventana UTC. No contiene
IDs internos, usuarios, cuenta, correo, teléfono, dirección, evidencia ni secretos. El contexto
público no permite leer otras tablas privadas ni guardar. La interfaz HTTP, rate limiting,
SignalR y la ampliación del DTO/timeline pertenecen al portal de seguimiento de Web.

## Caché

TenantCacheKey.Crear genera claves con operador, cuenta cuando aplica, origen, rol, usuario
y envío público. Prefijo agrupa la invalidación de una cuenta; PrefijoOperador abarca todas
las cuentas y perfiles de ese operador. Los componentes
variables se escapan para evitar colisiones. Se probó la separación entre operadores, entre
cuentas del mismo operador, usuarios, perfiles y tokens de envíos distintos.
El helper no conecta a Redis ni implementa su invalidación: los adaptadores de caché deben
usar estas claves tanto para leer como para escribir y acotar su invalidación al mismo ámbito.

## Migraciones y pruebas

OperatorIsolation conserva los datos anteriores asignando el operador por sus relaciones.
Se probó pasar del esquema inicial al nuevo con un grafo completo existente y comercio compartido.
Los filtros de cuenta, el interceptor y los resolvedores no cambian el esquema; no requieren
una tercera migración. La propiedad shadow TenantId conserva su nombre interno, manteniendo
OperadorId como identificador de negocio. No se modifica InitialCreate.

Las pruebas de integración cubren dos operadores y dos comercios de un mismo operador,
acceso por ID conocido, datos secundarios, programación histórica, altas propias, rechazo de
cuenta ajena, claims inválidos, usuarios inactivos, membresía revocada, usuario con varias
cuentas, mensajes Worker independientes, modificación/baja dentro de su cuenta y DTO público.

Verificación del 17/10/2026: 58 pruebas de Domain, 22 unitarias de Infrastructure y 26 de
integración contra PostgreSQL, todas satisfactorias (106 en total).

```powershell
./scripts/Start-LocalPostgres.ps1
./scripts/Test-LocalPersistence.ps1
dotnet test UltimaMilla.slnx
```

## Límites de la protección

Los filtros son de EF; una conexión SQL de desarrollo puede ver toda la base. IgnoreQueryFilters,
SQL directo y operaciones masivas son mecanismos privilegiados, no entradas para clientes.
ExecuteUpdate/ExecuteDelete no ejecutan el interceptor de SaveChanges. Las FK impiden relaciones
entre operadores, pero no son una autorización general de SQL ni una política RLS.
Las reglas de permisos de API, login, transporte RabbitMQ y adaptador Redis deben integrarse
con estas piezas en sus proyectos. No se presentan como implementados esos servicios externos.

Referencia de diseño: sección 11 y ADR-002 de NET - Grupo 5.docx.
Referencia técnica: [filtros globales de EF Core](https://learn.microsoft.com/en-us/ef/core/querying/filters).
