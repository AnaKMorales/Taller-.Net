# Portal del comercio: devoluciones, cuenta corriente y liquidaciones

La letra (sección 4, portal) pide "gestión de devoluciones" y "consulta de su cuenta corriente y liquidaciones"
sin definirlas más. Esta es la interpretación elegida: simple, **de solo lectura** y **derivada de datos que el
sistema ya guarda**. No agrega tablas ni migraciones.

| Página | Qué muestra |
| --- | --- |
| `/portal/devoluciones` | Envío, destinatario, estado (En proceso / Completada), inicio, plazo límite, cierre, bultos y destino |
| `/portal/cuenta` | Envíos admitidos en un período: fecha, estado, tarifa, recargos e importe, con totales |
| `/portal/liquidaciones` | Por mes de un año: cantidad de envíos, tarifas, recargos, total y motivos de no entrega |

Código: puerto `IConsultasCuentaPortal` y cálculo puro `CuentaPortal` en `Application/Portal`; consultas en
`Infrastructure/Persistence/Backoffice/ConsultasCuentaPortal.cs`; páginas Blazor (SSR, como el resto del portal).

## Interpretación

- **Cuenta corriente = detalle de cargos.** Cada envío admitido en el período con lo que se le cobra al comercio:
  la **tarifa calculada al alta** (`Envio.TarifaCalculada`, que ya incluye recargos y bonificaciones del cuadro) más
  los **recargos de visita** persistidos (`RecargoVisita.Importe`, el recargo fijo por bultos pendientes). El sistema
  no registra pagos ni cobros, así que no hay saldo: no se inventan movimientos de pago.
- **Liquidación = resumen mensual** de esa misma cuenta, más los motivos de no entrega de los intentos fallidos.
- **Período por fecha de admisión.** Un envío y sus recargos cuentan en el período de su alta, aunque la visita con
  recargo sea posterior: así cada envío aparece en un solo período.
- **Corte de día y de mes en hora de Uruguay (UTC−03:00 fija)**, la misma constante que usa el reporte del operador
  (`ReportesModel.HoraUruguay`): Uruguay no tiene horario de verano y el sistema no guarda una zona horaria por
  operador. Las fechas de estas páginas también se muestran en esa hora.
- **Devoluciones: solo consulta.** Las inicia y completa el depósito del operador. El plazo límite lo fija la
  `PoliticaDevolucion` de la versión de reglas del envío. La política no tiene un campo de destino: la mercadería
  siempre vuelve al comercio, y así se muestra (con los días máximos en depósito). El dominio permitiría que un
  usuario de comercio inicie una devolución (por ejemplo cancelar un envío reprogramado), pero no se expone: sería una
  escritura nueva con sus permisos y concurrencia.

## Por qué un cambio de tarifas no altera lo liquidado (letra 6.6)

Los importes se **guardan como números** cuando ocurren: la tarifa en el alta (con la versión de `CuadroTarifario`
que la calculó) y el recargo al iniciar la visita (con el importe de la versión de reglas del envío). Las versiones
en uso son inmutables y un cambio crea una versión nueva, que solo afecta altas futuras. Estas consultas suman lo
guardado y nunca recalculan con el tarifario vigente. Lo comprueba
`PortalCuentaUnTarifarioNuevoNoAlteraLaLiquidacionPasada`.

## Aislamiento

Las consultas devuelven vacío fuera de una sesión de comercio (`CuentaRestringida`). Dentro, los filtros globales de
EF (`CuentaOwnership`) dejan solo `Envio`, `RecargoVisita`, `IntentoEntrega` y `Devolucion` de la cuenta comercial
de la sesión, y el filtro de operador hace el resto. Que el usuario pertenezca a esa cuenta lo valida
`TenantResolver` en cada pedido. Pruebas: `PortalCuentaCadaComercioVeSoloSusEnviosDevolucionesYLiquidaciones`
(dos comercios del mismo operador, otro operador y el backoffice) y `CuentaPortalTests` (cálculo y corte de mes).

## Diferencia con el reporte del operador

La liquidación por comercio de `/backoffice/reportes` suma solo `TarifaCalculada`; la del portal suma además los
recargos de visita. Si hubo recargos, el total del portal es mayor. Queda para alinear el reporte del operador
(decisión del equipo; no se tocó).

## Límites

- Sin saldo, pagos, facturas ni exportación. [NO VERIFICADO] que la letra no espere un saldo: no lo define.
- Hasta 366 días por consulta de cuenta corriente; la liquidación es por año calendario.
- Volumen: una fila por envío en memoria, como el reporte del operador. [NO VERIFICADO con volúmenes grandes.]
- Los textos de estado y los importes usan los mismos formatos que el resto del portal (`Textos`).
