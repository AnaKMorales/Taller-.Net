# Protección de los logins (hallazgo H6)

**Hallazgo:** el login no tenía límite de tasa, y el bloqueo de Identity (5 fallos por cuenta → 5 minutos) permitía dejar
afuera a un repartidor conociendo solo su correo: bastaba con equivocar la contraseña 5 veces cada 5 minutos.

**Alcance:** los tres puntos que validan contraseña — `POST /login` (Backoffice y portal), `POST /api/repartidor/sesion`
(PWA) y `POST /api/comercios/sesion` (API de comercios). Todo está en `src/UltimaMilla.Web/Seguridad/ProteccionLogin.cs`.

## Qué se hizo

| Capa | Regla | Para qué |
| --- | --- | --- |
| Límite de tasa | 20 POST por minuto por origen (configurable con `Login:IntentosPorMinuto`). Mostrar el formulario (GET) no cuenta. Al pasarse: **429** | Frena la prueba masiva de contraseñas desde un mismo origen |
| Bloqueo por **(correo, origen)** | 5 fallos para un correo desde un origen bloquean **ese origen para ese correo** 5 minutos. Un login correcto reinicia la cuenta | El atacante se bloquea a sí mismo; el repartidor sigue entrando desde su celular |
| Bloqueo de Identity por cuenta | Subió de 5 a **20** fallos → 5 minutos | Respaldo ante un ataque distribuido en muchas IPs |

- **Origen** = la IP del cliente (Web confía en `X-Forwarded-For` solo si viene de Traefik, `CabecerasReenviadas`). Las IPv6
  se agrupan por prefijo /64, porque un mismo cliente suele tener todo el prefijo y podría rotar direcciones.
- **Misma respuesta siempre:** correo inexistente, contraseña incorrecta, cuenta bloqueada u origen bloqueado devuelven el
  mismo mensaje (Web) o 401 (APIs). Solo el límite de tasa responde distinto (429), y no revela nada de la cuenta.
- Los fallos por (correo, origen) se guardan en memoria del proceso Web: el despliegue tiene una sola instancia. Con varias
  habría que llevarlos a Redis. Hay un tope de 100.000 registros para que un barrido de correos no agote la memoria.

## Límites conocidos

- Un atacante con **4 o más orígenes distintos** todavía puede llegar a los 20 fallos de Identity y bloquear la cuenta
  5 minutos. Es el costo de mantener un tope por cuenta contra la fuerza bruta distribuida; quitarlo dejaría la cuenta
  protegida solo por la política de contraseñas.
- Varios usuarios detrás de la misma IP (NAT de una oficina) comparten el límite de 20 por minuto. Para el tamaño del
  equipo de depósito alcanza; si no, se sube `Login:IntentosPorMinuto`.

## Pruebas

- `ProteccionLoginTests`: 5 fallos bloquean solo ese origen y ese correo; el dueño desde otra IP no queda bloqueado; se
  libera a los 5 minutos; un login correcto reinicia; IPv6 por /64; las dos APIs devuelven 429 al pasar el límite; un origen
  bloqueado ya no llega a validar la contraseña; el formulario (GET) no cuenta.
- `IdentidadTests`: por la Web, 5 fallos bloquean el origen (ni con la clave correcta entra, misma respuesta) sin bloquear
  la cuenta; 20 fallos sí bloquean la cuenta 5 minutos.
- `ApiComerciosTests`: el login de comercios corta con 429 en el intento 21 del minuto.
