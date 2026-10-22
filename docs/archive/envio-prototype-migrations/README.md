# Migraciones del prototipo inicial de Envio

Se conservaron aquí, sin alterar su contenido, los archivos de `Infrastructure/Migrations`
incorporados en el merge. Correspondían a una entidad simplificada Envio y registraban
un segundo historial y snapshot para el mismo AppDbContext. Su migración crea Envios,
igual que InitialCreate, por lo que ambas no pueden aplicarse juntas desde cero.

Estos archivos no se compilan ni participan en `dotnet ef`. Las migraciones vigentes se
encuentran en `src/UltimaMilla.Infrastructure/Persistence/Migrations` y no fueron modificadas.

Una base que haya aplicado únicamente InicialEnvio pertenece al prototipo. Pasarla al modelo
completo requiere revisar y migrar sus datos por separado; no alcanza con ejecutar las
migraciones vigentes sobre esa tabla. No se borraron ni transformaron bases del prototipo.
