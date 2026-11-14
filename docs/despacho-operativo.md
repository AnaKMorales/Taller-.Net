# Consulta de preparación y despacho de rutas

Letra original, requisito 17. Backend implementado el 13/11/2026.

## API para integrar al backoffice

- `GET /api/despacho/rutas/{rutaId}/preparacion`: versión actual, instante de despacho
  y composición de bultos de las paradas activas, con indicación de carga.
- `POST /api/despacho/rutas/{rutaId}/despachar`: recibe
  `{ "versionEsperada": "<GUID recibido en la consulta>" }` y devuelve ruta,
  nueva versión e instante UTC de despacho.

Requiere administrador o despachador autenticado. El POST exige cookie y token
antiforgery, igual que las otras operaciones de despacho. Obtener el token en
`GET /api/antiforgery` y enviarlo en `X-CSRF-TOKEN` conservando las cookies.
Respuestas: 200 éxito; 400 solicitud/token CSRF inválidos; 401 sin sesión;
403 rol, identidad u operador ajenos; 409 versión obsoleta o condición operativa inválida.

El GET no sustituye al listado de rutas del backoffice ni registra escaneos. La carga debe
estar registrada antes de confirmar. La [API de carga](carga-rutas.md) ya está disponible;
falta su integración de interfaz.
La [recepción con discrepancias](recepcion-deposito.md) también tiene su propio backend.
El [armado regular de rutas](asignacion-regular.md) dispone de consultas y API de asignación.
El [cierre de devoluciones y rendición](cierre-deposito.md) también tiene backend disponible.
Este cambio no completa todo ese circuito ni añade el botón de despacho.

## Validaciones y guardado

`DespacharRutaHandler` vive en Application y usa las reglas de `Ruta.Despachar` de Domain.
Comprueba la versión esperada, el repartidor activo y la carga de todos los bultos de las
paradas activas. El escaneo debe ser de esa ruta, no futuro y posterior o igual a la última
asignación del envío. Una visita de pendientes solo exige su composición; no requiere
volver a cargar bultos entregados. Una ruta vacía, despachada o rendida se rechaza.

`DespachoRutaRepository` crea una unidad dedicada con el operador de la sesión y verifica
el usuario activo y su rol contra la base. Carga el grafo mediante consultas divididas
dentro de una lectura Repeatable Read, y luego cierra esa lectura antes de operar.
El guardado usa la concurrencia optimista existente de ruta y envíos, conservando sus
versiones originales. Si otra operación modifica los agregados mientras tanto, se rechaza
sin guardar parcialmente. Cambiar solo un escaneo también modifica las versiones.

Un único SaveChanges guarda ruta, estados de bultos/envíos, eventos, recargos previstos
que correspondan y mensajes del outbox. No requiere migración. Un fallo del outbox revierte
el despacho. No se agrega idempotencia por identificador de operación: volver a despachar
una ruta ya despachada devuelve conflicto, sin producir otro evento.

Después del despacho, el repartidor asignado puede listar y descargar la ruta mediante
la API y la PWA existentes. Los estados parciales y los bultos entregados se conservan.

## Pruebas

Pruebas unitarias del caso de uso, además de pruebas contra PostgreSQL para API real,
autenticación/antiforgery, aislamiento, versiones, carga incompleta, rollback de outbox,
despachos simultáneos y visita de pendientes. La cobertura se mide con el script habitual.
