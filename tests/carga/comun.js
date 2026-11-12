// Config y helpers comunes. Todo por variables de entorno (-e NOMBRE=valor).
import encoding from 'k6/encoding';

export const BASE = (__ENV.BASE_URL || 'http://127.0.0.1:5299').replace(/\/$/, '');

export function lista(nombre) {
  return (__ENV[nombre] || '').split(',').map((s) => s.trim()).filter((s) => s.length > 0);
}

export function requerir(nombre, valores) {
  if (valores.length === 0) throw new Error(`Falta la variable ${nombre}. Ver tests/carga/README.md.`);
  return valores;
}

export function guid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16);
  });
}

// La API solo valida el encabezado; el resto es relleno para que pese como una foto real
function imagen(encabezado, kb, tipo) {
  const bytes = new Uint8Array(Math.max(encabezado.length, kb * 1024));
  bytes.set(encabezado);
  for (let i = encabezado.length; i < bytes.length; i++) bytes[i] = i % 251;
  return `data:image/${tipo};base64,${encoding.b64encode(bytes.buffer)}`;
}
const FOTO_KB = Number(__ENV.FOTO_KB || 40);
export const FOTO = imagen([0xff, 0xd8, 0xff, 0xe0], FOTO_KB, 'jpeg');
export const FIRMA = imagen([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a], 8, 'png');

// Cerca de Montevideo
export function posicion() {
  return { latitud: -34.9011 + Math.random() / 100, longitud: -56.1645 + Math.random() / 100 };
}

// RAPIDO=1: etapas cortas para probar los scripts
export const RAPIDO = __ENV.RAPIDO === '1';
export function etapa(normal, rapido) {
  return RAPIDO ? rapido : normal;
}
