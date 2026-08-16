// Comprobación de cableado del botón "Subir pendientes".
// El bug que motiva este test: la delegación de clicks colgaba de #wpGrid, pero Bootstrap
// monta el modal de historial fuera de ese contenedor, así que el botón no hacía nada
// y el usuario no podía saber si su tiempo se había subido o no.
//
// Ejecutar:  node Tests/Web/upload-pending-wiring.test.mjs
import { readFileSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const read = p => readFileSync(join(root, p), 'utf8');

const app = read('Web/wwwroot/js/app.js');
const render = read('Web/wwwroot/js/render.js');
const html = read('Web/wwwroot/index.html');

let failed = 0;
const check = (ok, msg) => {
    console.log(`${ok ? '  ok  ' : 'FAIL  '}${msg}`);
    if (!ok) failed++;
};

check(/btn-upload-pending/.test(render), 'render.js emite el botón .btn-upload-pending');
check(/id="historyModal"/.test(html) && /id="historyBody"/.test(html), 'el modal de historial existe en index.html');

const gridBlock = app.slice(app.indexOf('function bindGridEvents'), app.indexOf('function bindConfirmDatesButton'));
check(!/btn-upload-pending/.test(gridBlock), 'la delegación NO cuelga de #wpGrid (el modal no está dentro)');
check(/getElementById\('historyModal'\)\.addEventListener\('click'/.test(app), 'la delegación cuelga de #historyModal');
check(/^bindHistoryModalEvents\(\);$/m.test(app), 'bindHistoryModalEvents() se invoca en el arranque');

// Visibilidad del estado del sistema: ningún camino puede terminar sin decirle al usuario qué pasó.
const handler = app.slice(app.indexOf('async function handleUploadPending'), app.indexOf('function setHistoryNotice'));
check(/setHistoryNotice\('danger'/.test(handler), 'avisa en pantalla cuando la subida falla');
check(/setHistoryNotice\('success'/.test(handler), 'avisa en pantalla cuando la subida funciona');
check(handler.indexOf('postUploadPending') < handler.indexOf('fetchTask'),
      'subida y refresco van en bloques separados (un refresco fallido no debe reportarse como subida fallida)');

console.log(failed ? `\n${failed} comprobación(es) fallida(s)` : '\nCableado correcto');
process.exit(failed ? 1 : 0);
