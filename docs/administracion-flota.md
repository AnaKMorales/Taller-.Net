# Altas y consultas de flota

Primer bloque del backend de administración del operador, requisito 6 de la sección 5.1
de la letra original. Implementado el 14/11/2026, sin migración nueva.

## API para conectar al backoffice

Solo administradores autenticados del operador. Cada operación revalida en la base que
el usuario siga activo y sea Administrador. El operador se obtiene del contexto de sesión;
no se elige desde el JSON. Los POST requieren el token de `/api/antiforgery` en
`X-CSRF-TOKEN`, conservando la cookie. Las respuestas no se almacenan en caché.

| Método | Dirección | Resultado |
| --- | --- | --- |
| GET | `/api/administracion/flota/repartidores` | Lista del operador: id, email y activo. Incluye inactivos para su consulta. |
| POST | `/api/administracion/flota/repartidores` | Crea un repartidor activo con contraseña; devuelve 201 e id/email/activo. |
| GET | `/api/administracion/flota/vehiculos` | Lista del operador: id, matrícula, capacidad de peso y volumen. |
| POST | `/api/administracion/flota/vehiculos` | Crea un vehículo con capacidades positivas; devuelve 201 y sus datos. |

Alta de repartidor:

```json
{ "email": "repartidor@operador.example", "contrasena": "<contraseña de 8 a 256 caracteres>" }
```

El email se recorta y convierte a minúsculas; admite hasta 254 caracteres y debe ser una
dirección sin nombre de presentación. No puede estar ocupado por ningún usuario del
operador, aunque tenga otro rol o esté inactivo. El rol creado siempre es Repartidor.
La contraseña se guarda como hash con el mecanismo existente y en la misma escritura
que el usuario. No se devuelve el hash ni la contraseña. El repartidor creado puede
iniciar sesión mediante `/api/repartidor/sesion`.

Se conserva la unicidad por operador del modelo. El login actual rechaza emails ambiguos
entre operadores, por lo que para ingresar sin selector se necesitan emails distinguibles
entre operadores. Este avance no cambia esa estrategia de autenticación.

Alta de vehículo:

```json
{ "matricula": "ABC123", "capacidadPesoKg": 350, "capacidadVolumenM3": 8 }
```

La matrícula se recorta y convierte a mayúsculas; admite entre 1 y 50 caracteres. Es única
por operador sin distinguir mayúsculas. Peso en kg y volumen en m³, ambos mayores que cero.
No se agrega un formato de matrícula específico de un país ni un estado activo al vehículo
que no existe en el modelo actual.

Respuestas: 200 consulta; 201 alta; 400 datos o antiforgery inválidos; 401 sin sesión;
403 rol, identidad o ámbito no autorizados; 409 email o matrícula ya usados. Las mismas
garantías se mantienen si dos administradores intentan crear el mismo registro a la vez.
Se reconocen los índices únicos concretos de email/matrícula para devolver 409 en esa carrera.
Repetir un alta confirmada también recibe 409; no es un alta idempotente por clave de
operación. Se puede consultar el listado para recuperar el registro existente.

## Integración con las rutas

La flota creada aparece en `/api/despacho/recursos` sin invalidaciones de caché adicionales.
Con sus identificadores, el despachador puede usar el POST de creación de ruta existente.
La planificación sigue mostrando únicamente repartidores activos con su rol correcto.
Estas altas y consultas no cambian las rutas, envíos o capacidades de vehículos anteriores.
Ver [asignación regular](asignacion-regular.md).

Application define `IAdministracionFlota` y los contratos. Infrastructure implementa
`AdministracionFlota`: contexto dedicado del operador, validación del administrador,
constructores del dominio, hash de contraseña y SaveChanges. La base conserva los índices
únicos existentes. Cada alta se confirma o revierte completa, sin tocar el esquema.

## Alcance pendiente

Quedan las pantallas, edición, desactivación/reactivación o bajas y cambio de contraseña.
Antes de permitir cambios de capacidades o roles se deberán definir las restricciones de
los datos ya usados en rutas, sin modificar silenciosamente la planificación en curso.
Las [altas y consultas de comercios/cuentas/usuarios](administracion-comercios.md) ya tienen
su propio backend, al igual que las [altas/consultas de zonas y franjas](administracion-zonas.md).
No se dan por completos todos los
requisitos de administración.

Se verifican API/CSRF/roles, normalización, contraseñas utilizables en la PWA, disponibilidad
en planificación/creación de ruta, aislamiento/revocación, duplicados entre roles,
altas simultáneas y rollback de usuario/credenciales con PostgreSQL real.
