# Pruebas de carga

Requerimiento opcional "Pruebas de carga automatizadas" (sección 7.2). Responsable: Ana Morales.

## Objetivo

Medir cómo responde el sistema desplegado en AWS ante carga real en los dos puntos más exigidos de la operación:

- **El seguimiento público**, que es el componente con más tráfico y el único sin autenticación.
- **La sincronización de los repartidores al volver al depósito**, el pico de escritura más concentrado del día
  (escenario que la letra pide explícitamente).

## Herramienta

**k6** (Grafana). Los escenarios se escriben en JavaScript, la carga se describe como etapas (rampa, pico, sostenido) y los
objetivos de servicio se declaran como `thresholds`: si no se cumplen, la corrida falla sola. Se ejecutó con la imagen
oficial de Docker (`grafana/k6:0.54.0`), sin instalar nada.

Los scripts están en `tests/carga/` y se parametrizan por variables de entorno (URL, cuentas, códigos de seguimiento), así
que no hay datos del ambiente en el repositorio.

## Escenarios

| Escenario | Qué simula | Carga |
| --- | --- | --- |
| **A. Capacidad del seguimiento** | Destinatarios consultando su envío: 2/3 por la API y 1/3 por la página Blazor | Llegadas por segundo en rampa: subida (1 min), pico (2 min), sostenido (3 min) y bajada (30 s) |
| **A'. Protección del seguimiento** | Una IP que consulta sin parar | 2 pedidos/s durante 1 min, contra el límite de 30 por minuto |
| **B. Regreso al depósito** | Cada usuario virtual es un dispositivo con la PWA: valida la sesión, baja la hoja de ruta, sube la entrega registrada sin conexión y reporta su estado. Reenvía la misma entrega en cada ciclo, como pasa con una conexión inestable | Rampa a 20 dispositivos en 30 s, 2 min sincronizando y reintentando |

En el escenario B, cada parada pendiente la usa un solo dispositivo, para que no se pisen, y un check verifica que el reenvío
de la misma entrega (mismo `IdOperacion`) no la duplica.

## Objetivos de servicio (SLO)

Se definieron antes de correr las pruebas.

| Métrica | Objetivo |
| --- | --- |
| Seguimiento por API: p95 / p99 | < 300 ms / < 800 ms |
| Seguimiento, página: p95 / p99 | < 500 ms / < 1,2 s |
| Seguimiento: pedidos fallidos | < 1 % (un 404 no cuenta: es un enlace mal copiado) |
| Protección: respuestas 429 | Tiene que haber al menos una |
| Subida de una entrega: p95 / p99 | < 1,5 s / < 3 s |
| Hoja de ruta: p95 | Lista < 500 ms, detalle < 800 ms |
| Sincronización: pedidos fallidos | < 1 % |
| Checks de la entrega (incluye "no duplica") | > 99 % |

## Ejecución en AWS

Fecha: 14/11/2026. Ambiente: una EC2 c7i-flex.large (2 vCPU, 4 GB) en us-east-2, con todo el sistema en Docker Compose y la
Web en modo Production. URL: `https://3-139-241-162.sslip.io`. k6 corrió desde una máquina en Montevideo, así que unos 165 ms
de cada respuesta corresponden a la latencia de red.

Pasos:

1. Se desplegó la versión vigente con Terraform y el script de despliegue.
2. Se cargaron datos con `deploy/aws/sembrar-demo.sh` tres veces: 6 operadores, 6 repartidores con una hoja de ruta
   despachada cada uno y 18 códigos de seguimiento.
3. Se corrió A' con el límite normal del seguimiento (30 pedidos por minuto por IP).
4. Se corrió B.
5. Para A se subió temporalmente el límite (`SEGUIMIENTO_PEDIDOS_POR_MINUTO`), porque k6 sale de una sola IP y el limitador
   habría cortado la prueba. Se corrió con un pico de 100 pedidos/s y después con uno de 60. Al terminar se volvió a bajar.

## Resultados

| Escenario | Resultado | SLO |
| --- | --- | --- |
| A'. Protección | 121 pedidos: 67 recibieron 429. p95 190 ms | ✓ |
| B. Regreso al depósito | 2.236 pedidos, 0 % de fallas. 6 entregas registradas; 324/324 envíos correctos, sin duplicados. Subida p95 679 ms; hoja de ruta p95 179 ms (lista) y 199 ms (detalle) | ✓ |
| A. Capacidad, pico de 60 pedidos/s | 18.149 pedidos, 0 % de fallas. API p95 173 ms, página p95 175 ms | ✓ |
| A. Capacidad, pico de 100 pedidos/s | Llegó a unos 67 pedidos/s. p95 1,1 s y 9 % de fallas: cerca del pico, el servidor rechazó conexiones durante un minuto. No hubo reinicios de contenedores ni falta de memoria | ✗ |

## Análisis

- **Capacidad:** la instancia sostiene unos **60 pedidos por segundo** en el seguimiento dentro de los objetivos. Entre 60 y
  100 se satura la CPU (2 vCPU atendiendo cientos de conexiones HTTPS nuevas). Se descartó un problema de memoria revisando
  el estado de los contenedores y el log del sistema. Para más carga hay que escalar: más vCPU o varias instancias detrás de
  un balanceador (el equipo de tres está eximido del balanceo).
- **Sincronización:** cumple con margen. Los reenvíos no duplicaron ninguna entrega, lo que confirma la idempotencia de la
  sincronización en condiciones de concurrencia.
- **Protección:** el limitador por IP corta a partir del pedido 31 del minuto, como está configurado.

## Mejoras que salieron de las pruebas

Antes de AWS se hizo una verificación local para validar los scripts. Encontró dos problemas:

1. **La página de seguimiento cargaba el envío completo en cada visita** (para ofrecer la reprogramación), aunque el
   destinatario no fuera a reprogramar. Con 40 pedidos/s el p95 pasaba de 0,5 s a más de 20 s. Se cambió para cargar las
   opciones solo cuando el destinatario las pide; después del cambio, el p95 local quedó en 32 ms.
2. **La subida de una entrega tardaba entre 0,7 y 1 s en local** (p95 3,2 s). En AWS el mismo escenario dio un p95 de
   679 ms: buena parte del costo local era el modo Development, que registra cada consulta SQL.

## Limitaciones

- El generador de carga fue una sola máquina, desde una sola IP. Por eso hubo que subir el límite del seguimiento para medir
  capacidad, y la latencia de red se suma a cada respuesta.
- Grafana no está desplegado en AWS: la evidencia es la salida de k6. En local, durante las pruebas se puede seguir el
  tablero técnico.
- El escenario B consume datos (registra entregas reales): hay que correrlo con datos recién sembrados.

## Cómo repetirlo

Los comandos y la preparación del ambiente están en `tests/carga/README.md`.
