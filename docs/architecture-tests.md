# Pruebas de arquitectura

El proyecto `tests/Architecture/UltimaMilla.Architecture.Tests` usa xUnit y
[NetArchTest.Rules 1.3.2](https://www.nuget.org/packages/NetArchTest.Rules/1.3.2).
Verifica las reglas del ADR-001. No necesita Docker, PostgreSQL, Redis ni RabbitMQ.

## Qué se comprueba

| Proyecto | Dependencias de proyectos permitidas |
| --- | --- |
| Domain | Ninguna |
| Application | Domain |
| Infrastructure | Application y Domain |
| Web | Application, Infrastructure y Domain |

NetArchTest inspecciona los tipos de los ensamblados compilados. Domain admite tipos de
su propio namespace y de System. Application admite los suyos, Domain y System. Así se
detecta, por ejemplo, una propiedad DbContext o un cliente Redis en un caso de uso.
Infrastructure no puede depender de Web, Worker o Repartidor. Web tampoco puede depender
de las otras aplicaciones ejecutables. Las referencias a Infrastructure desde Web están
permitidas para registrar los servicios al arrancar, según la estructura acordada.

Además, `ProyectosTests` lee los .csproj desde el repositorio y verifica las referencias
declaradas, incluidas las que todavía no aparecen en el código compilado. Domain y
Application no declaran paquetes, frameworks adicionales ni imports explícitos de terceros
en la estructura actual. Los proyectos de pruebas sí pueden referenciar las capas que
necesitan inspeccionar: eso no implica una dependencia nueva entre proyectos productivos.

Las pruebas leen los proyectos del checkout; deben ejecutarse desde una copia del repositorio,
no únicamente desde una carpeta con DLL publicadas. Si en el futuro se centralizan referencias
en Directory.Build.props/targets u otros imports, deberá ampliarse la comprobación de proyectos.
No se evalúa hoy todo el grafo condicional de MSBuild.

## Ejecución

Desde la raíz:

```powershell
dotnet test tests/Architecture/UltimaMilla.Architecture.Tests/UltimaMilla.Architecture.Tests.csproj
```

También se ejecutan con `dotnet test UltimaMilla.slnx` y desde el explorador de pruebas de Rider.
El proyecto está incorporado a la solución. No se agregó ni modificó un pipeline de CI;
cuando se configure, ejecutar la solución incluirá estas pruebas.

Los errores de NetArchTest enumeran los tipos que incumplen. Las pruebas de proyecto
muestran las referencias que no están permitidas. `DeteccionTests` comprueba que una
dependencia deliberadamente incorrecta se detecta y un caso válido se acepta. Estos
ejemplos están exclusivamente en Fixtures dentro del ensamblado de pruebas.

## Cuándo actualizar las reglas

- **Nueva entidad, DTO o caso de uso en una capa existente:** no requiere modificar las
  pruebas; se analiza automáticamente el ensamblado completo.
- **Nuevo proyecto productivo, como Worker o Repartidor:** la prueba de inventario falla
  para recordar que hay que actualizar la matriz, las referencias del proyecto de pruebas
  y el análisis de tipos para incluirlo. Sus dependencias se acuerdan antes de permitirlas.
- **Nueva biblioteca de infraestructura:** normalmente no requiere cambios si se usa
  dentro de Infrastructure y respeta las relaciones entre capas.
- **Cambio deliberado de arquitectura o nuevas dependencias del núcleo:** primero se
  revisa la decisión y se actualiza ADR-001; luego se ajustan las reglas. Una prueba que
  falla por un acceso accidental a EF desde Application se resuelve moviendo ese acceso
  a Infrastructure y definiendo el contrato en Application.

Estas pruebas protegen la dirección de dependencias. No demuestran las reglas de negocio,
el aislamiento de datos, la configuración de servicios ni la integridad de una transacción.
Tampoco verifican todavía que todo acceso a Infrastructure desde Web ocurra exclusivamente
en el registro de dependencias. Esas comprobaciones tienen otro alcance y sus propias pruebas.
