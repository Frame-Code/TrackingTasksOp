// Vista Árbol: la jerarquía de work packages, un nivel por expansión.
//
// Responde una pregunta distinta a la de la grilla ("¿cómo va la implantación?" en vez de
// "¿qué hago ahora?"), y por eso muestra tareas de CUALQUIER persona: las propias ofrecen
// Iniciar, las ajenas se ven pero no se accionan. Esa asimetría es su razón de ser.

import { store, getActiveSession } from './state.js';
import { fetchWorkPackageChildren } from './api.js';
import { escHtml, statusClass } from './helpers.js';

// id -> { id, subject, wp, childIds, loaded, mine }
// `wp` es null en los nodos que solo conocemos por el link de ancestro: de esos tenemos
// número y título, nada más, hasta que alguien expande a su padre.
const nodes = new Map();
let rootIds = [];
const expanded = new Set();
const loading = new Set();

function node(id, subject) {
    let n = nodes.get(id);
    if (!n) {
        n = { id, subject: subject || `#${id}`, wp: null, childIds: [], loaded: false, mine: false };
        nodes.set(id, n);
    } else if (subject && n.wp === null) {
        n.subject = subject;
    }
    return n;
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

function idFromHref(href) {
    if (!href) return 0;
    return parseInt(String(href).split('/').pop()) || 0;
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

export async function toggleNode(id) {
    if (expanded.has(id)) {
        expanded.delete(id);
        renderTree();
        return;
    }

    expanded.add(id);
    const n = nodes.get(id);

    // Una sola llamada por nodo en toda la vida de la vista: colapsar y reabrir no la repite.
    if (n && !n.loaded && !loading.has(id)) {
        loading.add(id);
        renderTree();
        try {
            const children = await fetchWorkPackageChildren(id);
            for (const child of children ?? []) {
                absorb(child);
                addChild(id, child.id);
            }
            n.loaded = true;
        } catch (e) {
            n.error = e.message;
        } finally {
            loading.delete(id);
        }
    }

    renderTree();
}

/** Abre el árbol en una tarea concreta: expande su cadena de ancestros y la resalta. */
export function focusNode(wpId) {
    const wp = store.workPackages.find(w => w.id === wpId);
    for (const ancestor of wp?._links?.ancestors ?? []) {
        const id = idFromHref(ancestor.href);
        if (id) expanded.add(id);
    }
    renderTree(wpId);

    const el = document.querySelector(`.tree-node[data-id="${wpId}"]`);
    el?.scrollIntoView({ block: 'center', behavior: 'smooth' });
}

export function renderTree(highlightId = null) {
    const container = document.getElementById('wpTree');
    if (!container) return;

    if (!rootIds.length) {
        container.innerHTML = `
            <div class="text-center py-4 text-muted">
                <i class="bi bi-diagram-3 display-6 d-block mb-2 opacity-25"></i>
                <p class="mb-0">Cargá tareas para ver de qué cuelgan.</p>
            </div>`;
        return;
    }

    const session = getActiveSession();
    const html = rootIds.map(id => renderNode(id, 0, session, highlightId)).join('');

    container.innerHTML = `
        <div class="tree-wrap">${html}</div>
        <div class="tree-legend">
            <span><i class="bi bi-person-fill"></i> asignada a vos: accionable</span>
            <span><i class="bi bi-person"></i> de otra persona</span>
            <span><i class="bi bi-caret-right-fill"></i> expandir</span>
        </div>`;
}

function renderNode(id, depth, session, highlightId) {
    const n = nodes.get(id);
    if (!n) return '';

    const isOpen = expanded.has(id);
    const isLoading = loading.has(id);
    const wp = n.wp;

    // Sin datos completos no sabemos si tiene hijos: se ofrece expandir igual y, si no
    // tiene, el propio nodo lo dice. Preguntarlo de antemano costaría una llamada por nodo.
    const knownChildren = wp?._links?.children?.length;
    const canExpand = n.childIds.length > 0 || knownChildren === undefined || knownChildren > 0;

    const caret = canExpand
        ? `<button class="tree-toggle" data-id="${id}" aria-label="${isOpen ? 'Colapsar' : 'Expandir'} la tarea ${id}" aria-expanded="${isOpen}">
               <i class="bi bi-${isLoading ? 'arrow-repeat spin' : isOpen ? 'caret-down-fill' : 'caret-right-fill'}"></i>
           </button>`
        : '<span class="tree-toggle tree-toggle--leaf"></span>';

    const statusTitle = wp?._links?.status?.title || '';
    const assignee = wp?._links?.assignee?.title || '';
    const pct = wp?.percentageDone ?? null;
    const isActive = session?.workPackageId === id;

    const startBtn = n.mine && !isActive
        ? `<button class="btn btn-success btn-sm tree-start btn-start" data-id="${id}" title="Iniciar sesión">
               <i class="bi bi-play-fill"></i>
           </button>`
        : isActive
            ? '<span class="badge text-bg-primary tree-running">en curso</span>'
            : '';

    const row = `
        <div class="tree-node${n.mine ? ' tree-node--mine' : ''}${highlightId === id ? ' tree-node--focus' : ''}"
             data-id="${id}" style="--tree-depth:${depth}">
            ${caret}
            <i class="bi bi-${n.mine ? 'person-fill' : 'person'} tree-person-icon" aria-hidden="true"></i>
            <span class="tree-id">#${id}</span>
            <span class="tree-subject" title="${escHtml(n.subject)}">${escHtml(n.subject)}</span>
            ${statusTitle ? `<span class="badge status-badge ${statusClass(statusTitle)} tree-status">${escHtml(statusTitle)}</span>` : ''}
            ${assignee ? `<span class="tree-assignee text-truncate">${escHtml(assignee)}</span>` : ''}
            ${pct !== null ? `<span class="tree-pct">${pct}%</span>` : ''}
            <span class="tree-actions">
                <button class="btn btn-sm wp-icon-btn tree-subtask" data-id="${id}" title="Crear una subtarea acá (se la pedís al asistente)">
                    <i class="bi bi-plus-lg"></i>
                </button>
                ${startBtn}
            </span>
        </div>`;

    if (!isOpen) return row;

    if (n.error)
        return row + `<div class="tree-note" style="--tree-depth:${depth + 1}">No se pudieron traer las subtareas: ${escHtml(n.error)}</div>`;

    if (isLoading && !n.childIds.length)
        return row + `<div class="tree-note" style="--tree-depth:${depth + 1}">Cargando subtareas…</div>`;

    if (n.loaded && !n.childIds.length)
        return row + `<div class="tree-note" style="--tree-depth:${depth + 1}">Sin subtareas.</div>`;

    const children = [...n.childIds].sort((a, b) => a - b);
    return row + children.map(childId => renderNode(childId, depth + 1, session, highlightId)).join('');
}
