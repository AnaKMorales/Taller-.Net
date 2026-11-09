# UltimaMilla.Repartidor (PWA)

Blazor WebAssembly para los repartidores. En producción la sirve **Web** bajo `/repartidor/`
(la publicación se copia a `wwwroot/repartidor` en `src/UltimaMilla.Web/Dockerfile`).

Por eso `wwwroot/index.html` trae `<base href="/repartidor/" />`, el manifest tiene `scope: /repartidor/`
y `service-worker.published.js` usa `base = "/repartidor/"`.

## URL de la API (`ApiUrl`)

| Archivo | Valor | Cuándo |
| --- | --- | --- |
| `wwwroot/appsettings.json` | `http://localhost:5243/` | Desarrollo: la API de Web corriendo aparte |
| `wwwroot/appsettings.Production.json` | `/` | Publicada dentro de Web (mismo origen) |

## Correrla suelta (desarrollo)

Como el `base href` es `/repartidor/`, el servidor de desarrollo tiene que servirla bajo ese prefijo:

```powershell
dotnet run --project src/UltimaMilla.Repartidor --pathbase=/repartidor
# http://localhost:5086/repartidor/
```

Como la sirve otro origen, Web tiene que permitirlo en CORS (`Repartidor:Origenes` en `appsettings.Development.json`,
ya incluye `http://localhost:5086`).

Alternativa sin `--pathbase`: cambiar el `<base href>` a `/` en `wwwroot/index.html` (y `base` en
`service-worker.published.js`) solo para esa corrida o publicación suelta. No dejar ese cambio en el repo.

## Publicarla suelta en la raíz de un sitio

```powershell
dotnet publish src/UltimaMilla.Repartidor -c Release -o salida
# editar salida/wwwroot/index.html: <base href="/" />, y salida/wwwroot/service-worker.js (base = "/") si se usa
```
