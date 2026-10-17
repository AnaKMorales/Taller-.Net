# Bitácora de asistencia de IA

## 14 de octubre de 2026

Área: núcleo y datos, a cargo de Agustín Rapetti según el reparto del equipo.

Se utilizó Codex para generar y revisar la implementación de las entidades de Domain a partir
de los cuatro diagramas UML, la consigna y la entrega inicial. También se generaron pruebas
xUnit y documentación del modelo. Esto incluye la reorganización de la carpeta Envio a Envios
y la sustitución del modelo inicial de Envio por relaciones entre objetos.

Las adaptaciones respecto del dibujo están detalladas en `domain-model.md`: CuentaComercial,
unidades, referencias a versiones, horas con TimeOnly, evidencia textual y URL del receptor,
entre otras. Se ignoraron las notas rojas de dudas por indicación del usuario.

La validación automatizada incluyó compilación de Domain con advertencias como errores y
39 pruebas unitarias satisfactorias. La revisión y aceptación del código por el integrante
responsable, y cualquier modificación o descarte que haga, deben registrarse después de
esa revisión. No se atribuye al integrante una revisión que todavía no se realizó.

### Correcciones de programación y escaneos

Tras contrastar el código con los diagramas y la letra, se reprodujeron tres diferencias:
la recepción exigía una ruta, la reprogramación no cambiaba la ventana y el alta permitía
ventanas incompatibles con la franja de la zona. El usuario autorizó corregirlas y pidió
conservar el modelo sin agregar la colección Envio.Paradas.

Se agregó el valor inmutable ProgramacionEntrega, la referencia a la franja, el historial
de programación en los eventos y las fábricas de escaneo por tipo. También se adaptaron
las pruebas existentes al requisito de recepción completa y se agregaron pruebas de regresión.
La ejecución posterior pasó 58 pruebas de Domain con advertencias tratadas como errores.

### Preparación de EF Core antes de Docker

El usuario autorizó preparar la persistencia antes de disponer de PostgreSQL. Se agregaron
Npgsql, EF Design, el manifiesto local de dotnet-ef, los mapeos Fluent API, constructores de
rehidratación y la fábrica de diseño. Se generaron InitialCreate, el snapshot y el SQL inicial;
no se aplicó la migración ni se conectó a una base.

Al revisar la rehidratación se corrigió la elección del último intento para usar su número,
sin depender del orden de carga de las filas. Una prueba carga los intentos en orden inverso
y comprueba las decisiones de reintento y devolución. Se documentaron las FK shadow, las
instantáneas de programación, los índices y las limitaciones pendientes.

La validación pasó 58 pruebas de Domain y 15 de Infrastructure. Estas últimas usan el modelo
Npgsql para generar SQL y almacenamiento InMemory para rehidratar grafos. No equivalen a
pruebas de integración contra PostgreSQL. Continúa pendiente la revisión humana del código.

### PostgreSQL local y pruebas reales

El usuario autorizó preparar Docker local para probar persistencia aunque el despliegue
corresponda inicialmente a otra integrante. Se agregó un Compose independiente para
PostgreSQL 17, publicado solo en localhost:55432, con volumen persistente, healthcheck y
credenciales generadas en un archivo excluido de Git. Se agregaron scripts para levantar,
migrar y probar sin mostrar las credenciales ni conservar variables del proceso modificadas.

Se levantó el contenedor, se aplicó InitialCreate a la base local y pasaron siete pruebas
de integración contra PostgreSQL 17.11. Estas crean y eliminan exclusivamente bases temporales
con nombres aleatorios; prueban esquema, materialización, flujos, restricciones y rollback.
Se mantienen pendientes aislamiento automático por tenant, concurrencia, protección del
historial y outbox. Este apoyo local no representa la entrega del despliegue completo.
