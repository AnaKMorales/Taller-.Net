const BASE = 'ultimamilla-repartidor';
const DATOS = 'datos';
const PROTECCION = 'proteccion';
const CLAVES_ANTERIORES = 'claves';
const CONFIGURACION = 'acceso';
const FORMATO_ANTERIOR = 1;
const FORMATO = 2;
const VERSION_DERIVACION = 1;
const ITERACIONES = 600_000;
const VERIFICACION = 'ultimamilla-acceso-local';
const texto = new TextEncoder();
const lector = new TextDecoder();
let conexionDb;
let claveCifrado;

function abrir() {
    conexionDb ??= new Promise((resolver, rechazar) => {
        const pedido = indexedDB.open(BASE, 3);
        let bloqueado = false;
        pedido.onupgradeneeded = () => {
            const db = pedido.result;
            if (!db.objectStoreNames.contains(DATOS)) db.createObjectStore(DATOS);
            db.createObjectStore(PROTECCION);
        };
        pedido.onsuccess = () => {
            const db = pedido.result;
            if (bloqueado) {
                db.close();
                return;
            }
            db.onversionchange = () => { db.close(); conexionDb = null; };
            resolver(db);
        };
        pedido.onblocked = () => {
            bloqueado = true;
            rechazar(new Error('Otra pestaña mantiene abierta la versión anterior del almacenamiento.'));
        };
        pedido.onerror = () => rechazar(pedido.error);
    }).catch(error => { conexionDb = null; throw error; });
    return conexionDb;
}

function ejecutar(db, almacenes, modo, operar) {
    return new Promise((resolver, rechazar) => {
        const tx = db.transaction(almacenes, modo);
        tx.onabort = () => rechazar(tx.error ?? new Error('Se revirtió el almacenamiento local.'));
        try {
            const resultado = operar(tx);
            tx.oncomplete = () => resolver(resultado());
        } catch (error) {
            tx.abort();
            rechazar(error);
        }
    });
}

async function transaccion(almacen, modo, operar) {
    return ejecutar(await abrir(), [almacen], modo, tx => {
        const pedido = operar(tx.objectStore(almacen));
        return () => pedido.result;
    });
}

function configuracion() {
    return transaccion(PROTECCION, 'readonly', s => s.get(CONFIGURACION));
}

async function derivar(frase, sal) {
    const bytes = texto.encode(frase);
    try {
        const material = await crypto.subtle.importKey('raw', bytes, 'PBKDF2', false, ['deriveKey']);
        return await crypto.subtle.deriveKey({ name: 'PBKDF2', hash: 'SHA-256', salt: sal, iterations: ITERACIONES },
            material, { name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt']);
    } finally {
        bytes.fill(0);
    }
}

async function cifrar(llave, contexto, valor) {
    const iv = crypto.getRandomValues(new Uint8Array(12));
    const datos = await crypto.subtle.encrypt({ name: 'AES-GCM', iv, additionalData: texto.encode(contexto) },
        llave, texto.encode(valor));
    return { formato: FORMATO, iv, datos };
}

async function descifrar(llave, contexto, registro) {
    const claro = await crypto.subtle.decrypt(
        { name: 'AES-GCM', iv: registro.iv, additionalData: texto.encode(contexto) }, llave, registro.datos);
    return lector.decode(claro);
}

export async function estadoAccesoLocal() {
    if (claveCifrado) return 'abierto';
    return await configuracion() ? 'bloqueado' : 'sinConfigurar';
}

export async function desbloquearAccesoLocal(frase) {
    const config = await configuracion();
    if (!config || config.version !== VERSION_DERIVACION || config.verificador?.formato !== FORMATO)
        throw new Error('Configuración de protección local inválida.');
    const llave = await derivar(frase, config.sal);
    try {
        if (await descifrar(llave, VERIFICACION, config.verificador) !== VERIFICACION) return false;
    } catch (error) {
        if (error.name === 'OperationError') return false;
        throw error;
    }
    claveCifrado = llave;
    return true;
}

export async function configurarAccesoLocal(frase) {
    if (frase.trim().length < 12) throw new Error('La frase requiere al menos 12 caracteres.');
    const db = await abrir();
    const almacenes = [DATOS, PROTECCION];
    if (db.objectStoreNames.contains(CLAVES_ANTERIORES)) almacenes.push(CLAVES_ANTERIORES);

    const anterior = await ejecutar(db, almacenes, 'readonly', tx => {
        const config = tx.objectStore(PROTECCION).get(CONFIGURACION);
        const datos = tx.objectStore(DATOS);
        const claves = datos.getAllKeys();
        const valores = datos.getAll();
        const llave = almacenes.includes(CLAVES_ANTERIORES) ? tx.objectStore(CLAVES_ANTERIORES).get('aes') : null;
        return () => ({ config: config.result, claves: claves.result, valores: valores.result, llave: llave?.result });
    });
    if (anterior.config) return desbloquearAccesoLocal(frase);

    const sal = crypto.getRandomValues(new Uint8Array(16));
    const llave = await derivar(frase, sal);
    const verificador = await cifrar(llave, VERIFICACION, VERIFICACION);
    const migrados = [];
    for (const [i, valor] of anterior.valores.entries()) {
        const nombre = anterior.claves[i];
        const claro = typeof valor === 'string' ? valor : await leerAnterior(anterior.llave, nombre, valor);
        migrados.push(await cifrar(llave, 'dato:' + nombre, claro));
    }

    const instalado = await ejecutar(db, almacenes, 'readwrite', tx => {
        const proteccion = tx.objectStore(PROTECCION);
        const actual = proteccion.get(CONFIGURACION);
        let guardado = false;
        actual.onsuccess = () => {
            if (actual.result) return;
            const datos = tx.objectStore(DATOS);
            migrados.forEach((registro, i) => datos.put(registro, anterior.claves[i]));
            if (almacenes.includes(CLAVES_ANTERIORES)) tx.objectStore(CLAVES_ANTERIORES).clear();
            proteccion.add({ version: VERSION_DERIVACION, sal, verificador }, CONFIGURACION);
            guardado = true;
        };
        return () => guardado;
    });
    if (!instalado) return desbloquearAccesoLocal(frase);
    claveCifrado = llave;
    return true;
}

function leerAnterior(llave, nombre, registro) {
    if (!llave || registro?.formato !== FORMATO_ANTERIOR)
        throw new Error('No se pudo migrar un registro anterior.');
    // La versión 2 usaba el nombre sin prefijo como dato asociado.
    return descifrar(llave, nombre, registro);
}

export function bloquearAccesoLocal() {
    claveCifrado = undefined;
}

window.addEventListener('pagehide', bloquearAccesoLocal);
window.addEventListener('pageshow', evento => {
    if (evento.persisted) location.reload();
});

export async function leer(nombre) {
    const llave = claveCifrado;
    if (!llave) return null;
    const registro = await transaccion(DATOS, 'readonly', s => s.get(nombre));
    if (!registro || registro.formato !== FORMATO) return null;
    try {
        return await descifrar(llave, 'dato:' + nombre, registro);
    } catch (error) {
        console.error('No se pudo descifrar un registro local.', error);
        return null;
    }
}

export async function guardar(nombre, valor) {
    if (!claveCifrado) throw new Error('El almacenamiento local está bloqueado.');
    const registro = await cifrar(claveCifrado, 'dato:' + nombre, valor);
    await transaccion(DATOS, 'readwrite', s => s.put(registro, nombre));
}

export async function borrar(nombre) {
    if (!claveCifrado) throw new Error('El almacenamiento local está bloqueado.');
    await transaccion(DATOS, 'readwrite', s => s.delete(nombre));
}
