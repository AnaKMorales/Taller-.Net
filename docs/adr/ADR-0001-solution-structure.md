# ADR 0001: Estructura de solución para UltimaMilla

## Estado
Aceptada

## Contexto
El proyecto necesita una estructura de repositorio clara, escalable y alineada con la documentación del dominio de negocio. Se requiere separar responsabilidades entre negocio, aplicación, infraestructura y presentación.

## Decisión
Usar una solución organizada por capas con el prefijo `UltimaMilla` y una división de proyectos acorde a la arquitectura sugerida.

## Consecuencias
- Facilita mantenimiento y onboarding.
- Separa reglas de negocio de integración técnica.
- Permite crecer con nuevas funcionalidades sin mezclar capas.
- Alinea la estructura del repositorio con la documentación del proyecto.

## Verificación de la decisión

Se implementó `UltimaMilla.Architecture.Tests`, con xUnit y NetArchTest.Rules, para comprobar
las dependencias de tipos entre las capas existentes. Domain depende únicamente de sí mismo
y System; Application admite además Domain. Infrastructure y Web no dependen de otras
aplicaciones ejecutables. Web puede referenciar Infrastructure para registrar servicios.

También se validan las referencias declaradas en los proyectos, aunque todavía no se
utilicen en código. Agregar Worker o Repartidor exige incluirlos en la matriz y en el
análisis de sus ensamblados. La ejecución está integrada en la solución; su conexión al
pipeline depende de la implementación de CI del equipo.

Repartidor (Blazor WebAssembly PWA) se incorporó a la matriz sin referencias a proyectos
del servidor: corre en el navegador y se comunica solo por HTTP con DTOs propios. Así no
puede llegar a Infrastructure ni descargar Domain al dispositivo. Infrastructure y Web
tampoco pueden depender de Repartidor.

Ver [detalle y criterios para modificar las reglas](../architecture-tests.md).
