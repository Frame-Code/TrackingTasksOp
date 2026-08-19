// Comprobación de cableado del sidebar de configuración: rail colapsable/expandido con
// grupos en acordeón (patrón de https://uxplanet.org/case-study-research-sidebar-navigation),
// que los botones movidos (Reporte, API Key, Cerrar sesión) sigan con los mismos ids, y que
// el rail expanda + abra la sección correcta al hacer clic en un ícono de grupo.
//
// Ejecutar:  node Tests/Web/sidebar-wiring.test.mjs
import { readFileSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const read = p => readFileSync(join(root, p), 'utf8');

const html    = read('Web/wwwroot/index.html');
const css     = read('Web/wwwroot/css/app.css');
const app     = read('Web/wwwroot/js/app.js');
const api     = read('Web/wwwroot/js/api.js');
const state   = read('Web/wwwroot/js/state.js');
const sidebar = read('Web/wwwroot/js/sidebar.js');

let failed = 0;
const check = (ok, msg) => {
    console.log(`${ok ? '  ok  ' : 'FAIL  '}${msg}`);
    if (!ok) failed++;
};

// ── Markup: sidebar persistente, no offcanvas ────────────────────────────────────
check(/id="appSidebar"/.test(html), 'el <aside id="appSidebar"> existe en index.html');
check(!/offcanvas/.test(html), 'ya no queda ningún rastro del offcanvas anterior');
check(!/id="mainNav"/.test(html), 'el navbar viejo ya no existe (el sidebar es el chrome principal)');

const sidebarBlock = html.slice(html.indexOf('id="appSidebar"'), html.indexOf('</aside>'));
check(/id="apiKeyBtn"/.test(sidebarBlock), 'apiKeyBtn vive dentro del sidebar');
check(/id="logoutBtn"/.test(sidebarBlock), 'logoutBtn vive dentro del sidebar');
check(/id="activeSessionBadge"/.test(sidebarBlock) && /id="navTimer"/.test(sidebarBlock),
      'la insignia de sesión activa y el timer viven dentro del sidebar');

// Reporte es una acción sobre tareas, no de cuenta: vive como ítem suelto del nav,
// no adentro del dropdown de cuenta (ver comentarios de la conversación).
const accountDropdownBlock = sidebarBlock.slice(sidebarBlock.indexOf('sidebar-footer'));
check(/id="reportBtn"/.test(sidebarBlock), 'reportBtn vive dentro del sidebar');
check(!/id="reportBtn"/.test(accountDropdownBlock), 'reportBtn ya NO está dentro del dropdown de cuenta');

const reportBtnStart = sidebarBlock.indexOf('id="reportBtn"');
const reportBtnTag = sidebarBlock.slice(sidebarBlock.lastIndexOf('<button', reportBtnStart), reportBtnStart + 300);
check(/class="sidebar-group-header sidebar-nav-item"/.test(reportBtnTag),
      'reportBtn es un ítem de nav suelto (mismo estilo que un header de grupo)');
check(!/data-bs-toggle="collapse"/.test(reportBtnTag),
      'reportBtn no es un trigger de acordeón (no tiene panel que abrir)');

['notifSessionEnabled', 'notifSessionInterval', 'notifPendingEnabled', 'notifPendingInterval',
 'themeSwitch', 'pageSizeSelect', 'pauseBehaviorSelect',
 'skipCancelConfirmSwitch', 'addRandomSlackTimeSwitch', 'sidebarInstanceUrl', 'sidebarUserEmail', 'sidebarAvatarInitials', 'sidebarError']
    .forEach(id => check(new RegExp(`id="${id}"`).test(sidebarBlock), `#${id} existe en el sidebar`));
check(!/id="defaultActivitySelect"/.test(sidebarBlock),
      'la actividad por defecto ya no existe (era contradictoria: no toda actividad vale para toda tarea)');

// ── Grupos en acordeón: cada uno con ícono + data-bs-target coherente ────────────
['groupNotifications', 'groupAppearance', 'groupTaskBehavior', 'groupOpenProject'].forEach(groupId => {
    check(new RegExp(`data-bs-target="#${groupId}"`).test(sidebarBlock) && new RegExp(`id="${groupId}"`).test(sidebarBlock),
          `el grupo #${groupId} tiene su header y su panel colapsable`);
});
// 4 grupos + el ítem suelto "Reporte" (que reutiliza la misma clase de ícono a propósito).
check((sidebarBlock.match(/class="bi bi-\w[\w-]* sidebar-group-icon"/g) || []).length === 5, 'cada grupo (y el ítem Reporte) tiene su propio ícono');
check(/data-bs-parent="#sidebarAccordion"/.test(sidebarBlock), 'los grupos comparten acordeón (uno abierto cierra los demás)');

// ── URL de la instancia: enlace clicable que envuelve, no <span> truncado ─────────
const instanceUrlTag = sidebarBlock.slice(sidebarBlock.indexOf('id="sidebarInstanceUrl"') - 10, sidebarBlock.indexOf('id="sidebarInstanceUrl"') + 150);
check(/<a[\s\S]*id="sidebarInstanceUrl"/.test(instanceUrlTag), 'la URL de instancia es un <a> (clicable), no un <span>');
check(/target="_blank"/.test(instanceUrlTag), 'abre la instancia real en una pestaña nueva');
check(/text-break/.test(instanceUrlTag) && !/text-truncate/.test(instanceUrlTag),
      'la URL puede ocupar varias líneas en vez de cortarse con "..."');

// ── Rail / expandido: script anti-flash + variables CSS ──────────────────────────
check(/localStorage\.getItem\('sidebarCollapsed'\)/.test(html.slice(0, html.indexOf('</head>'))),
      'el estado colapsado/expandido se aplica en <head>, antes del primer paint');
check(/--sidebar-width-rail/.test(css) && /--sidebar-width-expanded/.test(css), 'app.css define los dos anchos del sidebar');
check(/html\.sidebar-collapsed \.app-sidebar/.test(css), 'el CSS angosta el sidebar cuando <html> tiene la clase sidebar-collapsed');

// ── state.js ────────────────────────────────────────────────────────────────────
check(/export function getSidebarCollapsed/.test(state) && /export function setSidebarCollapsed/.test(state),
      'state.js expone getSidebarCollapsed/setSidebarCollapsed');
check(/export const NOTIFICATION_TYPES/.test(state), 'state.js expone NOTIFICATION_TYPES');
check(/export function getTheme/.test(state) && /export function setTheme/.test(state), 'state.js expone getTheme/setTheme');
check(/export function setPageSize/.test(state), 'state.js expone setPageSize');

// ── api.js ──────────────────────────────────────────────────────────────────────
check(/export async function fetchUserSettings/.test(api), 'api.js expone fetchUserSettings');
check(/export async function updateNotificationSetting/.test(api), 'api.js expone updateNotificationSetting');
check(/export async function updateTaskPreferences/.test(api), 'api.js expone updateTaskPreferences');

// ── sidebar.js: rail expande + abre la sección correcta ──────────────────────────
check(/export function initSidebar/.test(sidebar), 'sidebar.js expone initSidebar');
check(/export function renderSidebarFields/.test(sidebar), 'sidebar.js expone renderSidebarFields (para repoblar tras loadUserSettings)');
check(!/populateDefaultActivitySelect/.test(sidebar), 'sidebar.js ya no tiene rastro de la actividad por defecto');

const groupHeaderHandler = sidebar.slice(sidebar.indexOf('function bindGroupHeaders'), sidebar.indexOf('// ── Notificaciones'));
check(/getSidebarCollapsed\(\)/.test(groupHeaderHandler), 'el clic en un grupo revisa si el rail está colapsado');
check(/setSidebarCollapsed\(false\)/.test(groupHeaderHandler), 'un clic desde el rail expande el sidebar');
check(/bootstrap\.Collapse\.getOrCreateInstance/.test(groupHeaderHandler) && /\.show\(\)/.test(groupHeaderHandler),
      'un clic desde el rail abre explícitamente esa sección (no solo alterna)');

check(/refreshNotificationTimers/.test(sidebar), 'guardar una notificación reinicia los timers en caliente');
check(/updateNotificationSetting/.test(sidebar) && /updateTaskPreferences/.test(sidebar),
      'sidebar.js llama a los endpoints de guardado');

// ── app.js: wiring de arranque ────────────────────────────────────────────────────
check(/import \{ initSidebar, renderSidebarFields \} from '\.\/sidebar\.js'/.test(app),
      'app.js importa initSidebar y renderSidebarFields');
check(/^initSidebar\(\);$/m.test(app), 'initSidebar() se invoca en el arranque');
check(/loadUserSettings\(\)\.then\([\s\S]*?renderSidebarFields\(\);/.test(app),
      'el sidebar se repuebla apenas cargan las preferencias reales (no se queda con los defaults)');
check(!/defaultActivityId/.test(app), 'app.js ya no tiene rastro de la actividad por defecto');

// ── Preferencias aplicadas en el flujo real (no solo guardadas, también usadas) ────
check(/pauseDefaultBehavior/.test(app), 'la preferencia de pausa se consulta al pausar una tarea');
check(/store\.userSettings\?\.\s*skipCancelConfirmation/.test(app),
      'la preferencia de confirmación se consulta al cancelar una sesión');

console.log(failed ? `\n${failed} comprobación(es) fallida(s)` : '\nCableado correcto');
process.exit(failed ? 1 : 0);
