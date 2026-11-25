# Hallazgos de seguridad H2, H3 y H6

Respuesta a los hallazgos informativos del informe de revisión (anexo). Fecha: 25/11/2026.

## H2: página heredada `/Envios` sin `[Authorize]`

**Qué pasaba.** `Pages/Envios/Index` era la primera pantalla del proyecto, anterior al backoffice. No tenía
`[Authorize]` ni enlaces desde ninguna parte, y listaba nombre, teléfono y dirección de todos los envíos del operador
a cualquier rol autenticado (incluido el comercio).

**Qué se hizo.** Se borró la página. El backoffice ya muestra los envíos en el tablero y en el detalle de cada
envío, con `[Authorize(Policy = Politicas.Backoffice)]`, y el portal muestra solo los envíos de la cuenta del
comercio.

## H3: `deploy/.env` con contraseñas en la historia del repositorio

**Qué pasaba.** El commit `a38635f` (28/10) subió `deploy/.env` con `POSTGRES_PASSWORD` y `RABBITMQ_PASSWORD`.
El commit `999d22b`, del mismo día, lo borró y lo agregó a `.gitignore`, pero sigue en la historia.

**Alcance: las claves filtradas no dan acceso a nada.**
- Eran las del entorno local de desarrollo. En local, PostgreSQL y RabbitMQ escuchan solo en `127.0.0.1`
  (`deploy/compose.yaml`), así que desde otra máquina no se puede llegar a ellos aunque se conozca la clave.
- En AWS nunca se usaron: Terraform genera claves aleatorias propias (`random_password` en
  `deploy/terraform/modules/parametros/main.tf`) y el servidor las baja de SSM al desplegar. Además, ni PostgreSQL
  ni RabbitMQ publican puertos en la instancia: solo Traefik expone 80/443.

**Qué se hizo.**
- No se reescribió la historia con `git filter-repo`: obliga a los tres a volver a clonar y a forzar el push sobre
  `main`, y no agrega protección porque las claves no sirven fuera de cada máquina.
- Prevención: `.gitignore` excluye `.env` y `deploy/.env.*`, y solo se versionan los `*.example` sin secretos.
  Los secretos de AWS viven solo en SSM.

## H6: login sin límite de tasa y bloqueo que deja afuera a un usuario

**Qué pasaba.** El login de la Web y el de la PWA del repartidor no tenían límite de intentos (solo lo tenía la API
de comercios, con 10 por minuto). Y el bloqueo de Identity (5 fallos, 5 minutos) dejaba que cualquiera que supiera
el correo de un repartidor lo dejara sin poder entrar.

**Qué se hizo** (detalle, límites y pruebas en [protección de los logins](proteccion-login.md)):
- Límite de **20 intentos por minuto por IP** en los tres logins; del 21 en adelante responden 429.
- El bloqueo de 5 fallos pasó a ser por **(correo, IP)**: quien prueba contraseñas se bloquea a sí mismo y el
  repartidor sigue entrando desde su celular.
- El bloqueo por cuenta de Identity queda como respaldo, recién a los 20 fallos.
