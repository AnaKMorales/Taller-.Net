// Funciones del navegador que .NET no expone directamente.
export { estadoAccesoLocal, configurarAccesoLocal, desbloquearAccesoLocal, bloquearAccesoLocal,
    leer, guardar, borrar } from './almacen-local.js';

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

export async function biometriaDisponible() {
    return window.isSecureContext && !!navigator.credentials &&
        !!(await window.PublicKeyCredential?.isUserVerifyingPlatformAuthenticatorAvailable?.());
}

function desdeBase64Url(valor) {
    return Uint8Array.from(atob(valor.replace(/-/g, '+').replace(/_/g, '/')), c => c.charCodeAt(0));
}

function aBase64Url(valor) {
    if (valor == null) return undefined;
    const binario = Array.from(new Uint8Array(valor), byte => String.fromCharCode(byte)).join('');
    return btoa(binario).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

export async function confirmarConDispositivo(opcionesJson, registrar) {
    const opciones = JSON.parse(opcionesJson);
    opciones.challenge = desdeBase64Url(opciones.challenge);
    for (const lista of [opciones.excludeCredentials, opciones.allowCredentials]) {
        for (const credencial of lista ?? []) credencial.id = desdeBase64Url(credencial.id);
    }
    if (registrar) opciones.user.id = desdeBase64Url(opciones.user.id);
    const credencial = registrar
        ? await navigator.credentials.create({ publicKey: opciones })
        : await navigator.credentials.get({ publicKey: opciones });
    if (!credencial) throw new Error('El dispositivo no confirmó la operación.');

    const respuesta = credencial.response;
    return {
        id: credencial.id,
        rawId: aBase64Url(credencial.rawId),
        type: credencial.type,
        authenticatorAttachment: credencial.authenticatorAttachment,
        clientExtensionResults: credencial.getClientExtensionResults(),
        response: {
            clientDataJSON: aBase64Url(respuesta.clientDataJSON),
            attestationObject: aBase64Url(respuesta.attestationObject),
            transports: respuesta.getTransports?.(),
            authenticatorData: aBase64Url(respuesta.authenticatorData),
            signature: aBase64Url(respuesta.signature),
            userHandle: aBase64Url(respuesta.userHandle)
        }
    };
}

// Escaneo de códigos con la cámara. ZXing está incluido en la PWA para funcionar sin conexión.
const FORMATOS = ['code_128', 'qr_code', 'code_39', 'ean_13', 'ean_8', 'itf', 'upc_a', 'data_matrix'];
let escaneo = null;
let zxing = null;

export function escanerDisponible() {
    return window.isSecureContext && !!navigator.mediaDevices?.getUserMedia;
}

async function detectorNativo() {
    if (!('BarcodeDetector' in window)) return null;
    try {
        const soportados = await BarcodeDetector.getSupportedFormats();
        const formatos = FORMATOS.filter(f => soportados.includes(f));
        if (formatos.length === 0) return null;
        const detector = new BarcodeDetector({ formats: formatos });
        return async video => (await detector.detect(video))[0]?.rawValue;
    } catch {
        return null;
    }
}

function cargarZxing() {
    zxing ??= new Promise((resolver, rechazar) => {
        const script = document.createElement('script');
        script.src = 'lib/zxing/zxing.min.js';
        script.onload = () => resolver(window.ZXing);
        script.onerror = () => { zxing = null; rechazar(new Error('zxing')); };
        document.head.appendChild(script);
    });
    return zxing;
}

async function detectorZxing() {
    const Z = await cargarZxing();
    const f = Z.BarcodeFormat;
    const lector = new Z.MultiFormatReader();
    lector.setHints(new Map([
        [Z.DecodeHintType.POSSIBLE_FORMATS, [f.CODE_128, f.QR_CODE, f.CODE_39, f.EAN_13, f.EAN_8, f.ITF, f.UPC_A, f.DATA_MATRIX]],
        [Z.DecodeHintType.TRY_HARDER, true],
    ]));
    const lienzo = document.createElement('canvas');
    const ctx = lienzo.getContext('2d', { willReadFrequently: true });
    return async video => {
        if (!video.videoWidth) return null;
        // Achicado a 640 px de ancho: alcanza para leer y es mucho más rápido
        const escala = Math.min(1, 640 / video.videoWidth);
        lienzo.width = Math.round(video.videoWidth * escala);
        lienzo.height = Math.round(video.videoHeight * escala);
        ctx.drawImage(video, 0, 0, lienzo.width, lienzo.height);
        const imagen = new Z.BinaryBitmap(new Z.HybridBinarizer(new Z.HTMLCanvasElementLuminanceSource(lienzo)));
        try { return lector.decodeWithState(imagen).getText(); }
        catch { return null; } // sin código en este cuadro
    };
}

// Abre la cámara trasera en el <video> y avisa cada código leído (el mismo código, como mucho cada 2 s).
// Devuelve null si arrancó, o el motivo: 'no-disponible', 'permiso' (cámara denegada) o 'camara' (otro error).
export async function iniciarEscaneo(video, receptor) {
    detenerEscaneo();
    if (!escanerDisponible()) return 'no-disponible';
    let stream;
    try {
        stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false });
        video.srcObject = stream;
        video.setAttribute('playsinline', '');
        await video.play();
        const leer = await detectorNativo() ?? await detectorZxing();
        const estado = { stream, activo: true, ultimo: null, ultimoEn: 0 };
        escaneo = estado;
        const ciclo = async () => {
            if (!estado.activo) return;
            try {
                const valor = await leer(video);
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

// Avisos con la app cerrada (Web Push). La suscripción vive en el service worker: el celular despierta al
// service worker cuando llega un aviso y este muestra la notificación (service-worker.js, evento "push").
// Estados: "no-soportado" | "denegado" | "activo" | "inactivo".
export async function estadoAvisos() {
    if (!('serviceWorker' in navigator) || !('PushManager' in window) || !('Notification' in window))
        return 'no-soportado';
    if (Notification.permission === 'denied') return 'denegado';
    const registro = await navigator.serviceWorker.getRegistration();
    const suscripcion = await registro?.pushManager.getSubscription();
    return suscripcion && Notification.permission === 'granted' ? 'activo' : 'inactivo';
}

// Pide permiso y se suscribe con la clave pública VAPID del servidor. Devuelve { endpoint, p256dh, auth }
// para registrarla en la API, o { error } ("no-soportado" | "denegado" | "fallo").
export async function activarAvisos(clavePublica) {
    if ((await estadoAvisos()) === 'no-soportado') return { error: 'no-soportado' };
    if ((await Notification.requestPermission()) !== 'granted') return { error: 'denegado' };
    try {
        const registro = await navigator.serviceWorker.ready;
        let suscripcion = await registro.pushManager.getSubscription();
        const clave = base64UrlABytes(clavePublica);
        // Si el servidor cambió de clave, la suscripción vieja ya no sirve: se rehace.
        const actual = suscripcion?.options?.applicationServerKey;
        if (suscripcion && actual && !mismosBytes(new Uint8Array(actual), clave)) {
            await suscripcion.unsubscribe();
            suscripcion = null;
        }
        suscripcion ??= await registro.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: clave });
        const datos = suscripcion.toJSON();
        return { endpoint: datos.endpoint, p256dh: datos.keys.p256dh, auth: datos.keys.auth };
    } catch {
        return { error: 'fallo' };
    }
}

// Se da de baja en el navegador (al cerrar sesión) y devuelve el endpoint para borrarlo en el servidor.
export async function desactivarAvisos() {
    try {
        const registro = await navigator.serviceWorker?.getRegistration();
        const suscripcion = await registro?.pushManager.getSubscription();
        if (!suscripcion) return null;
        const endpoint = suscripcion.endpoint;
        await suscripcion.unsubscribe();
        return endpoint;
    } catch {
        return null;
    }
}

function base64UrlABytes(texto) {
    const b64 = (texto + '='.repeat((4 - texto.length % 4) % 4)).replace(/-/g, '+').replace(/_/g, '/');
    return Uint8Array.from(atob(b64), c => c.charCodeAt(0));
}

function mismosBytes(a, b) {
    return a.length === b.length && a.every((v, i) => v === b[i]);
}
