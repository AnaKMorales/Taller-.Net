# Pruebas de carga (k6)

Requerimiento opcional "Pruebas de carga automatizadas" (sección 7.2 de la letra). Responsable: Ana Morales.

Plan con escenarios representativos (concurrencia, rampa y duración), objetivos de nivel de servicio definidos
**antes** de correr, y ejecución sobre el ambiente remoto (AWS). Incluye el escenario obligatorio de sincronización
simultánea de varios repartidores al regresar al depósito.

## Escenarios

| Escenario | Script | Qué simula | Carga |
| --- | --- | --- | --- |
| **A. Seguimiento público (capacidad)** | `seguimiento.js` | Destinatarios consultando su envío: 2/3 la API JSON y 1/3 la página Blazor | Llegadas: rampa de 5 a `TASA_MAX/2` pedidos/s (1 min), subida a `TASA_MAX` (2 min), sostenido (3 min), bajada (30 s). `TASA_MAX` por defecto 100 |
| **A'. Seguimiento público (protección)** | `seguimiento.js` con `MODO=proteccion` | Una IP que consulta sin parar | 2 pedidos/s durante 1 min contra el límite de 30 por minuto |
| **B. Regreso al depósito** | `sincronizacion-repartidores.js` | Cada usuario virtual es un dispositivo con la PWA: valida su sesión, baja sus hojas de ruta, sube la entrega registrada sin conexión y reporta su estado. Reenvía la misma entrega en cada ciclo, como con una conexión inestable | Rampa de 0 a `DISPOSITIVOS` (por defecto 20) en 30 s, 2 min sincronizando y reintentando cada 3 a 7 s, bajada en 20 s |

Por qué estos escenarios:

- **A** es el componente con más tráfico y el único público (sección 4): pone a prueba la caché en Redis
  (ADR-004), el renderizado estático (ADR-003) y la limitación de tasa.
- **B** es el pico de escritura más concentrado de la operación: toda la flota vuelve a la misma hora y sube lo
  que registró sin conexión. Mide el registro de intentos, el outbox y la idempotencia de la sincronización
  (ADR-005): el reenvío con el mismo `IdOperacion` no duplica la entrega, lo verifica el check
  "reenvío sin duplicar".

## Objetivos de nivel de servicio (SLO)

Definidos antes de correr; k6 los evalúa como `thresholds` y la corrida falla si no se cumplen.

| Métrica | Objetivo | Motivo |
| --- | --- | --- |
| Seguimiento, API: p95 / p99 | < 300 ms / < 800 ms | Lectura cacheada; el destinatario recarga seguido |
| Seguimiento, página: p95 / p99 | < 500 ms / < 1,2 s | HTML renderizado en el servidor, sin circuito |
| Seguimiento: pedidos fallidos | < 1 % | 404 no cuenta como falla (enlace mal copiado) |
| Protección: respuestas 429 | > 0 | El límite por IP tiene que actuar |
| Subida de entrega (`POST /intentos`): p95 / p99 | < 1,5 s / < 3 s | Escritura con evidencias (fotos de ~40 KB) |
| Hoja de ruta: p95 | lista < 500 ms, detalle < 800 ms | Descarga al iniciar la sincronización |
| Sincronización: pedidos fallidos | < 1 % | 429 de la telemetría no cuenta (límite por dispositivo) |
| Checks de la entrega | > 99 % | Incluye que el reenvío no duplique |

## Cómo correrlas

Requiere k6 (`winget install k6`) o Docker (imagen `grafana/k6:0.54.0`). Nada de URLs, cuentas ni claves queda en el
repositorio: todo va por variables de entorno.

### 1. Preparar el ambiente remoto

1. Levantar el ambiente (`terraform apply` y despliegue; ver `deploy/terraform/README.md`).
2. Sembrar datos. Cada ejecución de `deploy/aws/sembrar-demo.sh` crea dos operadores con un repartidor y una hoja de
   ruta despachada cada uno. Para tener varios repartidores, correrlo varias veces: las cuentas y la clave quedan en
   `~/demo-cuentas.txt`, fuera del repositorio.
3. **Solo para el escenario A (capacidad):** k6 sale de una sola IP, así que hay que subir temporalmente el límite del
   seguimiento: `SEGUIMIENTO_PEDIDOS_POR_MINUTO=100000` en `/opt/ultimamilla/deploy/.env` de la instancia y recrear web; volver a
   bajarlo al terminar. El escenario A' se corre con el límite normal (30).

### 2. Ejecutar

```bash
cd tests/carga
# Escenario A
k6 run -e BASE_URL=https://<dominio> -e TOKENS=<códigos de seguimiento separados por coma> -e TASA_MAX=100 seguimiento.js
# Escenario A'
k6 run -e BASE_URL=https://<dominio> -e TOKENS=<códigos> -e MODO=proteccion seguimiento.js
# Escenario B (consume las paradas: correr con datos recién sembrados y reiniciarlos después)
k6 run -e BASE_URL=https://<dominio> -e REPARTIDORES=<email:clave,email:clave,...> -e DISPOSITIVOS=20 sincronizacion-repartidores.js
```

Con Docker: `docker run --rm -v "$PWD:/scripts" grafana/k6:0.54.0 run -e ... /scripts/<script>.js`.

`RAPIDO=1` acorta las etapas a segundos para verificar los scripts antes de la corrida real.
Al terminar, reiniciar los datos con `deploy/aws/reiniciar-datos.sh` y destruir el ambiente si no se usa.

Durante la corrida conviene mirar el tablero técnico de Grafana (latencias, errores, profundidad de colas, outbox
pendiente y aciertos de caché): k6 mide desde afuera y el tablero muestra qué pasa adentro.

## Resultados

### Verificación local (11/11/2026)

Corrida corta (`RAPIDO=1`) contra la Web en la máquina de desarrollo (modo Development, PostgreSQL y Redis en
Docker), para validar los scripts. **No reemplaza la corrida sobre AWS**, que es la que pide la letra.

| Escenario | Carga | Resultado |
| --- | --- | --- |
| A, servidor recién iniciado | Pico de 40 pedidos/s | Mediana 6 ms, pero p95 ≈ 5 s: arranque en frío (compilación del modelo de EF y apertura de conexiones). **No cumple** |
| A, servidor caliente | Pico de 40 pedidos/s | API p95 29 ms, página p95 32 ms, 0 % de fallas. **Cumple** |
| B, fotos de 40 KB | 12 dispositivos, 6 entregas | 100 % de checks (entregas registradas y reenvíos sin duplicar), 0 % de fallas. Hoja de ruta p95 51 ms (lista) y 359 ms (detalle). **Subida p95 3,2 s: no cumple** |
| B, fotos de 1 KB | 12 dispositivos, 6 entregas | Subida p95 1,31 s (cumple), pero con un mínimo de 0,67 s incluso en los reenvíos |

### Hallazgos

1. **La página de seguimiento cargaba el envío completo en cada visita** cuando el envío estaba no entregado o
   entregado parcialmente, para calcular las fechas de reprogramación. Con 40 pedidos/s llevaba su p95 a más de
   20 s. Se corrigió: las opciones se calculan recién cuando el destinatario pide reprogramar. Después del cambio,
   la página quedó en p95 32 ms.
2. **Arranque en frío:** los primeros segundos después de iniciar la Web son lentos. En la corrida real, el primer
   minuto de rampa actúa como calentamiento; con varias instancias convendría que `/ready` se marque recién después
   de una consulta de calentamiento.
3. **La subida de una entrega cuesta entre 0,7 y 1 s como mínimo**, incluso en los reenvíos que no guardan nada.
   Cada pedido ejecuta 14 consultas SQL y, con varios dispositivos a la vez, algunas tardan entre 60 y 80 ms
   (esperas entre transacciones). El tamaño de las fotos suma: con 40 KB el p95 pasa de 1,3 s a 3,2 s. Queda para
   revisar en el registro de intentos (área de núcleo). Parte del costo local es el modo Development, que registra
   cada consulta SQL; hay que confirmarlo en AWS, que corre en Production.

### Corrida sobre AWS

14/11/2026, contra `https://3-139-241-162.sslip.io` (EC2 c7i-flex.large: 2 vCPU, 4 GB; modo Production). k6 corrió desde
Montevideo, así que unos 165 ms de cada respuesta son latencia de red hasta Ohio (us-east-2). Datos: tres ejecuciones de
`sembrar-demo.sh` (6 operadores, 6 repartidores, 18 códigos de seguimiento).

| Escenario | Carga | Resultado | SLO |
| --- | --- | --- | --- |
| A'. Protección | 2 pedidos/s desde una IP, 1 min | 121 pedidos: 67 respondieron 429. p95 190 ms | ✓ |
| B. Regreso al depósito | 6 cuentas, 20 dispositivos, 2 min 50 s | 2.236 pedidos, 0 % de fallas. 6 entregas registradas y 324/324 envíos de entrega correctos (los reenvíos no duplicaron). Subida p95 679 ms; hoja de ruta p95 179 ms (lista) y 199 ms (detalle) | ✓ |
| A. Capacidad, pico de 60 pedidos/s | Rampa, 6 min 30 s | 18.149 pedidos, 0 % de fallas. API p95 173 ms; página p95 175 ms | ✓ |
| A. Capacidad, pico de 100 pedidos/s | Rampa, 6 min 30 s | Llegó a unos 67 pedidos/s efectivos. p95 1,1 s y 9 % de fallas: al acercarse al pico, el servidor rechazó conexiones durante cerca de un minuto (`connection refused` en el 443). No hubo reinicios de contenedores ni falta de memoria (`docker ps` y `dmesg`) | ✗ |

Conclusiones:

- **La instancia sostiene unos 60 pedidos por segundo en el seguimiento cumpliendo los SLO.** Entre 60 y 100 se satura:
  con 2 vCPU no alcanza para abrir tantas conexiones HTTPS nuevas a la vez. Para más carga, el camino es escalar
  (más vCPU o más instancias detrás de un balanceador; el equipo de tres está eximido del balanceo).
- **La sincronización de repartidores cumple con margen.** En AWS la subida de una entrega tarda la cuarta parte que en la
  verificación local (p95 679 ms contra 3,2 s): confirma que gran parte del costo local era el modo Development, que
  registra cada consulta SQL.
- **La limitación de tasa funciona**: una IP que insiste recibe 429 a partir del pedido 31 del minuto.
