# Gestión de versiones de reglas y tarifas

## Contrato disponible para las interfaces

Application define `IConfiguracionVersionada`. Infrastructure lo implementa mediante
`ConfiguracionVersionada`, reutilizando las entidades y validaciones de Domain y los
mapeos actuales de EF. No se agregan pantallas, endpoints, tablas ni migraciones.

Operaciones disponibles:

- ListarZonasAsync: zonas del operador para poblar el selector de líneas tarifarias.
- ListarReglasAsync y ListarTarifariosAsync: historial ordenado por número de versión.
- ObtenerReglasAsync y ObtenerTarifarioAsync: detalle de una versión, incluyendo sus hijos.
- ReglasVigentesAsync y TarifarioVigenteAsync: versión con mayor VigenteDesde no posterior
  al reloj del servidor; en caso de empate, mayor Version. Si no existe, devuelve null.
- CrearReglasAsync y CrearTarifarioAsync: guardar una nueva versión completa.

La creación no modifica ni copia automáticamente los objetos existentes. Para editar desde
una pantalla, obtener el detalle, modificar los datos del formulario y enviar el documento
completo como una versión nueva. No hay borradores editables, eliminación o cancelación de
versiones programadas en esta etapa. Los IDs de hijos nuevos se generan en Domain.

## Permisos y aislamiento

Las operaciones son administrativas: exigen TenantContext de usuario autenticado con rol
Administrador y verifican en la base que ese usuario siga activo y pertenezca al operador.
No reciben OperadorId ni UsuarioId desde el formulario. Los IDs de versiones ajenas devuelven
null en las consultas; una zona ajena al crear tarifas produce AislamientoOperadorException.
Los perfiles Comercio, Despachador, OperarioDeposito, Worker y procesos internos no pueden
usar este contrato administrativo. La autenticación de la cookie sigue a cargo del host.

Se abre un contexto separado por operación para evitar guardar cambios de otros componentes
accidentalmente. Las consultas son sin seguimiento y conservan los filtros de tenancy.

## Creación y conflictos

NuevaVersionReglas recibe DatosReglasDto: intentos, plazo entre intentos, tipos de prueba,
política de devolución, plazos por modalidad y catálogo de motivos. Se exige al menos una
prueba, un plazo y un motivo activo, plazos sin modalidades repetidas y nombres de motivos
sin duplicados. El resto de las validaciones se ejecuta en los constructores de Domain.

NuevaVersionTarifario recibe las líneas y sus ajustes porcentuales mediante LineaTarifaDto,
el mismo contrato de datos ya usado por la lectura/caché. Se comprueban zonas del operador,
rangos, modalidades, importes y ajustes. La combinación de porcentajes no puede dar un
precio negativo. Se mantienen los límites de volumen del modelo descritos en tarifas.md.

Ambas solicitudes incluyen UltimaVersionEsperada: la última versión creada que vio el
administrador, incluyendo las futuras. Usar cero cuando todavía no hay ninguna. No se usa
el número de la versión vigente si ya existe una versión futura más reciente.

La creación abre una transacción y bloquea la fila del operador con FOR UPDATE antes de
consultar la última versión. Dos creaciones simultáneas del mismo operador se serializan.
Si la versión esperada dejó de coincidir, se produce ConflictoConcurrenciaException y la
interfaz debe recargar y revisar. No se sobreescribe el cambio ajeno ni se reintenta automáticamente.
El número nuevo se asigna en el servidor; los índices únicos existentes permanecen como respaldo.

VigenteDesde nulo significa la hora UTC del servidor al crear. Una fecha explícita debe ser
UTC y no estar en el pasado. Las versiones futuras se conservan y entran en la selección por
fecha sin una tarea de activación. Esto no programa notificaciones ni implementa un scheduler.

## Conservación del histórico

ConfiguracionWriteGuard se ejecuta desde el interceptor de SaveChanges tanto en guardados
síncronos como asíncronos. Las versiones completas persistidas y sus líneas, ajustes, motivos,
plazos y políticas quedan inmutables, incluso antes de EnUso: se crea otra versión para cambiar.
Solo permite marcar EnUso de false a true como parte del alta de un envío que la referencia.
Los hijos nuevos solo pueden guardarse junto con una nueva versión completa.

Este control es de EF; SQL directo, ExecuteUpdate y ExecuteDelete pueden evitarlo y requieren
sus propios controles. No modifica tarifas ni reglas de envíos históricos y no congela otros
catálogos como ZonaCobertura o Vehiculo. No implementa protección general de EventoDeEnvio.

## Caché y resultado del guardado

Después de confirmar un tarifario nuevo, se llama al contrato existente IInvalidacionTarifario
para su operador. Esto también ocurre al crear versiones futuras para actualizar la próxima
vigencia conocida por la caché. Las reglas operativas no tienen caché propia en este flujo.

Si el contrato lanza un error, se registra y se devuelve VersionCreadaDto con
RevisarInvalidacionCache = true. La versión ya está confirmada: no debe reenviarse la creación
como si hubiera fallado. Si devuelve false, significa que el contrato terminó sin excepción;
no certifica una eliminación física en Redis, cuyo adaptador ya tiene tolerancia a fallos.
No hay transacción distribuida PostgreSQL/Redis ni reintento persistente de invalidación.

No se generan mensajes outbox de cambios de configuración: el outbox actual captura eventos
de envíos. Su integración con eventos adicionales queda fuera de esta etapa.

## Registro e integración pendientes

En la composición del host se puede usar:

```csharp
services.AgregarConfiguracionVersionada();
```

El host debe tener DbContextOptions<AppDbContext>, ITenantActual, logging e
IInvalidacionTarifario registrados. El registro es optativo y no se modificó Program.cs.
Las pantallas futuras deben usar una política exclusiva de Administrador, no la política
Backoffice compartida con despachadores y depósito. La verificación del backend es adicional.

Los casos de uso de alta aún deben seleccionar sus versiones vigentes y calcular sus fechas
y tarifa en el servidor: no se modificó CrearEnvioHandler ni la programación de entrega.
El circuito Blazor y sus formularios siguen requiriendo integración del equipo.

## Entrega parcial: política versionada (requisito 33)

DatosReglasDto y ReglasOperativas incorporan PermiteEntregaParcial, AccionPendientes
e ImporteRecargoNuevaVisita (decimal nullable).
La acción admite NuevaVisitaSinRecargo (0), NuevaVisitaConRecargo (1) y DevolverAlComercio (2).
Los nuevos argumentos son opcionales y sus valores predeterminados son false y 0.
Una acción puede quedar preconfigurada aunque el permiso esté desactivado; solo se aplicará
cuando el flujo por bulto produzca una entrega parcial. No es una bandera de funcionalidad.
NuevaVisitaConRecargo requiere un importe fijo positivo con hasta dos decimales; las otras
acciones requieren null. El recargo se devenga una vez al despachar la nueva visita,
separado de la tarifa original. No representa un pago realizado.

Por ejemplo, partiendo del detalle devuelto por ObtenerReglasAsync:

```csharp
var datos = detalle.Datos with
{
    PermiteEntregaParcial = true,
    AccionPendientes = AccionBultosPendientes.DevolverAlComercio,
    ImporteRecargoNuevaVisita = null
};
await gestion.CrearReglasAsync(new(ultimaVersionCreada, null, datos));
```

La versión esperada sigue siendo la última creada, incluyendo futuras, no necesariamente
la del detalle consultado. La creación exige administrador activo del operador.
Los formularios deben conservar estos campos al crear versiones sucesivas: omitirlos
produce false/0, no hereda la configuración de otra versión. No se agregó un formulario.

La migración 20261101185636_ReglasEntregaParcial completa las filas existentes con false/0
y una restricción CHECK rechaza acciones fuera de 0, 1 y 2. No agrega tablas ni cambia
referencias históricas. La inmutabilidad de versiones ya persistidas cubre estos campos.

RegistrarIntento mantiene la firma tradicional para envíos completos. RegistrarIntentoPorBulto
respeta el permiso parcial; el núcleo aplica nueva visita con/sin recargo y devolución
de pendientes. La migración PoliticasPendientesYRecargoFijo incorpora importes y composición
de devolución. No inventa importes para versiones previas: una política de recargo antigua
sin importe se puede leer, pero bloquea nuevas visitas. Ver [detalles y límites](politicas-pendientes.md).
Continúa pendiente integrar formularios y las aplicaciones de sus responsables.
