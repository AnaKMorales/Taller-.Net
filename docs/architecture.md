# Arquitectura del repositorio

La solución sigue una estructura por capas con un nombre de dominio coherente: `UltimaMilla`.

## Estructura propuesta

### `src/UltimaMilla.Domain`
Contiene entidades, enums, agregados, value objects, interfaces del dominio y reglas de negocio.

### `src/UltimaMilla.Application`
Agrupa casos de uso, contratos, DTOs, validaciones y servicios de aplicación.

### `src/UltimaMilla.Infrastructure`
Implementa EF Core, Redis, RabbitMQ, outbox, Identity, integración con servicios externos y persistencia.

### `src/UltimaMilla.Web`
Capa de exposición web: API, Razor Pages, Blazor y SignalR.

### `src/UltimaMilla.Repartidor`
Aplicación para repartidores, pensada como una PWA en Blazor WebAssembly.

### `src/UltimaMilla.Worker`
Procesos en segundo plano, consumidores de eventos y tareas asíncronas.

## Principios de diseño

- La capa de dominio no debe depender de infraestructura ni de la capa web.
- La aplicación orquesta los casos de uso, pero no conoce detalles de infraestructura.
- La infraestructura implementa las dependencias externas y el acceso a datos.
- La web expone el sistema a usuarios y clientes.
- El nombre del proyecto se mantiene consistente con el dominio del negocio: `UltimaMilla`.

## Regla general

Las dependencias deben fluir hacia el centro del dominio. Los cambios del negocio deben mantenerse aislados de detalles técnicos de almacenamiento o UI.
