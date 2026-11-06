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

// Un tamaño acotado permite guardar y reenviar la imagen junto con la visita.
const MAX_IMAGEN = 256 * 1024;
const MAX_DATA_URL = 4 * Math.ceil(MAX_IMAGEN / 3) - 4;
const firmas = new WeakMap();

export async function leerFoto(entrada) {
    const foto = entrada.files[0];
    if (!foto || foto.size > 20 * 1024 * 1024 || !/^image\/(jpeg|png|webp|heic|heif)$/.test(foto.type))
        return null;

    try {
        const imagen = await createImageBitmap(foto);
        try {
            const lienzo = document.createElement('canvas');
            const escala = Math.min(1, 960 / Math.max(imagen.width, imagen.height));
            lienzo.width = Math.max(1, Math.round(imagen.width * escala));
            lienzo.height = Math.max(1, Math.round(imagen.height * escala));
            const dibujo = lienzo.getContext('2d', { alpha: false });
            dibujo.fillStyle = 'white';
            dibujo.fillRect(0, 0, lienzo.width, lienzo.height);
            dibujo.drawImage(imagen, 0, 0, lienzo.width, lienzo.height);
            for (const calidad of [0.82, 0.62, 0.42, 0.25]) {
                const jpeg = lienzo.toDataURL('image/jpeg', calidad);
                if (jpeg.length - 'data:image/jpeg;base64,'.length <= MAX_DATA_URL) return jpeg;
            }
        } finally {
            imagen.close();
        }
    } catch {
        // Algunos navegadores no decodifican todos los formatos de la cámara.
    }
    return null;
}

export function limpiarFoto(entrada) {
    entrada.value = '';
}

export function prepararFirma(lienzo) {
    const dibujo = lienzo.getContext('2d');
    dibujo.strokeStyle = '#182432';
    dibujo.lineWidth = 3;
    dibujo.lineCap = 'round';
    dibujo.lineJoin = 'round';
    const estado = { puntero: null, punto: null, trazada: false };
    firmas.set(lienzo, estado);

    const punto = evento => {
        const caja = lienzo.getBoundingClientRect();
        return [
            (evento.clientX - caja.left) * lienzo.width / caja.width,
            (evento.clientY - caja.top) * lienzo.height / caja.height
        ];
    };
    lienzo.addEventListener('pointerdown', evento => {
        if (evento.button !== 0 || estado.puntero !== null) return;
        estado.puntero = evento.pointerId;
        estado.punto = punto(evento);
        lienzo.setPointerCapture(evento.pointerId);
    });
    lienzo.addEventListener('pointermove', evento => {
        if (evento.pointerId !== estado.puntero) return;
        const actual = punto(evento);
        dibujo.beginPath();
        dibujo.moveTo(...estado.punto);
        dibujo.lineTo(...actual);
        dibujo.stroke();
        estado.punto = actual;
        estado.trazada = true;
    });
    lienzo.addEventListener('lostpointercapture', () => { estado.puntero = null; });
}

export function leerFirma(lienzo) {
    return firmas.get(lienzo)?.trazada ? lienzo.toDataURL('image/png') : null;
}

export function limpiarFirma(lienzo) {
    lienzo.getContext('2d').clearRect(0, 0, lienzo.width, lienzo.height);
    firmas.get(lienzo).trazada = false;
}
