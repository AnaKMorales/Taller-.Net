# Visitas de bultos pendientes y capacidad

Avance del requisito 34 del cambio de requerimientos. Fecha: 03/11/2026.
Implementado en Domain, mapeos y persistencia, sin modificar Web, PWA ni Worker.

## Flujo del núcleo

Tras una entrega parcial, los entregados conservan su estado y los fallidos quedan
NoEntregado. Para preparar otra visita se llama a Reprogramar con una programación nueva.
Se mantienen la fecha comprometida original, los límites de intentos, el plazo entre ellos
y la exigencia de motivos reintentables. Las reglas son las de la versión del envío.

```csharp
envio.Reprogramar(nuevaProgramacion, responsable, ahoraUtc);
ruta.AgregarParada(envio, codigo, llegadaEstimadaUtc, responsable, ahoraUtc);
```

AgregarParada conserva su firma y elige automáticamente todos los pendientes preparados
en depósito o reprogramados. Excluye Entregado, Devuelto y Extraviado. No se divide un mismo
envío entre varias paradas activas: se conserva la restricción de una asignación activa por envío.
No se añade una colección de paradas históricas a Envio; las conserva cada Ruta como antes.

Se admiten NuevaVisitaSinRecargo y NuevaVisitaConRecargo para la continuación parcial.
La segunda captura el importe fijo versionado en la parada y lo registra al despachar,
sin modificar la tarifa original. DevolverAlComercio impide reprogramar y permite devolver
solo los pendientes. Ver [políticas, carga y migración](politicas-pendientes.md).
Los flujos anteriores de envíos completos conservan sus reglas de reintento.

## Composición y medidas

Parada.Bultos contiene BultoParada: relación con el bulto y copia de PesoKg/VolumenM3
al crear la parada. El peso y volumen de la parada suman esa composición, no el envío
completo. Al agregar otra parada se suma la carga de las paradas activas de la ruta.
Así, un envío de 13 kg con solo un pendiente de 1 kg puede continuar en un vehículo de 1 kg.

Las medidas usan kg y m³; las dimensiones originales del bulto siguen expresadas en cm.
La composición y las medidas de una parada persistida son históricas: SaveChanges rechaza
modificarlas, borrarlas o agregar miembros después. Retirar o aplazar una parada la desactiva,
pero conserva sus miembros y no los vuelve a contar en la capacidad activa.
SQL directo puede evitar esta protección de EF; las FK y CHECK siguen aplicándose.

El escaneo de carga exige que el bulto pertenezca a la parada activa y esté AsignadoARuta.
No se puede volver a cargar un entregado porque otro bulto del envío tenga asignación.

## Estados, intentos y eventos

Al reprogramar, asignar y despachar se cambian solo los pendientes. Si ya había entregados,
el resumen sigue EntregadoParcialmente mientras los restantes avanzan por su ciclo.
RegistrarIntentoPorBulto y RegistrarIntentoHandler aceptan todos y solo los bultos de la
parada actual. La firma tradicional de resultado completo se rechaza para estas visitas:
no debe tocar entregados previos. El último pendiente entregado lleva el envío a Entregado.
Las visitas sucesivas pueden reducir de nuevo la selección si vuelven a entregar solo una parte.

Se conserva la parada de origen y la huella para los reenvíos. Una nueva visita puede tener
otro repartidor; el anterior no puede registrarla. El original puede reconocer su intento
histórico y la respuesta distingue su resultado del estado actual del envío.

Los eventos de continuación parcial también se registran como envio.evento.v2, para no
introducir el estado parcial en V1. En EventoEnvioV2, IdOperacion ahora es nullable:
los eventos administrativos (reprogramación, asignación, despacho, etc.) no son intentos,
llevan IdOperacion null y Bultos vacío. El resumen de la transición conserva su instante;
no se fabrican resultados por bulto desde sus estados posteriores.
Los eventos de intento mantienen IdOperacion y detalles de los bultos de esa visita.
Los receptores V2 deben contemplar esta distinción; V1 mantiene su formato y flujos previos.

## Persistencia, carga y migración

BultosParada tiene unicidad por parada/bulto y FK compuestas que comparten EnvioId y TenantId
con ambos padres. No admite referencias de otro envío, cuenta u operador. Sus medidas deben
ser positivas. Se agregaron filtros de cuenta/operador y concurrencia por Envio y Ruta.
Dos asignaciones simultáneas no dejan miembros ni mensajes de la operación perdedora.

Al operar sobre rutas hay que cargar Paradas.Bultos.Bulto y Paradas.Recargo además de los agregados necesarios.
Al registrar un intento se carga Parada.Bultos.Bulto; el repositorio del caso de uso ya lo hace.
Calcular capacidad con una composición sin cargar produce una excepción, no una suma cero.

La migración 20261103121131_BultosPorParada agrega la tabla y claves. En el modelo anterior
cada parada incluía el envío completo, así que se inicializan sus miembros con todos los
bultos del mismo envío/operador, incluso si después fueron entregados. Las medidas se copian
desde los valores actuales al migrar: no es posible recuperar medidas originales de cada
visita porque antes no se almacenaban. No se crean eventos ni se recalculan tarifas.
La migración se probó en bases temporales, sin aplicarla a desarrollo en esta sesión.

Continúan pendientes la API de asignación/hoja de ruta y
conexiones de interfaz y sincronización de sus responsables.
