// Comprobación de cableado del recordatorio recurrente de sesiones sin subir a OpenProject.
// A diferencia del recordatorio de "sesión activa", debe arrancar en el init de la app
// (no solo dentro de un `if (getActiveSession())`), porque el usuario puede tener sesiones
// pendientes sin tener ninguna sesión en curso.
//
// Ejecutar:  node Tests/Web/pending-reminder-wiring.test.mjs
import { readFileSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const read = p => readFileSync(join(root, p), 'utf8');

const api   = read('Web/wwwroot/js/api.js');
const timer = read('Web/wwwroot/js/timer.js');
const app   = read('Web/wwwroot/js/app.js');

let failed = 0;
const check = (ok, msg) => {
    console.log(`${ok ? '  ok  ' : 'FAIL  '}${msg}`);
    if (!ok) failed++;
};

check(/export async function fetchPendingSummary/.test(api), 'api.js expone fetchPendingSummary');
check(/`\$\{API\}\/task\/pending_summary`/.test(api), 'fetchPendingSummary llama a /task/pending_summary');

check(/export function startPendingReminder/.test(timer), 'timer.js expone startPendingReminder');
check(/import \{ fetchPendingSummary \} from '\.\/api\.js'/.test(timer), 'timer.js importa fetchPendingSummary');

const pendingBlock = timer.slice(timer.indexOf('async function checkPendingSessions'), timer.indexOf('export function startPendingReminder'));
check(/tag:\s*'pending-upload-reminder'/.test(pendingBlock), 'la notificación de pendientes usa un tag propio (no pisa la de sesión activa)');
check(pendingBlock.indexOf("tag:  'session-reminder'") === -1, 'no reutiliza el tag de la notificación de sesión activa');

// El recordatorio de pendientes debe arrancar en el init general, no solo cuando hay sesión activa.
const initBlock = app.slice(app.lastIndexOf('loadProjects();'));
check(/^startPendingReminder\(\);$/m.test(initBlock), 'startPendingReminder() se invoca en el arranque de la app');
const activeSessionGuard = initBlock.slice(initBlock.indexOf('if (getActiveSession())'));
check(!/startPendingReminder/.test(activeSessionGuard), 'startPendingReminder() no depende de que haya sesión activa');

check(/startPendingReminder\(\);/.test(app) &&
      app.indexOf("result === 'granted'") < app.indexOf('startPendingReminder();'),
      'startPendingReminder() también se dispara justo al conceder el permiso de notificaciones');

console.log(failed ? `\n${failed} comprobación(es) fallida(s)` : '\nCableado correcto');
process.exit(failed ? 1 : 0);
