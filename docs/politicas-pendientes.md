# Política de pendientes y recargo fijo

Requisito 33 del cambio del 30/10, conectado con 31 y 34. Fecha: 03/11/2026.
Implementado en Domain, configuración administrativa y persistencia.

## Decisión y funcionamiento

Cada envío conserva su versión de ReglasOperativas. Tras una entrega parcial:

- NuevaVisitaSinRecargo permite reprogramar los pendientes sin generar un cargo.
- NuevaVisitaConRecargo permite otra visita y usa ImporteRecargoNuevaVisita, un importe
  fijo positivo con hasta dos decimales, en la misma moneda que la tarifa del sistema.
- DevolverAlComercio impide reprogramar y permite iniciar la devolución de los pendientes,
  aunque el motivo sea reintentable y todavía queden intentos.

Los reintentos siguen respetando máximo de intentos, plazo y motivos reintentables.
El agotamiento de intentos o un motivo no reintentable también pueden habilitar la
devolución según las reglas existentes. Los bultos Entregado, Devuelto y Extraviado
quedan fuera de las nuevas visitas y devoluciones.

El importe fijo fue elegido por el equipo. No es un porcentaje ni se multiplica por la
cantidad de bultos. Se captura en Parada.RecargoPrevisto al asignar la visita de pendientes.
Se registra como RecargoVisita al despachar esa ruta, una sola vez por parada. Planificar,
retirar y reasignar antes del despacho no devenga cargos. La primera visita no lleva este
recargo; tampoco cambia el tratamiento previo de reintentos de envíos sin entrega parcial.

Envio.Recargos conserva los importes históricos separados de TarifaCalculada, que mantiene
la tarifa original. El registro representa un cargo devengado, no un pago realizado ni
una integración de facturación. Cambiar las reglas crea una versión nueva y no modifica
el importe de los envíos anteriores.

## Devolución parcial

Devolucion.Bultos contiene BultoDevolucion, la composición capturada al iniciar el proceso.
Solo incluye pendientes del mismo envío. El cierre exige escanear todos esos bultos y
no exige volver a escanear los entregados. Una mezcla de Entregado y Devuelto conserva
el resumen EntregadoParcialmente; el detalle permite distinguir qué ocurrió con cada uno.
No se crea una segunda devolución del mismo envío ni se reabren bultos ya cerrados.

## Configuración e integración

DatosReglasDto incorpora el campo opcional decimal? ImporteRecargoNuevaVisita. Para crear
una versión con NuevaVisitaConRecargo es obligatorio proporcionar un valor positivo con
hasta dos decimales. Con las otras acciones debe enviarse null. ObtenerReglasAsync devuelve
el importe y CrearReglasAsync valida y guarda la versión completa. El formulario debe
reenviar la política y su importe al conservarlos; todavía no se modificó la interfaz.

Para despachar hay que cargar Envio con sus bultos, reglas, eventos y parada;
Parada.Recargo y Ruta.Paradas.Bultos.Bulto. Para consultar históricos: Envio.Recargos.Parada.
Para cerrar una devolución: Devolucion.Bultos.Bulto y los escaneos de esos bultos.
Guardar con el mismo contexto los cambios de envío y ruta mantiene la operación atómica.

## Persistencia y migración

20261103123758_PoliticasPendientesYRecargoFijo agrega el importe de reglas, el previsto
por parada, BultosDevolucion y RecargosVisita. Las FK comparten envío y operador, los
filtros respetan cuenta comercial y operador, y los cambios participan de las versiones
de Envio y Ruta. Un fallo del outbox o un conflicto revierte también el cargo y el despacho.
Un índice único impide dos cargos para la misma parada.

SaveChanges protege la composición de devolución, los cargos históricos y el importe
previsto. SQL directo puede saltar esas validaciones; siguen vigentes sus FK, unicidad y
restricciones de importe. No se trata de una auditoría inalterable frente a SQL administrativo.

Las devoluciones anteriores incluían todo el envío: la migración recupera esa composición.
Las paradas previas quedan con recargo previsto cero y no se inventan cargos históricos.
Las reglas anteriores con política de recargo quedan con importe null: se pueden leer,
pero no permiten una nueva visita con recargo sin importe. Debe definirse una nueva versión
para envíos nuevos; no se cambia retroactivamente la versión en uso ni se inventa un precio.
Los envíos antiguos afectados requieren una decisión explícita de negocio para continuar.

La migración se verifica en bases temporales. No se aplicó a la base de desarrollo.
Continúan pendientes los endpoints de asignación/despacho/devolución y las integraciones
de pantallas, PWA y Worker de sus responsables. El ADR-007 sigue en curso hasta la integración.
