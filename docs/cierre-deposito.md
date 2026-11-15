# Devoluciones y rendición en depósito

Backend del requisito 22 de la letra original, sección 5.4. Implementado el 14/11/2026.
Conecta las reglas de devolución y rendición que ya existían en Domain. No requiere
migración ni modifica los contratos de la PWA, los avisos o las visitas parciales.

## Permisos

Todas las operaciones requieren sesión del mismo operador con rol Administrador,
Despachador u OperarioDeposito. El usuario se revalida activo y con su rol contra la base.
No se reciben operador ni responsable desde el cuerpo. Comercio, repartidor y procesos
internos no pueden usar estos repositorios. Los POST requieren cookie y token obtenido
en `/api/antiforgery`, enviado en `X-CSRF-TOKEN`.

## Devoluciones

| Método | Dirección | Uso |
| --- | --- | --- |
| GET | `/api/deposito/devoluciones/abiertas` | Lista devoluciones EnProceso del operador, ordenadas por plazo e identificador, con envío y versión. |
| GET | `/api/deposito/envios/{envioId}/devolucion` | Consulta el estado del envío, su versión y la devolución con sus bultos/escaneos. Si todavía no hay devolución, devuelve `devolucion: null`. |
| POST | `/api/deposito/envios/{envioId}/devolucion/iniciar` | Inicia una devolución si lo permiten el estado y las reglas del envío; devuelve 201. |
| POST | `/api/deposito/envios/{envioId}/devolucion/bultos` | Registra la recepción por código de un bulto incluido en esa devolución; devuelve 200. |
| POST | `/api/deposito/envios/{envioId}/devolucion/completar` | Cierra la devolución después de recibir todos sus bultos; devuelve 200. |

Inicio y cierre usan la versión del **envío**:

```json
{ "versionEsperada": "<GUID devuelto al consultar el envío>" }
```

Escaneo:

```json
{ "codigo": "B001", "versionEsperada": "<último GUID del envío>" }
```

La consulta y las operaciones de inicio/cierre devuelven `envioId`, `codigoSeguimiento`,
`version`, `estado` del envío y `devolucion`. Esta última contiene `id`, `estado`,
`fechaInicio`, `plazoLimite`, `fechaCierre`, `bultos` y cantidades `esperados`, `recibidos`
y `faltantes`. Cada bulto incluye `id`, `codigo`, `estado` y `recibido`.
El escaneo devuelve `{ "envio": <ese estado>, "repetido": false }`.

### Reglas

- Iniciar reutiliza `Envio.IniciarDevolucion`: comprueba estado, motivos no reintentables,
  agotamiento de intentos y política de pendientes. No permite devolver arbitrariamente
  un envío que todavía debe reintentarse. También conserva el tratamiento de cancelación
  de envíos reprogramados que ya admite el dominio.
- La composición de la devolución se conserva al iniciarla. En una entrega parcial solo
  incluye pendientes; los entregados anteriores no se escanean ni cambian de estado.
- El código se compara sin distinguir mayúsculas ni espacios exteriores, dentro de la
  devolución del envío indicado. Un código desconocido o de un bulto excluido devuelve 400.
- Solo se reconoce un escaneo de tipo Devolucion vinculado a ese proceso, no anterior
  al inicio ni futuro. Un escaneo de recepción/carga no lo sustituye.
- Un reenvío de código ya recibido mientras siga EnProceso devuelve `repetido=true`,
  incluso con la versión anterior, sin insertar otro escaneo ni cambiar la versión.
  No es una clave de idempotencia global. Los demás cambios requieren la versión actual.
- Completar exige todos los escaneos de esa composición. Cambia los bultos a Devuelto;
  si había entregados, el resumen del envío conserva EntregadoParcialmente. Se registra
  fecha UTC del servidor y el evento DevolucionExitosa con responsable y origen Depósito.
- El plazo límite se consulta como dato operativo; no se agrega una prohibición de
  registrar una recepción tardía que el dominio actual no impone.
- Iniciar nuevamente una devolución existente, completar dos veces o escanear después
  de completar devuelve conflicto, sin duplicar registros o eventos.

El inicio y el cierre guardan estado, devolución, composición, evento y outbox en la
misma transacción. Los escaneos solos no generan un cambio de estado ni otro mensaje.
V1/V2 se elige mediante el outbox existente; el cierre de una entrega parcial conserva V2.
El inicio de pendientes desde `/api/despacho/envios/{id}/devolucion-pendientes` sigue
disponible con su contrato anterior; ambos caminos usan las mismas reglas del dominio.

## Rendición operativa de rutas

| Método | Dirección | Uso |
| --- | --- | --- |
| GET | `/api/deposito/rutas/por-rendir` | Rutas despachadas sin rendición, con versión y cantidad de paradas activas. |
| GET | `/api/deposito/rutas/{rutaId}/rendicion` | Estado de despacho/rendición y paradas ordenadas, con estado actual del envío y su devolución. |
| POST | `/api/deposito/rutas/{rutaId}/rendir` | Registra el cierre operativo y devuelve el estado actualizado. |

El POST recibe la versión de la **ruta**, no la del envío:

```json
{ "versionEsperada": "<GUID devuelto al consultar la ruta>" }
```

La respuesta contiene `rutaId`, `version`, `despachadaEn`, `rendicionId`, `rendidaEn`,
`paradas` y `paradasPendientes`. Se puede consultar una ruta ya rendida para recuperar
su resultado; el POST repetido devuelve 409.

`Ruta.Rendir` exige despacho previo, fecha no anterior al despacho y ninguna parada
activa pendiente. El servidor asigna la fecha UTC. Los listados muestran también las
rutas bloqueadas por paradas activas para que la interfaz pueda explicar el motivo.
Al rendir, la ruta deja de estar disponible en el listado/descarga de la hoja del repartidor.

La rendición cierra la ruta; no marca devoluciones como completas ni cambia estados de
envíos. Según el dominio existente, una devolución puede seguir EnProceso y completarse
después de rendir. Su estado se muestra en la consulta, sin inventar otro bloqueo.
Se guarda el registro de rendición y la nueva versión de la ruta conjuntamente.
No se crea un evento de envío ni un mensaje outbox por el mero cierre de la ruta.
La entidad Rendicion existente conserva ruta y fecha; la autorización se verifica con
el usuario de sesión, sin añadir una columna nueva de responsable a esa entidad.

Esta rendición es operativa: no incorpora cobros, conciliación de dinero, liquidaciones
ni el cierre diario del Worker.

## Concurrencia, errores e integración

Application contiene `DevolucionDepositoHandler` y `RendirRutaHandler`. Infrastructure
abre contextos dedicados, valida identidad y carga el grafo de cada agregado mediante
una lectura Repeatable Read. La lectura termina antes de operar; el guardado conserva
las versiones originales del envío o la ruta y usa el SaveChanges transaccional existente.
Una falla de escaneo, devolución, rendición o outbox revierte la operación completa.

Dos inicios, escaneos, cierres o rendiciones concurrentes compiten por sus versiones.
La base también impide duplicar devolución por envío y rendición por ruta. Si esa
restricción se alcanza antes del UPDATE de la versión, se traduce igualmente a conflicto.

Respuestas: 400 solicitud/código/antiforgery inválidos; 401 sin sesión; 403 rol, identidad
o datos inaccesibles de otro operador; 409 versión vieja o regla operativa incompatible.
Tras un conflicto nuevo se relee el estado, sin inventar versiones ni marcar la operación
como completada en la interfaz. Las respuestas no se almacenan en caché.

Queda conectar consultas y acciones a las pantallas del backoffice. No se agregan cámara,
escaneo offline ni botones en la PWA. El escaneo de devolución de esta API no exige una
ruta de origen: pertenece al proceso de devolución y lo registra el personal de depósito.

Las pruebas verifican el recorrido completo, parciales, políticas, faltantes, reenvíos,
roles/CSRF/aislamiento, revocación, outbox compatible, cierres simultáneos y rollback de
las cuatro escrituras. También se comprueba que rendir no cierre una devolución pendiente.

## Integración de pantallas (15/11/2026)

Desde **Depósito → Devoluciones y rendición** (`/backoffice/deposito/cierre`) se listan
las devoluciones abiertas y las rutas despachadas aún no rendidas del operador, sin
limitarse a la fecha actual. El detalle de cada envío enlaza a su devolución.

- `/backoffice/deposito/devolucion/{envioId}` permite consultar, iniciar según reglas,
  escanear sus bultos y confirmar el cierre una vez recibidos todos los esperados.
  Conserva la composición parcial y el reconocimiento de escaneos repetidos del backend.
- `/backoffice/deposito/rendicion/{rutaId}` muestra las paradas y permite confirmar la
  rendición si está despachada y sin paradas activas. Las devoluciones se cierran por separado.
- Administrador, despachador y operario de depósito pueden acceder. Los formularios Razor
  validan antiforgery y envían la versión que se mostró al usuario; los handlers mantienen
  la autorización, aislamiento, concurrencia y guardado transaccional existentes.

Para probar localmente: iniciar PostgreSQL, Redis y Web con sus scripts `Start-*`, abrir
`http://localhost:5243/login` con `emailAdministrador` y `contrasena` del escenario demo
y entrar a Depósito. También se admiten usuarios reales de depósito/despacho. Un listado
vacío indica que ese operador no tiene devoluciones abiertas o rutas por rendir: el demo
no garantiza un caso preparado para cada cierre. No es un proceso automático de cierre
diario del Worker ni una liquidación económica. No requiere migraciones.

### Escenario para practicar el cierre

En Development, `POST /dev/datos-demo-cierre` crea un operador separado con credenciales
demo, un envío fallido por motivo no reintentable y una ruta despachada sin paradas activas.
No inicia la devolución ni rinde la ruta: esas operaciones se hacen en las pantallas.
La respuesta trae `usuarios` (emailAdministrador, contrasena, codigoSeguimiento),
`envioId`, `rutaId` y `bultos`. Cada llamada crea otro escenario sin cambiar los previos.
Los endpoints demo normal y PWA conservan sus datos y reglas anteriores.

```powershell
$cierre = Invoke-RestMethod -Method Post -Uri "http://localhost:5243/dev/datos-demo-cierre" -ContentType "application/json" -Body '{}'
$cierre.usuarios | Select-Object emailAdministrador, contrasena, codigoSeguimiento
$cierre.bultos
"http://localhost:5243/backoffice/deposito/devolucion/$($cierre.envioId)"
"http://localhost:5243/backoffice/deposito/rendicion/$($cierre.rutaId)"
```

Cerrar la sesión anterior e ingresar con el administrador de este escenario. Iniciar
la devolución, recibir todos los códigos devueltos en `bultos`, confirmarla y rendir
la ruta. Después ambas operaciones desaparecen del listado de pendientes.
