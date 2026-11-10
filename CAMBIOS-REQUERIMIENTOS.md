# Seguimiento del cambio de requerimientos

Actualizado: 09/11/2026. Equipo de tres integrantes, Grupo 8.

## Alcance y fuentes

Este resumen acompaña a la letra original y a **Cambios de Requerimientos
Planificados.pdf**, presentado el 30/10/2026. No sustituye esos documentos:
la letra original sigue vigente y el segundo documento prevalece donde la modifica.
Las asignaciones de trabajo siguen el reparto del equipo, no las establece el PDF nuevo.

Para equipos de tres corresponde incorporar **entrega parcial de bultos** (31–36).
El retiro en domicilio (37–41) es para equipos de cuatro y queda fuera de nuestro alcance.
La entrega parcial debe demostrarse el **12/11**, sin conexión y luego sincronizada,
comprobando backoffice, seguimiento público y aviso recibido por el comercio.
También se exige un ADR que explique los componentes modificados y las dificultades.

## Qué cambia y qué está pendiente

| Requisito | Cambio necesario | Estado |
| --- | --- | --- |
| 31 | Estado por bulto, estado del envío derivado y entrega parcial; transiciones declarativas en ambos niveles | Implementado en el servidor. Pendiente comprobar el recorrido completo con todas las aplicaciones |
| 32 | Escanear bultos entregados, registrar motivo para los no entregados y asociar evidencias a los entregados; operación offline y conflictos por bulto | La PWA registra por bulto, conserva la visita offline y sincroniza. El servidor devuelve el diagnóstico por bulto. Falta presentarlo en la PWA |
| 33 | Reglas versionadas para permitir parciales y decidir nueva visita con/sin recargo o devolución al comercio | Formulario y backend versionados. La API de despacho puede iniciar la devolución de pendientes. El recargo se devenga al despachar y se conserva la tarifa original |
| 34 | Asignar bultos pendientes a una nueva hoja de ruta | API para crear ruta, programar y asignar solo los pendientes. La hoja del repartidor ya devuelve la composición de la visita despachada. Falta conectar la operación en backoffice |
| 35 | Seguimiento con cantidades entregadas y totales, sin identificadores internos | API y pantalla pública muestran el avance. Falta validarlo dentro del ensayo completo |
| 36 | Eventos de bulto y avisos versionados compatibles con receptores anteriores | El outbox publica V1 y V2. El Worker consume ambos y envía V1 a los comercios actuales. Falta ofrecer una salida V2 con detalle por bulto y comprobar su recepción |

La infraestructura de EF Core, aislamiento, concurrencia y outbox se reutilizará,
pero se debe extender y verificar para los nuevos registros. Tener esa base no
significa que la entrega parcial ya esté implementada.

## Reparto de integración

- **Agustín:** modelo y reglas de dominio; intentos y evidencias por bulto;
  relaciones de rutas y devolución; configuración versionada; EF Core y migraciones;
  aislamiento y concurrencia; contratos de eventos y registro transaccional en outbox;
  pruebas del núcleo e integración y documentación de sus decisiones.
- **Ana:** pantallas y formularios de configuración, backoffice, portal,
  seguimiento público, tiempo real y adaptación de caché.
- **Aldana:** PWA, escaneo y captura, almacenamiento offline y sincronización;
  Worker, transporte, avisos, reintentos, fallidos y adaptación de entrega de webhooks.

Los contratos y la política de conflictos deben acordarse entre las partes.
Este reparto no autoriza implementar las tareas de otra integrante.

## Orden de trabajo

1. **Revisar pendientes anteriores y preparar el 05/11.** Verificar el flujo crítico
   del núcleo con PostgreSQL real; revisar la API de hoja de ruta y las integraciones
   pendientes del backend de configuración. Despliegue por IaC y observabilidad
   técnica siguen siendo objetivos del monitoreo, a cargo de sus responsables.
2. **Definir el cambio antes de modificar contratos.** Acordar estados y transiciones,
   cálculo del estado conjunto, resultados y evidencias por bulto, política de pendientes,
   recargos y casos mixtos (por ejemplo, entregados y devueltos).
3. **Implementar el núcleo de entrega parcial por etapas.** Modelo y reglas, persistencia
   y migración, asignación de pendientes y concurrencia. Conservar y probar los flujos actuales.
4. **Publicar contratos compatibles.** Definir entrada de intentos y eventos nuevos;
   preservar el contrato V1 para receptores existentes. Coordinar las adaptaciones
   de Web, PWA y Worker en paralelo, sin esperar al último día.
5. **Integrar para el 12/11.** Probar el escenario offline completo, documentar el ADR
   y revisar regresiones e aislamiento. La prueba del núcleo no sustituye esta demostración.

Las fases 1 y 2 pueden avanzar en paralelo. No dejar todo el diseño del cambio para después del 05/11.

## Decisiones del núcleo y coordinación pendiente

- Estados y resumen mixto: implementados; ver avances y ADR-007.
- Resultado/evidencia por bulto y selección/capacidad por parada: implementados en el núcleo.
- Recargo fijo configurable por visita, capturado al asignar y registrado al despachar:
  implementado; no es un cobro efectivo. Devolución limitada a pendientes implementada.
- Históricos: se conservan sin inventar resultados, evidencias, precios o cargos anteriores.
- Pendientes de coordinación: acciones de despacho en backoffice, presentación del
  conflicto por bulto en la PWA y salida V2 para receptores compatibles.

El ADR sigue en curso hasta integrar y demostrar el recorrido completo.

## Aclaraciones adicionales del PDF

- **Caché distribuida obligatoria**, también para equipos de tres. Redis está disponible
  para tarifario y seguimiento. El seguimiento se invalida tras confirmar los cambios.
  El tablero separa tarifario y seguimiento y muestra los aciertos, fallos y tasa de aciertos
  de cada caché. En desarrollo puede usarse el fallback en memoria.
- Las **banderas de funcionalidad son opcionales** y distintas de las reglas operativas.
  Si se realizan, requieren al menos dos funcionalidades reales, administración de
  plataforma, activación por operador, propagación acotada, auditoría y aplicación
  en todas las partes involucradas. No se agregan como prioridad frente a lo obligatorio.

## Registro de avances

- **01/11:** creado este seguimiento y orden de trabajo. Entrega parcial todavía pendiente.
  Agregada y aprobada contra PostgreSQL real una prueba del flujo actual: alta persistida,
  recepción, asignación, carga, despacho, entrega, repetición de la misma operación y rendición.
  Cada etapa reabre el contexto; se comprueban historial, evidencias, outbox y aislamiento
  frente a otro operador. Pasaron también las 125 pruebas sin PostgreSQL. Las otras 68
  pruebas de integración no se repitieron con conexión en esta sesión.
  No se modificaron entidades, contratos ni migraciones de producción.

El hallazgo de asignación simultánea de vehículo y ruta corresponde a la letra original;
su corrección se documenta en [Multitenancy](docs/multitenancy.md), no como avance de entrega parcial.

### 01/11: configuración de entrega parcial (requisito 33, avance parcial)

- Cada versión de reglas guarda si permite entrega parcial y una acción de pendientes:
  nueva visita sin recargo, nueva visita con recargo o devolución al comercio.
- El backend administrativo existente recibe y devuelve ambos campos. Se sigue creando
  una versión nueva; las reglas y los envíos anteriores conservan sus referencias.
- La migración `20261101185636_ReglasEntregaParcial` agrega dos columnas y una restricción
  de valores válidos. Las reglas existentes quedan con entrega parcial deshabilitada;
  no se modifica el estado de envíos ni se inventan resultados de bultos.
- Para integrar: `DatosReglasDto` agrega `PermiteEntregaParcial` (bool, por defecto false)
  y `AccionPendientes` (enum: 0 nueva visita sin recargo, 1 con recargo, 2 devolución al comercio).
  Los usos anteriores de los constructores siguen compilando. Un cliente que omita los campos
  al crear una versión nueva guardará esos valores predeterminados: para conservar una
  configuración parcial ya habilitada debe reenviar los campos del detalle consultado.
- Ana podrá conectar esos datos con el formulario de reglas cuando integre su parte.
  No se modificaron pantallas, PWA ni Worker. Cada integrante deberá aplicar la migración
  a su base al incorporar el cambio; no se aplicó a la base de desarrollo en esta sesión.
- **Límite:** guardar el permiso todavía no habilita una entrega parcial operativa.
  Faltan estados, intentos y evidencias por bulto, aplicación de la política, reasignación,
  eventos y demostración offline. La opción con recargo todavía no calcula ni cobra un importe.

Detalle técnico y ejemplo de integración: [Configuración versionada](docs/configuracion-versionada.md).

Verificación de esta etapa: 204 pruebas aprobadas, incluidas 74 contra PostgreSQL real
en bases temporales (sin omisiones). Compilación Release sin errores ni advertencias.

### 01/11: estados de bultos y resumen del envío (requisito 31, primera etapa)

- `Bulto.Estado` guarda su etapa: Admitido (0), EnDeposito (1), AsignadoARuta (2),
  EnTransito (3), Entregado (4), NoEntregado (5), Reprogramado (6), EnDevolucion (7),
  Devuelto (8) o Extraviado (9). No hay un mutador público de ese estado.
- Las operaciones existentes validan y actualizan también los bultos: recepción,
  asignación, despacho, rechazo, aplazamiento, reintento, entrega, devolución y extravío.
  Una operación inválida no cambia los estados.
- El estado del envío se calcula desde los bultos después de cada operación y se conserva
  en la columna existente para consultas y outbox. Se validan transiciones declarativas
  tanto del envío como de sus bultos antes de aplicar cambios.
- `EstadoEnvio.EntregadoParcialmente` se agrega al final del enum (valor 10), sin cambiar
  valores anteriores. Algunos entregados y otros no producen parcial, incluso si los
  restantes se devolvieron o extraviaron. Todos entregados producen Entregado; todos
  devueltos producen Devuelto.
- Sin entregados y con estados mixtos se prioriza: Extraviado, EnDevolucion, EnTransito,
  AsignadoARuta, Reprogramado, NoEntregado, EnDeposito, Admitido y finalmente Devuelto.
  Es una decisión de diseño, no una precedencia impuesta por el PDF.
- La migración `20261101195520_EstadosPorBulto` agrega la columna y valida valores 0–9.
  Infiere los estados existentes desde cada envío y operador, dado que el modelo anterior
  solo registraba resultados completos. No crea eventos ni evidencias históricos.
- Para integrar: cargar **todos los bultos** antes de operar con el agregado. Las lecturas
  pueden seguir usando el estado del envío sin incluirlos. Las llamadas de entrega completa
  conservan su firma. El código que omita o filtre bultos no es válido para estas operaciones.
- **Límite:** todavía no hay registro de una entrega mixta ni resultados/evidencias por
  bulto. El estado parcial está preparado, pero no se genera en el flujo operativo normal.
  No se cambiaron hojas de ruta, contratos de eventos V1, Web, PWA ni Worker. Los requisitos
  31 y 32 todavía no se consideran completos.
- La migración no se aplicó a desarrollo. Al integrar la rama hay que aplicar las migraciones;
  las interfaces deberán contemplar el estado nuevo cuando se habilite el flujo parcial.

El ADR del cambio está [en curso](docs/adr/007-entrega-parcial.md).

Verificación final de esta etapa: 219 pruebas aprobadas, incluidas 77 contra PostgreSQL
real sin omisiones. Compilación Release sin errores ni advertencias y modelo sin cambios
pendientes de migración. Las bases de prueba se eliminaron al terminar.

### 01/11: registro real de resultados por bulto (requisito 32, núcleo)

- Ya se puede registrar en Domain una entrega de dos de tres bultos, con estados Entregado,
  Entregado y NoEntregado, y resumen EntregadoParcialmente. La versión de reglas del envío
  debe permitirlo; cada no entregado lleva motivo activo de su propio catálogo.
- Hay dos entidades nuevas: DetalleIntentoBulto (resultado y motivo) y PruebaBulto
  (asociación de evidencia). Una foto puede acreditar varios entregados; una incidencia
  conserva su propia evidencia si el motivo la exige. No se comparte una misma prueba
  entre un entregado y un no entregado.
- El contrato nuevo es RegistrarIntentoPorBulto: exige parada de origen, todos los resultados
  de la visita, repartidor asignado, instante UTC e identificador de operación estable.
  Se rechazan solicitudes incompletas, duplicados, bultos ajenos y visitas distintas.
- Un reenvío idéntico devuelve el intento anterior, sin duplicar. Se compara contenido y
  asociaciones mediante una huella; cambiar el contenido usando el mismo identificador
  produce conflicto. Dos decisiones concurrentes no se mezclan: una confirma y la otra
  debe releer. No hay fusión offline automática ni sincronizador implementado.
- La firma anterior de entrega completa se mantiene y genera detalles para los intentos
  nuevos. Los intentos históricos se conservan sin inventar asociaciones de evidencias.
- La migración `20261101201848_ResultadosEntregaPorBulto` agrega las dos tablas, huella y
  referencias compuestas. La base impide asociar bultos de otro envío, incluso de la misma
  cuenta, o pruebas de otro intento. Se mantienen aislamiento de operador/cuenta y
  concurrencia del agregado; SaveChanges protege el historial de intentos y evidencias.
- Se agregó EventoEnvioV2 y el tipo `envio.evento.v2` con detalle por bulto, sin datos
  personales, archivos ni token público. Las llamadas tradicionales mantienen V1.
  El publicador genérico puede transportar ambos; no se implementó RabbitMQ/Worker ni
  se cambió el formato enviado automáticamente a receptores antiguos.
- Los pendientes todavía no pueden asignarse a una visita nueva ni iniciar su devolución
  parcial. Eso continúa con los requisitos 33/34. El recargo tampoco se calcula/cobra todavía.
- No se modificaron Web, PWA ni Worker y no se aplicó la migración a desarrollo.

Para integrar y conocer los límites: [Intentos por bulto](docs/intentos-por-bulto.md).
El núcleo no es un endpoint HTTP: quedan por implementar su orquestación de Application
y exposición, además de las integraciones que corresponden a las otras áreas.

Verificación final de este avance: 243 pruebas aprobadas, incluidas 83 contra PostgreSQL
real sin omisiones. Release sin errores ni advertencias; modelo alineado con la migración.
Las bases temporales de prueba se eliminaron al finalizar.

### 03/11: caso de uso para registrar intentos (requisito 32 y flujo original)

- RegistrarIntentoHandler conecta la solicitud con Domain y guarda la unidad completa.
  No recibe usuario/operador/rol: el repositorio usa el contexto autenticado y comprueba
  en la base un repartidor activo del operador y su asignación o su operación histórica.
- La solicitud lleva envío, parada, versión esperada, identificador estable, instante UTC,
  resultados por bulto y definiciones/referencias de evidencia. Los motivos se resuelven
  desde el catálogo de la versión del envío y las evidencias compartidas se construyen una vez.
- Una operación nueva con versión vieja se rechaza antes de modificar el núcleo. Un reenvío
  idéntico reconoce el intento anterior incluso con la versión original y no vuelve a guardar.
  Otro contenido con el mismo identificador se rechaza. Las carreras durante el guardado
  siguen protegidas por la concurrencia y el rollback existentes.
- Cada ejecución carga un contexto dedicado con una lectura coherente del grafo y lo dispone
  al terminar, también si falla. La respuesta es un DTO con el intento y la versión resultante.
- Hay registro optativo de servicios; no se modificaron Web, PWA, Worker o sus endpoints.
  No requiere otra migración ni modifica el contrato de outbox de la etapa anterior.
- Las pruebas nuevas invocan el caso de uso con PostgreSQL real para comprobar identidad,
  permisos, versiones, repetición, guardado, rollback y decisiones simultáneas.

Para integrar: [Caso de uso de registro de intentos](docs/registro-intento-aplicacion.md).

Verificación: 258 pruebas aprobadas, incluidas 88 contra PostgreSQL real sin omisiones.
Release sin errores ni advertencias y modelo sin cambios pendientes de migración.
Las bases temporales se eliminaron al terminar; desarrollo no se modificó.

### 03/11: rutas de pendientes y capacidad por visita (requisito 34)

- Cada Parada conserva sus bultos y medidas mediante BultoParada. La nueva visita solo
  lleva pendientes; peso y volumen se calculan con esa selección y la carga de otras
  paradas activas. Las paradas anteriores mantienen su composición.
- Se puede reprogramar, asignar, cargar, despachar y entregar el pendiente, incluso con
  otro repartidor, hasta completar el envío. Los entregados previos no se cambian ni
  aparecen en los resultados/evidencias de la visita siguiente. Se conservan la fecha
  comprometida, los límites de intentos y una sola asignación activa por envío.
- La política admitida en esta etapa es nueva visita sin recargo. Si las reglas indican
  recargo o devolución se rechaza la reprogramación: falta implementar esos tratamientos.
- RegistrarIntentoHandler ya carga la composición y acepta los resultados de la parada
  actual. Su contrato de solicitud no cambia; el body incluye solo los bultos de esa visita.
- **Integración V2:** IdOperacion es ahora nullable. Los eventos administrativos de
  continuación parcial van por V2 con IdOperacion null y Bultos vacío; los intentos
  mantienen identificador y detalle. No se incorporan estados parciales nuevos a V1.
- La migración 20261103121131_BultosPorParada agrega composición, medidas y restricciones.
  Para las paradas previas se infieren todos los bultos de su envío, como funcionaba antes;
  las medidas se inicializan con los valores actuales, no se reconstruyen medidas históricas.
  No se generan eventos ni se modifica la tarifa. No se aplicó a desarrollo.
- Se mantienen filtros de operador/cuenta y concurrencia de envío/ruta, con FK del mismo
  envío y composición histórica protegida en SaveChanges. No se modificaron Web, PWA o Worker.

Para integrar y cargar las relaciones: [Rutas de pendientes](docs/rutas-pendientes.md).

Verificación: 272 pruebas aprobadas, incluidas 92 contra PostgreSQL real sin omisiones.
Release sin errores ni advertencias; EF confirma el modelo alineado con las migraciones.

### 03/11: política de pendientes, devolución parcial y recargo fijo (requisito 33)

- Las tres opciones de la versión de reglas ya tienen tratamiento en el núcleo: nueva
  visita sin recargo, nueva visita con recargo fijo y devolución al comercio.
- Se eligió un importe fijo configurable por visita. DatosReglasDto agrega
  ImporteRecargoNuevaVisita (decimal nullable): positivo con hasta dos decimales para la
  opción con recargo y null para las otras. El backend administrativo lo recibe y devuelve.
  Los formularios deberán conservar y enviar el campo al integrar su parte.
- La parada captura el importe al asignarse. Al despachar se registra RecargoVisita una
  sola vez; retirar/reasignar antes de salir no genera cargos. Se mantienen separados de
  TarifaCalculada. Es un cargo devengado, no un pago efectivo ni facturación implementada.
- La devolución conserva sus pendientes en BultoDevolucion; solo exige los escaneos de
  esos bultos. Entregados y devueltos permanecen cerrados. Entregado + Devuelto conserva
  el resumen EntregadoParcialmente, con el detalle individual disponible.
- Se mantiene aislamiento por operador/cuenta, FK del mismo envío, historial protegido
  en SaveChanges y concurrencia de los agregados. Un fallo del outbox o un conflicto
  revierte cargo, despacho y estados; la base impide duplicar el cargo de una parada.
- La migración PoliticasPendientesYRecargoFijo recupera la composición de devoluciones
  anteriores completas, sin inventar cargos ni precios. Las reglas antiguas con recargo
  quedan con importe null y bloquean nuevas visitas: requieren una decisión explícita,
  no se les asigna un precio arbitrario ni se cambia su versión en uso.
- No se aplicó a desarrollo. No se modificaron Web, PWA ni Worker. Continúan pendientes
  endpoints, formularios e integraciones de sus responsables y la demostración del 12/11.

Detalle para integrar: [políticas de pendientes y recargo fijo](docs/politicas-pendientes.md).

Verificación final: 287 pruebas aprobadas, incluidas 98 contra PostgreSQL real, sin omisiones.
Release sin errores ni advertencias y modelo alineado con las migraciones. Se actualizó el
SQL de instalación y se eliminaron las bases temporales; desarrollo no se modificó.

### 03/11: integración HTTP de compromisos anteriores (letra original)

La API de hoja de ruta del 22/10 ya permite listar y descargar rutas despachadas del
repartidor autenticado, con orden manual documentado y versiones de ruta/envío. Reutiliza
la composición por parada y por eso también devuelve correctamente visitas de pendientes.
La API de asignación de nuevas visitas y el endpoint de intentos siguen pendientes.

Para el 29/10 se registró el backend de configuración en Web y se expusieron consultas y
creación de versiones administrativas con antiforgery, aislamiento y conflictos de versión.
No se implementaron pantallas ni mensajería real. No se trabajó en opcionales, cobertura,
ensayo o cierre del ADR-007 del 12/11. No requiere otra migración.

Detalle y límites por responsable: [backend de monitoreos](docs/api-monitoreos.md).

Verificación: 293 pruebas aprobadas, incluidas 104 con PostgreSQL real, sin omisiones.
Release sin errores ni advertencias; modelo sin cambios pendientes. Bases temporales
eliminadas y base de desarrollo sin cambios.

### 05/11: registro y sincronización por bulto en la PWA (requisito 32)

La PWA permite marcar los bultos entregados por código o selección, pedir el motivo de
cada bulto fallido y capturar las pruebas indicadas por la parada. IndexedDB conserva la
visita por repartidor, con sus evidencias, parada, versión esperada e identificador de
operación. Al volver la conexión se envía el mismo cuerpo al endpoint autenticado. Los
conflictos quedan visibles en la cola y requieren actualizar la hoja.

La prueba manual con el navegador y la API real confirmó el registro offline y la
sincronización al reconectar. El seguimiento público devolvió `EntregadoParcialmente`.
La prueba automatizada usa la cola y el cliente de la PWA contra la API real y PostgreSQL
con un almacén local en memoria. Si se pierde la respuesta después del guardado, reenvía el
mismo cuerpo y confirma la visita existente. Una segunda acción con la versión anterior
recibe 409 y queda rechazada. Se conserva un solo intento y un solo mensaje de outbox.
