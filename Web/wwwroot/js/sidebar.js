// Wiring del sidebar de configuración: rail colapsable/expandido (patrón de
// https://uxplanet.org/case-study-research-sidebar-navigation), grupos en acordeón
// (notificaciones, apariencia, comportamiento de tareas, OpenProject) y cuenta.

import { store, NOTIFICATION_TYPES, getTheme, setTheme, setPageSize,
         getSidebarCollapsed, setSidebarCollapsed } from './state.js';
import { updateNotificationSetting, updateTaskPreferences, updateAiApiKey } from './api.js';
import { refreshNotificationTimers } from './timer.js';
import { escHtml } from './helpers.js';

function showSidebarError(msg) {
    const el = document.getElementById('sidebarError');
    el.textContent = msg;
    el.classList.remove('d-none');
}

function hideSidebarError() {
    document.getElementById('sidebarError').classList.add('d-none');
}

// ── Rail / expandido ──────────────────────────────────────────────────────────────

/** En rail no hay espacio para el contenido de un grupo abierto: se cierran todos al colapsar. */
function collapseAllGroups() {
    document.querySelectorAll('#sidebarAccordion .collapse.show').forEach(el => {
        bootstrap.Collapse.getOrCreateInstance(el, { toggle: false }).hide();
    });
}

function bindSidebarToggle() {
    const expand = () => setSidebarCollapsed(false);
    const collapse = () => { collapseAllGroups(); setSidebarCollapsed(true); };
    const toggle = () => (getSidebarCollapsed() ? expand() : collapse());

    // El logo del header solo actúa como botón mientras está en modo rail (expande).
    document.getElementById('sidebarToggleBtn').addEventListener('click', () => {
        if (getSidebarCollapsed()) expand();
    });
    document.getElementById('sidebarCollapseBtn').addEventListener('click', toggle);
}

/**
 * Si el sidebar está en modo rail, un clic en el encabezado de un grupo debe expandirlo
 * Y dejar ese grupo abierto (el rail funciona como atajo directo a la sección), en vez de
 * dejar que Bootstrap alterne el estado interno de un panel que nunca se llegó a ver.
 */
function bindGroupHeaders() {
    // Solo los headers de acordeón (no ítems sueltos como "Reporte", que no tienen
    // panel que expandir ni sentido en abrir el sidebar primero).
    document.querySelectorAll('.sidebar-group-header[data-bs-toggle="collapse"]').forEach(header => {
        header.addEventListener('click', (e) => {
            if (!getSidebarCollapsed()) return;

            e.preventDefault();
            e.stopPropagation();
            setSidebarCollapsed(false);

            const target = document.querySelector(header.getAttribute('data-bs-target'));
            bootstrap.Collapse.getOrCreateInstance(target, { toggle: false }).show();
        });
    });
}

// ── Notificaciones ──────────────────────────────────────────────────────────────

const NOTIF_FIELDS = {
    [NOTIFICATION_TYPES.SESSION_REMINDER]: { enabled: 'notifSessionEnabled', interval: 'notifSessionInterval', status: 'notifSessionStatus' },
    [NOTIFICATION_TYPES.PENDING_UPLOAD_REMINDER]: { enabled: 'notifPendingEnabled', interval: 'notifPendingInterval', status: 'notifPendingStatus' }
};

function notifStatusText(enabled) {
    if (!('Notification' in window) || Notification.permission === 'denied')
        return 'Bloqueada por el navegador';
    if (Notification.permission === 'default')
        return 'Pendiente de autorizar (inicia una sesión para pedir permiso)';
    return enabled ? 'Activa' : 'Desactivada por ti';
}

function renderNotificationFields() {
    for (const [typeCode, ids] of Object.entries(NOTIF_FIELDS)) {
        const pref = store.userSettings?.notifications?.[typeCode] ?? { enabled: true, intervalMinutes: 15 };
        document.getElementById(ids.enabled).checked = pref.enabled;
        document.getElementById(ids.interval).value = pref.intervalMinutes;
        document.getElementById(ids.status).textContent = notifStatusText(pref.enabled);
    }
}

async function saveNotificationSetting(typeCode) {
    const ids = NOTIF_FIELDS[typeCode];
    const enabledEl = document.getElementById(ids.enabled);
    const intervalEl = document.getElementById(ids.interval);
    const previous = store.userSettings?.notifications?.[typeCode];

    const enabled = enabledEl.checked;
    const intervalMinutes = parseInt(intervalEl.value);
    if (!intervalMinutes || intervalMinutes <= 0 || intervalMinutes > 1440) {
        showSidebarError('El intervalo debe ser un número entre 1 y 1440 minutos.');
        if (previous) intervalEl.value = previous.intervalMinutes;
        return;
    }

    hideSidebarError();
    try {
        await updateNotificationSetting(typeCode, enabled, intervalMinutes);
        store.userSettings.notifications[typeCode] = { typeCode, enabled, intervalMinutes };
        document.getElementById(ids.status).textContent = notifStatusText(enabled);
        refreshNotificationTimers();
    } catch (e) {
        showSidebarError(`No se pudo guardar: ${e.message}`);
        if (previous) {
            enabledEl.checked = previous.enabled;
            intervalEl.value = previous.intervalMinutes;
        }
    }
}

function bindNotificationFields() {
    for (const [typeCode, ids] of Object.entries(NOTIF_FIELDS)) {
        document.getElementById(ids.enabled).addEventListener('change', () => saveNotificationSetting(typeCode));
        document.getElementById(ids.interval).addEventListener('change', () => saveNotificationSetting(typeCode));
    }
}

// ── Apariencia ───────────────────────────────────────────────────────────────────

function renderAppearanceFields() {
    document.getElementById('themeSwitch').checked = getTheme() === 'dark';
    document.getElementById('pageSizeSelect').value = String(store.pageSize);
}

function bindAppearanceFields() {
    document.getElementById('themeSwitch').addEventListener('change', (e) => {
        setTheme(e.target.checked ? 'dark' : 'light');
    });

    document.getElementById('pageSizeSelect').addEventListener('change', (e) => {
        setPageSize(parseInt(e.target.value));
        // La paginación la resuelve el servidor: cambiar el tamaño cambia QUÉ tareas trae
        // la página, no solo cómo se recortan las que ya están en memoria. Se usa un evento
        // para no importar app.js desde aquí (app.js ya importa este módulo).
        document.dispatchEvent(new CustomEvent('tasks:reload'));
    });
}

// ── Comportamiento de tareas ───────────────────────────────────────────────────────

function renderTaskBehaviorFields() {
    document.getElementById('pauseBehaviorSelect').value = store.userSettings?.pauseDefaultBehavior ?? 'Ask';
    document.getElementById('skipCancelConfirmSwitch').checked = store.userSettings?.skipCancelConfirmation ?? false;
    // Default true: preserva el comportamiento histórico (con holgura) si el fetch falló.
    document.getElementById('addRandomSlackTimeSwitch').checked = store.userSettings?.addRandomSlackTime ?? true;
    renderDefaultStatusFilterChecks();
}

/** Un check por estado de OpenProject conocido (mismo catálogo que las píldoras de filtro
 *  de la vista principal). Se repinta también desde loadStatuses() en app.js, por si los
 *  estados llegan después de la primera vez que se pinta el sidebar. */
function renderDefaultStatusFilterChecks() {
    const container = document.getElementById('defaultStatusFilterChecks');
    if (!store.statuses.length) {
        container.innerHTML = '<div class="form-text mb-0">No hay estados cargados todavía.</div>';
        return;
    }

    const selected = new Set(store.userSettings?.defaultStatusIds ?? []);
    container.innerHTML = store.statuses.map(s => `
        <div class="form-check">
            <input class="form-check-input default-status-filter-check" type="checkbox"
                   value="${s.id}" id="defaultStatusCheck${s.id}" ${selected.has(s.id) ? 'checked' : ''}>
            <label class="form-check-label" for="defaultStatusCheck${s.id}">${escHtml(s.name)}</label>
        </div>`).join('');
}

async function saveTaskPreferences() {
    const pauseBehaviorEl = document.getElementById('pauseBehaviorSelect');
    const skipConfirmEl = document.getElementById('skipCancelConfirmSwitch');
    const slackTimeEl = document.getElementById('addRandomSlackTimeSwitch');
    const previous = store.userSettings;

    const pauseDefaultBehavior = pauseBehaviorEl.value;
    const skipCancelConfirmation = skipConfirmEl.checked;
    const addRandomSlackTime = slackTimeEl.checked;
    const defaultStatusIds = [...document.querySelectorAll('.default-status-filter-check:checked')]
        .map(el => parseInt(el.value));

    hideSidebarError();
    try {
        await updateTaskPreferences(pauseDefaultBehavior, skipCancelConfirmation, addRandomSlackTime, defaultStatusIds);
        store.userSettings = { ...store.userSettings, pauseDefaultBehavior, skipCancelConfirmation, addRandomSlackTime, defaultStatusIds };
    } catch (e) {
        showSidebarError(`No se pudo guardar: ${e.message}`);
        if (previous) {
            pauseBehaviorEl.value = previous.pauseDefaultBehavior;
            skipConfirmEl.checked = previous.skipCancelConfirmation;
            slackTimeEl.checked = previous.addRandomSlackTime;
            store.userSettings = previous;
            renderDefaultStatusFilterChecks();
        }
    }
}

function bindTaskBehaviorFields() {
    document.getElementById('pauseBehaviorSelect').addEventListener('change', saveTaskPreferences);
    document.getElementById('skipCancelConfirmSwitch').addEventListener('change', saveTaskPreferences);
    document.getElementById('addRandomSlackTimeSwitch').addEventListener('change', saveTaskPreferences);
    // Los checks de estado se repintan en cada renderTaskBehaviorFields(), así que se
    // delega en el contenedor en vez de bindear cada uno (que dejaría de existir al repintar).
    document.getElementById('defaultStatusFilterChecks').addEventListener('change', (e) => {
        if (e.target.classList.contains('default-status-filter-check')) saveTaskPreferences();
    });
}

// ── IA (bot) ─────────────────────────────────────────────────────────────────────

function renderAiFields() {
    const hasCustomKey = store.userSettings?.hasCustomAiApiKey ?? false;
    document.getElementById('aiKeyStatus').textContent = hasCustomKey
        ? 'Usando tu propia API key (sin límite diario).'
        : 'Usando la key compartida (con límite diario de mensajes).';
    // Nunca se muestra la key guardada (ni cifrada llega al cliente): el campo queda
    // siempre vacío, listo para pegar una nueva o dejarlo así para no tocar la actual.
    document.getElementById('aiApiKeyInput').value = '';
}

async function saveAiApiKey() {
    const input = document.getElementById('aiApiKeyInput');
    const apiKey = input.value.trim();
    if (!apiKey) {
        showSidebarError('Pega tu API key de Groq, o usa "Quitar" para volver a la compartida.');
        return;
    }

    hideSidebarError();
    try {
        await updateAiApiKey(apiKey);
        store.userSettings = { ...store.userSettings, hasCustomAiApiKey: true };
        renderAiFields();
    } catch (e) {
        showSidebarError(`No se pudo guardar: ${e.message}`);
    }
}

async function clearAiApiKey() {
    hideSidebarError();
    try {
        await updateAiApiKey(null);
        store.userSettings = { ...store.userSettings, hasCustomAiApiKey: false };
        renderAiFields();
    } catch (e) {
        showSidebarError(`No se pudo quitar la key: ${e.message}`);
    }
}

function bindAiFields() {
    document.getElementById('saveAiApiKeyBtn').addEventListener('click', saveAiApiKey);
    document.getElementById('clearAiApiKeyBtn').addEventListener('click', clearAiApiKey);
}

// ── OpenProject / Cuenta ────────────────────────────────────────────────────────────

function initials(email) {
    if (!email) return '?';
    const name = email.split('@')[0];
    return name.slice(0, 2).toUpperCase();
}

function renderAccountFields() {
    const instanceUrl = store.userSettings?.openProjectInstanceUrl;
    const instanceLink = document.getElementById('sidebarInstanceUrl');
    instanceLink.textContent = instanceUrl || '—';
    if (instanceUrl) {
        instanceLink.href = instanceUrl;
    } else {
        instanceLink.removeAttribute('href'); // sin URL, el enlace queda inerte (no navega a "#")
    }

    document.getElementById('sidebarUserEmail').textContent = store.userSettings?.email || '';
    document.getElementById('sidebarAvatarInitials').textContent = initials(store.userSettings?.email);
}

// ── Init ──────────────────────────────────────────────────────────────────────────

/** Repuebla todos los campos desde store.userSettings — llamar de nuevo tras loadUserSettings(). */
export function renderSidebarFields() {
    renderNotificationFields();
    renderAppearanceFields();
    renderTaskBehaviorFields();
    renderAiFields();
    renderAccountFields();
}

export function initSidebar() {
    bindSidebarToggle();
    bindGroupHeaders();
    bindNotificationFields();
    bindAppearanceFields();
    bindTaskBehaviorFields();
    bindAiFields();
    renderSidebarFields();
}
