// NeoSTP Cloud — k6 baseline load test (Sprint 20 Hardening)
//
// Smoke/baseline de carga sobre endpoints críticos: health, login y un endpoint
// autenticado. Sirve para fijar una línea base de latencia/errores antes de
// producción y para validar que el rate limiting responde 429 bajo presión.
//
// Uso:
//   k6 run -e BASE_URL=https://localhost:7043 -e USER=admin.prueba -e PASS=ChangeMe!2026 ops/k6/baseline.js
//
// Requisitos: https://k6.io  (no se ejecuta en CI por defecto; es una herramienta de ops)

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

const errorRate = new Rate('errors');

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5043';
const USER = __ENV.USER || 'admin.prueba';
const PASS = __ENV.PASS || 'ChangeMe!2026';

export const options = {
  scenarios: {
    smoke: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '30s', target: 10 },  // ramp-up
        { duration: '1m', target: 10 },    // sostenido
        { duration: '15s', target: 0 },    // ramp-down
      ],
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<800'], // p95 < 800ms
    errors: ['rate<0.05'],            // < 5% de errores (excluye 429 esperados)
    http_req_failed: ['rate<0.10'],
  },
};

export function setup() {
  // Health no requiere auth.
  const health = http.get(`${BASE_URL}/health`);
  check(health, { 'health 200': (r) => r.status === 200 });

  const res = http.post(`${BASE_URL}/api/auth/login`, JSON.stringify({
    usernameOrEmail: USER,
    password: PASS,
  }), { headers: { 'Content-Type': 'application/json' } });

  const token = res.json('data.accessToken');
  return { token };
}

export default function (data) {
  const headers = { Authorization: `Bearer ${data.token}` };

  const me = http.get(`${BASE_URL}/api/auth/me`, { headers });
  // 200 OK o 429 (rate limit) son ambos respuestas válidas del sistema.
  check(me, {
    'me ok o rate-limited': (r) => r.status === 200 || r.status === 429,
  });
  errorRate.add(me.status >= 500);

  const dashboard = http.get(`${BASE_URL}/api/dashboard/empresa`, { headers });
  check(dashboard, { 'dashboard sin 5xx': (r) => r.status < 500 });
  errorRate.add(dashboard.status >= 500);

  sleep(1);
}
