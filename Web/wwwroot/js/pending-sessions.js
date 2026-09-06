// Sesiones cerradas guardadas solo en local, sin subir a OpenProject: badge de conteo
// (compartido entre index.html y settings.html) y el modal de detalle/envío (solo index.html,
// que es la única página con Bootstrap JS cargado).

import { fetchPendingSummary, fetchPendingSessions, postUploadPending } from './api.js';
import { showToast } from './ui.js';
import { escHtml } from './helpers.js';

/**
 * Actualiza todo elemento .js-pending-badge presente en la página actual (topbar en
 * index.html, tarjeta en settings.html). Se llama de forma diferida tras el render
 * principal: es un fetch liviano (mismo query que ya corre para el recordatorio), así
 * que no hace falta ningún worker en segundo plano.
 *
 * Acepta un summary ya obtenido (el recordatorio de timer.js ya pide el mismo endpoint)
 * para no duplicar el fetch; sin argumento, lo pide él mismo.
 */
export async function refreshPendingBadge(summary) {
    if (summary === undefined) {
        try {
            summary = await fetchPendingSummary();
        } catch {
            return; // un fallo de red no debe romper nada visible
        }
    }

    const count = summary?.count ?? 0;
    for (const el of document.querySelectorAll('.js-pending-badge')) {
        el.textContent = count > 99 ? '99+' : String(count);
        el.style.display = count > 0 ? '' : 'none';
    }
}

let _pendingRows = [];

function filterPendingRows(query) {
    const q = query.trim().toLowerCase();
    if (!q) return _pendingRows;
    return _pendingRows.filter(r =>
        r.taskName.toLowerCase().includes(q) || String(r.workPackageId).includes(q));
}

function renderPendingRows(rows) {
    const body = document.getElementById('pendingSessionsBody');
    if (!body) return;

    if (!rows.length) {
        body.innerHTML = '<p class="text-muted text-center py-3 mb-0">No hay sesiones sin enviar.</p>';
        return;
    }

    body.innerHTML = `
        <div class="table-responsive">
        <table class="table table-sm align-middle">
            <thead><tr><th>ID</th><th>Tarea</th><th>Cliente</th><th class="text-end">Horas</th><th></th></tr></thead>
            <tbody>
                ${rows.map(r => `
                    <tr>
                        <td class="text-muted">#${r.workPackageId}</td>
                        <td>${escHtml(r.taskName)}</td>
                        <td>${escHtml(r.projectName)}</td>
                        <td class="text-end font-monospace">${r.hours.toFixed(2)} h</td>
                        <td class="text-end">
                            <button type="button" class="btn btn-sm btn-outline-primary btn-upload-one" data-id="${r.workPackageId}">
                                <i class="bi bi-cloud-upload"></i> Enviar
                            </button>
                        </td>
                    </tr>`).join('')}
            </tbody>
        </table>
        </div>`;
}

/** Abre el modal y carga la lista. Solo tiene sentido en index.html (tiene Bootstrap JS). */
export async function openPendingSessionsModal() {
    new bootstrap.Modal(document.getElementById('pendingSessionsModal')).show();
    document.getElementById('pendingSessionsSearch').value = '';
    document.getElementById('pendingSessionsBody').innerHTML =
        '<div class="text-center py-4"><div class="spinner-border text-primary"></div></div>';

    try {
        _pendingRows = await fetchPendingSessions();
        renderPendingRows(_pendingRows);
    } catch (e) {
        document.getElementById('pendingSessionsBody').innerHTML =
            `<div class="alert alert-danger mb-0">No se pudo cargar: ${escHtml(e.message)}</div>`;
    }
}

async function uploadOne(wpId, btn) {
    const originalHtml = btn.innerHTML;
    btn.disabled = true;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm"></span>';

    try {
        await postUploadPending(wpId);
        _pendingRows = _pendingRows.filter(r => r.workPackageId !== wpId);
        renderPendingRows(filterPendingRows(document.getElementById('pendingSessionsSearch').value));
        showToast('Sesión enviada a OpenProject.', 'success');
        refreshPendingBadge();
    } catch (e) {
        btn.disabled = false;
        btn.innerHTML = originalHtml;
        showToast(`No se pudo enviar: ${e.message}`, 'danger');
    }
}

async function uploadAll(btn) {
    if (!_pendingRows.length) return;
    btn.disabled = true;
    let okCount = 0, failCount = 0;
    const total = _pendingRows.length;

    // Secuencial a propósito: cada envío es una llamada real a OpenProject; en paralelo
    // se arriesga a golpear la API externa con todas las tareas a la vez.
    for (const row of [..._pendingRows]) {
        btn.innerHTML = `<span class="spinner-border spinner-border-sm me-1"></span>Enviando ${okCount + failCount + 1}/${total}…`;
        try {
            await postUploadPending(row.workPackageId);
            _pendingRows = _pendingRows.filter(r => r.workPackageId !== row.workPackageId);
            okCount++;
        } catch {
            failCount++;
        }
        renderPendingRows(filterPendingRows(document.getElementById('pendingSessionsSearch').value));
    }

    btn.disabled = false;
    btn.innerHTML = '<i class="bi bi-cloud-upload me-1"></i>Enviar todo';
    showToast(
        failCount === 0
            ? `${okCount} sesión(es) enviada(s) a OpenProject.`
            : `${okCount} enviada(s), ${failCount} fallaron. Las que quedan en la lista se pueden reintentar.`,
        failCount === 0 ? 'success' : 'warning');
    refreshPendingBadge();
}

/** Cablea el modal una sola vez (solo index.html — llamar durante Init). */
export function bindPendingSessionsModal() {
    document.getElementById('pendingSessionsBtn').addEventListener('click', openPendingSessionsModal);

    document.getElementById('pendingSessionsSearch').addEventListener('input', (e) => {
        renderPendingRows(filterPendingRows(e.target.value));
    });

    document.getElementById('pendingSessionsBody').addEventListener('click', (e) => {
        const btn = e.target.closest('.btn-upload-one');
        if (btn) uploadOne(parseInt(btn.dataset.id), btn);
    });

    document.getElementById('uploadAllPendingBtn').addEventListener('click', (e) => uploadAll(e.currentTarget));
}
