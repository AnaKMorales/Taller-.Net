# Plan de cierre de requisitos

Revisión del 14/11/2026. Grupo 8, tres integrantes. Solo planificación: no se implementaron
funciones en esta revisión. Este documento distingue obligaciones, verificaciones y mejoras.
No significa que cada limitación documentada deba convertirse en una funcionalidad nueva.

Coordinación posterior del 14/11: Agustín confirmó que sus compañeras trabajan en cuenta
corriente, etiquetas y pantalla de importación, y posteriormente indicó que también
trabajan en liquidaciones (avance por confirmar con ellas). Esos trabajos quedan a cargo de ellas;
Agustín no los implementa ni duplica. Solo se acordarán apoyos concretos de backend si los solicitan.

Autenticación V1: el usuario informó que la compañera responsable de esa parte la
revisará. Agustín no inicia una integración paralela de Identity/OIDC mientras esté
en revisión por ella.

Revisión adicional de backend posterior a esa coordinación (sin implementar ni ejecutar
pruebas nuevas):

- **Registro operativo de extravío (letra 5.2, requisito 11, pág. 4):** backend implementado
  posteriormente a esta revisión: caso de uso y API GET/POST para Administrador/Despachador,
  versiones de envío/ruta, detalle y reloj del servidor. Historial/outbox y liberación de
  parada atómicos, con pruebas de concurrencia/rollback y entrega parcial. Sin migración.
  Solo queda conectar el botón/formulario en Web. Ver [contrato](docs/registro-extravios.md).
- **Separación de comercios en reportes (letra 5.5, requisito 30, pág. 4):**
  `ReportesGestion` proyecta el nombre de CuentaComercial y agrupa por ese texto, sin
  conservar CuentaComercialId como clave. Dos clientes del mismo operador con el mismo
  nombre terminan en un solo grupo de cumplimiento/liquidación. Es una corrección del
  backend existente, no una función nueva. Confirmar con las compañeras de liquidación
  antes de modificar esa consulta/DTOs y añadir un caso de regresión con nombres iguales.

Estos hallazgos salen de lectura del código; esta revisión no certifica por sí sola
el cumplimiento completo ni reemplaza una reproducción automatizada del caso de reportes.

Revisión posterior a A2: la pantalla `Pages/Backoffice/Envio.cshtml` y su PageModel ya
ofrecen asignación y devolución de bultos pendientes mediante IDespachoPendientes.
Ese desarrollo no vuelve a planificarse; queda comprobarlo en la demo. Persisten en el
código revisado el uso de almacén propio de usuarios (V1), la conversión de eventos V2
a salida externa V1 sin detalle de bultos y la ausencia de un proceso de cierre diario
o recálculo de indicadores en Worker. Las dos últimas corresponden a coordinación con
Aldana; no se implementan unilateralmente. A3 queda en seguimiento de las compañeras;
Agustín solo apoyará si una integración concreta requiere backend faltante.

Avance A1: implementado el cambio de email y baja lógica de usuarios de Comercio de
una única cuenta propia, y la modificación de ficha local y baja lógica de la cuenta
comercial, con Version y migración `AdministracionCuentaComercial`. La ficha global
compartida no se modifica; login/escrituras del comercio y nuevas altas quedan bloqueados
tras la baja, conservando el procesamiento histórico. Revisión de perfiles/permisos
cubierta con RBAC y ámbito de cuenta: el backend de A1 queda cerrado dentro del
contrato documentado. Resta la presentación para cerrar el requisito completo.
No se requiere crear un editor genérico de permisos. Ver [contrato](docs/administracion-comercios.md).

## Fuentes y alcance de la revisión

- `Laboratorio__NET_2026.pdf`, escritorio de Agustín: secciones 4, 5.1–5.5, 6.3, 6.15, 7 y 8.
- `Cambios de Requerimientos Planificados.pdf`: 2.1 (31–36), 2.3 y aclaraciones de caché/opcionales.
- `Nos organizamos.pdf`: tareas numeradas de cada integrante y diagnóstico de áreas sin tarea.
- Código integrado de administración, carga, consultas del portal, reportes, autenticación,
  Worker y documentación de contratos/ADR. No se ejecutó una nueva demo ni pruebas en esta revisión.

Las referencias de páginas de la letra son las impresas, distintas del número de página del PDF.
El reparto asigna áreas; los responsables de brechas adicionales son propuestas de coordinación,
no tareas numeradas nuevas impuestas por ese documento. No se auditaron aquí todas las
capacidades de dispositivo, infraestructura remota u opcionales: requieren evidencia de sus responsables.

## A. Brechas obligatorias confirmadas y plan de Agustín

| ID | Fuente | Qué está y qué falta | Alcance y criterio de cierre |
| --- | --- | --- | --- |
| A1 | Letra 5.1, requisito 1, pág. 3 | Backend cubierto: altas/consultas, edición y baja de cuenta, modificación/baja de usuarios y autorización por rol Comercio y cuenta. Restan pantallas. | Agustín: cierre del backend y contrato con pruebas. Ana/responsables de Web: integración visual. No se agregan perfiles comerciales adicionales ni editor genérico de permisos como obligación implícita. Ver docs/administracion-comercios.md. |
| A2 | Letra 5.1, requisito 5, pág. 3 | Backend implementado: configuración por operador con versión y lecturas administrativa, autenticada, por token público y por ámbito de mensaje. Migración IdentidadVisualOperador. | Agustín: backend/contrato verificados. Ana: formulario y aplicación en portal/seguimiento. Aldana: consumo en comunicaciones. El requisito sigue abierto hasta demostrar las tres salidas. Ver docs/identidad-operador.md. |
| A3 | Letra 4, portal, pág. 3 | Exige etiquetas, gestión de devoluciones y cuenta corriente/liquidaciones. Las consultas del portal revisadas ofrecen envíos y avisos; el reporte de liquidación es de backoffice y no acredita el acceso del comercio. | Agustín y Ana: precisar cada operación requerida y qué servicio existente se reutiliza. Agustín implementa solo consultas/reglas que falten, limitadas a la cuenta autenticada; Ana integra presentación/impresión. No inventar cobros, pasarela de pagos o un sistema contable completo. Cerrar cada subflujo con el comercio accediendo solo a su información. |
| A4 | Cambio 2.3; reparto Agustín 6; letra 8.2 | ADR-007 existe y todavía figura en curso. Hay ADR con texto desactualizado (por ejemplo ADR-006 describe broker/host como pendientes). | Agustín: actualizar sus decisiones con el resultado real, dificultades y limitaciones. Cierre definitivo tras evidencia del ensayo integrado, sin dar por implementadas las partes ajenas que falten. Cada autora actualiza sus propios ADR. |

### Orden de trabajo

1. **Acordar las dos decisiones que afectan a todos:** autenticación (V1) y tratamiento
   de bajas/edición de comercios compartidos (A1). Evita rehacer usuarios y permisos después.
2. **A1 por etapas:** contrato de modificación/baja de la cuenta comercial, operaciones
   necesarias de usuarios y comprobación de perfiles/permisos. Cada etapa termina con
   pruebas y documentación. No empezar editando catálogos o vehículos por costumbre.
3. **A2**, tras acordar cómo consumirán la configuración Ana y Aldana.
4. **A3 en coordinación:** cuenta corriente y etiquetas están en curso por las compañeras,
   al igual que la pantalla de importación del requisito 7. No iniciar implementaciones
   paralelas. Verificar gestión de devoluciones y acordar solo apoyos faltantes de backend.
5. **A4 y ensayo conjunto**, manteniendo la documentación actualizada durante los cambios.

Si otro integrante necesita antes un contrato de A2/A3 para integrar una obligación,
se adelanta ese contrato. Las pantallas, PWA y Worker no se implementan unilateralmente.

## B. Obligaciones por verificar o coordinar antes de programar más

| ID | Fuente | Evidencia actual / comprobación pendiente | Responsable |
| --- | --- | --- | --- |
| V1 | Letra 6.3, pág. 5 | Exige OIDC/OAuth 2.1 con PKCE o ASP.NET Identity como almacén. Se encontraron cookies, Bearer y PasswordHasher sobre usuarios propios; no una integración completa de esas alternativas. Usar el hasher de Identity no demuestra que se use como almacén. Acordar cómo cumplir antes de cambiar autenticación. | Agustín y Aldana, coordinando impacto con Ana. No está asignado como tarea nueva independiente en el reparto. |
| V2 | Letra 5.4, requisito 19, pág. 4 | La API calcula faltantes y rechaza el código que no pertenece a la ruta con un mensaje explícito. Eso puede servir como aviso de sobrante: comprobar que PWA/backoffice lo muestran en el flujo de carga, también offline según corresponda. **La letra no pide un historial persistido de sobrantes**; no crear tablas solo por esa limitación del contrato. | Aldana en carga/PWA, Ana si hay interfaz de backoffice; Agustín solo si la comprobación revela una brecha real del backend. |
| V3 | Letra 4, Worker, pág. 3 | Exige recálculo de indicadores y cierre diario. Hay reportes bajo demanda; no se encontró la ejecución programada correspondiente en Worker. Precisar el cierre y comprobar qué cambios pendientes tiene Aldana antes de definir reglas o tablas. | Aldana en Worker; Agustín únicamente para las reglas/consultas faltantes acordadas. El diagnóstico del reparto lo menciona, pero no asigna tarea numerada al cierre. |
| V4 | Letra 5.1, requisito 6, pág. 4 | Se exige gestión de repartidores/vehículos con capacidad. Hay altas, consultas y uso en planificación. Contrastar con la demo y el equipo qué gestión adicional necesita cumplir ese flujo. No convertir automáticamente cada combinación de edición, reactivación y eliminación en requisito separado. | Agustín en backend; Ana en presentación. |
| V5 | Cambio 31–36 y 2.3; reparto Ana 4, Aldana 4–5, Agustín 2–3 y 6 | Núcleo, outbox V1/V2 y APIs existen. Pendientes documentados: operación de pendientes en backoffice, diagnóstico por bulto en PWA y avisos externos con detalle por bulto compatibles. Verificar cada uno y ejecutar entrega parcial offline → sincronización → backoffice → seguimiento → receptor. | Ana y Aldana en sus adaptaciones; Agustín en contratos/núcleo, regresión y ADR. No basta probar solamente PostgreSQL. |
| V6 | Letra 6.15, 7 y 8.3–8.4; reparto Agustín 1, 4–5 | Hay CI y cobertura local >70 %. Confirmar el CI del commit final, las evidencias de los opcionales elegidos (mínimo 8 puntos en equipo de tres), la demo remota y los entregables. No afirmar que puntos o despliegue se acreditan solo por existir código. | Cada integrante aporta evidencia de lo suyo; revisión conjunta. |

## C. Mejoras fuera del plan obligatorio inmediato

- Reactivación de todos los recursos, pantallas de cambio/restablecimiento de contraseña
  y gestión genérica de roles: no agregarlas automáticamente. Lo necesario para cumplir
  A1 sí es obligatorio; el resto debe justificar su incorporación.
- Edición, desactivación o versionado completo de zonas/franjas: el requisito 2 pide
  definirlas; altas/consultas y adición de franjas ya existen. No exige expresamente
  todas esas variantes. Revisar solo si una operación obligatoria las necesita.
- Idempotencia de las altas administrativas, deduplicación global por nombre/email,
  historial de sobrantes y ampliaciones de edición de flota: mejoras posibles,
  sin convertirlas en requisitos implícitos. La idempotencia obligatoria de importación
  y consumidor sigue siendo independiente y ya forma parte del núcleo existente.
- Nuevos opcionales ajenos a la selección del equipo. **Opcional no significa prescindible
  de todo el bloque:** el mínimo de 8 puntos del equipo sigue siendo exigido.
- Retiro en domicilio (37–41) y escalabilidad/balanceo eximidos para equipos de tres:
  no añadirlos como pendientes obligatorios del grupo.

## Qué ya está cubierto por el backend reciente

Altas/consultas de flota, comercios/cuentas/usuarios y zonas/franjas; importación idempotente
y API de alta del comercio; recepción con discrepancias, asignación regular, carga, despacho,
devolución y rendición operativa. Sus contratos están en `docs/`. La integración de interfaz
pendiente no obliga a reconstruir esos servicios.

## Regla para actualizar el plan

Un pendiente nuevo debe tener fuente concreta, evidencia del hueco en el código o la demo,
responsable y criterio de cierre. Si solo es una mejora de comodidad, queda en C.
La lista es una planificación contrastada de las brechas revisadas, no una certificación
de cumplimiento completo de toda la letra ni de las áreas que aún esperan revisión conjunta.
