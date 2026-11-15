// Tablero en vivo por SignalR.
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
    for (const [indice, texto] of columnas.entries()) {
      const celda = document.createElement('td');
      if (indice === 4) {
        // Enlace al detalle del envío; el id es un Guid y el texto va por textContent (no se inyecta HTML).
        const enlace = document.createElement('a');
        enlace.href = '/backoffice/envios/' + encodeURIComponent(cambio.envioId);
        enlace.textContent = texto;
        celda.appendChild(enlace);
      } else {
        celda.textContent = texto; // textContent evita inyectar HTML
      }
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

  // Envíos en riesgo y flota: el servidor arma el HTML (mismo parcial que la carga inicial) y acá se reemplaza.
  // Se refresca cuando llega un cambio (agrupando ráfagas) y cada minuto, porque un envío entra en riesgo
  // también con el solo paso del tiempo.
  const panel = document.getElementById('panel-riesgo');
  let refrescoPendiente = null;
  async function refrescarPanel() {
    refrescoPendiente = null;
    try {
      const respuesta = await fetch('/backoffice/tablero?handler=Panel', { headers: { 'Accept': 'text/html' } });
      if (respuesta.ok) {
        panel.innerHTML = await respuesta.text(); // HTML generado y escapado por el servidor
        dibujarFlota();
      }
    } catch { /* sin conexión: se reintenta en el próximo ciclo */ }
  }

  // Mapa de la flota: un marcador por vehículo con su última posición
  const mapa = window.L ? L.map('mapa-flota').setView([-34.88, -56.17], 11) : null;
  mapa && L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19, attribution: '&copy; OpenStreetMap'
  }).addTo(mapa);
  const marcadores = mapa ? L.layerGroup().addTo(mapa) : null;
  let encuadrado = false;

  function dibujarFlota() {
    if (!mapa) return;
    const posiciones = JSON.parse(document.getElementById('posiciones-flota')?.textContent ?? '[]');
    marcadores.clearLayers();
    for (const p of posiciones) {
      const texto = document.createElement('div'); // textContent: sin inyectar HTML
      texto.textContent = `${p.vehiculo ?? 'Sin vehículo'} · ${p.repartidor} · ${p.hora}`;
      L.marker([p.lat, p.lon]).bindPopup(texto).bindTooltip(p.vehiculo ?? p.repartidor).addTo(marcadores);
    }
    // Solo se encuadra la primera vez, para no mover el mapa mientras el usuario lo mira
    if (!encuadrado && posiciones.length > 0) {
      mapa.fitBounds(posiciones.map(p => [p.lat, p.lon]), { maxZoom: 14, padding: [30, 30] });
      encuadrado = true;
    }
  }
  dibujarFlota();
  function pedirRefresco() {
    if (refrescoPendiente === null) refrescoPendiente = setTimeout(refrescarPanel, 1000);
  }
  setInterval(refrescarPanel, 60000);

  conexion.on('cambiosEnvio', cambios => {
    for (const cambio of cambios) {
      ajustarContador(cambio.estadoAnterior, -1);
      ajustarContador(cambio.estadoNuevo, +1);
      agregarFila(cambio);
    }
    pedirRefresco();
  });

  conexion.onreconnecting(() => mostrarConexion('Reconectando…', 'aviso'));
  // Se recarga para no perder cambios ocurridos durante el corte.
  conexion.onreconnected(() => location.reload());
  conexion.onclose(() => mostrarConexion('Desconectado: recargá la página', 'error'));

  conexion.start()
    .then(() => mostrarConexion('En vivo', 'ok'))
    .catch(() => mostrarConexion('No se pudo conectar', 'error'));

  // Simulación (solo desarrollo).
  const resultado = document.getElementById('resultado-simulacion');
  for (const boton of document.querySelectorAll('[data-simular]')) {
    boton.addEventListener('click', async () => {
      const respuesta = await fetch(boton.dataset.simular, { method: 'POST' });
      resultado.textContent = respuesta.ok ? '' : 'No hay envíos para esa acción.';
    });
  }
})();
