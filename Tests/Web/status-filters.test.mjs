// Filtros de estado + búsqueda.
// Bug que motiva este test: la búsqueda anulaba los filtros de estado, pero las pills
// seguían pintadas como activas, así que la pantalla mostraba un filtro que no se estaba
// aplicando. Ahora se combinan (AND) y lo que se ve es lo que está en efecto.
//
// Ejecutar:  node Tests/Web/status-filters.test.mjs
import { readFileSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const read = p => readFileSync(join(root, p), 'utf8');

let failed = 0;
const check = (ok, msg) => {
    console.log(`${ok ? '  ok  ' : 'FAIL  '}${msg}`);
    if (!ok) failed++;
};

// ── Lógica de filtrado, replicada tal cual está en renderCards ────────────────
const wps = [
    { id: 2398, subject: 'Montar la base de datos de produccion', _links: { status: { title: 'Developed' } } },
    { id: 2397, subject: 'LANEC: Correccion tras Test Failed',    _links: { status: { title: 'Developed' } } },
    { id: 2353, subject: 'LANEC: CORRECION DEL RECOSTEO',         _links: { status: { title: 'Test failed' } } },
    { id: 2314, subject: 'Agregar campos sap',                    _links: { status: { title: 'In progress' } } },
    { id:   23, subject: 'Tarea vieja',                           _links: { status: { title: 'New' } } }
];

function filter(query, activeFilters) {
    const q = query.trim().toLowerCase();
    const active = new Set(activeFilters);
    return wps.filter(wp => {
        const matchesQuery = !q
            || wp.subject?.toLowerCase().includes(q)
            || String(wp.id).includes(q);
        if (!matchesQuery) return false;
        if (active.size === 0) return true;
        return active.has(wp._links?.status?.title || 'Sin estado');
    });
}

const ids = r => r.map(w => w.id).sort((a, b) => a - b).join(',');

check(ids(filter('', [])) === '23,2314,2353,2397,2398', 'sin búsqueda ni filtros: pasa todo');
check(ids(filter('', ['Developed'])) === '2397,2398', 'solo filtro de estado');
check(ids(filter('LANEC', [])) === '2353,2397', 'solo búsqueda');

// El caso de la captura: buscar "23" con filtros encendidos.
check(ids(filter('23', ['Developed'])) === '2397,2398',
      'búsqueda Y filtro se combinan (antes el filtro se ignoraba)');
check(ids(filter('23', ['New'])) === '23', 'la combinación acota al estado elegido');
check(filter('LANEC', ['In progress']).length === 0,
      'combinación sin coincidencias devuelve vacío, no "todo"');
check(ids(filter('', ['Developed', 'New'])) === '23,2397,2398', 'multi-selección es OR entre estados');

// ── Claridad visual de las pills ──────────────────────────────────────────────
const render = read('Web/wwwroot/js/render.js');
const css = read('Web/wwwroot/css/app.css');

check(/aria-pressed="\$\{isActive\}"/.test(render), 'las pills exponen aria-pressed');
check(/bi-check2.*:.*bi-plus/.test(render), 'ícono distinto según seleccionada o no (canal no cromático)');

const pillCss = css.slice(css.indexOf('/* ── Status filter pills'), css.indexOf('/* ── History table'));
check(!/^\s*opacity:\s*0\.35/m.test(pillCss), 'la pill inactiva ya no se atenúa (se leía como deshabilitada)');
check(/:not\(\.is-active\)/.test(pillCss), 'la pill inactiva tiene estilo propio de contorno');
check(/focus-visible/.test(pillCss), 'el foco de teclado es visible');

console.log(failed ? `\n${failed} comprobación(es) fallida(s)` : '\nFiltros correctos');
process.exit(failed ? 1 : 0);
