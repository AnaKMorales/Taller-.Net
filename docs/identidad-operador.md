# Identidad visual y contacto del operador (A2)

Backend de la letra original, 5.1 requisito 5 y 6.5. Implementado el 14/11/2026.
Permite guardar marca, colores, contacto público y una URL opcional de logo por operador.
La presentación en portal/seguimiento y las comunicaciones aún deben integrarse por
Ana y Aldana. No se da por cerrado el requisito completo solo por disponer del backend.

## API y permisos

| Método | Dirección | Acceso y resultado |
| --- | --- | --- |
| GET | `/api/administracion/identidad` | Administrador activo: `{datos, version}`. |
| PUT | `/api/administracion/identidad` | Administrador activo, con cookie/Bearer válido y antiforgery: guarda y devuelve `{datos, version}`. |
| GET | `/api/operador/identidad` | Usuario autenticado y revalidado: datos del operador de su sesión, sin versión. Comercio requiere cuenta activa y vínculo vigente. |
| GET | `/api/seguimiento/{token}/identidad` | Público: datos de marca del operador del envío identificado por el token opaco. |

El PUT requiere `X-CSRF-TOKEN`, obtenido mediante `/api/antiforgery` en la sesión
administrativa. No acepta operador/cuenta/usuario como ámbito desde el cuerpo o la URL.
La autorización se revalida contra usuario/rol persistidos. El rol Comercio puede leer
la marca de su operador pero no modificarla ni acceder a la administración.

```json
{
  "datos": {
    "marca": "Mi operador",
    "colorPrimario": "#1E40AF",
    "colorSecundario": "#0F172A",
    "telefonoContacto": "099123456",
    "emailContacto": "contacto@operador.example",
    "logoUrl": "https://operador.example/logo.png"
  },
  "versionEsperada": "<version del GET administrativo>"
}
```

Marca obligatoria hasta 200 caracteres; colores obligatorios de seis dígitos hexadecimales
`#RRGGBB` (se guardan en mayúsculas). Teléfono opcional hasta 50 caracteres; email opcional
válido, sin nombre de presentación, hasta 254 y normalizado a minúsculas. Logo opcional:
URL HTTPS absoluta hasta 2048 caracteres, sin credenciales. Opcionales vacíos se guardan
como null. Se recortan espacios. La validación completa ocurre antes de cambiar el estado.

Se guarda solo la URL del logo: no se descarga, valida existencia o almacena la imagen,
ni hay un endpoint de subida. La marca/contacto son datos destinados a publicación:
no colocar allí secretos o información privada. La interfaz debe renderizarlos como texto
codificado, no como HTML, y usar los colores validados. Debe decidir accesibilidad y contraste.

Respuestas: 200 consulta/actualización; 400 datos o antiforgery inválidos; 401 sin sesión;
403 rol/contexto no permitido; 409 versión desactualizada o conflicto concurrente.
La consulta pública devuelve 404 tanto para token malformado como inexistente y usa
la limitación de tasa compartida del seguimiento (429 al excederla).

## Lecturas y caché

`IdentidadOperadorDto` contiene únicamente los seis campos de `datos`; la respuesta pública
no lleva IDs de operador, cuenta, envío, usuario, versión, razón social legal o destinatario.
La búsqueda por token proyecta solo los datos del operador asociado a ese envío. No existe
un catálogo público por ID de operador. Las rutas de marca devuelven `Cache-Control: no-store`.

La marca se consulta por separado del estado del envío. No cambia el contrato existente
`SeguimientoPublicoDto`: su campo `Operador` conserva la razón social anterior. Por ello,
la interfaz deberá usar la nueva consulta para mostrar la marca. No necesita invalidarse
el caché de seguimiento o tarifario al modificar la identidad: estas respuestas separadas
se leen de la base y no se cachean. Un cliente que ya mostró la marca debe volver a
consultarla para reflejar los cambios; no hay notificación push de identidad en esta etapa.

## Integración de las compañeras

- **Ana:** formulario administrativo GET/PUT; lectura `/api/operador/identidad` para portal;
  lectura por token para seguimiento y aplicación de marca, colores, contacto y logo.
  No inferir el operador de parámetros del navegador ni usar la razón social global como
  sustituto de la marca configurada.
- **Aldana:** usar `IConsultaIdentidadOperador.ActualAsync` al preparar comunicaciones,
  dentro del ámbito de operador/cuenta del mensaje autenticado. Infrastructure implementa
  el puerto con `IdentidadOperador(options, tenantActual)`. Web ya lo registra; el host
  Worker debe resolverlo/registrarlo dentro de su ámbito de mensaje para consumirlo.
  Esta etapa no cambió el host Worker, plantillas, contratos de eventos ni contenido de avisos.
- La lectura de mensaje revalida que operador/cuenta existan, permite cuentas dadas de
  baja para procesar mensajes anteriores, y no habilita escritura administrativa. No se
  expone una API HTTP para pasar un ámbito de Worker o leer marcas por operador arbitrario.

Las comunicaciones pueden usar la configuración vigente al procesarse; no se almacena
una copia histórica de la marca en cada evento o envío. Si el equipo necesita esa garantía,
debe acordarla antes de ampliar el contrato de eventos.

## Persistencia y migración

Domain guarda los datos en Operador mediante `ConfigurarIdentidad`, sin alterar su razón
social o sus relaciones. Cada actualización genera `VersionIdentidad`; EF la usa como
token de concurrencia. La misma versión no puede confirmar dos actualizaciones simultáneas.
Application define DTOs y puertos; Infrastructure aplica validaciones de contexto, EF y
consultas mínimas; Web expone las rutas y controles de autorización/antiforgery.

La migración `20261114215630_IdentidadVisualOperador` conserva cada razón social, la copia
como marca inicial, asigna una versión y usa colores iniciales `#1E40AF`/`#0F172A`.
Contacto y logo previos quedan null: no se inventan ni se toman de usuarios/comercios.

```powershell
.\scripts\Start-LocalPostgres.ps1
.\scripts\Start-Web.ps1
```

El segundo script usa las credenciales locales y aplica migraciones antes de arrancar.
Para otros ambientes, configurar la conexión y usar EF database update como de costumbre.
El SQL actualizado de instalación desde cero está en `docs/database-initial.sql`.

## Verificación y cierre

Se verifican normalización, validación atómica, permisos/CSRF, lecturas comercial/pública,
ausencia de IDs, frescura de la consulta separada, aislamiento, revocación, ámbito Worker
de solo lectura, concurrencia, rollback y recuperación de datos previos con la migración.
Queda pendiente aplicar los datos en las tres salidas (portal, seguimiento y comunicaciones)
y demostrarlo en conjunto. La configuración del backend por sí sola no prueba esa integración.
