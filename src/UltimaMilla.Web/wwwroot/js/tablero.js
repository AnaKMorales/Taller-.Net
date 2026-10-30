// Tablero en vivo: escucha "cambiosEnvio" en /hubs/tablero y actualiza contadores y tabla sin recargar.
(() => {
  const textos = JSON.parse(document.getElementById('textos-tablero').textContent);
  const indicador = document.getElementById('conexion');
  const cuerpo = document.getElementById('cambios');
  const filasVisibles = 20;

  const nombreEstado = estado => (estado == null ? '—' : textos.estados[estado] ?? estado);
  const fecha = iso => new Date(iso).toLocaleString('es-UY');

  function resaltar(elemento) {
    elemento.classList.add('cambio');
    setTimeout(() => elemento.classList.remove('cambio'), 1500);
  }

  function ajustarContador(estado, diferencia) {
    if (estado == null) return; // el alta no tiene estado anterior
    const contador = document.getElementById('estado-' + estado);
    if (!contador) return;
    contador.textContent = Math.max(0, Number(contador.textContent) + diferencia);
    resaltar(contador.parentElement);
  }

  function agregarFila(cambio) {
    document.getElementById('sin-cambios')?.remove();
    const fila = document.createElement('tr');
    const columnas = [fecha(cambio.ocurridoEn), textos.eventos[cambio.tipo] ?? cambio.tipo,
      nombreEstado(cambio.estadoAnterior), nombreEstado(cambio.estadoNuevo),
      cambio.envioId.replaceAll('-', '').slice(0, 8), cambio.detalle ?? ''];
    for (const texto of columnas) {
      const celda = document.createElement('td');
      celda.textContent = texto; // textContent, nunca innerHTML: el detalle es texto libre
      fila.appendChild(celda);
    }
    fila.classList.add('nuevo');
    cuerpo.prepend(fila);
    setTimeout(() => fila.classList.remove('nuevo'), 1500);
    while (cuerpo.rows.length > filasVisibles) cuerpo.deleteRow(-1);
  }

  function mostrarConexion(texto, clase) {
    indicador.textContent = texto;
    indicador.className = clase;
  }

  const conexion = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/tablero')
    .withAutomaticReconnect()
    .build();

  conexion.on('cambiosEnvio', cambios => {
    for (const cambio of cambios) {
      ajustarContador(cambio.estadoAnterior, -1);
      ajustarContador(cambio.estadoNuevo, +1);
      agregarFila(cambio);
    }
  });

  conexion.onreconnecting(() => mostrarConexion('Reconectando…', 'aviso'));
  // Durante el corte se pudieron perder cambios: se recarga para volver a leer los totales.
  conexion.onreconnected(() => location.reload());
  conexion.onclose(() => mostrarConexion('Desconectado: recargá la página', 'error'));

  conexion.start()
    .then(() => mostrarConexion('En vivo', 'ok'))
    .catch(() => mostrarConexion('No se pudo conectar', 'error'));

  // Botones de simulación (solo existen en desarrollo).
  const resultado = document.getElementById('resultado-simulacion');
  for (const boton of document.querySelectorAll('[data-simular]')) {
    boton.addEventListener('click', async () => {
      const respuesta = await fetch(boton.dataset.simular, { method: 'POST' });
      resultado.textContent = respuesta.ok ? '' : 'No hay envíos para esa acción.';
    });
  }
})();
