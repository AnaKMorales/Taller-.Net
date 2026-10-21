# ADR-002 Multitenancy por fila con cuenta comercial

Estado: implementado el núcleo y los adaptadores de contexto en persistencia.
Responsable del área: Agustín Rapetti. Actualización: 17/10/2026.
Origen de la decisión: sección 11 y ADR-002 del documento inicial del Grupo 5.

La decisión del equipo es compartir una base PostgreSQL y un esquema, identificando al
operador propietario en cada registro privado. Los usuarios de comercio tienen además
una CuentaComercial activa; un comercio global puede mantener cuentas con varios operadores.
Las alternativas por esquema y por base por operador se descartaron en el documento inicial
por su complejidad de migraciones y operación para el alcance y el equipo de tres.

TenantContext conserva operador, identidad, rol y cuenta cuando corresponde. Se resuelve desde
claims autenticados, el sobre del mensaje Worker o un token público impredecible. Se captura
por operación y no se recicla entre peticiones/mensajes. Los filtros de EF y el interceptor
centralizan lecturas y validación de escrituras; las FK compuestas respaldan la pertenencia
al operador. TenantId es el nombre de la propiedad shadow de persistencia para el OperadorId.

La protección por cuenta se aplica también a entidades secundarias y a sus referencias.
Los catálogos compartidos del operador se leen sin otorgar permiso de edición a un comercio.
Los datos comunes de Comercio se protegen frente a modificaciones desde un contexto de operador.
El seguimiento devuelve un DTO limitado y usa un contexto de solo lectura. Las claves de
caché incluyen todos los ámbitos que afectan la visibilidad.

La implementación reutilizable se documenta en [Multitenancy](../multitenancy.md). Web y Worker
deben conectarla a Identity, API, RabbitMQ y Redis; esos servicios no se consideran entregados
por este cambio. No se utiliza RLS: los accesos SQL privilegiados y la desactivación explícita
de filtros siguen requiriendo control en infraestructura. La concurrencia de negocio es otro paso.

Las pruebas usan PostgreSQL real con dos operadores y varios comercios por operador,
cuentas compartidas, referencias e identidades falsificadas, revocación, mensajes independientes,
datos existentes y consultas públicas sin información interna.
