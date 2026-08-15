// Cableado del registro manual de tiempo.
// El botón vive en la tarjeta (dentro de #wpGrid) y el modal se engancha por id,
// así que aquí se comprueba que cada pieza cuelgue del contenedor correcto —
// el fallo que ya tuvimos con "Subir pendientes".
//
// Ejecutar:  node Tests/Web/log-time-wiring.test.mjs
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

// Todos los ids que toca el JS tienen que existir en el HTML.
const ids = ['logTimeModal', 'logTimeTaskName', 'logTimeDate', 'logTimeStart', 'logTimeEnd',
             'logTimeHours', 'logTimeActivity', 'logTimeComment', 'logTimeError', 'confirmLogTimeBtn'];
ids.forEach(id => check(html.includes(`id="${id}"`), `#${id} existe en index.html`));

check(/btn-log-time/.test(render), 'la tarjeta muestra el botón .btn-log-time');

// El botón está en la tarjeta, así que su delegación SÍ va en #wpGrid.
const gridBlock = app.slice(app.indexOf('function bindGridEvents'), app.indexOf('function bindConfirmDatesButton'));
check(/btn-log-time/.test(gridBlock), 'el botón de la tarjeta se atiende desde la delegación de #wpGrid');
check(/^bindLogTimeModal\(\);$/m.test(app), 'bindLogTimeModal() se invoca en el arranque');
check(/confirmLogTimeBtn'\)\.addEventListener\('click'/.test(app), 'el botón Registrar tiene handler');

// Estado del sistema y prevención de errores.
const handler = app.slice(app.indexOf('async function handleLogTime'), app.indexOf('function showLogTimeError'));
check(/btn\.disabled = true/.test(handler), 'el botón se bloquea mientras registra (evita doble envío)');
check(/showLogTimeError\(e\.message\)/.test(handler), 'el error se muestra en el modal, no solo en un toast');
check(/finally/.test(handler), 'el botón se restaura pase lo que pase');
check(/dateInput\.max = today/.test(app), 'el selector de fecha no permite fechas futuras');

// extractId se usa para project/status: debe estar importado o el modal revienta al enviar.
check(/import \{[^}]*extractId[^}]*\} from '\.\/helpers\.js'/s.test(app), 'extractId está importado');

console.log(failed ? `\n${failed} comprobación(es) fallida(s)` : '\nCableado correcto');
process.exit(failed ? 1 : 0);
