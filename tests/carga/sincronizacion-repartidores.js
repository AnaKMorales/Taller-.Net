// Escenario B: varios repartidores vuelven al depósito y sincronizan a la vez.
// Cada VU es un dispositivo; reenvía la misma entrega (mismo IdOperacion) para probar la idempotencia.
// Consume las paradas: usar datos recién sembrados.
import http from 'k6/http';
import { check, sleep, fail } from 'k6';
import { BASE, lista, requerir, guid, FOTO, FIRMA, posicion, etapa } from './comun.js';

const REPARTIDORES = requerir('REPARTIDORES', lista('REPARTIDORES'));
const CLAVE = __ENV.CLAVE || fail('Falta la variable CLAVE.');
const DISPOSITIVOS = Number(__ENV.DISPOSITIVOS || 20);
const json = { headers: { 'Content-Type': 'application/json' } };

export const options = {
  scenarios: {
    regreso_al_deposito: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { target: DISPOSITIVOS, duration: etapa('30s', '5s') },
        { target: DISPOSITIVOS, duration: etapa('2m', '20s') },
        { target: 0, duration: etapa('20s', '3s') },
      ],
      gracefulRampDown: '10s',
    },
  },
  // SLO (ver README)
  thresholds: {
    'http_req_duration{name:POST /api/repartidor/intentos}': ['p(95)<1500', 'p(99)<3000'],
    'http_req_duration{name:GET /api/repartidor/rutas/{id}}': ['p(95)<800'],
    'http_req_duration{name:GET /api/repartidor/rutas}': ['p(95)<500'],
    http_req_failed: ['rate<0.01'],
    'checks{paso:intento}': ['rate>0.99'],
  },
};

// 429 en telemetría = límite por dispositivo
http.setResponseCallback(http.expectedStatuses(200, 204, 429));

function autorizado(token, extra = {}) {
  return { headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' }, ...extra };
}

// Login de cada cuenta y reparto de paradas: una por dispositivo
export function setup() {
  const cuentas = [];
  const trabajos = [];
  for (const email of REPARTIDORES) {
    const login = http.post(`${BASE}/api/repartidor/sesion`, JSON.stringify({ email, contrasena: CLAVE }), json);
    if (login.status !== 200) fail(`No se pudo iniciar sesión con ${email} (HTTP ${login.status}).`);
    const token = login.json('accessToken');
    cuentas.push({ email, token });
    const rutas = http.get(`${BASE}/api/repartidor/rutas`, autorizado(token)).json() || [];
    for (const ruta of rutas) {
      const hoja = http.get(`${BASE}/api/repartidor/rutas/${ruta.rutaId}`, autorizado(token)).json();
      for (const p of (hoja && hoja.paradas) || []) {
        if (p.estadoEnvio === 'EnTransito') trabajos.push({ token, parada: p });
      }
    }
  }
  console.log(`${cuentas.length} cuentas, ${trabajos.length} paradas pendientes para ${DISPOSITIVOS} dispositivos.`);
  return { cuentas, trabajos };
}

// Entrega con las pruebas que piden las reglas
function solicitudEntrega(parada, idOperacion) {
  const ahora = new Date().toISOString();
  const evidencias = parada.reglas.pruebasExigidas.map((tipo) => ({
    referencia: guid(),
    tipo,
    hora: ahora,
    posicion: posicion(),
    archivo: tipo === 'Foto' ? FOTO : tipo === 'Firma' ? FIRMA : null,
    texto: tipo === 'Nombre' ? 'Receptor de prueba' : tipo === 'Documento' ? '12345678' : null,
  }));
  return {
    envioId: parada.envioId,
    paradaId: parada.paradaId,
    versionEsperada: parada.versionEsperadaEnvio,
    idOperacion,
    ocurridoEn: ahora,
    bultos: parada.bultos.map((b) => ({ bultoId: b.bultoId, resultado: 'Entregado', motivoId: null,
      evidencias: evidencias.map((e) => e.referencia) })),
    evidencias,
    posicion: posicion(),
  };
}

// Por VU
const dispositivo = guid();
let entregaPendiente = null;

export default function (data) {
  const i = __VU - 1;
  const cuenta = data.cuentas[i % data.cuentas.length];
  const trabajo = data.trabajos[i];
  // Se arma una vez; los reintentos mandan la misma
  if (trabajo && entregaPendiente === null) entregaPendiente = JSON.stringify(solicitudEntrega(trabajo.parada, guid()));

  const sesion = http.get(`${BASE}/api/repartidor/sesion`, autorizado(cuenta.token, { tags: { name: 'GET /api/repartidor/sesion' } }));
  check(sesion, { 'sesión vigente': (r) => r.status === 200 });

  const rutas = http.get(`${BASE}/api/repartidor/rutas`, autorizado(cuenta.token, { tags: { name: 'GET /api/repartidor/rutas' } }));
  check(rutas, { 'hojas de ruta': (r) => r.status === 200 });
  const primera = rutas.status === 200 ? (rutas.json() || [])[0] : null;
  if (primera) {
    const hoja = http.get(`${BASE}/api/repartidor/rutas/${primera.rutaId}`,
      autorizado(cuenta.token, { tags: { name: 'GET /api/repartidor/rutas/{id}' } }));
    check(hoja, { 'detalle de la hoja': (r) => r.status === 200 });
  }

  if (entregaPendiente !== null) {
    const res = http.post(`${BASE}/api/repartidor/intentos`, entregaPendiente,
      autorizado(trabajo.token, { tags: { name: 'POST /api/repartidor/intentos', paso: 'intento' } }));
    check(res, {
      'entrega sincronizada': (r) => r.status === 200,
      'reenvío sin duplicar': (r) => r.status !== 200 || r.json('estadoActualEnvio') === 'Entregado',
    }, { paso: 'intento' });
  }

  http.post(`${BASE}/api/repartidor/telemetria`, JSON.stringify({ dispositivoId: dispositivo, pendientes: 0, rechazadas: 0 }),
    autorizado(cuenta.token, { tags: { name: 'POST /api/repartidor/telemetria' } }));

  sleep(3 + Math.random() * 4);
}
