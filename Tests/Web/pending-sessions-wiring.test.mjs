// Comprobación de cableado del modal "Sesiones sin enviar" (detalle por tarea + badge de
// conteo en el topbar y en settings.html).
//
// Ejecutar:  node Tests/Web/pending-sessions-wiring.test.mjs
import { readFileSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const read = p => readFileSync(join(root, p), 'utf8');

const api      = read('Web/wwwroot/js/api.js');
const pending  = read('Web/wwwroot/js/pending-sessions.js');
const app      = read('Web/wwwroot/js/app.js');
const timer    = read('Web/wwwroot/js/timer.js');
const settings = read('Web/wwwroot/js/settings.js');
const index    = read('Web/wwwroot/index.html');
const settingsHtml = read('Web/wwwroot/settings.html');

let failed = 0;
const check = (ok, msg) => {
    console.log(`${ok ? '  ok  ' : 'FAIL  '}${msg}`);
    if (!ok) failed++;
};

// ── API ──────────────────────────────────────────────────────────────────────────
check(/export async function fetchPendingSessions/.test(api), 'api.js expone fetchPendingSessions');
check(/`\$\{API\}\/task\/pending_sessions`/.test(api), 'fetchPendingSessions llama a /task/pending_sessions');

// ── Módulo pending-sessions.js ──────────────────────────────────────────────────────
check(/export async function refreshPendingBadge/.test(pending), 'expone refreshPendingBadge');
check(/export async function openPendingSessionsModal/.test(pending), 'expone openPendingSessionsModal');
check(/export function bindPendingSessionsModal/.test(pending), 'expone bindPendingSessionsModal');
check(/querySelectorAll\('\.js-pending-badge'\)/.test(pending), 'el badge se actualiza vía la clase compartida .js-pending-badge');

// ── index.html: entrada + modal ─────────────────────────────────────────────────────
check(/id="pendingSessionsBtn"/.test(index), 'botón del topbar existe');
check(/id="pendingSessionsBadge"[^>]*class="[^"]*js-pending-badge/.test(index), 'badge del topbar usa la clase compartida');
check(/id="pendingSessionsModal"/.test(index) && /id="pendingSessionsBody"/.test(index) &&
      /id="pendingSessionsSearch"/.test(index) && /id="uploadAllPendingBtn"/.test(index),
      'el modal (búsqueda, cuerpo, enviar todo) existe en index.html');

// ── settings.html: entrada sin modal (no carga Bootstrap JS) ───────────────────────
check(/id="viewPendingSessionsBtn"/.test(settingsHtml), 'botón en settings.html existe');
check(/<span[^>]*class="[^"]*js-pending-badge[^"]*"[^>]*id="pendingBadgeSettings"/.test(settingsHtml), 'badge de settings.html usa la clase compartida');
check(!/bootstrap\.bundle/.test(settingsHtml), 'settings.html sigue sin Bootstrap JS (por eso no abre el modal ahí)');

// ── app.js: cableado ─────────────────────────────────────────────────────────────
check(/bindPendingSessionsModal\(\);/.test(app), 'bindPendingSessionsModal() se invoca en el arranque');
check(/refreshPendingBadge\(\);/.test(app), 'refreshPendingBadge() se invoca en el arranque');
check(/loadProjects\(\);[\s\S]*loadStatuses\(\);[\s\S]*refreshPendingBadge\(\);/.test(app),
      'el badge se pide después de arrancar la carga del grid (no bloquea el camino crítico)');
check(/URLSearchParams\(location\.search\)\.get\('openPending'\)/.test(app), 'soporta abrir el modal por query param (?openPending=1)');

const pauseStart = app.indexOf('async function handlePauseSession');
const pauseHandler = app.slice(pauseStart, pauseStart + 600);
check(/if \(!uploadNow\) refreshPendingBadge\(\);/.test(pauseHandler), 'pausar guardando en local refresca el badge');

const uploadHandler = app.slice(app.indexOf('async function handleUploadPending'), app.indexOf('function setHistoryNotice'));
check(/if \(uploaded > 0\) refreshPendingBadge\(\);/.test(uploadHandler), 'subir pendientes desde el historial refresca el badge');

// ── timer.js: click en la notificación abre el modal ────────────────────────────────
check(/import \{ refreshPendingBadge, openPendingSessionsModal \} from '\.\/pending-sessions\.js'/.test(timer),
      'timer.js importa el módulo de sesiones pendientes');
check(/notif\.onclick = \(\) => \{[\s\S]*openPendingSessionsModal\(\);[\s\S]*\}/.test(timer),
      'clic en la notificación de OS abre el modal');

// ── settings.js: botón navega, sin duplicar el modal ────────────────────────────────
check(/viewPendingSessionsBtn.*addEventListener\('click', \(\) => \{ location\.href = '\/\?openPending=1'; \}\)/.test(settings),
      'settings.js navega a index.html con ?openPending=1 en vez de reimplementar el modal');

console.log(failed ? `\n${failed} comprobación(es) fallida(s)` : '\nCableado correcto');
process.exit(failed ? 1 : 0);
