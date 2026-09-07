// Vista Árbol: la jerarquía de work packages, un nivel por expansión.
//
// Responde una pregunta distinta a la de la grilla ("¿cómo va la implantación?" en vez de
// "¿qué hago ahora?"), y por eso muestra tareas de CUALQUIER persona: las propias ofrecen
// Iniciar, las ajenas se ven pero no se accionan.
//
// La fila es una grilla de columnas fijas (tarea · estado · asignado · avance · acciones)
// para que se pueda escanear una columna de arriba abajo. Antes cada dato quedaba pegado al
// anterior y el ojo tenía que releer fila por fila.

import { store, getActiveSession } from './state.js';
import { fetchWorkPackageChildren } from './api.js';
import { escHtml, statusClass } from './helpers.js';

// id -> { id, subject, wp, childIds, childCount, loaded, mine, error }
// `wp` es null en los nodos que solo conocemos por el link de ancestro: de esos tenemos
// número y título, nada más, hasta que alguien expande a su padre.
const nodes = new Map();
let rootIds = [];
const expanded = new Set();
const loading = new Set();

function node(id, subject) {
    let n = nodes.get(id);
    if (!n) {
        n = { id, subject: subject || `#${id}`, wp: null, childIds: [], childCount: null,
              loaded: false, mine: false, error: null };
        nodes.set(id, n);
    } else if (subject && n.wp === null) {
        n.subject = subject;
    }
    return n;
}

function idFromHref(href) {
    if (!href) return 0;
    return parseInt(String(href).split('/').pop()) || 0;
}

function addChild(parentId, childId) {
    const parent = node(parentId);
    if (!parent.childIds.includes(childId)) parent.childIds.push(childId);
}

function absorb(wp, { mine = false } = {}) {
    const n = node(wp.id, wp.subject);
    n.wp = wp;
    n.subject = wp.subject;
    if (mine) n.mine = true;

    // Cuántos hijos tiene, cuando OpenProject lo dice. Es lo que permite no ofrecer
    // "expandir" en una hoja: hacer clic para descubrir "sin subtareas" es trabajo perdido.
    const children = wp._links?.children;
    if (Array.isArray(children)) n.childCount = children.length;

    const ancestors = wp._links?.ancestors ?? [];
    let previousId = null;
    for (const ancestor of ancestors) {
        const id = idFromHref(ancestor.href);
        if (!id) continue;
        node(id, ancestor.title);
        if (previousId) addChild(previousId, id);
        previousId = id;
    }

    const parentId = previousId ?? idFromHref(wp._links?.parent?.href);
    if (parentId) addChild(parentId, wp.id);

    return { node: n, rootId: ancestors.length ? idFromHref(ancestors[0].href) : (parentId || wp.id) };
}

/**
 * Arma el bosque con lo que el navegador YA tiene: mis tareas de la página vienen con sus
 * ancestros (número + título) en el mismo payload, así que las raíces se deducen sin
 * ninguna llamada extra.
 */
export function buildTreeFromPage() {
    nodes.clear();
    rootIds = [];

    for (const wp of store.workPackages) {
        const { rootId } = absorb(wp, { mine: true });
        if (rootId && !rootIds.includes(rootId)) rootIds.push(rootId);
    }
}

async function loadChildren(id) {
    const n = nodes.get(id);
    if (!n || n.loaded || loading.has(id)) return;

    loading.add(id);
    n.error = null;
    renderTree();

    try {
        const children = await fetchWorkPackageChildren(id);
        for (const child of children ?? []) {
            absorb(child);
            addChild(id, child.id);
        }
        n.childCount = n.childIds.length;
        n.loaded = true;
    } catch (e) {
        n.error = e.message;
    } finally {
        loading.delete(id);
        renderTree();
    }
}

export async function toggleNode(id) {
    if (expanded.has(id)) {
        expanded.delete(id);
        renderTree();
        return;
    }

    expanded.add(id);
    renderTree();
    // Una sola llamada por nodo en toda la vida de la vista: colapsar y reabrir no la repite.
    await loadChildren(id);
}

/** Reintento tras un error de red, sin tener que colapsar y volver a abrir. */
export async function retryNode(id) {
    const n = nodes.get(id);
    if (!n) return;
    n.error = null;
    n.loaded = false;
    await loadChildren(id);
}

/** Abre el árbol en una tarea concreta: expande su cadena de ancestros y la resalta. */
export function focusNode(wpId) {
    const wp = store.workPackages.find(w => w.id === wpId);
    for (const ancestor of wp?._links?.ancestors ?? []) {
        const id = idFromHref(ancestor.href);
        if (id) expanded.add(id);
    }
    renderTree(wpId);

    document.querySelector(`.tree-node[data-id="${wpId}"]`)
        ?.scrollIntoView({ block: 'center', behavior: 'smooth' });
}

export function renderTree(highlightId = null) {
    const container = document.getElementById('wpTree');
    if (!container) return;

    if (!rootIds.length) {
        container.innerHTML = `
            <div class="tree-empty">
                <i class="bi bi-diagram-3" aria-hidden="true"></i>
                <p class="mb-1">Todavía no hay nada que mostrar aquí.</p>
                <p class="mb-0 small">Selecciona un proyecto y haz clic en <strong>Cargar tareas</strong>:
                   el árbol se arma con tus tareas y las tareas superiores de las que dependen.</p>
            </div>`;
        return;
    }

    const session = getActiveSession();
    const rows = rootIds.map(id => renderNode(id, 0, session, highlightId)).join('');

    // El encabezado nombra las columnas: sin él, "STIN TEST 0%" es una hilera de datos sueltos
    // que hay que interpretar cada vez.
    container.innerHTML = `
        <div class="tree-wrap">
            <div class="tree-head" role="presentation">
                <span>Tarea</span>
                <span>Estado</span>
                <span>Asignado</span>
                <span>Avance</span>
                <span class="visually-hidden">Acciones</span>
            </div>
            <div role="tree" aria-label="Jerarquía de tareas">${rows}</div>
        </div>`;
}

function renderNode(id, depth, session, highlightId) {
    const n = nodes.get(id);
    if (!n) return '';

    const isOpen = expanded.has(id);
    const isLoading = loading.has(id);
    const wp = n.wp;

    // Tres estados distintos, y se ven distinto: sabemos que tiene N hijos, sabemos que es
    // hoja, o no sabemos (nodo que solo conocemos por el link del ancestro).
    const known = n.childCount;
    const isLeaf = known === 0 && n.childIds.length === 0;

    const hasChildren = known > 0 || n.childIds.length > 0;
    const toggleVerb = isOpen ? 'Ocultar' : 'Mostrar';

    const branch = `
        <span class="tree-branch" style="--tree-depth:${depth}">
            ${isLeaf
                ? '<span class="tree-toggle tree-toggle--leaf" aria-hidden="true" title="Esta tarea no tiene subtareas"></span>'
                : `<button class="tree-toggle" data-id="${id}" aria-expanded="${isOpen}"
                           title="${toggleVerb} las subtareas de la tarea #${id}"
                           aria-label="${toggleVerb} las subtareas de la tarea ${id}">
                       <i class="bi bi-${isLoading ? 'arrow-repeat tree-spin' : isOpen ? 'chevron-down' : 'chevron-right'}"></i>
                   </button>`}
        </span>`;

    const count = known > 0
        ? `<span class="tree-count" title="Tiene ${known} subtarea${known !== 1 ? 's' : ''} directa${known !== 1 ? 's' : ''}">${known}</span>`
        : '';

    const statusTitle = wp?._links?.status?.title || '';
    const assignee = wp?._links?.assignee?.title || '';
    const pct = wp?.percentageDone ?? null;
    const isActive = session?.workPackageId === id;

    // El nombre real de la persona, tal como está en OpenProject, y "(Tú)" solo como sufijo:
    // la columna dice de quién es la tarea, no si es tuya. Una etiqueta genérica obligaba a
    // abrir OpenProject para saber quién era.
    const youSuffix = n.mine ? ' <span class="tree-you">(Tú)</span>' : '';
    const assigneeCell = assignee
        ? `<span class="text-truncate" title="Asignada a ${escHtml(assignee)}${n.mine ? ' — eres tú' : ''}">${escHtml(assignee)}${youSuffix}</span>`
        : n.mine
            ? '<span class="text-truncate" title="Asignada a ti">Tú</span>'
            : `<span class="tree-dash" title="Todavía no se sabe: los datos de esta tarea se cargan al mostrar las subtareas de la tarea de la que depende">—</span>`;

    const progressCell = pct === null
        ? '<span class="tree-dash" title="Todavía no se sabe: los datos de esta tarea se cargan al mostrar las subtareas de la tarea de la que depende">—</span>'
        : `<span class="tree-bar" role="img" aria-label="Avance ${pct} por ciento" title="Avance: ${pct}%">
               <span class="tree-bar-fill" style="width:${pct}%"></span>
           </span>
           <span class="tree-pct" title="Avance: ${pct}%">${pct}%</span>`;

    // Mismo verbo y mismo color que en la grilla: "Iniciar" es un botón con texto, no un
    // triángulo que hay que adivinar.
    const startBtn = isActive
        ? `<span class="badge text-bg-primary tree-running" title="El cronómetro está corriendo en esta tarea">
               <i class="bi bi-record-fill" aria-hidden="true"></i> En curso
           </span>`
        : n.mine
            ? `<button class="btn btn-success btn-sm tree-start" data-id="${id}"
                       title="Iniciar el cronómetro en la tarea #${id}">
                   <i class="bi bi-play-fill" aria-hidden="true"></i> Iniciar
               </button>`
            : '';

    const subjectTitle = wp
        ? `${n.subject} (tarea #${id})`
        : `${n.subject} (tarea #${id}) — tarea superior: sus datos se cargan al mostrar sus subtareas`;

    const row = `
        <div class="tree-node${n.mine ? ' tree-node--mine' : ''}${hasChildren ? ' tree-node--branch' : ''}${highlightId === id ? ' tree-node--focus' : ''}"
             role="treeitem" aria-level="${depth + 1}" ${isLeaf ? '' : `aria-expanded="${isOpen}"`}
             data-id="${id}">
            <span class="tree-main">
                ${branch}
                <span class="tree-id" title="Número de la tarea en OpenProject">#${id}</span>
                <span class="tree-subject" title="${escHtml(subjectTitle)}">${escHtml(n.subject)}</span>
                ${count}
            </span>
            <span class="tree-col tree-status-col">
                ${statusTitle
                    ? `<span class="badge ${statusClass(statusTitle)}" title="Estado: ${escHtml(statusTitle)}">${escHtml(statusTitle)}</span>`
                    : '<span class="tree-dash" title="Todavía no se sabe: los datos de esta tarea se cargan al mostrar las subtareas de la tarea de la que depende">—</span>'}
            </span>
            <span class="tree-col tree-assignee-col">${assigneeCell}</span>
            <span class="tree-col tree-progress-col">${progressCell}</span>
            <span class="tree-col tree-actions">
                <button class="btn btn-sm tree-subtask" data-id="${id}"
                        title="Crear una subtarea dentro de la tarea #${id} (se la pides al asistente)"
                        aria-label="Crear una subtarea dentro de la tarea ${id}">
                    <i class="bi bi-plus-lg" aria-hidden="true"></i>
                </button>
                ${startBtn}
            </span>
        </div>`;

    if (!isOpen) return row;

    if (n.error)
        return row + note(depth + 1, `
            <i class="bi bi-exclamation-triangle" aria-hidden="true"></i>
            No se pudieron cargar las subtareas: ${escHtml(n.error)}
            <button class="btn btn-link btn-sm tree-retry" data-id="${id}"
                    title="Volver a pedir las subtareas de la tarea #${id}">Reintentar</button>`, 'tree-note--error');

    if (isLoading && !n.childIds.length)
        return row + note(depth + 1, '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span> Cargando subtareas…');

    if (n.loaded && !n.childIds.length)
        return row + note(depth + 1, 'Sin subtareas.');

    const children = [...n.childIds].sort((a, b) => a - b);
    return row + children.map(childId => renderNode(childId, depth + 1, session, highlightId)).join('');
}

function note(depth, html, extraClass = '') {
    return `<div class="tree-note ${extraClass}"><span class="tree-branch" style="--tree-depth:${depth}"></span>${html}</div>`;
}

// ponytail: sin navegación por flechas (↑↓←→) entre nodos. Los botones son enfocables con
// Tab y tienen foco visible, que es el piso accesible; el patrón completo de treeview con
// roving tabindex se agrega si alguien lo pide.
