// Escaneo con la cámara en la carga de la ruta (Depósito → Ruta). Mismo criterio que la PWA del repartidor:
// BarcodeDetector si el navegador lo trae; si no, ZXing (lib/zxing). Cada código leído se manda con el formulario
// de carga. La página se recarga después de cada bulto: la cámara se vuelve a abrir sola si estaba prendida.
(() => {
    const boton = document.getElementById('escanear');
    const video = document.getElementById('camara');
    const aviso = document.getElementById('aviso-camara');
    const formulario = document.getElementById('form-carga');
    if (!boton || !video || !formulario) return;
    if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) return;
    boton.hidden = false;

    const CLAVE = 'escaner-carga-activo';
    const FORMATOS = ['code_128', 'qr_code', 'code_39', 'ean_13', 'ean_8', 'itf', 'upc_a', 'data_matrix'];
    let stream = null;
    let activo = false;

    const recordar = (valor) => { try { sessionStorage.setItem(CLAVE, valor ? '1' : ''); } catch { } };
    const recordado = () => { try { return sessionStorage.getItem(CLAVE) === '1'; } catch { return false; } };
    const ULTIMO = 'escaner-carga-ultimo';
    const ultimo = () => { try { return JSON.parse(sessionStorage.getItem(ULTIMO)) ?? {}; } catch { return {}; } };

    async function detectorNativo() {
        if (!('BarcodeDetector' in window)) return null;
        try {
            const soportados = await BarcodeDetector.getSupportedFormats();
            const formatos = FORMATOS.filter(f => soportados.includes(f));
            if (formatos.length === 0) return null;
            const detector = new BarcodeDetector({ formats: formatos });
            return async () => (await detector.detect(video))[0]?.rawValue;
        } catch {
            return null;
        }
    }

    function cargarZxing() {
        return new Promise((resolver, rechazar) => {
            if (window.ZXing) return resolver(window.ZXing);
            const script = document.createElement('script');
            script.src = '/lib/zxing/zxing.min.js';
            script.onload = () => resolver(window.ZXing);
            script.onerror = rechazar;
            document.head.appendChild(script);
        });
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
        return async () => {
            if (!video.videoWidth) return null;
            const escala = Math.min(1, 640 / video.videoWidth);
            lienzo.width = Math.round(video.videoWidth * escala);
            lienzo.height = Math.round(video.videoHeight * escala);
            ctx.drawImage(video, 0, 0, lienzo.width, lienzo.height);
            const imagen = new Z.BinaryBitmap(new Z.HybridBinarizer(new Z.HTMLCanvasElementLuminanceSource(lienzo)));
            try { return lector.decodeWithState(imagen).getText(); } catch { return null; }
        };
    }

    function apagar() {
        activo = false;
        stream?.getTracks().forEach(t => t.stop());
        stream = null;
        video.hidden = true;
        boton.textContent = 'Escanear con la cámara';
    }

    async function prender() {
        aviso.textContent = '';
        try {
            stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false });
            video.srcObject = stream;
            video.hidden = false;
            await video.play();
            const leer = await detectorNativo() ?? await detectorZxing();
            activo = true;
            recordar(true);
            boton.textContent = 'Cerrar cámara';
            const ciclo = async () => {
                if (!activo) return;
                const codigo = (await leer().catch(() => null))?.trim();
                // Al reabrir la cámara el mismo bulto suele seguir enfrente: se ignora unos segundos
                if (codigo && !(codigo === ultimo().codigo && Date.now() - ultimo().en < 4000)) {
                    navigator.vibrate?.(80);
                    try { sessionStorage.setItem(ULTIMO, JSON.stringify({ codigo, en: Date.now() })); } catch { }
                    formulario.elements['Codigo'].value = codigo;
                    apagar();
                    formulario.requestSubmit();
                    return;
                }
                setTimeout(ciclo, 250);
            };
            ciclo();
        } catch (error) {
            apagar();
            recordar(false);
            aviso.textContent = error?.name === 'NotAllowedError'
                ? 'No hay permiso para usar la cámara. Habilitalo en el navegador o ingresá el código.'
                : 'No se pudo abrir la cámara. Ingresá el código a mano.';
        }
    }

    boton.addEventListener('click', () => {
        if (activo) { apagar(); recordar(false); } else { prender(); }
    });
    // Después de registrar un bulto la página se recarga: si la cámara estaba abierta, sigue escaneando.
    if (recordado()) prender();
})();
