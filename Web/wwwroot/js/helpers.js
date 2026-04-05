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

export function statusClass(title) {
    const t = (title || '').toLowerCase();
    if (t.includes('new')      || t.includes('nuevo'))    return 'status-new';
    if (t.includes('progress') || t.includes('progreso')) return 'status-inprogress';
    if (t.includes('done')     || t.includes('completado')) return 'status-done';
    if (t.includes('closed')   || t.includes('cerrado'))  return 'status-closed';
    if (t.includes('hold')     || t.includes('espera'))   return 'status-hold';
    return 'status-default';
}
