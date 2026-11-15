# Separación de comercios en reportes de gestión

Corresponde a la letra original, sección 5.5, requisito 30 (pág. 4 impresa).

`ReportesGestion` obtiene los envíos del período dentro del ámbito del contexto existente.
Para cumplimiento por comercio y liquidación agrupa por `CuentaComercialId`, la identidad
de la relación comercial con el operador. La razón social local se usa solo para mostrar
el nombre. Antes se agrupaba por ese texto y dos cuentas con igual nombre se sumaban
como si fueran un solo cliente.

Por ejemplo, dos cuentas llamadas «Comercio» con importes de 200,25 y 200 producen dos
filas y un total de 400,25. Cada fila conserva su cantidad de envíos e indicadores de
entrega. Un cambio de nombre no une cuentas distintas. La baja lógica tampoco elimina
los envíos históricos del reporte.

## Consumo y límites

- Se mantienen los DTOs `Cumplimiento`, `LiquidacionComercio` y `ReporteGestion`.
  Sus filas pueden tener nombres iguales: deben mostrarse por separado y no reagruparse
  por el nombre. Las pantallas actuales recorren las filas sin esa reagrupación.
- El identificador de cuenta se usa internamente; no se agregó a la respuesta. Si una
  futura pantalla necesita abrir una cuenta desde el reporte, deberá acordarse esa
  ampliación del contrato con su responsable.
- No cambia el período por fecha de admisión y desfase horario, los filtros de ámbito,
  las agrupaciones por zona/repartidor ni la suma de `TarifaCalculada` del reporte actual.
  No incorpora recargos, pagos o saldos a esa fórmula.
- Es una corrección de la consulta existente, sin migración. No implementa ni modifica
  el módulo de cuenta corriente/liquidaciones que trabajan las compañeras.

## Verificación

Dos regresiones contra PostgreSQL reproducen la fusión de cuentas antes del cambio y
comprueban su separación después: nombres iguales, renombrado, baja lógica, importes,
cumplimiento, período vacío y aislamiento entre operadores. También pasa la prueba previa
del reporte de gestión. No sustituye el ensayo funcional completo del equipo.
