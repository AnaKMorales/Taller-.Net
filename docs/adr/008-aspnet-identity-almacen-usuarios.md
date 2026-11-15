# ADR-008: ASP.NET Identity como almacén de usuarios

Estado: aceptado. Fecha: 15/11/2026. Responsable: Aldana. Requisito 6.3 de la letra.

## Contexto

La letra (6.3) pide "OpenID Connect/OAuth 2.1 con PKCE o ASP.NET Identity como almacén de usuarios"
y penaliza una implementación nominal. Hasta ahora solo se usaba el `PasswordHasher` de Identity: el
hash estaba en la columna `Usuarios.ContrasenaHash` y el login (`BuscadorUsuariosLogin`) lo verificaba
a mano, sin bloqueo por intentos fallidos.

Restricciones: Domain no puede depender de Identity ni de EF (prueba de arquitectura); la sesión
(cookie del backoffice/portal y Bearer de la PWA y de la API de comercios) y sus claims (`operador_id`,
rol, `cuenta_comercial_id`) no deben cambiar, porque `TenantResolver` y la PWA dependen de ellos; y el
login ocurre antes de que exista un `TenantContext`.

## Decisión

- **Store real de Identity con EF Core.** `AppDbContext` hereda de `IdentityUserContext<CuentaAcceso, Guid>`
  y crea `AspNetUsers`, `AspNetUserClaims`, `AspNetUserLogins` y `AspNetUserTokens` en la misma base y la
  misma historia de migraciones (el migrador no cambia). `CuentaAcceso : IdentityUser<Guid>` vive en
  Infrastructure (`Persistence/Identidad`), con el **mismo Id que el `Usuario`** del dominio y una FK 1 a 1
  hacia `Usuarios`. Operador, rol, estado y cuentas siguen en `Usuario`. No se usan roles de Identity.
- **Tabla aparte, no la misma tabla `Usuarios`.** Compartirla (table splitting) chocaba con la herencia
  TPH de `Usuario`/`Repartidor`, la columna calculada `EmailNormalizado` y dos entidades escribiendo el email.
- **`UserManager` sobre `UserOnlyStore`.** `FabricaCuentasAcceso` arma el `UserManager` con los servicios de
  `AddIdentityCore` (opciones, hasher, validadores, normalizador) sobre el `AppDbContext` del caso de uso, para
  que el alta del `Usuario` y la de su `CuentaAcceso` vayan en el mismo `SaveChanges`.
- **Login** (`BuscadorUsuariosLogin`): `FindByNameAsync` → `IsLockedOutAsync` → `CheckPasswordAsync` →
  `AccessFailedAsync` o `ResetAccessFailedCountAsync`. Es lo que hace `SignInManager.CheckPasswordSignInAsync`;
  no se usa `SignInManager` para no meter el framework de ASP.NET Core en Infrastructure. La sesión la sigue
  emitiendo Web con los mismos claims.
- **Email único global.** `UserName` = email; `RequireUniqueEmail`. Un email que ya usa otro operador se
  rechaza con un mensaje genérico ("No se pudo crear el usuario con ese email.").
- **Bloqueo:** 5 intentos fallidos bloquean 5 minutos. Email inexistente, usuario inactivo, clave incorrecta
  y cuenta bloqueada dan la misma respuesta.
- **Política de contraseña:** mínimo 8, con mayúscula, minúscula, número y símbolo (`Demo1234!` es válida).
  Excepción: la clave propia de `POST /dev/datos-demo` se sigue validando como antes (mínimo 12, sin exigir
  símbolos), porque `deploy/aws/sembrar-demo.sh` genera claves alfanuméricas; la cuenta igual se crea con
  `UserManager.CreateAsync`.
- **Multitenancy.** Las tablas de Identity no llevan `TenantId` ni filtro de operador (el login todavía no
  conoce el operador); `TenantModel` y `CuentaModel` las excluyen (`IdentidadModel`). Las escrituras:
  - Con tenant, `IdentidadWriteGuard` exige que la fila sea de un `Usuario` del operador actual; una sesión de
    comercio no puede escribirlas.
  - **Sin tenant, única excepción** a "sin tenant no se escribe": modificar una `CuentaAcceso` cambiando solo
    `AccessFailedCount`, `LockoutEnd`, `SecurityStamp` y `ConcurrencyStamp`. Se comparan valores originales
    contra actuales (el store marca toda la entidad como modificada). Cualquier otra escritura sin tenant
    (contraseña, email, altas, bajas, o esos campos junto con otra entidad) sigue fallando.
- **Migración `AgregarIdentidad`:** antes de tocar nada aborta con un mensaje claro si hay emails repetidos
  entre operadores o emails no ASCII (la normalización de .NET y `upper()` de PostgreSQL solo coinciden en
  ASCII); corre en una transacción. Copia cada usuario a `AspNetUsers` con el **mismo hash**: el formato de
  `PasswordHasher` (V3, PBKDF2) no depende del tipo de usuario, así que las contraseñas existentes siguen
  valiendo sin reseteo. `ContrasenaHash` **no se borra** en esta migración: queda como respaldo y ya no se
  lee ni se escribe.

## Alternativas descartadas

- **OpenID Connect / OAuth 2.1 con PKCE** (Keycloak u OpenIddict): suma un servidor de identidad, otro
  contenedor y su memoria en una EC2 de 4–8 GB, redirecciones y manejo de tokens en la PWA offline, y
  configuración de clientes por entorno. Para un solo producto con usuarios propios no aporta lo que cuesta.
- **`IdentityDbContext` con `Usuario : IdentityUser`:** obliga a que Domain dependa de Identity; lo prohíbe la
  prueba de arquitectura.
- **`IUserStore` propio sobre `Usuarios`:** técnicamente es Identity, pero se podía leer como implementación
  nominal; el store de EF con `AspNetUsers` no deja dudas.

## Consecuencias

- Dos operadores ya no pueden tener usuarios con el mismo email.
- Las altas y el cambio de email de usuarios pasan por `UserManager` (validación de unicidad y de política).
  Con el guardado diferido se hace una sola actualización de `UserManager` por cuenta y contexto (dos seguidas
  rompen el `ConcurrencyStamp`; ver comentario en `FabricaCuentasAcceso.Para`).
- Revertir la migración conserva las contraseñas anteriores a ella (en `ContrasenaHash`), pero pierde las de
  usuarios creados después. Una migración posterior borrará `ContrasenaHash`.
- Si `CheckPasswordAsync` pidiera rehash (cambio futuro de las opciones del hasher), el login sin tenant no
  podría guardar el hash nuevo y fallaría: la excepción del interceptor no incluye `PasswordHash` a propósito.
  Con las opciones actuales no ocurre (lo comprueba una prueba). Si se cambian, revisar este punto.
- Dos logins concurrentes del mismo usuario pueden chocar en el `ConcurrencyStamp`; el perdedor se rechaza
  como intento fallido sin contarse. [NO VERIFICADO con una prueba de concurrencia; sale de leer el código.]
- El costo de hash para email inexistente o cuenta bloqueada es una verificación contra un hash ficticio; que el
  tiempo sea indistinguible del caso real es [NO VERIFICADO: no se midió].
- La base de AWS no se migró ni se probó en este cambio: se probó en bases temporales locales (vacía y con
  datos demo sembrados por la versión anterior, migrando dos veces). Sus emails demo llevan sufijo aleatorio,
  así que no deberían repetirse. [NO VERIFICADO contra la base real.]
