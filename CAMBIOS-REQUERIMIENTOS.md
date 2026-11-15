# Seguimiento del cambio de requerimientos

Actualizado: 14/11/2026. Equipo de tres integrantes, Grupo 8.

Para priorizar trabajo nuevo, usar el [plan de cierre de requisitos](PLAN-PENDIENTES.md):
separa brechas obligatorias confirmadas, puntos por verificar y mejoras. Las limitaciones
de cada contrato no son automáticamente requisitos nuevos. En particular, el requisito 1
sí exige modificación y baja de comercios/usuarios; el 19 exige aviso de sobrantes, no
un historial persistido de cada sobrante.

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

## Pendientes de la letra original para retomar después del monitoreo

Las referencias apuntan a la numeración y las páginas impresas de `Laboratorio__NET_2026.pdf`.
La tabla reúne las brechas detectadas en el repositorio para contrastarlas con el sistema
integrado después del monitoreo del 12/11. Los responsables son propuestas para confirmar
en equipo según las áreas de «Nos organizamos».

| Pendiente | Sección de la letra original | Estado y trabajo por revisar | Responsable propuesto |
| --- | --- | --- | --- |
| Ejecutar en CI las pruebas de integración con PostgreSQL | **6.15 · Calidad** (pág. 7). | Implementado con dependencias efímeras, resultados TRX y control de omisiones. El usuario confirmó build-test y E2E aprobados en main tras integrar depósito/rutas. Los nuevos cambios deben volver a pasar CI al subirlos. | Agustín, coordinando el pipeline con Aldana. |
| Exponer el despacho operativo de rutas | **5.3 · Planificación y despacho, requisito 17** (pág. 4). | Backend de consulta de preparación y confirmación implementado, con carga, permisos y versiones verificadas. La PWA descarga las rutas despachadas. Falta conectar las pantallas del backoffice. Ver [despacho](docs/despacho-operativo.md). | Agustín en backend y Ana en backoffice. |
| Completar la administración del operador | **5.1 · Administración y configuración del operador, requisitos 1, 2 y 6** (págs. 3–4). | Backend A1 cubierto dentro del contrato de comercios/cuentas/usuarios: altas, consultas, modificación, bajas y autorización por rol/cuenta. Hay altas/consultas de flota y zonas/franjas. Restan pantallas y validar la gestión de flota en la demo; no son pendientes automáticos un editor genérico de permisos ni CRUD completo de todos los catálogos. Ver [A1](docs/administracion-comercios.md) y [plan](PLAN-PENDIENTES.md). | Agustín en backend y validación; Ana/responsables de Web en pantallas. |
| Configurar la identidad visual por operador | **5.1 · Requisito 5** (pág. 3) y **6.5 · Multitenancy** (pág. 6). | Backend de marca/colores/contacto y URL de logo con migración y concurrencia, API administrativa y lecturas seguras disponibles. Falta formulario y aplicar los datos en portal, seguimiento y comunicaciones. Ver [contrato](docs/identidad-operador.md). | Agustín backend; Ana pantallas; Aldana comunicaciones. |
| Completar las modalidades de alta y el cálculo de tarifa | **5.2 · Ciclo de vida del envío, requisitos 7 y 9** (pág. 4). | Alta individual del portal con cálculo automático y backend de importación idempotente implementados. El alta por API para comercios ya expone login Bearer, opciones, cotización y confirmación por referencia, reutilizando la idempotencia de importación. Quedan lectura del archivo e integración de importación en el portal. Ver [API](docs/api-comercios.md) e [importación](docs/importacion-envios.md). | Agustín en backend, cálculo e idempotencia. Ana en portal e importación desde la interfaz. |
| Completar operaciones del portal del comercio | **4 · Alcance, portal del comercio** (pág. 3). | Revisar impresión de etiquetas, gestión de devoluciones y consulta de cuenta corriente y liquidaciones desde el portal. El reporte de liquidación ya existe en backoffice. | Ana en portal y Agustín en las operaciones de backend que falten. |
| Conectar el circuito de depósito y rutas | **5.2–5.4 · Requisitos 10, 13, 19 y 22** (pág. 4). | Ya hay backend de recepción con discrepancias, asignación regular con consultas de envíos/rutas/flota, carga, despacho, devolución y rendición operativa. Falta integrar esas operaciones en las interfaces y completar el aviso/control de sobrantes al cargar. El punto 4 de Aldana contempla el escaneo en la PWA y el punto 4 de Ana la vista de parciales y pendientes. Ver [recepción](docs/recepcion-deposito.md), [asignación](docs/asignacion-regular.md) y [cierre](docs/cierre-deposito.md). | Agustín en backend, Ana en backoffice y Aldana en las acciones de la PWA. |
| Revisar el recálculo de indicadores y el cierre diario | **4 · Alcance, servicio de procesamiento en segundo plano** (pág. 3). | Los reportes actuales calculan sus resultados al consultar. Falta comprobar el procesamiento de indicadores y el cierre diario desde el Worker. El cierre aparece en el diagnóstico de «Nos organizamos» y quedó sin tarea numerada. | Aldana en Worker y Agustín para las reglas de cierre que falten. |
| Confirmar la opción de autenticación exigida | **6.3 · Seguridad y autenticación** (pág. 5). | La aplicación usa cookies, tokens Bearer y `PasswordHasher`. En la revisión no se encontró ASP.NET Identity como almacén de usuarios ni OpenID Connect configurado. Confirmar con el equipo cómo se acredita o completa la opción de autenticación que pide la letra. | Agustín y Aldana, con Ana para validar el impacto en Web. |

La reprogramación solicitada por el destinatario (**26**) y las notificaciones al
destinatario (**27**) ya figuran en el reparto de Ana y Aldana, respectivamente. Los
pendientes del cambio de entrega parcial siguen en la tabla de requisitos 31–36 de
este documento.

Al cerrar el monitoreo, revisar cada fila contra la versión integrada y confirmar
qué queda por hacer, quién lo toma y cómo se comprueba antes de la entrega del 15/11.
Repetir el chequeo de cobertura de **7.2 · Datos y rendimiento** (pág. 9) cuando se
integren cambios en Domain o Application. La medición local actualizada del **14/11** es
**86,55 % en Domain** y **80,02 % en Application**. Estos porcentajes no sustituyen la
medición del CI de cada nueva versión. Las demás filas siguen siendo una lista de revisión;
no se dan por completas áreas ajenas sin contrastarlas con su implementación e integración.

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

### 14/11: separación de comercios homónimos en reportes (letra original, requisito 30)

Corregida la agrupación de cumplimiento y liquidación del reporte de gestión: utiliza
el identificador de cuenta comercial en lugar del nombre editable. Dos clientes con
igual nombre mantienen sus métricas e importes separados, incluso tras renombrar o dar
de baja lógica una cuenta. Se verificaron aislamiento entre operadores, filtro de fechas
y conservación de totales con PostgreSQL real. No cambia el contrato público, las
pantallas ni las fórmulas de importes; no requiere migración. No implementa el módulo
de cuenta corriente/liquidaciones de las compañeras. Ver [detalle](docs/reportes-gestion.md).

### 14/11: registro operativo de extravíos (letra original, requisito 11)

Caso de uso y API para consultar y registrar extravíos sobre las reglas existentes:
Administrador/Despachador activo, envío propio, versiones de envío y ruta, detalle y
hora del servidor. Conserva bultos entregados en parciales y libera la parada activa
con historial/outbox/estados/versiones en una sola escritura atómica. Pruebas de permisos,
concurrencia y rollback. Sin migración ni cambios de pantallas/PWA/Worker. Queda el
formulario del backoffice. Ver [contrato](docs/registro-extravios.md).

### 14/11: A2, backend de identidad visual y contacto del operador

Configuración persistida de marca, colores HEX, contacto y URL HTTPS de logo por operador,
con versión de concurrencia. API administrativa con permisos/antiforgery; lectura por
sesión y lectura pública mínima por token de seguimiento. Puerto de lectura para el
ámbito confiable de mensajes, sin modificar Worker ni contratos de eventos. Migración
`IdentidadVisualOperador` copia razón social a marca, asigna versiones y mantiene contacto
previo vacío. La marca se consulta sin caché, separada del estado del envío, sin cambiar
su contrato existente. Quedan formulario y aplicación visual con Ana y consumo en
comunicaciones con Aldana: A2 integrado sigue abierto. Ver [contrato](docs/identidad-operador.md).

### 14/11: cierre de revisión de perfiles/permisos del backend A1

Revisada la asignación del perfil Comercio en servidor y sus permisos por rol/cuenta.
Se agregaron pruebas de los ocho endpoints administrativos contra Despachador, Operario
de depósito y Repartidor, y pruebas con Bearer real de Comercio sin elevación de privilegios
por campos extra. Administrador activo del operador conserva la gestión exclusiva.
No se inventa un editor genérico de permisos ni nuevos perfiles comerciales sin requisito.
El backend de A1 queda cubierto según el contrato; las pantallas siguen pendientes de Web.
Esto no cierra V1 (elección de autenticación exigida por la letra), que sigue separado.

### 14/11: A1, modificación y baja de la cuenta comercial

Backend para editar razón social, teléfono y email locales del operador y dar de baja
la cuenta comercial sin modificar el Comercio global compartido. Migración
`AdministracionCuentaComercial`: copia datos existentes, mantiene cuentas activas y
asigna versión de concurrencia. Baja lógica: bloquea autenticación y nuevas altas,
conservando historial, procesamiento operativo y mensajes pendientes. El trigger de
PostgreSQL serializa el INSERT de envío con la baja de cuenta. Reportes/detalles del
backoffice usan la ficha local. Sin cambios de pantallas/PWA/Worker. A1 sigue abierto
para revisar perfiles/permisos y presentación; las pantallas de cuenta corriente,
etiquetas e importación continúan con las compañeras. Ver [contrato](docs/administracion-comercios.md).

### 14/11: A1, primera etapa de modificación/baja de usuarios

El backend permite modificar el email y dar de baja lógica usuarios de Comercio de una
única cuenta propia. Revalida administrador, cuenta y rol, exige antiforgery, compara el
email/estado leídos dentro de una transacción con bloqueo por usuario y conserva los
vínculos, credenciales e historial. La baja rechaza nuevos logins y el uso de sesiones
anteriores en las operaciones que revalidan la identidad. Sin migración. Quedan edición
y baja del comercio/cuenta, revisión de perfiles/permisos y pantallas; A1 sigue abierto.
Cuenta corriente, etiquetas y pantalla de importación están en curso por las compañeras
según confirmó Agustín; no se implementan en paralelo aquí. Ver [plan](PLAN-PENDIENTES.md)
y [contrato](docs/administracion-comercios.md).

### 14/11: altas y consultas de zonas/franjas (letra original, 5.1)

API administrativa para crear zonas con al menos una franja, consultar zonas y horarios
y añadir franjas a una zona propia. Administrador activo revalidado, aislamiento y CSRF.
El alta de zona/franjas es atómica; las adiciones concurrentes no reemplazan la colección.
Se reutilizan los catálogos del tarifario y las opciones de alta existentes, sin modificar
ventanas o precios históricos. Una zona nueva requiere una línea de tarifa para cotizar.
Sin migración ni cambios de pantallas/PWA/Worker. Quedan edición/bajas y pantallas;
los POST no son idempotentes. Ver [contrato y límites](docs/administracion-zonas.md).

### 14/11: altas y consultas de comercios/cuentas/usuarios (letra original, 5.1)

API administrativa para crear comercio y cuenta propia juntos, listarlos y crear/listar
usuarios de Comercio vinculados a una cuenta del operador. Hash y vínculo se guardan
juntos; email normalizado único por operador, incluso ante carreras. Se conservan los
comercios compartidos existentes sin permitir descubrir/enlazar o editar datos ajenos.
Roles, antiforgery, identidad y aislamiento verificados. Sin migración ni cambios de
pantallas/PWA/Worker. Quedan edición/bajas/permisos adicionales y zonas/franjas. Ver
[contrato y límites](docs/administracion-comercios.md).

### 14/11: altas y consultas de flota (letra original, requisito 6)

API administrativa para registrar/listar repartidores con contraseña hasheada y vehículos
con capacidades positivas. Normaliza email/matrícula y valida duplicados del operador,
también ante altas simultáneas. Revalida administrador activo, exige antiforgery y deriva
el operador de la sesión. Los recursos aparecen en planificación y permiten crear rutas.
Sin migración ni cambios de pantallas, PWA o Worker. No incluye edición ni bajas; los
otros bloques administrativos siguen pendientes. Ver [contrato](docs/administracion-flota.md).

Este registro conserva el alcance y los pendientes de cada etapa histórica. Para el
estado actualizado de los puntos revisados se usa la tabla anterior y la entrada más
reciente; una tarea pendiente en una entrada antigua puede haberse completado después.

### 14/11: alta por API para comercios (letra original, requisitos 7 y 9)

Login con credenciales de comercio y Bearer existente; consulta de opciones y cotización;
alta con referencia estable por cuenta/operador. Reutiliza cálculo, reglas y guardado
idempotente de la importación: 201 al crear, 200 al reconocer el reenvío y 409 si cambia
el contenido de una referencia existente. Alta, historial, outbox y referencia se guardan
juntos. El login exige una única cuenta y limita intentos por IP. Sin migración nueva,
cambios de pantallas, PWA o Worker. Ver [contrato y alcance](docs/api-comercios.md).

### 14/11: devolución y rendición operativa (letra original, requisito 22)

APIs de devoluciones abiertas, consulta por envío, inicio según reglas, recepción de sus
bultos y cierre con historial/outbox. Respetan la composición parcial y los entregados.
APIs de rutas por rendir, consulta y rendición, exigiendo despacho y ninguna parada activa.
Permisos de depósito, antiforgery, versiones y rollback; escaneos repetidos reconocidos
mientras la devolución siga abierta. La rendición no completa devoluciones automáticamente
ni agrega cobros/liquidaciones. Sin migración ni cambios de pantallas/PWA/Worker.
Quedan las integraciones de interfaz. Ver [contrato y límites](docs/cierre-deposito.md).

### 14/11: asignación regular de envíos (letra original, requisitos 13–16)

Consultas de envíos preparados, rutas abiertas con ocupación/capacidad y flota del
operador. API para asignar un envío completo conservando programación y tarifa, con
orden manual, validaciones del dominio y versiones de ambos agregados. Se reutiliza
la creación de rutas existente. Estado, parada, composición, evento y outbox se guardan
juntos. Se traduce también a conflicto la carrera por el mismo número de parada.
Las entregas parciales siguen usando su flujo específico. Sin migración ni cambios de
pantallas/PWA; quedan formularios, cierre de devoluciones y rendición. Ver
[contrato](docs/asignacion-regular.md).

### 14/11: recepción en depósito (letra original, requisito 10)

Consulta de manifiesto y cantidades; escaneo por código; registro de discrepancias de
envío o bulto con valores declarado y recibido; confirmación del ingreso cuando todos
los bultos estén escaneados. Identidad del operador/responsable resuelta desde sesión,
permisos de depósito, antiforgery, versiones y reconocimiento de reenvíos sin duplicar.
La confirmación guarda estado, evento y outbox juntos. Se conservan los valores declarados
y la tarifa. No requiere migración ni modifica pantallas/PWA. Asignación regular y cierre
de devoluciones/rendición continúan pendientes. Ver [contrato](docs/recepcion-deposito.md).

### 13/11: servicios de integración en CI (letra original, 6.15)

Configurado el runner con PostgreSQL, RabbitMQ, Redis y el compose de observabilidad;
variables de prueba, espera de disponibilidad y publicación de reportes TRX. El pipeline
rechaza pruebas omitidas mediante `Assert-TestResults.ps1`. No requiere AWS activo.
Pendiente confirmar la ejecución completa sin omisiones en GitHub Actions después del
push. Los timeouts de datos demo anotados previamente no se ocultan ni se dan por resueltos.

### 13/11: backend de carga de rutas (letra original, requisito 19)

Consulta de esperados/cargados/faltantes y registro por código, con variante de backoffice
y Bearer para el repartidor asignado. Reenvíos sin duplicar escaneos, códigos ambiguos
desambiguados por bulto, versiones optimistas y rollback. Se conecta al despacho y respeta
las visitas de pendientes. Sin migración ni cambios de pantallas; la integración de las
interfaces y el resto del circuito de depósito siguen pendientes. Ver [contrato](docs/carga-rutas.md).

### 13/11: backend de despacho (letra original, requisito 17)

API autenticada de consulta de preparación y despacho, con antiforgery, versión esperada,
carga verificada por composición activa y guardado transaccional con eventos/outbox.
Admite nuevas visitas de pendientes. No requiere migración. Quedan la conexión visual
en backoffice y el circuito de registro de carga; las demás operaciones de depósito y
asignación regular siguen pendientes. Ver [contrato y límites](docs/despacho-operativo.md).

### 13/11: backend de importación (letra original, requisito 7)

Implementado el procesamiento por fila con referencia estable por operador/cuenta,
reenvío sin duplicados, conflicto ante contenido distinto y transacción conjunta de alta,
outbox y registro de importación. Reutiliza el cálculo automático del alta individual.
Faltan la lectura del archivo y la conexión desde el portal, a cargo de la integración web.
El alta por API para comercios continúa pendiente. Ver [contrato y límites](docs/importacion-envios.md).

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
