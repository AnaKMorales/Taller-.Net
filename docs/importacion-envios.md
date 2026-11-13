# Backend de importación idempotente

Letra original, requisito 7. Implementado el 13/11/2026.

`ImportarEnviosHandler` está registrado en Web y acepta entre 1 y 500 filas.
Cada `FilaImportacion` contiene una `Referencia`, una `SolicitudAltaPortal` y una
`ClaveEntrega` de las opciones del alta (`yyyy-MM-dd|idFranja`). La cuenta y el operador
se resuelven desde la identidad autenticada; no los decide el archivo.

La futura pantalla debe convertir el archivo al contrato y llamar al caso de uso.
Este avance no incorpora lector CSV/Excel, formulario ni endpoint de importación.
El alta individual existente no cambia. No constituye el opcional de API pública.

## Resultado por fila

- `Creado`: envío nuevo, con tarifa, reglas y fechas resueltas por el alta del servidor.
- `Existente`: misma referencia y contenido; devuelve el resultado de la primera alta.
- `Conflicto`: referencia ya utilizada con otros datos; no modifica el envío original.
- `Error`: datos inválidos; no guarda esa fila y continúa con las siguientes.

La referencia es estable por pedido, independiente del número de fila o del nombre del
archivo. Se recortan espacios en sus extremos, distingue mayúsculas y tiene un máximo de
100 caracteres. Debe reutilizarse al reimportar, incluso en otro lote. Una corrección
posterior a un alta exitosa requiere otro flujo: no se actualiza implícitamente un envío.

La huella SHA-256 incluye todos los datos solicitados y la fecha/franja elegida. Ordenar
los bultos de otra forma o cambiar la escala decimal (1 frente a 1,00) no cambia la
huella. Cambiar otros textos o la programación sí produce conflicto. Las tarifas y reglas
calculadas no forman parte de la huella: un reenvío conserva la respuesta original aunque
la configuración o la fecha actual cambien. No se almacena el archivo ni una copia de los
datos personales en el registro técnico.

## Persistencia y concurrencia

`AltasImportadas` guarda referencia, huella, resultado original y vínculo al envío/cuenta.
Tiene filtros de operador y cuenta, claves foráneas con operador y una restricción única
por operador, cuenta y referencia. El registro no admite modificaciones ni borrado por EF.

Cada fila usa un contexto y transacción propios. Un bloqueo transaccional de PostgreSQL
coordina solicitudes simultáneas de la misma referencia; su identificador se deriva de
SHA-256 del operador, cuenta y referencia. Una colisión de bloqueo solo serializa trabajo,
no comparte resultados. El índice único garantiza la unicidad en la base.

El alta, su evento/outbox y el registro técnico se confirman juntos. Si falla la última
inserción, se revierte también el alta. Las filas válidas previas permanecen confirmadas.
Ante cancelación o fallo técnico se puede reenviar todo el lote con sus referencias
originales. Los fallos de autorización y los errores técnicos se propagan; no se presentan
como errores de contenido de una fila. La identidad y pertenencia se comprueban también
antes de devolver una importación existente.

La migración `ImportacionIdempotente` debe aplicarse antes de usar el servicio. El SQL de
referencia se regeneró; no se aplicó la migración a la base de desarrollo.

## Verificación

Pruebas unitarias del procesamiento por fila y pruebas con PostgreSQL real para lotes
parciales, reenvíos, contenido diferente, solicitudes simultáneas, aislamiento de operador
y cuenta, usuario desactivado, recuperación de respuesta histórica y rollback del registro.

La comprobación final aprobó los ocho casos seleccionados de importación, aislamiento,
migración y alta individual. La suite amplia de PostgreSQL detectó además dos timeouts
en las pruebas HTTP de `/dev/datos-demo`; quedan por investigar y no se considera aprobada
la regresión completa. Su prueba genérica de aislamiento se adaptó a la nueva tabla vacía
y pasó en la repetición. Cobertura: Domain 85,97 %; Application 74,12 %.
