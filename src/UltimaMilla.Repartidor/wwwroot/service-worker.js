// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
self.addEventListener('fetch', () => { });

// Avisos push con la app cerrada (docs/push-repartidor.md). El celular despierta a este service worker cuando
// llega un aviso del Worker (reasignación desde el Backoffice) y se muestra la notificación del sistema.
self.addEventListener('push', event => event.waitUntil(mostrarAviso(event)));
self.addEventListener('notificationclick', event => event.waitUntil(abrirApp(event)));

async function mostrarAviso(event) {
    let aviso = {};
    try { aviso = event.data ? event.data.json() : {}; } catch { aviso = { cuerpo: event.data?.text() }; }
    let cuerpo = aviso.cuerpo || 'Hay cambios en tu hoja de ruta.';
    if (aviso.llegadaEstimada) {
        const hora = new Date(aviso.llegadaEstimada).toLocaleTimeString('es', { hour: '2-digit', minute: '2-digit' });
        cuerpo += ` Llegada estimada ${hora}.`;
    }
    await self.registration.showNotification(aviso.titulo || 'UltimaMilla', {
        body: cuerpo,
        tag: aviso.etiqueta || 'ultimamilla',
        renotify: true,
        icon: 'icon-192.png',
        badge: 'icon-192.png',
        data: { url: self.registration.scope },
    });
}

async function abrirApp(event) {
    event.notification.close();
    const destino = event.notification.data?.url || self.registration.scope;
    const ventanas = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    const abierta = ventanas.find(ventana => ventana.url.startsWith(self.registration.scope));
    if (abierta) return abierta.focus();
    return self.clients.openWindow(destino);
}
