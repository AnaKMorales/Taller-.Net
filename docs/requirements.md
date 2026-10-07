# Requisitos del proyecto

## Requisitos funcionales

- Gestionar entregas y recorridos de última milla.
- Registrar clientes, pedidos, entregas y estados asociados.
- Exponer endpoints de negocio para web y repartidores.
- Permitir seguimiento en tiempo real de entregas.
- Soportar operación multicliente y aislamiento por tenancy.
- Manejar flujos de validación y tareas de backend en segundo plano.

## Requisitos no funcionales

- Arquitectura modular y mantenible por capas.
- Separación clara entre dominio, aplicación, infraestructura y presentación.
- Pruebas unitarias, de integración y de arquitectura.
- Persistencia robusta con EF Core y colas/eventos.
- Seguridad y autenticación con Identity.
- Observabilidad y manejo de errores centralizado.
- Preparación para despliegue con Docker Compose y Terraform.

## Cobertura esperada

- `Unit`: validación lógica de dominio y casos puntuales.
- `Integration`: pruebas entre capas y flujo de datos reales.
- `Architecture`: validación de dependencias de la solución.
- `Tenancy`: validación de aislamiento de datos y contexto multicliente.
