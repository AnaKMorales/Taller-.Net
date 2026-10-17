# Modelo de dominio

El proyecto `UltimaMilla.Domain` representa las entidades y relaciones de los cuatro
diagramas UML aportados por el equipo. Está organizado por áreas y no referencia EF Core,
ASP.NET, Identity ni otros proyectos de la solución. Todas las entidades tienen un `Guid`
generado al crearse. Las propiedades no tienen setters públicos y las colecciones se exponen
como vistas de solo lectura.

## Organización

| Área | Entidades |
| --- | --- |
| Operadores | Operador |
| Comercios | Comercio, CuentaComercial |
| Usuarios | Usuario, Repartidor |
| Configuración | ReglasOperativas, PoliticaDevolucion, MotivoNoEntrega, PlazoServicio, CuadroTarifario, LineaTarifa, AjusteTarifario, ZonaCobertura, FranjaHoraria |
| Envíos | Envio, Bulto, Destinatario, Direccion, EventoDeEnvio, IntentoEntrega, PruebaEntrega |
| Despacho | Vehiculo, Ruta, Parada |
| Depósito | EscaneoBulto, Discrepancia, Devolucion, Rendicion |
| Notificaciones | SuscripcionAviso, EntregaAviso, Notificacion |

`Common` contiene la base `Entidad`, las validaciones internas y los valores inmutables
`GeoPunto` y `ArchivoRef`. Las enumeraciones viven junto a las entidades que las usan.
`Envios/ProgramacionEntrega` es un valor inmutable que conserva la franja, la fecha de entrega
y los extremos concretos de la ventana con sus desplazamientos horarios.

## Relaciones

| Relación | Representación y protección en memoria |
| --- | --- |
| Operador y Comercio | Muchos a muchos mediante CuentaComercial; una cuenta por pareja operador/comercio. |
| Operador y Usuario | Un operador por usuario; el email no se repite dentro de ese operador. |
| Usuario y CuentaComercial | Un usuario de comercio puede tener varias cuentas de su operador; cada cuenta admite varios usuarios. |
| Usuario y Repartidor | Repartidor hereda de Usuario y fija su perfil. |
| Operador y Vehiculo | Un operador por vehículo, con matrícula única dentro del operador. |
| Operador y configuración | Varias zonas, versiones de reglas y versiones de tarifario. |
| ZonaCobertura y FranjaHoraria | Una o más franjas por zona; una franja no se comparte entre zonas. |
| ReglasOperativas y PoliticaDevolucion | Una política exclusiva por versión de reglas. |
| ReglasOperativas y MotivoNoEntrega | Varios motivos por versión. |
| ReglasOperativas y PlazoServicio | Varios plazos, sin repetir modalidad en la misma versión. |
| CuadroTarifario y LineaTarifa | Varias líneas, sin superponer rangos de peso para una misma zona y modalidad. |
| LineaTarifa y AjusteTarifario | Varios ajustes por línea. |
| CuentaComercial y Envio | Varios envíos; cada envío pertenece a una cuenta concreta. |
| Envio y Bulto | Uno o más bultos por envío; no se comparten ni se repiten sus códigos dentro del envío. |
| Envio y Destinatario | Un destinatario exclusivo por envío. |
| Destinatario y Direccion | Una dirección exclusiva por destinatario. |
| Envio y configuración | Referencias a la zona y las versiones de reglas y tarifario utilizadas. |
| Envio y FranjaHoraria | La programación identifica una franja perteneciente a la zona del envío y conserva su ventana concreta. |
| Repartidor y Ruta | Varias rutas por repartidor. |
| Vehiculo y Ruta | Varias rutas por vehículo. |
| Ruta y Parada | Varias paradas, con código único dentro de la ruta. Cada parada creada identifica un envío. |
| Envio y Parada | Cero o una asignación activa. Las paradas anteriores permanecen en la ruta como historial. |
| Envio y EventoDeEnvio | Varios eventos inmutables, con usuario responsable. |
| Envio y IntentoEntrega | Varios intentos, vinculados también a la parada en la que ocurrieron. |
| IntentoEntrega y MotivoNoEntrega | Motivo obligatorio en un fallo y ausente en una entrega exitosa. |
| IntentoEntrega y PruebaEntrega | Varias pruebas exclusivas del intento. |
| Envio y Devolucion | Cero o una devolución. |
| Envio y Discrepancia | Varias discrepancias; cada una puede identificar además un bulto del mismo envío. |
| Bulto y EscaneoBulto | Varios escaneos de recepción, carga o devolución por bulto. |
| Ruta y EscaneoBulto | Obligatoria para carga, ausente en recepción y opcional como referencia de origen en devolución. |
| Devolucion y EscaneoBulto | Los escaneos de devolución identifican obligatoriamente el proceso activo al que pertenecen. |
| Ruta y Rendicion | Cero o una rendición, creada al cerrar una ruta sin paradas activas. |
| CuentaComercial y SuscripcionAviso | Varias suscripciones por cuenta. |
| SuscripcionAviso y EntregaAviso | Varias entregas; no se duplica una entrega para el mismo evento y suscripción. |
| EventoDeEnvio y EntregaAviso | Un evento puede generar entregas para distintas suscripciones de su cuenta. |
| Destinatario y Notificacion | Varias notificaciones por destinatario. |

Los objetos secundarios que se preparan antes de asociarlos (bultos, destinatarios, direcciones,
franjas, políticas y pruebas) pueden estar temporalmente sin propietario. Una vez vinculados,
los métodos públicos no permiten compartirlos con otro propietario.

## Convenciones y ajustes al UML

- `CuentaComercial` concreta la relación muchos a muchos de operador/comercio y el diseño
  de aislamiento de la entrega inicial. Los envíos y las suscripciones pertenecen a la cuenta,
  evitando mezclar datos del mismo comercio entre operadores.
- `Rendcion` se normaliza a `Rendicion`; se usa `FechaComprometida` de forma consistente.
- `MaxParadas` pertenece a Ruta, como en el último diagrama. Las capacidades de peso y
  volumen pertenecen al vehículo.
- Peso en kilogramos, dimensiones de bultos en centímetros y volumen calculado en metros
  cúbicos. Esta convención se adopta explícitamente porque los atributos del UML no indican
  todas las unidades.
- Las horas del día se representan con `TimeOnly`. Las franjas actuales deben iniciar y
  finalizar dentro del mismo día. Application resuelve la zona horaria del operador y
  entrega los extremos locales como `DateTimeOffset`. ProgramacionEntrega verifica la
  fecha y las horas de la franja y expone los límites en UTC, sin consultar la zona del equipo.
  La fábrica `Crear` permite usar un único desplazamiento para ambos extremos; el constructor
  acepta desplazamientos diferentes si la zona horaria cambia dentro de la ventana.
  Las fechas de operaciones siguen siendo `DateTime` que Application debe normalizar a UTC.
- Los rangos de peso del tarifario son `[PesoDesde, PesoHasta)`: incluyen el límite inferior
  y excluyen el superior. Se admiten rangos contiguos. El UML solo aporta un límite superior
  de volumen; no se han inventado bandas adicionales de volumen.
- Firma y foto usan `ArchivoRef`. Nombre y documento usan `ValorTexto`, para no obligar a
  almacenar texto como si fuera un archivo. Las pruebas registradas en un intento requieren
  geolocalización.
- La suscripción agrega `UrlDestino` para identificar al receptor HTTP. El modelo no ejecuta
  peticiones ni firma mensajes.
- `EnviadaEn` y `UltimoIntentoEn` son nulos mientras no se ha enviado la notificación o no se
  ha intentado entregar el aviso.
- `TipoEscaneo` agrega Recepcion, Carga y Devolucion, conforme al uso descrito en las notas
  amarillas del diagrama de depósito.
- `TipoEvento.EventoAnomalo` permite representar Extraviado, conforme a la tabla de estados
  de la entrega inicial. Los recuadros rojos de dudas no se usan como requisitos.

## Comportamiento implementado

El alta requiere cuenta comercial, destinatario, al menos un bulto, zona, configuración,
tarifa, fechas, programación y responsable. La franja de la programación debe pertenecer
a la zona del envío, y sus horas y fecha deben coincidir con los extremos locales recibidos.
Crea el primer evento de admisión y fija las referencias de
configuración. Las reglas y el tarifario pasan a ser inmutables al usarse; una modificación
debe crear otra versión.

Las transiciones están declaradas en `Envios/TransicionesEnvio.cs`, siguiendo la tabla de
la entrega inicial. Se aplican desde métodos de Envio, Ruta y Devolucion, y cada cambio de
estado agrega su evento. Los estados Entregado, Devuelto y Extraviado no tienen salidas.

Asignar una parada verifica estado, operador, asignación activa, ventana horaria, máximo
de paradas y capacidad del vehículo. Registrar un intento verifica repartidor, motivo,
pruebas exigidas y límite de intentos. Un `IdOperacion` repetido devuelve el intento original
sin duplicar el efecto; reutilizarlo para otro resultado se rechaza.

Las devoluciones usan la política de la versión conservada en el envío. Su cierre exige
escanear todos los bultos. Se permite registrar un cierre tardío: la fecha límite queda
disponible para detectar el incumplimiento, no para impedir cerrar una devolución real.

## Programación y reprogramación

Envio conserva una ProgramacionEntrega actual. VentanaDesde, VentanaHasta y FranjaHoraria
se consultan desde ese valor, evitando mantener campos independientes que puedan contradecirse.
La ventana es un intervalo con inicio incluido y final excluido.

`Reprogramar(nuevaProgramacion, responsable, ocurridoEn)` valida la zona, que la ventana
no haya terminado y que permita respetar el plazo entre intentos. Conserva las reglas de
motivo reintentable y máximo de intentos. `Aplazar` también recibe una nueva programación
cuando se pospone una asignación antes de salir a reparto.

Ambos métodos actualizan la programación operativa y guardan ProgramacionAnterior y
ProgramacionNueva en el evento inmutable. La fecha comprometida original no se modifica,
de modo que los reportes puedan detectar un atraso aunque el reparto haya sido reprogramado.
El evento de admisión conserva además la programación inicial.

El plazo entre intentos se comprueba contra la llegada estimada y contra la hora del nuevo
intento, no contra la hora en que el despachador planifica la ruta. Se puede preparar una ruta
por adelantado sin autorizar una entrega antes del plazo. No se agrega la colección Envio.Paradas;
se mantiene la referencia a la parada activa ya existente.

## Escaneos por tipo

- `RegistrarRecepcion(bulto, responsable, fechaHora)` funciona sin ruta mientras el envío
  está Admitido. `RecibirEnDeposito` verifica que todos sus bultos tengan un escaneo de recepción
  registrado como máximo a la hora de confirmación, antes de cambiar el estado.
- `RegistrarCarga(ruta, bulto, responsable, fechaHora)` exige que el envío tenga una parada
  activa en esa ruta y que esta todavía no esté despachada ni rendida.
- `RegistrarDevolucion(devolucion, bulto, responsable, fechaHora, rutaOrigen)` exige que el
  bulto sea de la devolución activa. La ruta de origen es opcional; si se proporciona, debe
  contener el envío. El cierre utiliza únicamente los escaneos vinculados a esa devolución.

Las fábricas verifican operador, responsable activo, fechas y pertenencia antes de registrar
el escaneo en el bulto o en la ruta. Una validación fallida no agrega relaciones parciales.

## Alcance pendiente

Infrastructure ya configura todas las entidades, colecciones y relaciones mediante Fluent API.
La migración inicial y los índices únicos están generados para PostgreSQL.
En la instancia local de Docker se aplicó la migración y se
ejecutaron siete pruebas de integración reales. También se comprobó el modelo Npgsql,
la generación de SQL y la rehidratación en memoria. El alcance está en [Persistencia](persistence.md).

Las propiedades de identificadores relacionadas siguen derivándose de las navegaciones;
Infrastructure usa FK shadow para almacenarlas. Los constructores privados de rehidratación
(protegido en Usuario para su herencia) y los setters privados no permiten modificar el modelo
desde su API pública. Las colecciones se rellenan mediante sus campos privados. El último
intento se identifica por su número, sin depender del orden de carga de las filas.

Todavía faltan tokens de concurrencia, filtros de tenant, validación de escrituras y pruebas
específicas de aislamiento y concurrencia contra PostgreSQL real. Ya se probaron restricciones
de unicidad, referencias, borrado restringido, rollback y los principales flujos de persistencia.

También faltan los casos de uso, cálculo de tarifa y fecha comprometida desde la configuración,
resolución de la zona horaria del operador en Application, autorización por perfil en API, Identity,
outbox transaccional, sincronización persistente, webhooks, notificaciones reales, reportes
y liquidaciones. No todos esos requisitos corresponden a entidades dibujadas en estos UML.

Los eventos son inmutables desde la API de Domain; su inmutabilidad y consistencia en la
base de datos todavía necesitan protección adicional en Infrastructure. La migración incluye
índices únicos por envío e identificador de operación; falta probar específicamente las
carreras de idempotencia y tratar sus errores desde los casos de uso.

## Pruebas

Las pruebas xUnit están en `tests/Unit/UltimaMilla.Domain.Tests` y están incluidas en la
solución. Cubren relaciones, aislamiento del grafo, propiedad exclusiva, versiones,
asignaciones, transiciones, evidencia, idempotencia, devoluciones y avisos.

```powershell
dotnet test tests/Unit/UltimaMilla.Domain.Tests/UltimaMilla.Domain.Tests.csproj
```

Son pruebas unitarias. No sustituyen las pruebas de aislamiento, concurrencia y transacciones
sobre PostgreSQL ni acreditan por sí solas el opcional de cobertura superior al 70 %.
