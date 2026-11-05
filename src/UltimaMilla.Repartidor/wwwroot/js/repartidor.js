// Funciones del navegador que .NET no expone directamente: IndexedDB, estado de red y GPS.
// Se mantienen mínimas a propósito; la lógica vive en C#.

const BASE = 'ultimamilla-repartidor';
const ALMACEN = 'datos';
let conexionDb;

function abrir() {
    conexionDb ??= new Promise((resolver, rechazar) => {
        const pedido = indexedDB.open(BASE, 1);
        pedido.onupgradeneeded = () => pedido.result.createObjectStore(ALMACEN);
        pedido.onsuccess = () => resolver(pedido.result);
        pedido.onerror = () => rechazar(pedido.error);
    });
    return conexionDb;
}

// Ejecuta una operación y espera a que la transacción quede confirmada en disco.
async function transaccion(modo, operacion) {
    const db = await abrir();
    return new Promise((resolver, rechazar) => {
        const tx = db.transaction(ALMACEN, modo);
        const pedido = operacion(tx.objectStore(ALMACEN));
        tx.oncomplete = () => resolver(pedido.result);
        tx.onerror = () => rechazar(tx.error);
        tx.onabort = () => rechazar(tx.error);
    });
}

export async function leer(clave) {
    return (await transaccion('readonly', s => s.get(clave))) ?? null;
}

export async function guardar(clave, valor) {
    await transaccion('readwrite', s => s.put(valor, clave));
}

export async function borrar(clave) {
    await transaccion('readwrite', s => s.delete(clave));
}

// Avisa a .NET cada vez que cambia la conexión y devuelve el estado actual.
export function escucharConexion(receptor) {
    const avisar = () => receptor.invokeMethodAsync('CambioConexion', navigator.onLine);
    window.addEventListener('online', avisar);
    window.addEventListener('offline', avisar);
    return navigator.onLine;
}

// Posición actual o null si no hay permiso/señal. El GPS funciona sin internet.
export function ubicacion() {
    return new Promise(resolver => {
        if (!navigator.geolocation) return resolver(null);
        navigator.geolocation.getCurrentPosition(
            p => resolver([p.coords.latitude, p.coords.longitude]),
            () => resolver(null),
            { enableHighAccuracy: true, timeout: 10000, maximumAge: 60000 });
    });
}
