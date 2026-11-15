// Funciones del navegador que .NET no expone directamente: IndexedDB, estado de red y GPS.
// Se mantienen mínimas a propósito; la lógica vive en C#.

const BASE = 'ultimamilla-repartidor';
const ALMACEN = 'datos';
const CLAVES = 'claves';
let conexionDb;

// Versión 2: agrega el almacén de la clave de cifrado. Los datos de la versión 1 se conservan.
function abrir() {
    conexionDb ??= new Promise((resolver, rechazar) => {
        const pedido = indexedDB.open(BASE, 2);
        pedido.onupgradeneeded = () => {
            const db = pedido.result;
            if (!db.objectStoreNames.contains(ALMACEN)) db.createObjectStore(ALMACEN);
            if (!db.objectStoreNames.contains(CLAVES)) db.createObjectStore(CLAVES);
        };
        pedido.onsuccess = () => {
            // Si otra pestaña con una versión nueva necesita actualizar la base, esta la libera.
            pedido.result.onversionchange = () => { pedido.result.close(); conexionDb = null; };
            resolver(pedido.result);
        };
        pedido.onerror = () => rechazar(pedido.error);
    });
    return conexionDb;
}

// Ejecuta una operación y espera a que la transacción quede confirmada en disco.
async function transaccion(modo, operacion, almacen = ALMACEN) {
    const db = await abrir();
    return new Promise((resolver, rechazar) => {
        const tx = db.transaction(almacen, modo);
        const pedido = operacion(tx.objectStore(almacen));
        tx.oncomplete = () => resolver(pedido.result);
        tx.onerror = () => rechazar(tx.error);
        tx.onabort = () => rechazar(tx.error);
    });
}

// --- Cifrado en reposo (letra 6.3): todo lo que la PWA guarda (sesión con el token, hoja de ruta con datos de los
// destinatarios, cola de acciones con fotos y firmas) se guarda cifrado con AES-GCM de 256 bits.
// La clave se genera en el dispositivo como NO exportable: el código puede usarla pero nadie puede leer sus bytes,
// y se guarda como CryptoKey en su propio almacén. Ver docs/cifrado-pwa.md (alcance y límites).
const FORMATO = 1;
const texto = new TextEncoder();
let claveCifrado;

function claveDelDispositivo() {
    claveCifrado ??= (async () => {
        const existente = await transaccion('readonly', s => s.get('aes'), CLAVES);
        if (existente) return existente;
        const nueva = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt']);
        try {
            await transaccion('readwrite', s => s.add(nueva, 'aes'), CLAVES);
            return nueva;
        } catch {
            // Otra pestaña la creó al mismo tiempo: se usa esa, para que las dos lean lo mismo.
            return await transaccion('readonly', s => s.get('aes'), CLAVES);
        }
    })().catch(error => { claveCifrado = null; throw error; });
    return claveCifrado;
}

// El nombre del dato va como dato asociado: un registro cifrado no se puede mover a otra clave sin que falle.
async function cifrar(clave, valor) {
    const iv = crypto.getRandomValues(new Uint8Array(12));
    const datos = await crypto.subtle.encrypt({ name: 'AES-GCM', iv, additionalData: texto.encode(clave) },
        await claveDelDispositivo(), texto.encode(valor));
    return { formato: FORMATO, iv, datos };
}

async function descifrar(clave, registro) {
    const claro = await crypto.subtle.decrypt({ name: 'AES-GCM', iv: registro.iv, additionalData: texto.encode(clave) },
        await claveDelDispositivo(), registro.datos);
    return new TextDecoder().decode(claro);
}

export async function leer(clave) {
    const registro = (await transaccion('readonly', s => s.get(clave))) ?? null;
    if (registro === null) return null;
    if (typeof registro === 'string') {
        // Dato guardado antes del cifrado: se devuelve igual y se migra cifrado (no se pierde nada).
        await migrarCifrado(clave, registro);
        return registro;
    }
    try {
        if (registro.formato !== FORMATO) throw new Error('formato desconocido');
        return await descifrar(clave, registro);
    } catch (error) {
        // Clave perdida o dato alterado: no se devuelve un dato que no es confiable. Se trata como ausente para que
        // la app siga (volver a ingresar o a descargar la hoja) en vez de quedar trabada.
        console.error(`No se pudo descifrar "${clave}" en este dispositivo.`, error);
        return null;
    }
}

// Reemplaza el texto viejo por su versión cifrada solo si nadie lo cambió entretanto: si otra parte de la app guardó
// un valor nuevo después de leerlo, ese valor (ya cifrado) se respeta.
async function migrarCifrado(clave, viejo) {
    const registro = await cifrar(clave, viejo);
    await transaccion('readwrite', s => {
        const actual = s.get(clave);
        actual.onsuccess = () => { if (actual.result === viejo) s.put(registro, clave); };
        return actual;
    });
}

export async function guardar(clave, valor) {
    const registro = await cifrar(clave, valor);
    await transaccion('readwrite', s => s.put(registro, clave));
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

// Como ubicacion(), pero informa el motivo del fallo:
// [0, lat, lon] = posición; [1] = permiso denegado; [2] = sin señal; [3] = tiempo agotado; [-1] = sin soporte.
export function ubicacionDetallada() {
    return new Promise(resolver => {
        if (!navigator.geolocation) return resolver([-1]);
        navigator.geolocation.getCurrentPosition(
            p => resolver([0, p.coords.latitude, p.coords.longitude]),
            e => resolver([e.code]),
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

// --- Escaneo de códigos con la cámara (BarcodeDetector, nativo del navegador: funciona sin conexión) ---
// Chrome y Edge en Android lo traen; Safari/iOS y Firefox no: ahí queda el ingreso manual o un lector externo.
const FORMATOS = ['code_128', 'qr_code', 'code_39', 'ean_13', 'ean_8', 'itf', 'upc_a', 'data_matrix'];
let escaneo = null;

export function escanerDisponible() {
    return 'BarcodeDetector' in window && !!navigator.mediaDevices?.getUserMedia;
}

// Abre la cámara trasera en el <video> y avisa cada código leído (el mismo código, como mucho cada 2 s).
// Devuelve null si arrancó, o el motivo: 'no-disponible', 'permiso' (cámara denegada) o 'camara' (otro error).
export async function iniciarEscaneo(video, receptor) {
    detenerEscaneo();
    if (!escanerDisponible()) return 'no-disponible';
    let stream;
    try {
        const soportados = await BarcodeDetector.getSupportedFormats();
        const formatos = FORMATOS.filter(f => soportados.includes(f));
        if (formatos.length === 0) return 'no-disponible';
        const detector = new BarcodeDetector({ formats: formatos });
        stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false });
        video.srcObject = stream;
        video.setAttribute('playsinline', '');
        await video.play();
        const estado = { stream, activo: true, ultimo: null, ultimoEn: 0 };
        escaneo = estado;
        const ciclo = async () => {
            if (!estado.activo) return;
            try {
                const valor = (await detector.detect(video))[0]?.rawValue;
                const ahora = Date.now();
                if (valor && (valor !== estado.ultimo || ahora - estado.ultimoEn > 2000)) {
                    estado.ultimo = valor;
                    estado.ultimoEn = ahora;
                    navigator.vibrate?.(80);
                    await receptor.invokeMethodAsync('CodigoLeido', valor);
                }
            } catch { /* cuadro sin imagen todavía: se sigue intentando */ }
            if (estado.activo) setTimeout(ciclo, 250);
        };
        ciclo();
        return null;
    } catch (error) {
        stream?.getTracks().forEach(t => t.stop());
        return error?.name === 'NotAllowedError' || error?.name === 'SecurityError' ? 'permiso' : 'camara';
    }
}

export function detenerEscaneo() {
    if (!escaneo) return;
    escaneo.activo = false;
    escaneo.stream.getTracks().forEach(t => t.stop());
    escaneo = null;
}
