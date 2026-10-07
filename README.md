# UltimaMilla

Este repositorio está organizado siguiendo la estructura base indicada en la documentación del proyecto.

## Estructura

- `src/`
  - `UltimaMilla.Domain/` - entidades, reglas y máquina de estados.
  - `UltimaMilla.Application/` - casos de uso y contratos.
  - `UltimaMilla.Infrastructure/` - EF Core, Redis, RabbitMQ, outbox, Identity.
  - `UltimaMilla.Web/` - API, Razor Pages, Blazor y SignalR.
  - `UltimaMilla.Repartidor/` - Blazor WebAssembly PWA.
  - `UltimaMilla.Worker/` - consumidores y tareas en segundo plano.
- `tests/`
  - `Unit/`
  - `Integration/`
  - `Architecture/`
  - `Tenancy/`
- `deploy/` - docker compose y Terraform.
- `docs/`
  - `adr/` - registros de decisión de arquitectura.
- `scripts/` - scripts de automatización y soporte.

## Recomendaciones

1. Mantener la capa `Domain` libre de dependencias externas.
2. Usar `Application` para orquestar casos de uso.
3. Centralizar infraestructura y acceso a datos en `Infrastructure`.
4. Mantener pruebas separadas del código de producción.
5. Mantener la nomenclatura del dominio consistente con `UltimaMilla`.
