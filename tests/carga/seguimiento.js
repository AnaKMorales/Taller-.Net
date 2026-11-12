// Escenario A: destinatarios mirando el seguimiento.
// MODO=capacidad (subir Seguimiento__PedidosPorMinuto en el server) o MODO=proteccion (espera 429).
import http from 'k6/http';
import { check } from 'k6';
import { Counter } from 'k6/metrics';
import { BASE, lista, requerir, etapa } from './comun.js';

const TOKENS = requerir('TOKENS', lista('TOKENS'));
const MODO = __ENV.MODO || 'capacidad';
const TASA_MAX = Number(__ENV.TASA_MAX || 100);

const limitados = new Counter('seguimiento_limitados_429');

const capacidad = {
  executor: 'ramping-arrival-rate',
  startRate: 5,
  timeUnit: '1s',
  preAllocatedVUs: 50,
  maxVUs: 300,
  stages: [
    { target: Math.round(TASA_MAX / 2), duration: etapa('1m', '5s') }, // rampa
    { target: TASA_MAX, duration: etapa('2m', '5s') },                 // subida al pico
    { target: TASA_MAX, duration: etapa('3m', '10s') },                 // sostenido
    { target: 0, duration: etapa('30s', '3s') },                       // bajada
  ],
};

const proteccion = { executor: 'constant-arrival-rate', rate: 2, timeUnit: '1s', duration: etapa('1m', '20s'), preAllocatedVUs: 5 };

export const options = {
  scenarios: { seguimiento: MODO === 'proteccion' ? proteccion : capacidad },
  // SLO (ver README)
  thresholds: MODO === 'proteccion'
    ? { seguimiento_limitados_429: ['count>0'] }
    : {
        'http_req_duration{tipo:api}': ['p(95)<300', 'p(99)<800'],
        'http_req_duration{tipo:pagina}': ['p(95)<500', 'p(99)<1200'],
        http_req_failed: ['rate<0.01'],
        checks: ['rate>0.99'],
      },
};

// 404 = token mal copiado, no es falla
http.setResponseCallback(http.expectedStatuses(200, 404));

export default function () {
  const token = TOKENS[Math.floor(Math.random() * TOKENS.length)];
  // 2/3 API, 1/3 página
  const pagina = Math.random() < 1 / 3;
  const res = pagina
    ? http.get(`${BASE}/seguimiento/${token}`, { tags: { tipo: 'pagina', name: 'GET /seguimiento/{token}' } })
    : http.get(`${BASE}/api/seguimiento/${token}`, { tags: { tipo: 'api', name: 'GET /api/seguimiento/{token}' } });

  if (res.status === 429) limitados.add(1);
  check(res, {
    'responde 200': (r) => MODO === 'proteccion' ? [200, 429].includes(r.status) : r.status === 200,
    'sin identificadores internos': (r) => r.status !== 200 || pagina || !/"(envioId|operadorId|id)"/i.test(r.body),
  });
}
