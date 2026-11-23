# Pruebas de extremo a extremo (Playwright)

Opcional "Pruebas de extremo a extremo automatizadas" (7.2). Abren un navegador real (Chromium) contra la Web
levantada y recorren los flujos críticos:

| Test | Flujo |
| --- | --- |
| `ElAdministradorPublicaUnaNuevaVersionDelTarifario` | Admin cambia un precio y publica una versión nueva |
| `ElComercioDaDeAltaUnEnvioConLaTarifaCalculada` | Comercio carga un envío, calcula la tarifa y confirma |
| `ElTableroMuestraLaFlotaEnElMapa` | Admin abre el tablero y ve los vehículos en el mapa |
| `ElAdministradorDaDeAltaZonaFlotaYUnComercioQueIngresaAlPortal` | Admin crea zona, vehículo, repartidor y comercio con usuario; ese usuario entra al portal |
| `ElDepositoRecibeAsignaCargaYDespacha` | Comercio crea un envío; admin lo recibe, crea ruta, asigna, carga y despacha |
| `ElComercioImportaUnArchivoSinDuplicarAlReimportar` | Comercio sube un CSV dos veces: crea y después marca "Ya importado" |
| `ElComercioImprimeUnaEtiquetaPorBulto` | Comercio abre las etiquetas del envío de 3 bultos |
| `ElDestinatarioVeElAvanceDeUnaEntregaParcial` | Seguimiento público con "2 de 3 bultos entregados" |
| `ElDestinatarioReprogramaTrasUnIntentoFallido` | Destinatario elige otro día desde el seguimiento |
| `UnTokenInexistenteNoRevelaNada` | Token inválido: 404 con mensaje genérico |
| `ElBackofficeMuestraElDetallePorBultoDeLaEntregaParcial` | Admin abre el detalle del envío parcial |
| `UnComercioNoEntraAlBackoffice` | Un comercio no accede a la configuración |
| `ElRepartidorSoloCambiaSuContrasenaConUnaConfirmacionValidaYDeUnSoloUso` | Registra una passkey, rechaza confirmaciones inválidas o repetidas y cambia la contraseña con WebAuthn virtual |

Cada corrida crea sus datos con `POST /dev/datos-demo`, así que la Web tiene que estar en Development.
Sin `E2E_BASE_URL` los tests se saltean.

## Local

```powershell
# Una vez: instalar Chromium
pwsh tests/E2E/UltimaMilla.E2E.Tests/bin/Release/net10.0/playwright.ps1 install chromium

# Con la Web levantada (por ejemplo, el compose en http://localhost:8000 con WEB_ENTORNO=Development)
$env:E2E_BASE_URL = "http://localhost:8000"
dotnet test tests/E2E/UltimaMilla.E2E.Tests -c Release
```

## Pipeline

El job `e2e` de `.github/workflows/ci.yml` levanta PostgreSQL y Redis como servicios, aplica las migraciones,
arranca la Web, instala Chromium y corre estos tests. Publica el log de la Web y los resultados como artefacto.

## Errores que encontraron

- La página de seguimiento devolvía 404 **vacío** con un token inválido (en .NET 10 Blazor descarta la página
  si se fija el 404 a mano). Se resolvió con `Router.NotFoundPage` y `NavigationManager.NotFound()`.
- La CSP del seguimiento bloqueaba el `fetch` de la navegación de Blazor: el enlace "¿No vas a estar?" no hacía
  nada en un navegador real. Se agregó `connect-src 'self'`.
- Las líneas del tarifario cambiaban de orden entre recargas. Ahora se ordenan por zona, modalidad y peso.
