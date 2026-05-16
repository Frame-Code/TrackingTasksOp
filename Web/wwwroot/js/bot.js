const chatMessages = document.getElementById('chat-messages');
const userInput = document.getElementById('user-input');
const sendBtn = document.getElementById('send-btn');
const cancelBtn = document.getElementById('cancel-btn');
const suggestionChips = document.querySelectorAll('.suggestion-chip');

let sessionId = localStorage.getItem('chat_session_id') || ('sess_' + Math.random().toString(36).substr(2, 9));
localStorage.setItem('chat_session_id', sessionId);

let currentAbortController = null;

function formatMarkdown(text) {
    // 0. Corrección AGRESIVA de codificación
    let cleanText = text;
    try {
        const encodingMap = {
            '├▒': 'ñ', '├í': 'á', '├©': 'é', '├¡': 'í', '├│': 'ó', '├║': 'ú',
            '├æ': 'Ñ', '├ï': 'É', '├ì': 'Í', '├ô': 'Ó', '├Ü': 'Ú',
            '┬¿': '¿', '┬¡': '¡', '├á': 'à', '├¿': 'è', '├▓': 'ò', '├¹': 'ù',
            'ubicaci├│n': 'ubicación'
        };
        for (const [key, value] of Object.entries(encodingMap)) {
            cleanText = cleanText.split(key).join(value);
        }
    } catch(e) {}

    // 1. Detectar LISTA DE ESTADOS DISPONIBLES (Nombre (ID: X))
    // Transforma "New (ID: 1)" en una burbuja de estado con salto de línea
    const statusListPattern = /([A-Za-z\s\-/]+)\s*\(ID:\s*(\d+)\)/g;
    cleanText = cleanText.replace(statusListPattern, (match, name, id) => {
        const statusClass = getStatusClass(name);
        return `<div style="margin-bottom: 8px;"><span class="task-status ${statusClass}">${name.trim()} <small style="opacity:0.8; font-size:0.6rem;">ID: ${id}</small></span></div>`;
    });

    // 2. Detectar TARJETAS DE TAREA (#ID: Texto (Status))
    // Patrón mejorado para detectar tareas con o sin prefijos y capturar el estado aunque tenga markdown
    const taskPattern = /(?:^|\n)(?:[-*•]\s+)?#(\d+):\s*([^\n]+)/g;

    cleanText = cleanText.replace(taskPattern, (match, id, rest) => {
        let subject = rest.trim();
        let status = '';
        
        // Buscar cualquier texto entre paréntesis que parezca un estado (al final o cerca del final)
        const statusMatch = subject.match(/\s*[\*_~`]*\(([^)]+)\)[\*_~`]*\s*$/) || subject.match(/\s*\(([^)]+)\)\s*$/);
        
        if (statusMatch) {
            status = statusMatch[1].replace(/[*_~`]/g, '').trim();
            subject = subject.replace(statusMatch[0], '').trim();
        }

        // Limpieza profunda del sujeto (quitar markdown residual)
        const cleanSubject = subject.replace(/[*_~`]/g, '').trim();
        const statusClass = getStatusClass(status);
        const statusHtml = status ? `<span class="task-status ${statusClass}">${status}</span>` : '';
        
        return `
            <div class="task-item" onclick="window.open('http://localhost:8080/work_packages/${id}', '_blank')">
                <div class="task-header">
                    <span class="task-id">#${id}</span>
                    ${statusHtml}
                </div>
                <div class="task-subject">${cleanSubject}</div>
            </div>
        `;
    });

    // 3. Detectar Imágenes ([image:ID])
    const imagePattern = /\[image:(\d+)\]/g;
    cleanText = cleanText.replace(imagePattern, (match, id) => {
        return `
            <div class="chat-image-container" onclick="window.open('/api/v1/Attachment/${id}/content', '_blank')">
                <img src="/api/v1/Attachment/${id}/content" class="chat-image" alt="Adjunto ${id}" loading="lazy">
            </div>
        `;
    });

    // 4. Formateo estándar de Markdown restante (Estilo GPT)
    return cleanText
        .replace(/\*\*\*(.*?)\*\*\*/g, '<strong><em>$1</em></strong>')
        .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
        .replace(/\*(.*?)\*/g, '<em>$1</em>')
        .replace(/^- (.*)/gm, '• $1')
        .replace(/\n(?!<div)/g, '<br>');
}

// Función auxiliar para centralizar la lógica de colores de estados
function getStatusClass(statusName) {
    if (!statusName) return 'status-default';
    const s = statusName.toLowerCase();
    
    // VERDE: Finalizados, Desplegados, Probados con éxito
    if (s.includes('developed') || s.includes('deployed') || s.includes('cerrado') || 
        s.includes('finalizado') || s.includes('tested') || s.includes('done')) {
        return 'status-developed';
    }
    
    // NARANJA: Pruebas, Fallos de prueba, Atención requerida
    if (s.includes('testing') || s.includes('pruebas') || s.includes('failed') || s.includes('hold')) {
        return 'status-testing';
    }
    
    // AZUL: En curso, Especificando, Desarrollo activo
    if (s.includes('progress') || s.includes('curso') || s.includes('desarrollo') || 
        s.includes('doing') || s.includes('specification') || s.includes('specified')) {
        return 'status-in-progress';
    }
    
    // GRIS: Nuevos, Agendados, Espera inicial
    if (s.includes('new') || s.includes('nuevo') || s.includes('espera') || 
        s.includes('todo') || s.includes('scheduled')) {
        return 'status-new';
    }
    
    return 'status-default';
}

function createActionCard(data) {
    if (!data.workPackageId) return '';
    return `
        <div class="action-card">
            <div class="card-header">
                <i class="bi bi-check2-circle"></i> Acción Ejecutada
            </div>
            <div class="card-body">
                <strong>Tarea:</strong> ${data.name || 'Sin nombre'}<br>
                <strong>ID:</strong> #${data.workPackageId}<br>
                <span style="color: var(--text-secondary); font-size: 0.8rem;">${data.status || ''}</span>
            </div>
            <div class="card-footer">
                <button class="btn-action" onclick="window.open('http://localhost:8080/work_packages/${data.workPackageId}', '_blank')">
                    Ver en OpenProject <i class="bi bi-box-arrow-up-right"></i>
                </button>
            </div>
        </div>
    `;
}

function addMessage(content, isUser = false, isRawData = false) {
    const row = document.createElement('div');
    row.className = `message-row ${isUser ? 'user-row' : ''}`;
    
    const avatarHtml = isUser 
        ? '<div class="avatar user"><i class="bi bi-person-fill"></i></div>'
        : '<div class="avatar bot"><i class="bi bi-stars"></i></div>';
    
    let contentHtml = isUser ? content : formatMarkdown(content);
    
    row.innerHTML = `
        ${avatarHtml}
        <div class="message-content">
            ${contentHtml}
        </div>
    `;
    
    chatMessages.appendChild(row);
    chatMessages.scrollTop = chatMessages.scrollHeight;
    return row;
}

async function sendMessage(text = null) {
    const prompt = text || userInput.value.trim();
    if (!prompt) return;

    if (!text) userInput.value = '';
    userInput.disabled = true;
    sendBtn.style.display = 'none';
    cancelBtn.style.display = 'flex';

    addMessage(prompt, true);

    // Indicador de escritura animado
    const typingRow = document.createElement('div');
    typingRow.className = 'message-row';
    typingRow.innerHTML = `
        <div class="avatar bot"><i class="bi bi-stars"></i></div>
        <div class="message-content">
            <div class="typing-dots">
                <div class="dot"></div>
                <div class="dot"></div>
                <div class="dot"></div>
            </div>
        </div>
    `;
    chatMessages.appendChild(typingRow);
    chatMessages.scrollTop = chatMessages.scrollHeight;

    currentAbortController = new AbortController();

    try {
        const response = await fetch(`/api/v1/Bot/Chat/${sessionId}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ Prompt: prompt }),
            signal: currentAbortController.signal
        });

        const data = await response.json();
        chatMessages.removeChild(typingRow);

        // Si la respuesta contiene datos de una tarea creada/iniciada, mostramos la tarjeta
        const botRow = addMessage(data.response || 'Operación completada.');
        
        if(data.metadata) {
            botRow.querySelector('.message-content').innerHTML += createActionCard(data.metadata);
        }

    } catch (error) {
        if (typingRow.parentNode) {
            chatMessages.removeChild(typingRow);
        }
        if (error.name === 'AbortError') {
            addMessage('🛑 <i>Petición cancelada por el usuario.</i>');
        } else {
            addMessage('Lo siento, no pude conectar con el servidor. Verifica que el backend esté corriendo.');
        }
    } finally {
        currentAbortController = null;
        userInput.disabled = false;
        sendBtn.style.display = 'flex';
        cancelBtn.style.display = 'none';
        userInput.focus();
    }
}

cancelBtn.addEventListener('click', () => {
    if (currentAbortController) {
        currentAbortController.abort();
    }
});

sendBtn.addEventListener('click', () => sendMessage());
userInput.addEventListener('keypress', (e) => {
    if (e.key === 'Enter') sendMessage();
});

suggestionChips.forEach(chip => {
    chip.addEventListener('click', () => sendMessage(chip.innerText));
});
