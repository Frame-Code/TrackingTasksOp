// Utilidades puras sin dependencias externas

export function escHtml(str) {
    if (!str) return '';
    return str
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

export function extractId(href) {
    if (!href) return 0;
    return parseInt(href.split('/').pop()) || 0;
}

export function formatDuration(totalSeconds) {
    const h = Math.floor(totalSeconds / 3600);
    const m = Math.floor((totalSeconds % 3600) / 60);
    const s = Math.floor(totalSeconds % 60);
    return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

export function formatDateTime(date) {
    return date.toLocaleString('es-MX', {
        day: '2-digit', month: '2-digit', year: 'numeric',
        hour: '2-digit', minute: '2-digit'
    });
}

/**
 * Estado → clase de color. El orden importa: "Test failed" y "In testing" contienen
 * ambos "test", así que lo más específico se evalúa primero.
 */
export function statusClass(title) {
    const t = (title || '').toLowerCase();
    if (t.includes('failed')    || t.includes('fallo') || t.includes('fallid')) return 'status-testfailed';
    if (t.includes('reject')    || t.includes('rechaz'))     return 'status-rejected';
    if (t.includes('testing')   || t.includes('pruebas'))    return 'status-testing';
    if (t.includes('tested')    || t.includes('probado'))    return 'status-tested';
    if (t.includes('deployed')  || t.includes('desplegado')) return 'status-deployed';
    if (t.includes('developed') || t.includes('desarrollado')) return 'status-developed';
    if (t.includes('new')       || t.includes('nuevo'))      return 'status-new';
    if (t.includes('progress')  || t.includes('progreso'))   return 'status-inprogress';
    if (t.includes('done')      || t.includes('completado')) return 'status-done';
    if (t.includes('closed')    || t.includes('cerrado'))    return 'status-closed';
    if (t.includes('hold')      || t.includes('espera'))     return 'status-hold';
    return 'status-default';
}

/**
 * Tipo de work package → clase de color. Es una escala CATEGÓRICA: no hay orden ni
 * "mejor/peor", a diferencia del estado, que sí avanza por un flujo. Por eso el tipo
 * se pinta como contorno y el estado como relleno sólido (ver app.css).
 */
export function typeClass(title) {
    const t = (title || '').toLowerCase();
    if (t.includes('implementa'))                           return 'type-implementation';
    if (t.includes('desarrollo') || t.includes('develop'))   return 'type-development';
    if (t.includes('bug') || t.includes('error') || t.includes('defecto')) return 'type-bug';
    if (t.includes('soporte') || t.includes('support'))      return 'type-support';
    if (t.includes('feature') || t.includes('funcional'))    return 'type-feature';
    if (t.includes('epic') || t.includes('épica') || t.includes('epica')) return 'type-epic';
    if (t.includes('hito') || t.includes('milestone'))       return 'type-milestone';
    if (t.includes('fase') || t.includes('phase'))           return 'type-phase';
    return 'type-default';
}
