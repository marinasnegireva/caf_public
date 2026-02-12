/**
 * Modal Management Module
 * Handles all modal dialogs (create, edit, versions)
 */

import { showMessage, escapeHtml, formatDateTime, createElementFromHTML, removeElement } from './utils.js';

/**
 * Show create message modal
 * @param {string} type - Message type (Persona, Perception, etc)
 * @param {number} defaultPriority - Default priority for context files (0=hot, 2=cold)
 */
export async function showCreateModal(type, defaultPriority = 1) {
    document.getElementById('modalTitle').textContent = `Create New ${type}`;
    document.getElementById('editId').value = '';
    document.getElementById('editType').value = type;
    document.getElementById('editName').value = '';
    document.getElementById('editContent').value = '';
    document.getElementById('editDescription').value = '';
    document.getElementById('editTags').value = '';
    document.getElementById('editNotes').value = '';
    document.getElementById('editIsActive').checked = false;

    // Show/hide user profile checkbox and priority based on type
    const userProfileGroup = document.getElementById('userProfileGroup');
    const priorityGroup = document.getElementById('priorityGroup');

    if (type === 'ContextFile') {
        userProfileGroup.style.display = 'block';
        document.getElementById('editIsUserProfile').checked = false;

        // Show and set priority
        if (priorityGroup) {
            priorityGroup.style.display = 'block';
            document.getElementById('editPriority').value = defaultPriority.toString();
        }
    } else {
        userProfileGroup.style.display = 'none';
        if (priorityGroup) {
            priorityGroup.style.display = 'none';
        }
    }

    document.getElementById('editModal').classList.add('show');
}

/**
 * Show edit message modal
 * @param {number} messageId - Message ID
 * @param {string} type - Message type (Persona, Perception, etc)
 */
export async function showEditModal(messageId, type) {
    // Find the message in window.currentMessages
    const messages = window.currentMessages?.[type];
    if (!messages) {
        showMessage('Error: Messages not loaded', 'error');
        return;
    }

    const message = messages.find(m => m.id === messageId);
    if (!message) {
        showMessage('Error: Message not found', 'error');
        return;
    }

    // Populate modal with message data
    document.getElementById('modalTitle').textContent = `Edit ${type}: ${message.name}`;
    document.getElementById('editId').value = message.id;
    document.getElementById('editType').value = type;
    document.getElementById('editName').value = message.name || '';
    document.getElementById('editContent').value = message.content || '';
    document.getElementById('editDescription').value = message.description || '';
    document.getElementById('editTags').value = Array.isArray(message.tags) ? message.tags.join(', ') : '';
    document.getElementById('editNotes').value = message.notes || '';
    document.getElementById('editIsActive').checked = message.isActive || false;

    // Show/hide user profile checkbox and priority based on type
    const userProfileGroup = document.getElementById('userProfileGroup');
    const priorityGroup = document.getElementById('priorityGroup');

    if (type === 'ContextFile') {
        userProfileGroup.style.display = 'block';
        document.getElementById('editIsUserProfile').checked = message.isUserProfile || false;

        // Show and set priority
        if (priorityGroup) {
            priorityGroup.style.display = 'block';
            document.getElementById('editPriority').value = (message.priority ?? 1).toString();
        }
    } else {
        userProfileGroup.style.display = 'none';
        if (priorityGroup) {
            priorityGroup.style.display = 'none';
        }
    }

    document.getElementById('editModal').classList.add('show');
}

/**
 * Close main modal
 */
export function closeModal() {
    document.getElementById('editModal').classList.remove('show');
}

/**
 * Show version history modal
 * @param {Array} versions - Array of versions
 */
export function showVersionHistoryModal(versions) {
    if (versions.length === 0) {
        showMessage('No version history available for this message');
        return;
    }

    const modalHtml = `
        <div id="versionHistoryModal" class="modal show">
            <div class="modal-content" style="max-width: 1200px;">
                <div class="modal-header">
                    <h3>Version History</h3>
                    <button class="modal-close" onclick="window.modalManager.closeVersionHistoryModal()">&times;</button>
                </div>
                <div class="modal-body">
                    <p style="color: var(--text-secondary); margin-bottom: 20px;">
                        View all versions of this system message. Click "Set as Active" to make a version active.
                    </p>

                    <div style="max-height: 500px; overflow-y: auto;">
                        ${versions.map(version => renderVersionCard(version)).join('')}
                    </div>
                </div>
                <div class="modal-footer">
                    <button class="secondary" onclick="window.modalManager.closeVersionHistoryModal()">Close</button>
                </div>
            </div>
        </div>
    `;

    const modal = createElementFromHTML(modalHtml);
    document.body.appendChild(modal);
}

/**
 * Render a version card
 */
function renderVersionCard(version) {
    return `
        <div class="message-card ${version.isActive ? 'active' : ''}" style="margin-bottom: 16px;">
            <div class="message-header">
                <div style="flex: 1;">
                    <div class="message-title">
                        <span class="badge badge-version">v${version.version}</span>
                        ${version.isActive ? '<span class="badge badge-active">ACTIVE</span>' : ''}
                        ${escapeHtml(version.name)}
                    </div>
                    <div class="message-meta">
                        Created: ${formatDateTime(version.createdAt)}
                        ${version.modifiedBy ? ` | By: ${escapeHtml(version.modifiedBy)}` : ''}
                    </div>
                    ${version.description ?
            `<div class="message-meta" style="margin-top: 4px;">${escapeHtml(version.description)}</div>`
            : ''
        }
                </div>
            </div>
            <div class="message-content" style="max-height: 150px; overflow-y: auto; margin-top: 12px;">
                ${escapeHtml(version.content)}
            </div>
            ${!version.isActive ? `
                <div style="margin-top: 12px;">
                    <button class="success" onclick="window.apiClient.setActive(${version.id}); window.modalManager.closeVersionHistoryModal();">
                        Set as Active
                    </button>
                </div>
            ` : ''}
        </div>
    `;
}

/**
 * Close version history modal
 */
export function closeVersionHistoryModal() {
    removeElement('versionHistoryModal');
}

export default {
    showCreateModal,
    showEditModal,
    closeModal,
    showVersionHistoryModal,
    closeVersionHistoryModal
};