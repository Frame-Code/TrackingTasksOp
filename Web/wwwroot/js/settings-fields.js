// Campos de configuración de la app: notificaciones, apariencia y comportamiento de tareas.
//
// Vive aparte de settings.js porque es la lógica que antes estaba en el sidebar del dashboard:
// se movió tal cual, sin reescribirla, porque direcciona todo por ID y no le importa en qué
// página estén los elementos.

import { store, NOTIFICATION_TYPES, getTheme, setTheme, setPageSize } from './state.js';
import { updateNotificationSetting, updateTaskPreferences } from './api.js';
import { escHtml } from './helpers.js';

function showFieldsError(msg) {
    const el = document.getElementById('settingsError');
    el.textContent = msg;
    el.classList.remove('hidden');
}

function hideFieldsError() {
    document.getElementById('settingsError').classList.add('hidden');
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
        showFieldsError('El intervalo debe ser un número entre 1 y 1440 minutos.');
        if (previous) intervalEl.value = previous.intervalMinutes;
        return;
    }

    hideFieldsError();
    try {
        await updateNotificationSetting(typeCode, enabled, intervalMinutes);
        store.userSettings.notifications[typeCode] = { typeCode, enabled, intervalMinutes };
        document.getElementById(ids.status).textContent = notifStatusText(enabled);
        // Los timers no se refrescan acá: ajustes es una página aparte y el dashboard
        // recarga las preferencias al volver.
    } catch (e) {
        showFieldsError(`No se pudo guardar: ${e.message}`);
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

    // El tamaño de página se guarda en localStorage; el dashboard lo lee al cargar.
    document.getElementById('pageSizeSelect').addEventListener('change', (e) => {
        setPageSize(parseInt(e.target.value));
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

/** Un check por estado de OpenProject conocido (mismo catálogo que las píldoras de filtro). */
function renderDefaultStatusFilterChecks() {
    const container = document.getElementById('defaultStatusFilterChecks');
    if (!store.statuses.length) {
        container.innerHTML = '<div class="form-text">No hay estados cargados todavía.</div>';
        return;
    }

    const selected = new Set(store.userSettings?.defaultStatusIds ?? []);
    container.innerHTML = store.statuses.map(s => `
        <div class="form-check">
            <input class="form-check-input default-status-filter-check" type="checkbox"
                   value="${s.id}" id="defaultStatusCheck${s.id}" ${selected.has(s.id) ? 'checked' : ''}>
            <label for="defaultStatusCheck${s.id}">${escHtml(s.name)}</label>
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

    hideFieldsError();
    try {
        await updateTaskPreferences(pauseDefaultBehavior, skipCancelConfirmation, addRandomSlackTime, defaultStatusIds);
        store.userSettings = { ...store.userSettings, pauseDefaultBehavior, skipCancelConfirmation, addRandomSlackTime, defaultStatusIds };
    } catch (e) {
        showFieldsError(`No se pudo guardar: ${e.message}`);
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
    // Los checks de estado se repintan enteros, así que se delega en el contenedor en vez
    // de bindear cada uno (que dejaría de existir al repintar).
    document.getElementById('defaultStatusFilterChecks').addEventListener('change', (e) => {
        if (e.target.classList.contains('default-status-filter-check')) saveTaskPreferences();
    });
}

// ── OpenProject ────────────────────────────────────────────────────────────────────

function renderInstanceUrl() {
    const instanceUrl = store.userSettings?.openProjectInstanceUrl;
    const link = document.getElementById('sidebarInstanceUrl');
    link.textContent = instanceUrl || '—';
    if (instanceUrl) link.href = instanceUrl;
    else link.removeAttribute('href'); // sin URL, el enlace queda inerte (no navega a "#")

    // Solo admins de OpenProject pueden conectar OAuth para la organización (el backend
    // también lo valida — esto es nada más para no ofrecer una acción que va a rebotar).
    document.getElementById('oauthConnectSection').classList.toggle('hidden', !store.userSettings?.isAdmin);
}

// ── Init ──────────────────────────────────────────────────────────────────────────

/** Repuebla todos los campos desde store — llamar de nuevo tras cargar los settings. */
export function renderSettingsFields() {
    renderNotificationFields();
    renderAppearanceFields();
    renderTaskBehaviorFields();
    renderInstanceUrl();
}

export function bindSettingsFields() {
    bindNotificationFields();
    bindAppearanceFields();
    bindTaskBehaviorFields();
}
