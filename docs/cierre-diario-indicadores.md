# Cierre diario y recálculo de indicadores

Implementado el 15/11/2026 para la letra original, sección 4, «Servicio de procesamiento
en segundo plano» (pág. 3). La letra pide recálculo y cierre diario sin definir un cierre
contable. Se adopta un cierre estadístico por operador y día. No finaliza automáticamente
rutas, devoluciones, pagos ni liquidaciones: esas operaciones conservan sus controles.

## Funcionamiento y significado de las fechas

`CierreOperativoWorker`, un `BackgroundService` de .NET, ejecuta un ciclo al arrancar
y luego cada cinco minutos. Procesa operadores por separado y utiliza PostgreSQL con
EF Core. Guarda una fila por operador y día en `ResumenesOperativosDiarios`.

El calendario usa UTC−03:00, igual que el criterio local de los reportes de Uruguay.
Las marcas de ejecución se guardan en UTC. Cada fila contiene:

- **Actual:** último cálculo confirmado, con fecha de actualización y contador de recálculos.
- **AlCierre:** copia del primer cálculo confirmado cuando el día ya terminó.
- **CerradoEn:** instante real de ese primer cierre; permanece sin cambios.
- **VersionCalculo:** versión del formato y criterio de cálculo, actualmente 1.

El cierre se toma en la primera ejecución posterior al día, que puede ocurrir minutos
o días después si el Worker estuvo detenido. **No reconstruye el estado exacto de la
medianoche.** Si una entrega offline llega después del cierre, Actual puede cambiar
y AlCierre conserva lo que se conocía al ejecutar el primer cierre.

## Qué se calcula

Se reutiliza `ReportesGestion`: volumen, entrega, cumplimiento del compromiso, primer
intento, intentos fallidos, agrupaciones por zona/repartidor/cuenta comercial, motivos
e importes según las fórmulas actuales. La población son los **envíos admitidos ese
día**, aunque sus intentos ocurran más tarde; no son todas las entregas realizadas ese día.
Una entrega parcial no cuenta como una entrega completa en estas tasas.

Además se cuentan las rutas despachadas ese día que siguen sin rendición, y las
devoluciones iniciadas ese día que siguen abiertas, al momento del cálculo. No representan
el total acumulado de pendientes de todos los días. El resumen de importes reutiliza la
tarifa del reporte existente y no reemplaza la contabilidad ni incorpora nuevas fórmulas
de recargos. La pantalla de Reportes sigue consultando datos en vivo; la lectura de los
resúmenes guardados se ofrece mediante la API descrita abajo, sin pantalla nueva.

## Recuperación, concurrencia y aislamiento

Cada ciclo revisa hoy y ayer, y rota las otras fechas desde la primera admisión por
antigüedad de actualización. Procesa hasta 31 días por operador y ciclo (configurable
entre 3 y 366). Así recupera cierres atrasados y vuelve a revisar días antiguos por datos
offline tardíos sin limitarse permanentemente a los últimos 31 días. La recuperación de
un historial grande puede requerir varios ciclos; también se generan días vacíos.

Cada día se lee y guarda dentro de una transacción Repeatable Read. Un bloqueo asesor
transaccional de PostgreSQL por operador/fecha evita que dos instancias procesen ese día
a la vez; la segunda lo omite y vuelve a intentarlo en otro ciclo. Un índice único protege
la pareja operador/fecha. Repetir el cálculo actualiza la misma fila y su contador, sin
crear otro primer cierre. Un fallo revierte ese día; los días ya confirmados se conservan.

La enumeración interna obtiene únicamente los identificadores de operadores. Después
cada operador usa contextos nuevos con su ámbito y los filtros/validaciones de tenancy
existentes. Un error de un operador se registra y no impide continuar con los demás.
El proceso no emite eventos de envío ni mensajes de outbox por actualizar estadísticas.

## Configuración y uso

Aplicar la migración nueva antes de iniciar el Worker, con la conexión local configurada:

```powershell
.\scripts\Start-Web.ps1
```

Ese script aplica las migraciones y arranca Web. El Worker debe ejecutarse aparte con
su configuración habitual de PostgreSQL y RabbitMQ:

```powershell
dotnet run --project src/UltimaMilla.Worker -c Release
```

La configuración `CierreOperativo` de `appsettings.json` del Worker contiene
`Intervalo: "00:05:00"` y `MaximoDiasPorOperador: 31`. Se pueden sobrescribir mediante
`CierreOperativo__Intervalo` y `CierreOperativo__MaximoDiasPorOperador`.

Con una sesión de Administrador o Despachador, consultar:

```text
GET /api/operacion/cierres?desde=2026-11-14&hasta=2026-11-15
```

El período es inclusivo y admite entre 1 y 366 días. El operador se obtiene de la sesión;
no se recibe como parámetro. Comercio, Repartidor y usuarios anónimos no tienen acceso.
La respuesta contiene solo las fechas ya calculadas: si el Worker todavía no ejecutó
el proceso para ese operador, devuelve una lista vacía.

La migración `20261115222840_ResumenesOperativosDiarios` agrega solo la tabla de lectura,
índices y restricciones, sin recalcular ni cerrar datos durante su aplicación.

## Verificación

Pruebas unitarias de calendario y ciclo del Worker; integración con PostgreSQL real de
datos tardíos, primer cierre conservado, aislamiento/permisos de API, bloqueo entre
instancias, reejecución sin cierres duplicados y recuperación tras fallo de guardado.
El ensayo final con todos los servicios desplegados sigue siendo una verificación aparte.
