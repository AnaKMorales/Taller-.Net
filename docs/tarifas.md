# Calculador de tarifas

## Alcance

Se implementó el cálculo aislado de una tarifa usando el cuadro ya obtenido por el servidor.
No consulta la base ni modifica configuraciones, envíos, caché o formularios. No requiere
Docker ni una sesión autenticada para probar su aritmética. No cambia el esquema de datos.

La consigna exige calcular la tarifa al admitir un envío con el tarifario vigente y contemplar
zona, peso, volumen, modalidad, recargos y bonificaciones. No especifica la fórmula de
combinación de porcentajes ni el redondeo. Las siguientes convenciones son decisiones
explícitas de esta implementación, revisables por el equipo.

## Reglas aplicadas

- Se suman los pesos de todos los bultos en kg y sus volúmenes en m³. Las dimensiones
  de entrada están en cm: alto × ancho × largo / 1.000.000, como define Bulto.
- Se filtran las líneas por zona y modalidad. El peso pertenece a [PesoDesde, PesoHasta):
  incluye el inferior y excluye el superior. El volumen total debe ser menor o igual a
  VolumenHasta. Se utiliza el precio fijo de la línea; no se multiplica por cada kg o bulto.
- Ninguna coincidencia produce un error; más de una también. No se inventa un precio
  por defecto ni se elige arbitrariamente la primera línea de una configuración ambigua.
- Cada ajuste porcentual se calcula sobre el precio base. Se suman los recargos y se
  restan las bonificaciones. No se aplican porcentajes de forma compuesta ni según su orden.
- Se usa decimal. El total se redondea una vez a dos decimales con MidpointRounding.AwayFromZero.
  El precio base y los importes del desglose conservan su precisión previa al redondeo.
- Una bonificación individual no puede superar 100%. Si la combinación de ajustes deja
  un total negativo, se rechaza la configuración; un total cero es válido.

Ejemplo: base 100, recargos de 10% y 20%, bonificación de 5% → total 125.

## Componentes

- **Domain/Configuracion/CalculadoraTarifa:** política pura que selecciona entre los tramos
  ya filtrados y aplica los ajustes. Sus valores TramoCalculoTarifa, AjusteCalculoTarifa y
  DesgloseTarifa no son entidades ni tienen mapeos EF.
- **Application/Tarifas/ICalculadorTarifa:** contrato para los casos de uso.
- **Application/Tarifas/CalculadorTarifa:** adapta TarifarioDto sin cambiar ese contrato
  existente, valida BultoAlta con las reglas de Bulto, suma medidas y delega el cálculo a Domain.
- **TarifaCalculada:** devuelve CuadroId y Version para identificar la configuración,
  PesoTotalKg, VolumenTotalM3 y Desglose con PrecioBase, Recargos, Bonificaciones y Total.

Los bultos temporales del cálculo no se vinculan a un envío ni se agregan a un contexto EF.
El cálculo no cambia las colecciones del DTO, no marca versiones como utilizadas y no guarda nada.

## Integración pendiente del alta

El servidor debe obtener el cuadro mediante ITarifarioVigente para el operador de su contexto
autorizado. Puede utilizar la implementación cacheada existente sin modificarla. El calculador
recibe ese DTO; no resuelve el tenant ni comprueba por sí solo que el cuadro esté vigente.
No se debe aceptar un tarifario ni un precio calculado enviado por el navegador.

Uso desde el futuro caso de uso (tarifarios es ITarifarioVigente y calculador es ICalculadorTarifa):

```csharp
var cuadro = await tarifarios.ObtenerAsync(tenant.OperadorId, cancellationToken)
    ?? throw new InvalidOperationException("El operador no tiene tarifario vigente.");
var calculada = calculador.Calcular(cuadro, zonaId, modalidad, bultos);
var precio = calculada.Desglose.Total;
```

Para la composición del host se puede registrar:

```csharp
services.AddSingleton<ICalculadorTarifa, CalculadorTarifa>();
```

No se agregó ese registro ni se modificó Web en esta etapa. Al conectar el alta, el caso de uso
debe emplear el mismo CuadroId calculado y preparar las fechas de entrega en el servidor.
CrearEnvioHandler sigue recibiendo PreparacionAltaEnvio; no invoca automáticamente este
calculador. La interfaz y la caché quedan disponibles para su integración por el equipo.

## Límites del modelo actual

El UML y el modelo acordado aportan un límite superior de volumen por línea. No hay bandas
de volumen y se prohíben rangos de peso superpuestos para una misma zona/modalidad. Por eso,
el cálculo actual valida el volumen máximo, pero no ofrece distintos precios solo por volumen
dentro de un mismo rango de peso. Si se necesitan esas bandas para completar la variación de
precio exigida por la consigna, habrá que acordar la extensión del modelo y su configuración.
No se cambió ese diseño como parte de este paso aislado.

La moneda, impuestos y reglas adicionales de recargos condicionados tampoco se agregan:
se aplican los porcentajes que ya contiene la línea seleccionada. Un envío admitido conserva
su tarifa guardada; este servicio no recalcula importes históricos.

## Pruebas

Se agregaron 14 casos de prueba en Domain y cinco en el nuevo proyecto de pruebas unitarias
de Application. Cubren límites de peso y volumen, rangos ambiguos, porcentajes, independencia
del orden de ajustes, redondeo final, bonificaciones, sumas de varios bultos, unidades, zona,
modalidad y versión del DTO. No necesitan servicios externos.

```powershell
dotnet test tests/Unit/UltimaMilla.Domain.Tests/UltimaMilla.Domain.Tests.csproj
dotnet test tests/Unit/UltimaMilla.Application.Tests/UltimaMilla.Application.Tests.csproj
```
