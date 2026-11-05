# Caso de uso de registro de intentos

Implementado el 03/11/2026. Conecta el núcleo de resultados por bulto con Application
y la persistencia. Aplica al flujo original y al requisito 32 del documento nuevo.
No agrega endpoints, pantallas, PWA ni Worker. El caso de uso no requiere otra migración;
la composición de visitas de pendientes requiere BultosPorParada, documentada por separado.

## Entrada y respuesta

RegistrarIntentoHandler recibe RegistrarIntentoCommand, con:

| Dato | Significado |
| --- | --- |
| EnvioId y ParadaId | Envío y visita de origen del trabajo. |
| VersionEsperada | RowVersion del envío obtenida al consultar/preparar la visita; Guid no vacío. |
| IdOperacion | Identificador estable de la operación, conservado al reenviar. |
| OcurridoEn y Posicion | Instante de captura UTC y posición opcional del evento. |
| Bultos | Resultado por bulto, Id del motivo y referencias a evidencias de esta solicitud. |
| Evidencias | Definiciones con referencia local, tipo, hora UTC, posición y archivo/texto. |

No recibe operador, usuario ni rol. Las referencias locales de evidencia son GUID creados
por el cliente para relacionar una definición con varios bultos; no son Id persistidos.
No se permiten referencias desconocidas, duplicadas por bulto ni evidencias sin utilizar.
Application construye una sola PruebaEntrega por definición y resuelve los motivos dentro
del catálogo de la versión de reglas referenciada por el envío. Domain conserva las
validaciones de permisos parciales, resultados, visita, motivos y evidencia exigida.

La respuesta IntentoRegistradoDto incluye Id del intento/operación, resultado histórico,
resultados por bulto, estado del envío y versión del envío en la operación, e indica Repetido.
EstadoActualEnvio y VersionActualEnvio son el resumen y token de la lectura/guardado actual;
en un reenvío posterior pueden reflejar avances posteriores al intento original.
La respuesta no devuelve entidades EF, datos del destinatario ni archivos.

## Identidad y carga

El host debe autenticar la cookie/JWT y resolver sus claims con ITenantResolver antes
de entregar ITenantActual. Este caso de uso no verifica firmas ni implementa el login.
RegistroIntentoRepository exige un contexto UsuarioAutenticado con rol Repartidor y
contrasta el Id, rol y estado activo con el registro real de ese operador.

Solo carga un envío si está asignado al repartidor o si contiene la operación solicitada
en una visita anterior de ese repartidor. Operador ajeno, envío inexistente y asignación
ajena se rechazan con AislamientoOperadorException. El núcleo vuelve a comprobar la parada
y el responsable, incluyendo la identidad en la huella del contenido al reconocer un reenvío.
El estado activo y rol se consultan nuevamente antes de guardar.

Cada ejecución abre un AppDbContext dedicado, con el tenant capturado. Carga bultos,
configuración, catálogo, intentos, detalles, evidencias, parada/ruta, repartidores e historial.
Usa AsSplitQuery dentro de una transacción de lectura RepeatableRead para que las consultas
observen un mismo estado del grafo. Esa transacción se confirma y termina antes de invocar
Domain. La transacción de escritura es la existente de SaveChanges.

## Versiones, repetición y guardado

Si el identificador de operación es nuevo, VersionEsperada debe coincidir con la versión
cargada. Una diferencia genera ConflictoConcurrenciaException antes de modificar Domain.
Si coincide, se registra por bulto y un único SaveChangesAsync guarda estados, intento,
detalles, pruebas, evento y outbox V2 con las garantías transaccionales existentes.
La versión original de EF sigue controlando carreras ocurridas después de la carga,
tanto en el envío como en la ruta afectada al finalizar la parada.

Si la operación ya existe, Domain debe reconocer su contenido exacto antes de devolverla.
Ese reenvío puede llevar la versión original, ahora vieja: no se vuelve a guardar ni a
renovar el token. El mismo identificador con otro contenido se rechaza.
No se hace un reintento automático de decisiones concurrentes ni se fusionan resultados.
Después de un conflicto se consulta nuevamente o se reenvía la misma operación para
reconocerla, según corresponda.

La unidad se dispone siempre, también al rechazar una versión, fallar la validación o el
guardado. Un nuevo intento del caso de uso usa otro contexto, sin cambios fallidos pendientes.
La cancelación se propaga a carga y persistencia; no se informa éxito si falla el guardado.

## Registro e integración pendiente

El registro optativo en el host es:

```csharp
services.AgregarRegistroIntentos();
```

Requiere DbContextOptions<AppDbContext> e ITenantActual con la identidad resuelta. Se registran
el handler y su repositorio. Program.cs todavía no registra este servicio ni expone
un endpoint de intentos; sí incorpora por separado la descarga de hojas y la configuración.
La API futura debe autenticar, resolver tenant y traducir errores de solicitud/permisos/
concurrencia sin aceptar identidad del body. La PWA deberá conservar versión, parada e
IdOperacion y mapear sus capturas al contrato. Archivo es una referencia a evidencia ya
almacenada: este caso de uso no recibe ni sube bytes de archivos.

El repositorio ya carga Parada.Bultos.Bulto y admite el registro de la visita de pendientes
con o sin recargo. La solicitud contiene solo esos bultos; los entregados antes no se repiten.
La carga incluye el recargo de la parada y los históricos del envío. Continúan pendientes las
API de asignación. La [descarga autenticada de hoja](api-monitoreos.md) ya incluye su
composición y versión esperada. Ver [políticas de pendientes](politicas-pendientes.md).
Esta implementación no sustituye la sincronización offline ni la demo del 12/11.
