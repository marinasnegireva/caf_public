/**
 * Conversation Page Module
 * Handles conversation UI and turn management
 */

import {
    escapeHtml,
    formatDateTime,
    normalizeText,
    hasText,
    showMessage,
    showError,
    API_BASE_URL
} from './js/common-utils.js';

// State
let currentSession = null;
let turns = [];

// DOM Elements
const sessionInfo = document.getElementById('sessionInfo');
const turnsContainer = document.getElementById('turnsContainer');
const turnModal = document.getElementById('turnModal');
const turnDetails = document.getElementById('turnDetails');
const closeBtn = document.querySelector('.close');

const editResponseModal = document.getElementById('editResponseModal');
const editResponseClose = document.getElementById('editResponseClose');
const editResponseTurnId = document.getElementById('editResponseTurnId');
const editResponseTextarea = document.getElementById('editResponseTextarea');
const editResponseCancelBtn = document.getElementById('editResponseCancelBtn');
const editResponseSaveBtn = document.getElementById('editResponseSaveBtn');

const editInputModal = document.getElementById('editInputModal');
const editInputClose = document.getElementById('editInputClose');
const editInputTurnId = document.getElementById('editInputTurnId');
const editInputTextarea = document.getElementById('editInputTextarea');
const editInputCancelBtn = document.getElementById('editInputCancelBtn');
const editInputSaveBtn = document.getElementById('editInputSaveBtn');

const editStrippedModal = document.getElementById('editStrippedModal');
const editStrippedClose = document.getElementById('editStrippedClose');
const editStrippedTurnId = document.getElementById('editStrippedTurnId');
const editStrippedTextarea = document.getElementById('editStrippedTextarea');
const editStrippedCancelBtn = document.getElementById('editStrippedCancelBtn');
const editStrippedSaveBtn = document.getElementById('editStrippedSaveBtn');

const copyAllBtn = document.getElementById('copyAllBtn');
const copyStrippedBtn = document.getElementById('copyStrippedBtn');
const clearAllStrippedBtn = document.getElementById('clearAllStrippedBtn');
const copyStatus = document.getElementById('copyStatus');
const acceptedInfo = document.getElementById('acceptedInfo');
const tokenInfo = document.getElementById('tokenInfo');

// Initialize
document.addEventListener('DOMContentLoaded', async () => {
    await loadActiveSession();
    setupEventListeners();
});

function setupEventListeners() {
if (closeBtn) closeBtn.addEventListener('click', closeTurnModal);

    // Edit Response Modal
    if (editResponseClose) editResponseClose.addEventListener('click', closeEditResponseModal);
    if (editResponseCancelBtn) editResponseCancelBtn.addEventListener('click', closeEditResponseModal);
    if (editResponseSaveBtn) editResponseSaveBtn.addEventListener('click', saveEditedResponse);

    // Edit Input Modal
    if (editInputClose) editInputClose.addEventListener('click', closeEditInputModal);
    if (editInputCancelBtn) editInputCancelBtn.addEventListener('click', closeEditInputModal);
    if (editInputSaveBtn) editInputSaveBtn.addEventListener('click', saveEditedInput);

    // Edit Stripped Modal
    if (editStrippedClose) editStrippedClose.addEventListener('click', closeEditStrippedModal);
    if (editStrippedCancelBtn) editStrippedCancelBtn.addEventListener('click', closeEditStrippedModal);
    if (editStrippedSaveBtn) editStrippedSaveBtn.addEventListener('click', saveEditedStripped);

    if (copyAllBtn) copyAllBtn.addEventListener('click', copyAllToClipboard);
    if (copyStrippedBtn) copyStrippedBtn.addEventListener('click', copyStrippedToClipboard);
    if (clearAllStrippedBtn) clearAllStrippedBtn.addEventListener('click', clearAllStripped);

    window.addEventListener('click', (e) => {
        if (e.target === turnModal) {
            closeTurnModal();
        }
        if (e.target === editResponseModal) {
            closeEditResponseModal();
        }
        if (e.target === editInputModal) {
            closeEditInputModal();
        }
        if (e.target === editStrippedModal) {
            closeEditStrippedModal();
        }
    });
}

async function loadActiveSession() {
    try {
        const response = await fetch(`${API_BASE_URL}/sessions/active`);

        if (!response.ok) {
            if (response.status === 404) {
                showNoActiveSession();
                return;
            }
            throw new Error('Failed to load active session');
        }

        currentSession = await response.json();
        renderSessionInfo();
        await loadTurns();
    } catch (error) {
        console.error('Error loading active session:', error);
        turnsContainer.innerHTML = '<p class="error">Failed to load session</p>';
    }
}

function showNoActiveSession() {
    if (sessionInfo) sessionInfo.innerHTML = '<span class="error">No active session</span>';
    if (turnsContainer) turnsContainer.innerHTML = `
        <div class="no-data">
            <p>No active session found.</p>
            <a href="sessions.html" class="btn btn-primary">Go to Sessions</a>
        </div>
    `;
}

function renderSessionInfo() {
    if (!sessionInfo || !currentSession) return;
    sessionInfo.innerHTML = `
        <span class="session-number">#${currentSession.number}</span>
        <span class="session-name">${escapeHtml(currentSession.name)}</span>
        <span class="badge badge-active">Active</span>
    `;
}

async function loadTurns() {
    if (!currentSession) return;

    try {
        const response = await fetch(`${API_BASE_URL}/conversation/turns/${currentSession.id}`);
        if (!response.ok) throw new Error('Failed to load turns');

        turns = await response.json();
        renderAcceptedInfo();
        await loadTokenInfo();
        renderTurns();
    } catch (error) {
        console.error('Error loading turns:', error);
        if (turnsContainer) turnsContainer.innerHTML = '<p class="error">Failed to load turns</p>';
    }
}

function renderAcceptedInfo() {
    if (!acceptedInfo) return;
    const acceptedCount = turns.filter(t => t.accepted).length;
    acceptedInfo.textContent = `Session ${currentSession?.id ?? ''} - ${acceptedCount} accepted records`;
}

async function loadTokenInfo() {
    if (!tokenInfo || !currentSession) return;

    try {
        const response = await fetch(`${API_BASE_URL}/conversation/sessions/${currentSession.id}/last-turn-tokens`);
        if (!response.ok) throw new Error('Failed to load token info');

        const data = await response.json();
        tokenInfo.textContent = `Last turn input token count: ${data.inputTokens}`;
    } catch (error) {
        console.error('Error loading token info:', error);
        tokenInfo.textContent = '';
    }
}

function renderTurns() {
if (!turnsContainer) return;
const visibleTurns = turns.filter(t => t.accepted);
renderAcceptedInfo();

if (visibleTurns.length === 0) {
    turnsContainer.innerHTML = '<p class="no-data">No turns yet. Start the conversation!</p>';
    return;
}

turnsContainer.innerHTML = visibleTurns.map(turn => {
    const input = normalizeText(turn.input);
    // Use displayResponse for UI display to hide log content after separator
    const response = normalizeText(turn.displayResponse || turn.response);
    const strippedTurn = normalizeText(turn.strippedTurn);

        const leftBorder = '#27ae60';

        return `
        <div class="turn-card ${turn.accepted ? 'accepted' : ''}" style="border-left: 5px solid ${leftBorder}; padding: 1.5rem; background: #ffffff; border-radius: 8px; margin-bottom: 1.5rem;">
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.5rem; padding-bottom: 0.75rem; border-bottom: 2px solid #ecf0f1;">
                <span style="color: #7f8c8d; font-size: 0.85rem;"><i class="fas fa-clock"></i> ${formatDateTime(turn.createdAt)}</span>
                <div style="display: flex; gap: 0.5rem;">
                    <button class="btn btn-small" onclick="window.conversationActions.rejectTurn(${turn.id})" style="background: #e74c3c; color: white; padding: 0.25rem 0.5rem; font-size: 0.75rem;">
                        <i class="fas fa-times"></i> Reject
                    </button>
                    <button class="btn btn-small" onclick="window.conversationActions.showTurnDetails(${turn.id})" style="background: #3498db; color: white; padding: 0.25rem 0.5rem; font-size: 0.75rem;">
                        <i class="fas fa-info-circle"></i> Details
                    </button>
                </div>
            </div>

            <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 2rem;">
                <div style="background: #f8f9fa; padding: 1.5rem; border-radius: 8px; line-height: 1.8; font-size: 0.95rem;">
                    <h4 style="color: #8e44ad; margin-bottom: 1rem; font-size: 0.9rem; text-transform: uppercase; letter-spacing: 0.5px; display: flex; justify-content: space-between; align-items: center;">
                        <span><i class="fas fa-file-alt"></i> Original</span>
                        <button type="button" class="btn btn-sm" onclick="window.conversationActions.openEditInput(${turn.id})" style="border-radius: 4px; padding: 0.25rem 0.5rem; font-size: 0.75rem; background: #f39c12; color: white;" title="Edit Input">
                            <i class="fas fa-edit"></i> Edit
                        </button>
                    </h4>

                    <div style="margin-bottom: 2rem; padding-bottom: 2rem; border-bottom: 2px solid #dee2e6;">
                        <div style="margin-bottom: 0.5rem; font-weight: 600; color: #9b59b6; font-size: 0.85rem;">
                            <i class="fas fa-keyboard"></i> INPUT:
                        </div>
                        <div style="white-space: pre-wrap; color: #2c3e50;">${escapeHtml(input)}</div>
                    </div>

                    <div>
                        <div style="margin-bottom: 0.5rem; font-weight: 600; color: #9b59b6; font-size: 0.85rem; display: flex; justify-content: space-between; align-items: center;">
                            <span><i class="fas fa-robot"></i> RESPONSE:</span>
                            <button type="button" class="btn btn-sm" onclick="window.conversationActions.openEditResponse(${turn.id})" style="border-radius: 4px; padding: 0.25rem 0.5rem; font-size: 0.75rem; background: #f39c12; color: white;" title="Edit Response">
                                <i class="fas fa-edit"></i> Edit
                            </button>
                        </div>
                        <div style="white-space: pre-wrap; color: #2c3e50;">${escapeHtml(response)}</div>
                    </div>
                </div>

                <div class="stripped-panel" data-panel="stripped" style="background: #f8f9fa; padding: 1.5rem; border-radius: 8px; line-height: 1.8; font-size: 0.9rem;">
                    <h4 style="color: #16a085; margin-bottom: 1rem; font-size: 0.9rem; text-transform: uppercase; letter-spacing: 0.5px; display: flex; justify-content: space-between; align-items: center;">
                        <span><i class="fas fa-cut"></i> Stripped</span>
                        <div style="display: flex; gap: 0.25rem;">
                            <button type="button" class="btn btn-sm" onclick="window.conversationActions.openEditStripped(${turn.id})" style="border-radius: 4px; padding: 0.25rem 0.5rem; font-size: 0.75rem; background: #f39c12; color: white;" title="Edit Stripped">
                                <i class="fas fa-edit"></i> Edit
                            </button>
                            <button type="button" class="btn btn-sm btn-info" onclick="window.conversationActions.restripTurn(${turn.id})" style="border-radius: 4px; padding: 0.25rem 0.5rem; font-size: 0.75rem; background: #3498db; color: white;" title="Redo with default model">
                                <i class="fas fa-redo"></i> Redo
                            </button>
                        </div>
                    </h4>

                    <div class="stripped-turn-content" data-turn-id="${turn.id}" style="white-space: pre-wrap; color: #2c3e50; font-family: 'Courier New', monospace; font-size: 0.85rem;">
                        ${hasText(strippedTurn) ? escapeHtml(strippedTurn) : '<span style="color: #95a5a6; font-style: italic;">No stripped content available</span>'}
                    </div>
                </div>
            </div>
        </div>`;
    }).join('');

    turnsContainer.scrollTop = turnsContainer.scrollHeight;
}

function showTurnDetails(id) {
    const turn = turns.find(t => t.id === id);
    if (!turn) return;

    turnDetails.innerHTML = `
        <div class="details-section">
            <h4>Turn Information</h4>
            <div class="details-grid">
                <div class="detail-item">
                    <strong>ID:</strong>
                    <span>${turn.id}</span>
                </div>
                <div class="detail-item">
                    <strong>Session ID:</strong>
                    <span>${turn.sessionId}</span>
                </div>
                <div class="detail-item">
                    <strong>Created:</strong>
                    <span>${formatDateTime(turn.createdAt)}</span>
                </div>
                <div class="detail-item">
                    <strong>Accepted:</strong>
                    <span>${turn.accepted ? 'Yes' : 'No'}</span>
                </div>
            </div>
        </div>

        <div class="details-section">
            <h4>Original Input</h4>
            <pre>${escapeHtml(normalizeText(turn.input))}</pre>
        </div>

        ${hasText(turn.jsonInput) ? `
        <div class="details-section">
            <h4>JSON Input</h4>
            <pre>${escapeHtml(normalizeText(turn.jsonInput))}</pre>
        </div>
        ` : ''}

        ${hasText(turn.response) ? `
        <div class="details-section">
            <h4>Response</h4>
            <pre>${escapeHtml(normalizeText(turn.response))}</pre>
        </div>
        ` : ''}

        ${hasText(turn.strippedTurn) ? `
        <div class="details-section">
            <h4>Stripped Turn</h4>
            <pre>${escapeHtml(normalizeText(turn.strippedTurn))}</pre>
        </div>
        ` : ''}
    `;

    turnModal.style.display = 'block';
}

function closeTurnModal() {
    turnModal.style.display = 'none';
}

function openEditResponse(turnId) {
    const turn = turns.find(t => t.id === turnId);
    if (!turn || !editResponseModal) return;

    if (editResponseTurnId) editResponseTurnId.value = String(turnId);
    if (editResponseTextarea) editResponseTextarea.value = normalizeText(turn.response);
    editResponseModal.style.display = 'block';
}

function closeEditResponseModal() {
    if (!editResponseModal) return;
    editResponseModal.style.display = 'none';
}

async function saveEditedResponse() {
    const id = Number(editResponseTurnId?.value);
    if (!id || !editResponseTextarea) return;

    const responseText = editResponseTextarea.value;
    if (!responseText.trim()) {
        showError('Response cannot be empty');
        return;
    }

    try {
        const res = await fetch(`${API_BASE_URL}/conversation/turns/${id}/response`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ response: responseText })
        });

        if (!res.ok) {
            const text = await res.text();
            // Truncate long error messages and extract first meaningful line
            const errorMsg = text.length > 200 ? text.substring(0, 200) + '...' : text;
            const firstLine = errorMsg.split('\n')[0];
            throw new Error(firstLine || 'Failed to update response');
        }

        const updated = await res.json();
        const idx = turns.findIndex(t => t.id === updated.id);
        if (idx !== -1) turns[idx] = updated;

        await loadTurns();
        showMessage('Response updated successfully');
    } catch (e) {
        console.error('Error updating response:', e);
        showError(e.message || 'Failed to update response');
    } finally {
        closeEditResponseModal();
    }
}

async function acceptTurn(id) {
    try {
        const response = await fetch(`${API_BASE_URL}/conversation/turns/${id}/accept`, {
            method: 'PUT'
        });

        if (!response.ok) throw new Error('Failed to accept turn');

        const updatedTurn = await response.json();
        const index = turns.findIndex(t => t.id === id);
        if (index !== -1) {
            turns[index] = updatedTurn;
            renderTurns();
        }
        showMessage('Turn accepted');
    } catch (error) {
        console.error('Error accepting turn:', error);
        showError('Failed to accept turn');
    }
}

async function rejectTurn(id) {
    try {
        const response = await fetch(`${API_BASE_URL}/conversation/turns/${id}/reject`, {
            method: 'PUT'
        });

        if (!response.ok) throw new Error('Failed to reject turn');

        const updatedTurn = await response.json();
        turns = turns.filter(t => t.id !== id);
        renderTurns();
        showMessage('Turn rejected');
    } catch (error) {
        console.error('Error rejecting turn:', error);
        showError('Failed to reject turn');
    }
}

async function copyAllToClipboard() {
    const entries = [];

    for (const t of turns) {
        const parts = [];
        if (hasText(t.input)) parts.push(normalizeText(t.input).trim());
        // Use displayResponse instead of full response to exclude log content after separator
        if (hasText(t.displayResponse)) parts.push(normalizeText(t.displayResponse).trim());
        const entry = parts.join('\n\n');
        if (entry) entries.push(entry);
    }

    if (entries.length === 0) {
        showError('No entries found to copy');
        return;
    }

    const allContent = entries.join('\n\n');

    try {
        await navigator.clipboard.writeText(allContent);
        showCopyStatus(copyAllBtn, 'Copy All to Clipboard', entries.length, allContent.length);
    } catch (err) {
        console.error('Failed to copy:', err);
        showError('Failed to copy to clipboard');
    }
}

async function copyStrippedToClipboard() {
    const entries = [];

    for (const t of turns) {
        if (hasText(t.strippedTurn)) {
            entries.push(normalizeText(t.strippedTurn).trim());
        }
    }

    if (entries.length === 0) {
        showError('No stripped content available to copy');
        return;
    }

    const allContent = entries.join('\n\n');

    try {
        await navigator.clipboard.writeText(allContent);
        showCopyStatus(copyStrippedBtn, 'Copy Stripped', entries.length, allContent.length);
    } catch (err) {
        console.error('Failed to copy stripped content:', err);
        showError('Failed to copy stripped content to clipboard');
    }
}

function showCopyStatus(btn, resetText, entriesCount, charCount) {
    if (!btn || !copyStatus) return;

    const originalHtml = btn.innerHTML;
    btn.innerHTML = '<i class="fas fa-check"></i> Copied!';

    copyStatus.textContent = `Copied ${entriesCount} entries (${charCount} characters)`;
    copyStatus.style.display = 'block';
    copyStatus.style.color = '#27ae60';

    setTimeout(() => {
        btn.innerHTML = originalHtml;
        copyStatus.style.display = 'none';
    }, 3000);
}

async function clearAllStripped() {
    if (!currentSession) return;

    const originalHtml = clearAllStrippedBtn ? clearAllStrippedBtn.innerHTML : null;
    if (clearAllStrippedBtn) {
        clearAllStrippedBtn.disabled = true;
        clearAllStrippedBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Clearing...';
    }

    try {
        const res = await fetch(`${API_BASE_URL}/conversation/sessions/${currentSession.id}/clear-all-stripped`, { method: 'POST' });
        if (!res.ok) {
            const text = await res.text();
            throw new Error(text || 'Failed to clear stripped content');
        }

        await loadTurns();
        if (clearAllStrippedBtn) {
            clearAllStrippedBtn.innerHTML = '<i class="fas fa-check"></i> Cleared!';
        }

        if (copyStatus) {
            copyStatus.textContent = 'All stripped content cleared successfully';
            copyStatus.style.display = 'block';
            copyStatus.style.color = '#27ae60';
        }

        setTimeout(() => {
            if (clearAllStrippedBtn) {
                clearAllStrippedBtn.disabled = false;
                clearAllStrippedBtn.innerHTML = originalHtml;
            }
            if (copyStatus) copyStatus.style.display = 'none';
        }, 2000);
    } catch (e) {
        console.error(e);
        showError(e.message);
        if (clearAllStrippedBtn) {
            clearAllStrippedBtn.disabled = false;
            clearAllStrippedBtn.innerHTML = originalHtml;
        }
    }
}

async function restripTurn(turnId, model = null) {
    let url = `${API_BASE_URL}/conversation/turns/${turnId}/restrip`;
    if (model) {
        url += `?model=${encodeURIComponent(model)}`;
    }

    let progressEl = null;
    let previousHtml = null;
    try {
        progressEl = document.querySelector(`.stripped-turn-content[data-turn-id="${turnId}"]`);
        previousHtml = progressEl ? progressEl.innerHTML : null;
        if (progressEl) {
            const modelLabel = model ? ` (${model})` : '';
            progressEl.innerHTML = `<span style="color: #3498db; font-style: italic;"><i class="fas fa-spinner fa-spin"></i> Stripping${modelLabel}...</span>`;
        }

        const res = await fetch(url, { method: 'POST' });
        if (!res.ok) {
            const text = await res.text();
            throw new Error(text || 'Failed to restrip turn');
        }

        const updated = await res.json();
        const idx = turns.findIndex(t => t.id === updated.id);
        if (idx !== -1) turns[idx] = updated;

        await loadTurns();
        showMessage('Turn re-stripped successfully');
    } catch (e) {
        console.error(e);
        showError(e.message);

        if (progressEl && previousHtml !== null) progressEl.innerHTML = previousHtml;
    }
}

function openEditInput(turnId) {
    const turn = turns.find(t => t.id === turnId);
    if (!turn || !editInputModal) return;

    if (editInputTurnId) editInputTurnId.value = String(turnId);
    if (editInputTextarea) editInputTextarea.value = normalizeText(turn.input);
    editInputModal.style.display = 'block';
}

function closeEditInputModal() {
    if (!editInputModal) return;
    editInputModal.style.display = 'none';
}

async function saveEditedInput() {
    const id = Number(editInputTurnId?.value);
    if (!id || !editInputTextarea) return;

    const inputText = editInputTextarea.value;
    if (!inputText.trim()) {
        showError('Input cannot be empty');
        return;
    }

    try {
        const res = await fetch(`${API_BASE_URL}/conversation/turns/${id}/input`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ input: inputText })
        });

        if (!res.ok) {
            const text = await res.text();
            // Truncate long error messages and extract first meaningful line
            const errorMsg = text.length > 200 ? text.substring(0, 200) + '...' : text;
            const firstLine = errorMsg.split('\n')[0];
            throw new Error(firstLine || 'Failed to update input');
        }

        const updated = await res.json();
        const idx = turns.findIndex(t => t.id === updated.id);
        if (idx !== -1) turns[idx] = updated;

        await loadTurns();
        showMessage('Input updated successfully');
    } catch (e) {
        console.error('Error updating input:', e);
        showError(e.message || 'Failed to update input');
    } finally {
        closeEditInputModal();
    }
}

function openEditStripped(turnId) {
    const turn = turns.find(t => t.id === turnId);
    if (!turn || !editStrippedModal) return;

    if (editStrippedTurnId) editStrippedTurnId.value = String(turnId);
    if (editStrippedTextarea) editStrippedTextarea.value = normalizeText(turn.strippedTurn);
    editStrippedModal.style.display = 'block';
}

function closeEditStrippedModal() {
    if (!editStrippedModal) return;
    editStrippedModal.style.display = 'none';
}

async function saveEditedStripped() {
    const id = Number(editStrippedTurnId?.value);
    if (!id || !editStrippedTextarea) return;

    const strippedText = editStrippedTextarea.value;
    if (!strippedText.trim()) {
        showError('Stripped content cannot be empty');
        return;
    }

    try {
        const res = await fetch(`${API_BASE_URL}/conversation/turns/${id}/stripped`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ stripped: strippedText })
        });

        if (!res.ok) {
            const text = await res.text();
            // Truncate long error messages and extract first meaningful line
            const errorMsg = text.length > 200 ? text.substring(0, 200) + '...' : text;
            const firstLine = errorMsg.split('\n')[0];
            throw new Error(firstLine || 'Failed to update stripped content');
        }

        const updated = await res.json();
        const idx = turns.findIndex(t => t.id === updated.id);
        if (idx !== -1) turns[idx] = updated;

        await loadTurns();
        showMessage('Stripped content updated successfully');
    } catch (e) {
        console.error('Error updating stripped content:', e);
        showError(e.message || 'Failed to update stripped content');
    } finally {
        closeEditStrippedModal();
    }
}

// Expose functions to window for HTML onclick handlers
window.conversationActions = {
    showTurnDetails,
    openEditResponse,
    openEditInput,
    openEditStripped,
    acceptTurn,
    rejectTurn,
    restripTurn
};