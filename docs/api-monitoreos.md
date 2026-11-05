# Backend de los monitoreos anteriores al 12/11

Fecha: 03/11/2026. Alcance: tareas de núcleo/datos e integración HTTP de Agustín.
Corresponde a la letra original y al reparto inicial. No incluye opcionales, medición
de cobertura, ensayo, pantallas, autenticación móvil, PWA ni Worker.

## Estado por compromiso

| Fecha | Parte propia | Estado |
| --- | --- | --- |
| 22/10 | Rutas/paradas, restricciones, asignación única y API de hoja de ruta | Núcleo existente y descarga HTTP implementada; seis pruebas nuevas verifican esta API y configuración con PostgreSQL real |
| 29/10 | Outbox y backend de tarifas/reglas editables | Registro transaccional y publicador existentes; configuración registrada en Web y expuesta por API administrativa. Las pantallas y el adaptador/host de mensajería siguen con sus responsables |
| 05/11 | Pruebas de integración del flujo crítico | Ya existe el recorrido de alta, recepción, asignación, carga, despacho, intento, reenvío y rendición, reabriendo contextos |

Esto no declara completos los monitoreos del equipo: faltan las integraciones de sus
responsables y la demostración en ejecución. La API de registro de intentos, la asignación
HTTP de nuevas visitas, la cobertura y el cierre del ADR-007 no se implementaron en esta etapa.

## Descarga de hoja de ruta

IHojasRuta en Application define contratos DTO de lectura. HojasRuta en Infrastructure
consulta PostgreSQL con el tenant autenticado y comprueba que sea un Repartidor real,
activo, del operador. No acepta operador, comercio o repartidor elegidos en la solicitud.

| Método | Ruta | Respuesta |
| --- | --- | --- |
| GET | /api/repartidor/rutas | Rutas del usuario despachadas y todavía no rendidas, con fecha de despacho UTC, matrícula, versión y cantidad de paradas activas |
| GET | /api/repartidor/rutas/{id} | Hoja de una de esas rutas; 404 si no está disponible o pertenece a otro usuario/operador |

Se listan todas las rutas abiertas: el modelo no tiene fecha de ruta ni zona horaria
del operador para inferir un día local. No se usa la fecha del servidor para ocultar una
ruta que siga abierta desde el día anterior. La fecha de despacho permite seleccionar
la hoja correspondiente. No se exponen borradores sin despacho ni rutas rendidas.

Las paradas activas se ordenan por Cod ascendente, que representa la secuencia manual
fijada por el despachador al planificar. Es un criterio determinista y documentado;
no es optimización de distancias. La inserción o el orden de lectura de PostgreSQL
no determinan la secuencia. Las paradas ya finalizadas no aparecen en la descarga.

Cada parada incluye identidad de parada/envío, versión esperada del envío, estado,
ventana UTC y llegada estimada, nombre/teléfono/dirección del destinatario, requisitos
de evidencia y motivos activos de la versión de reglas del envío. Los bultos proceden
de BultoParada y usan sus medidas capturadas. Una visita de pendientes no vuelve a
incluir los entregados en otra visita. No se envían el email ni el token público.
Los identificadores internos sí son necesarios en esta API privada para registrar operaciones.

La lectura del detalle usa RepeatableRead y consultas divididas en un contexto dedicado,
para que versiones, composición y datos pertenezcan a un mismo snapshot. No guarda ni
renueva versiones. El resultado deja de ser vigente si otra operación cambia el envío:
el registro posterior debe seguir verificando la versión esperada, como ya hace su caso de uso.
Las respuestas llevan Cache-Control: no-store por contener datos operativos personales.

## Configuración administrativa

Program registra AgregarConfiguracionVersionada y reutiliza el backend existente. Solo
un administrador autenticado y activo puede usar estas rutas; el servicio vuelve a
comprobar la identidad persistida. No basta pertenecer al grupo genérico de backoffice.

| Método | Ruta | Operación |
| --- | --- | --- |
| GET | /api/configuracion/zonas | Zonas del operador |
| GET | /api/configuracion/reglas | Versiones de reglas |
| GET | /api/configuracion/reglas/vigentes | Detalle vigente |
| GET | /api/configuracion/reglas/{id} | Detalle de una versión propia |
| POST | /api/configuracion/reglas | NuevaVersionReglas; devuelve 201 y Location del detalle |
| GET | /api/configuracion/tarifarios | Versiones de tarifas |
| GET | /api/configuracion/tarifarios/vigente | Detalle vigente |
| GET | /api/configuracion/tarifarios/{id} | Detalle de una versión propia |
| POST | /api/configuracion/tarifarios | NuevaVersionTarifario; devuelve 201 y Location del detalle |

Los DTO y ejemplos de datos están en configuracion-versionada.md. Cada POST crea una
versión completa y exige la última versión creada como esperada. Reglas y tarifas nuevas
entran en vigor sin reiniciar la Web; los envíos anteriores conservan sus referencias
y tarifa original. Una actualización de tarifario solicita la invalidación de su caché
mediante el servicio existente. RevisarInvalidacionCache informa una invalidación fallida
después de confirmar la nueva versión, como ya estaba definido en el contrato.

La autenticación actual usa cookies. Antes de un POST, el cliente autenticado debe llamar
a GET /api/antiforgery, conservar la cookie recibida y enviar el campo token devuelto en
la cabecera X-CSRF-TOKEN. El token está asociado a esa identidad; hay que renovarlo al
cambiar de sesión. Un POST sin token válido devuelve 400 y no escribe. No se habilitó CORS
adicional ni se desactivó antiforgery para los formularios existentes.

Las API nuevas devuelven 401 sin sesión, 403 sin permiso, 400 para datos inválidos y 409
ante versión desactualizada o una operación no admitida. Los detalles de configuración
ajenos devuelven 404. Los errores inesperados devuelven un problema genérico y se registran
en el servidor. Las páginas existentes conservan sus redirecciones de autenticación.

## Integración y límites

- Ana puede conectar sus formularios con estos endpoints o con IConfiguracionVersionada.
  No se agregaron formularios ni se completó el cálculo/preparación del alta.
- La PWA debe disponer de una identidad autenticada que incluya el operador y el perfil
  Repartidor. No se agregó emisión de tokens, login de repartidores ni almacenamiento offline.
  El login Web actual sigue reservando al repartidor el acceso desde su propia aplicación.
- Las pruebas HTTP arrancan el Program real mediante WebApplicationFactory y PostgreSQL
  temporal. Solo ese servidor de pruebas acepta cabeceras de identidad sintéticas;
  producción no tiene ese mecanismo. Se reutilizan el resolver real y sus consultas a usuarios.
- La prueba de invalidación usa la caché en memoria de desarrollo; no acredita Redis real,
  la infraestructura remota ni la recepción de mensajes en un comercio.
- Continúan pendientes la integración de pantallas, la autenticación/captura móvil y el
  adaptador/host de mensajería de sus responsables. El CI actual sigue necesitando que
  su responsable configure PostgreSQL para ejecutar estas integraciones automáticamente.

No hay cambios de esquema ni una migración nueva en esta etapa.

Verificación final: 293 pruebas aprobadas, incluidas 104 con PostgreSQL real, sin omisiones.
Release compila con advertencias como errores sin errores ni advertencias. EF confirma
que el modelo coincide con las migraciones existentes. Se eliminaron las bases temporales
y la base de desarrollo no se modificó.
