# Concurrencia optimista de envíos y rutas

Se aplica el criterio del documento inicial, sección 5.3: detectar decisiones simultáneas
sobre un envío y mantener la restricción de una sola asignación activa. También se protege
la ruta, porque dos asignaciones de envíos diferentes pueden competir por su capacidad.

## Funcionamiento

`Envios` y `Rutas` incorporan `RowVersion`, un UUID marcado como token de concurrencia de
EF Core. Es una versión lógica administrada por la aplicación, equivalente al propósito de
RowVersion del documento; no es el tipo binario `rowversion` de SQL Server ni `xmin`.
Se mantiene como propiedad shadow: Domain no depende de EF ni de PostgreSQL.

1. Se consulta el agregado con seguimiento. EF conserva su versión original.
2. Domain valida la operación usando el estado y las colecciones cargadas.
3. Al guardar, se genera una nueva versión para cada raíz afectada.
4. El UPDATE o DELETE comprueba Id, TenantId y la versión original. Si otra operación ya
   cambió la versión, EF detecta que no se modificó la fila esperada.
5. `AppDbContext` transforma ese fallo en `ConflictoConcurrenciaException`, definido en
   Application. No se reintenta automáticamente ni se sustituyen las versiones originales.

Ejemplo conceptual, omitiendo las demás columnas:

```sql
UPDATE "Envios"
SET "Estado" = @nuevo_estado, "RowVersion" = @nueva_version
WHERE "Id" = @id AND "TenantId" = @operador
  AND "RowVersion" = @version_que_se_leyo;
```

Cambiar solo un hijo también participa en la concurrencia. `AggregateConcurrency` reúne
las raíces afectadas por bultos, eventos, intentos, pruebas, devoluciones, discrepancias,
escaneos, paradas y rendiciones. Una parada afecta tanto al envío como a su ruta; un escaneo
con ruta también la comprueba. Así, insertar una parada obliga a actualizar la versión de
la ruta, aunque sus propiedades de negocio no hayan cambiado.

BultoParada vincula la composición con ambos agregados: crear miembros de una parada nueva
también compite por Envio y Ruta. La composición persistida es histórica e inmutable en
SaveChanges. En continuaciones parciales el resumen puede seguir EntregadoParcialmente,
pero los cambios de bultos, paradas/eventos y sus versiones igual detectan la competencia.

Si una raíz antes intacta pasa a UPDATE por este mecanismo, se vuelven a validar los
ámbitos de operador y cuenta comercial antes de escribir. Un guardado sin cambios no
renueva versiones y los envíos independientes no comparten un contador global.

El índice único filtrado `IX_Paradas_EnvioId` sigue impidiendo dos paradas activas para un
envío. Si una carrera alcanza ese índice antes de comprobar la versión, su violación también
se traduce a `ConflictoConcurrenciaException`. También se traducen los índices de número
e IdOperacion por envío de IntentosEntrega y de IdOperacion de EventosDeEnvio: dos decisiones
pueden colisionar allí antes del UPDATE de la raíz. No es una respuesta idempotente automática;
hay que releer. Los demás errores de unicidad conservan su tratamiento original.

## Cómo deben usarlo los casos de uso

- Cargar con seguimiento en un DbContext nuevo para la operación. Incluir todas las
  relaciones y colecciones que usará Domain. Para capacidad: ruta, paradas activas,
  sus envíos/bultos y vehículo; para estados e intentos: el grafo necesario del envío.
- Realizar la operación y guardar todos sus cambios con un único SaveChanges, o dentro
  de una transacción explícita que abarque la unidad completa.
- Mantener las transacciones automáticas de EF. Ante un fallo en una transacción propia,
  hacer rollback de la unidad completa y descartar el contexto.
- Ante `ConflictoConcurrenciaException`, descartar el contexto y volver a consultar.
  Revalidar la intención con Domain antes de intentar una operación nueva. No copiar
  valores originales desde la base para forzar el guardado de una decisión anterior.

Los hijos operativos requieren que su raíz y los padres intermedios estén cargados en
el mismo contexto. No se consulta una versión nueva al guardar: eso haría pasar por actual
una decisión tomada con datos viejos. Adjuntar un envío o ruta sin su versión original
tampoco está soportado. La API futura puede convertir el conflicto en HTTP 409; todavía
no existe ese endpoint ni el contrato de versiones para formularios desconectados.

## Migración y límites

`20261021210217_OptimisticConcurrency` agrega las dos columnas con `gen_random_uuid()`
como valor predeterminado, dando versiones a las filas existentes sin cambiar sus datos
de negocio. El SQL de instalación desde cero incluye las tres migraciones.

Esta protección cubre las escrituras operativas indicadas mediante SaveChanges. No garantiza
la integridad de colecciones cargadas parcialmente ni controla SQL directo, ExecuteUpdate,
ExecuteDelete o escritores que omitan este mecanismo. El UUID no se renueva automáticamente
en PostgreSQL para esos escritores. Tampoco convierte todas las configuraciones y catálogos
en agregados con versión; editar la capacidad del vehículo mientras se despacha requiere
su propia coordinación. Las futuras operaciones deben revisar qué agregados leen y modifican.

Concurrencia e idempotencia son distintas: aquí se rechazan decisiones incompatibles basadas
en la misma versión. El tratamiento completo de mensajes repetidos, inmutabilidad persistente
del historial y outbox quedan para los siguientes pasos.

## Verificación

Los cargos de nueva visita participan de las versiones de Envio y Ruta; la composición
de devolución participa de Envio. Si dos despachos insertan el mismo cargo antes de
actualizar la raíz, el índice único también se traduce a ConflictoConcurrenciaException.
La operación perdedora revierte su cargo, estados y outbox. Ver [política de pendientes](politicas-pendientes.md).

Las pruebas contra PostgreSQL comprueban decisiones sobre el mismo envío, dos despachadores
con guardados paralelos, capacidad de una ruta compartida, cambios que solo insertan hijos,
SaveChanges síncrono y asíncrono, rollback del intento perdedor, continuación con contexto
nuevo, guardados vacíos, envíos independientes, rechazo de hijos sin su raíz y migración
con datos existentes. Se ejecutan con `scripts/Test-LocalPersistence.ps1`.

Referencia: [tokens de concurrencia administrados por la aplicación en EF Core](https://learn.microsoft.com/en-us/ef/core/saving/concurrency#application-managed-concurrency-tokens).
