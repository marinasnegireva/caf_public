/**
 * System Messages Manager - Main Application
 * Modular architecture with ES6 imports
 */

import { showMessage, setupAutoResize, getCurrentTabType, escapeHtml } from './js/utils.js';
import { renderMessageCard } from './js/message-card.js';
import * as diffViewer from './js/diff-viewer.js';
import * as modalManager from './js/modal-manager.js';
import * as apiClient from './js/api-client.js';
import * as messageOperations from './js/message-operations.js';
import * as versionManager from './js/version-manager.js';
import { populateContentFields } from './js/systemmessages-debug.js';

// Global state - initialize on window directly
window.currentMessages = window.currentMessages || {};

// Expose modules to window for HTML onclick handlers
window.diffViewer = diffViewer;
window.modalManager = modalManager;
window.apiClient = apiClient;
window.messageOperations = messageOperations;
window.versionManager = versionManager;

/**
 * Switch between tabs
 */
window.switchTab = async function (tab, event) {
    document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
    document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));

    event.target.closest('.tab').classList.add('active');
    document.getElementById(`${tab}-tab`).classList.add('active');

    if (tab !== 'preview') {
        // Convert tab name to proper type format
        const typeMap = {
            'persona': 'Persona',
            'perception': 'Perception',
            'technical': 'Technical',
            'hotcontext': 'HotContext',
            'coldcontext': 'ColdContext'
        };
        const type = typeMap[tab.toLowerCase()];
        if (type) {
            await window.loadMessages(type);
        }
    }
};

/**
 * Render a category section with messages
 */
function renderCategorySection(categoryName, messages, type) {
    return `
        <div class="category-header">
            <div class="category-title">
                ${categoryName}
                <span class="category-count">${messages.length}</span>
            </div>
        </div>
        <div class="category-messages">
            ${messages.map(m => renderMessageCard(m, type)).join('')}
        </div>
    `;
}

/**
 * Render archived messages in a collapsible section
 */
function renderArchivedSection(messages, type) {
    const sectionId = `archived-section-${type}`;
    return `
        <div class="category-header" style="margin-top: 32px;">
            <div class="category-title" onclick="toggleArchived('${sectionId}')" style="cursor: pointer;">
                <span class="expand-icon" id="icon-${sectionId}">
                    <i class="fas fa-chevron-down"></i>
                </span>
                Archived
                <span class="category-count">${messages.length}</span>
            </div>
        </div>
        <div class="category-messages" id="${sectionId}" style="display: none;">
            ${messages.map(m => renderMessageCard(m, type)).join('')}
        </div>
    `;
}

/**
 * Convert message type to container ID suffix
 */
function getContainerIdSuffix(type) {
    const suffixMap = {
        'Persona': 'persona',
        'Perception': 'perception',
        'Technical': 'technical',
        'ContextFile': 'context',
        'HotContext': 'hotcontext',
        'ColdContext': 'coldcontext'
    };
    return suffixMap[type] || type.toLowerCase();
}

/**
 * Load messages for a specific type
 */
async function loadMessages(type) {
    // Convert type to container ID suffix
    const containerSuffix = getContainerIdSuffix(type);
    const containerId = `${containerSuffix}-list`;
    const container = document.getElementById(containerId);
    if (!container) {
        console.error(`Container not found for type: ${type}, looking for: ${containerId}`);
        return;
    }

    container.innerHTML = '<div class="loading">Loading...</div>';

    try {
        let messages;

        // Handle Hot/Cold context as filtered ContextFile
        if (type === 'HotContext' || type === 'ColdContext') {
            // Load all context files
            const allContextFiles = await apiClient.loadMessages('ContextFile');

            // Filter by priority
            if (type === 'HotContext') {
                // Hot = Priority 0-1
                messages = allContextFiles.filter(m => (m.priority ?? 1) <= 1);
            } else {
                // Cold = Priority 2-3
                messages = allContextFiles.filter(m => (m.priority ?? 1) >= 2);
            }

            // Store with original type for operations
            window.currentMessages['ContextFile'] = allContextFiles;
        } else {
            messages = await apiClient.loadMessages(type);
            window.currentMessages[type] = messages;
        }

        console.log('Loaded messages for', type, messages);

        if (messages.length === 0) {
            const emptyMessage = type === 'HotContext'
                ? 'No hot context files found. Create one with Priority 0-1!'
                : type === 'ColdContext'
                    ? 'No cold context files found. Create one with Priority 2-3!'
                    : 'No messages found. Create your first one!';
            container.innerHTML = `<div class="no-messages">${emptyMessage}</div>`;
            return;
        }

        // Separate active, inactive, and archived messages
        const active = messages.filter(m => m.isActive && !m.isArchived);
        const inactive = messages.filter(m => !m.isActive && !m.isArchived);
        const archived = messages.filter(m => m.isArchived);

        // Use ContextFile as the type for rendering (so edit/delete works correctly)
        const renderType = (type === 'HotContext' || type === 'ColdContext') ? 'ContextFile' : type;

        let html = '';

        // Active section
        if (active.length > 0) {
            html += renderCategorySection('Active', active, renderType);
        }

        // Inactive section
        if (inactive.length > 0) {
            html += renderCategorySection('Inactive', inactive, renderType);
        }

        // Archived section (collapsible)
        if (archived.length > 0) {
            html += renderArchivedSection(archived, renderType);
        }

        container.innerHTML = html;
        setupAutoResize();

        // Populate content fields programmatically to avoid HTML attribute issues
        populateContentFields(window.currentMessages);
    } catch (error) {
        console.error('Error loading messages:', error);
        container.innerHTML = `<div class="no-messages">Error: ${error.message}</div>`;
    }
}

/**
 * Toggle archived section visibility
 */
window.toggleArchived = function (sectionId) {
    const section = document.getElementById(sectionId);
    const icon = document.getElementById(`icon-${sectionId}`);

    if (section) {
        section.style.display = section.style.display === 'none' ? 'block' : 'none';
    }
    if (icon) {
        icon.classList.toggle('open');
    }
};

/**
 * Get current tab type (corrected)
 */
window.getCurrentTabType = function () {
    const activeTab = document.querySelector('.tab.active');
    if (!activeTab) return null;

    const badgeElement = activeTab.querySelector('[class*="badge-"]');
    if (!badgeElement) return null;

    const classes = badgeElement.className;
    if (classes.includes('badge-persona')) return 'Persona';
    if (classes.includes('badge-perception')) return 'Perception';
    if (classes.includes('badge-technical')) return 'Technical';
    if (classes.includes('badge-hot')) return 'HotContext';
    if (classes.includes('badge-cold')) return 'ColdContext';
    if (classes.includes('badge-context')) return 'ContextFile';
    return null;
};

/**
 * Expose loadMessages globally
 */
window.loadMessages = loadMessages;

/**
 * Mark message as modified (exposed globally)
 */
window.markAsModified = function (messageId) {
    messageOperations.markAsModified(messageId);
};

/**
 * Reset changes (exposed globally)
 */
window.resetChanges = function (messageId) {
    messageOperations.resetChanges(messageId, window.currentMessages);
};

/**
 * Toggle diff view (exposed globally)
 */
window.toggleDiff = function (messageId) {
    diffViewer.toggleDiff(messageId, window.currentMessages);
};

/**
 * Update message (exposed globally)
 */
window.updateMessage = async function (messageId) {
    await messageOperations.updateMessage(messageId, window.currentMessages, loadMessages);
};

/**
 * Show create modal (exposed globally)
 */
window.showCreateModal = function (type) {
    modalManager.showCreateModal(type);
};

/**
 * Close modal (exposed globally)
 */
window.closeModal = function () {
    modalManager.closeModal();
};

/**
 * Save message (exposed globally)
 */
window.saveMessage = async function () {
    const id = document.getElementById('editId').value;
    const type = document.getElementById('editType').value;

    const message = {
        name: document.getElementById('editName').value.trim(),
        content: document.getElementById('editContent').value.trim(),
        type: type,
        description: document.getElementById('editDescription').value.trim(),
        tags: document.getElementById('editTags').value.split(',').map(t => t.trim()).filter(t => t),
        notes: document.getElementById('editNotes').value.trim(),
        isActive: document.getElementById('editIsActive').checked
    };

    // Add isUserProfile and priority for context files
    if (type === 'ContextFile') {
        message.isUserProfile = document.getElementById('editIsUserProfile').checked;
        message.priority = parseInt(document.getElementById('editPriority')?.value ?? '1');
    }

    if (id) {
        // Update existing message
        try {
            await apiClient.updateMessage(parseInt(id), message);
            modalManager.closeModal();
            const tabType = window.getCurrentTabType();
            if (tabType) await window.loadMessages(tabType);
        } catch (error) {
            // Error already shown
        }
    } else {
        // Create new message - include profileId if available
        const activeProfileId = await apiClient.fetchActiveProfile();
        if (activeProfileId !== null) {
            message.profileId = activeProfileId;
        }
        await messageOperations.saveMessage(message, modalManager.closeModal, loadMessages);
    }
};

/**
 * Set message as active (exposed globally)
 */
window.setActive = async function (messageId) {
    try {
        await apiClient.setActive(messageId);
        const tabType = window.getCurrentTabType();
        if (tabType) await window.loadMessages(tabType);
    } catch (error) {
        // Error already shown
    }
};

/**
 * Delete message (exposed globally)
 */
window.deleteMessage = async function (messageId) {
    if (!confirm('Are you sure you want to delete this message?')) return;

    try {
        await apiClient.deleteMessage(messageId);
        const tabType = window.getCurrentTabType();
        if (tabType) await window.loadMessages(tabType);
    } catch (error) {
        // Error already shown
    }
};

/**
 * Create version (exposed globally)
 */
window.createVersion = async function (messageId) {
    await versionManager.createVersion(messageId, loadMessages);
};

/**
 * View versions (exposed globally)
 */
window.viewVersions = async function (messageId) {
    await versionManager.viewVersions(messageId);
};

/**
 * Load preview (exposed globally)
 */
window.loadPreview = async function () {
    const previewContent = document.getElementById('preview-content');
    previewContent.innerHTML = '<div class="loading">Loading preview...</div>';

    try {
        const [preview, persona, userProfile] = await Promise.all([
            apiClient.loadPreview(),
            apiClient.getActivePersona(),
            apiClient.getActiveUserProfile()
        ]);

        const personaName = persona?.name || 'No active persona';
        const userName = userProfile?.name || 'No active user';
        const userSource = userProfile?.source === 'context' ? ' (Context File)' : userProfile?.source === 'profile' ? ' (User Profile)' : '';
        const tokenCount = preview.tokenCount || 0;

        // Build itemized list HTML
        let itemsListHtml = '';
        if (preview.items && preview.items.length > 0) {
            itemsListHtml = `
                <h4 style="margin-top: 30px; margin-bottom: 16px; color: var(--text-secondary);">Items Breakdown:</h4>
                <div style="background: var(--bg-secondary); border-radius: 8px; overflow: hidden; border: 1px solid var(--border-color);">
                    <table style="width: 100%; border-collapse: collapse;">
                        <thead>
                            <tr style="background: var(--bg-tertiary); border-bottom: 2px solid var(--border-color);">
                                <th style="padding: 12px 16px; text-align: left; color: var(--text-secondary); font-weight: 600; font-size: 12px; text-transform: uppercase; letter-spacing: 0.5px;">Item Name</th>
                                <th style="padding: 12px 16px; text-align: left; color: var(--text-secondary); font-weight: 600; font-size: 12px; text-transform: uppercase; letter-spacing: 0.5px;">Type</th>
                                <th style="padding: 12px 16px; text-align: right; color: var(--text-secondary); font-weight: 600; font-size: 12px; text-transform: uppercase; letter-spacing: 0.5px;">Token Count</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${preview.items.map((item, index) => `
                                <tr style="border-bottom: 1px solid var(--border-color); ${index % 2 === 0 ? 'background: var(--bg-primary);' : ''}">
                                    <td style="padding: 12px 16px; color: var(--text-primary); font-weight: 500;">${escapeHtml(item.name)}</td>
                                    <td style="padding: 12px 16px; color: var(--text-muted);">
                                        <span style="display: inline-block; padding: 4px 8px; border-radius: 4px; font-size: 11px; font-weight: 500; ${item.type === 'Persona' ? 'background: #e3f2fd; color: #1976d2;' :
                    item.type === 'Context File' ? 'background: #fff3e0; color: #e65100;' : ''
                }">${escapeHtml(item.type)}</span>
                                    </td>
                                    <td style="padding: 12px 16px; text-align: right; color: var(--accent-blue); font-weight: 600; font-family: 'Courier New', monospace;">
                                        ${item.tokenCount.toLocaleString()}
                                    </td>
                                </tr>
                            `).join('')}
                            <tr style="background: var(--bg-tertiary); border-top: 2px solid var(--border-color); font-weight: 600;">
                                <td colspan="2" style="padding: 12px 16px; color: var(--text-primary);">Total</td>
                                <td style="padding: 12px 16px; text-align: right, color: var(--accent-blue); font-family: 'Courier New', monospace; font-size: 14px;">
                                    ${tokenCount.toLocaleString()}
                                </td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            `;
        }

        previewContent.innerHTML = `
            <div class="message-content" style="max-height: none;">
                <h3 style="margin-top: 0; color: #4a9eff;">Persona Preview</h3>

                <div style="background: var(--bg-secondary); padding: 16px; border-radius: 8px; margin-bottom: 20px; border: 1px solid var(--border-color);">
                    <h4 style="margin-top: 0; color: var(--text-secondary);">Active Configuration:</h4>
                    <div style="display: grid; grid-template-columns: auto 1fr; gap: 12px; color: var(--text-primary);">
                        <strong>Persona Name:</strong>
                        <span style="color: ${persona ? 'var(--accent-green)' : 'var(--text-muted)'};">
                            ${personaName}
                        </span>

                        <strong>User Name:</strong>
                        <span style="color: ${userProfile ? 'var(--accent-green)' : 'var(--text-muted)'};">
                            ${userName}${userSource}
                        </span>

                        <strong>Token Count:</strong>
                        <span style="color: var(--accent-blue); font-weight: 600;">
                            ${tokenCount.toLocaleString()} tokens
                        </span>
                    </div>
                </div>

                ${itemsListHtml}

                <div style="background: var(--bg-tertiary); padding: 12px 16px; border-radius: 8px 8px 0 0; border: 1px solid var(--border-color); border-bottom: none; margin-top: 30px;">
                    <h4 style="margin: 0; color: var(--text-secondary); font-size: 12px; text-transform: uppercase; letter-spacing: 0.5px;">
                        Persona Content:
                    </h4>
                </div>
                <pre style="white-space: pre-wrap; word-wrap: break-word; line-height: 1.6; background: var(--bg-primary); padding: 16px; margin: 0; border-radius: 0 0 8px 8px; border: 1px solid var(--border-color); max-height: 600px; overflow-y: auto;">${escapeHtml(preview.completeMessage || 'No active persona configured')}</pre>

                <h4 style="margin-top: 30px; color: var(--text-secondary);">Component Summary:</h4>
                <ul style="color: var(--text-primary); line-height: 1.8;">
                    <li>
                        <strong>Active Persona:</strong>
                        <span style="color: ${preview.hasPersona ? 'var(--accent-green)' : 'var(--accent-red)'};">
                            ${preview.hasPersona ? '<i class="fas fa-check"></i> Active' : '<i class="fas fa-times"></i> None'}
                        </span>
                    </li>
                    <li>
                        <strong>Context Files:</strong>
                        <span style="color: ${preview.contextCount > 0 ? 'var(--accent-green)' : 'var(--text-muted)'};">
                            ${preview.contextCount || 0} attached
                        </span>
                    </li>
                </ul>

                <div style="margin-top: 20px; padding: 12px; background: var(--bg-secondary); border-left: 3px solid var(--accent-blue); border-radius: 4px;">
                    <p style="margin: 0; color: var(--text-secondary); font-size: 14px;">
                        <strong><i class="fas fa-info-circle"></i> Note:</strong> This preview shows only the active persona and its attached context files.
                        Technical messages and perceptions are excluded from this view. In conversations, the system will use <strong>${userName}</strong> for user messages
                        and <strong>${personaName}</strong> for assistant responses.
                    </p>
                </div>
            </div>
        `;
    } catch (error) {
        previewContent.innerHTML = `<div class="empty-state">Error: ${error.message}</div>`;
    }
};

/**
 * Initialize application
 */
document.addEventListener('DOMContentLoaded', async () => {
    console.log('System Messages Manager initialized (modular)');

    // Fetch active profile first
    await apiClient.fetchActiveProfile();

    // Load persona messages by default
    await window.loadMessages('Persona');

    // Setup textarea auto-resize
    setupAutoResize();

    // Re-apply auto-resize when messages are loaded
    const observer = new MutationObserver(() => {
        setupAutoResize();
    });

    const personaList = document.getElementById('persona-list');
    const perceptionList = document.getElementById('perception-list');
    const technicalList = document.getElementById('technical-list');
    const hotcontextList = document.getElementById('hotcontext-list');
    const coldcontextList = document.getElementById('coldcontext-list');

    if (personaList) observer.observe(personaList, { childList: true, subtree: true });
    if (perceptionList) observer.observe(perceptionList, { childList: true, subtree: true });
    if (technicalList) observer.observe(technicalList, { childList: true, subtree: true });
    if (hotcontextList) observer.observe(hotcontextList, { childList: true, subtree: true });
    if (coldcontextList) observer.observe(coldcontextList, { childList: true, subtree: true });
});

/**
 * Show modal for updating context files from folder
 */
window.showUpdateFromFolderModal = function () {
    // Create modal if it doesn't exist
    let modal = document.getElementById('updateFromFolderModal');
    if (!modal) {
        modal = document.createElement('div');
        modal.id = 'updateFromFolderModal';
        modal.className = 'modal';
        modal.innerHTML = `
            <div class="modal-content" style="max-width: 600px;">
                <div class="modal-header">
                    <h3>Update Context Files from Folder</h3>
                    <button class="modal-close" onclick="closeUpdateFromFolderModal()">&times;</button>
                </div>
                <div class="modal-body">
                    <div class="form-group">
                        <label for="folderPath">Folder Path *</label>
                        <input type="text" id="folderPath"
                               placeholder="e.g., F:\\git\\personal\\caf\\CAF\\Data\\framework\\v1\\"
                               value="F:\\git\\personal\\caf\\CAF\\Data\\framework\\v1\\"
                               style="font-family: monospace;">
                        <small>Path to folder containing .md files. Subdirectories will be searched.</small>
                    </div>
                    <div style="background: #f5f5f5; padding: 12px; border-radius: 6px; margin-top: 12px;">
                        <p style="margin: 0 0 8px 0; font-weight: 600; color: #333;">How it works:</p>
                        <ul style="margin: 0; padding-left: 20px; color: #666; font-size: 13px;">
                            <li>Finds all <strong>active context files</strong> in the system</li>
                            <li>Matches them with <strong>.md files</strong> by name (case-insensitive)</li>
                            <li>Creates a <strong>new version</strong> of each context file with updated content</li>
                            <li>Skips files where content hasn't changed</li>
                        </ul>
                    </div>
                </div>
                <div class="modal-footer">
                    <button class="secondary" onclick="closeUpdateFromFolderModal()">Cancel</button>
                    <button class="success" onclick="executeUpdateFromFolder()">
                        <i class="fas fa-sync-alt"></i> Update Context Files
                    </button>
                </div>
            </div>
        `;
        document.body.appendChild(modal);
    }
    modal.style.display = 'block';
};

/**
 * Close update from folder modal
 */
window.closeUpdateFromFolderModal = function () {
    const modal = document.getElementById('updateFromFolderModal');
    if (modal) {
        modal.style.display = 'none';
    }
};

/**
 * Execute update from folder
 */
window.executeUpdateFromFolder = async function () {
    const folderPath = document.getElementById('folderPath').value.trim();

    if (!folderPath) {
        showMessage('Please enter a folder path', true);
        return;
    }

    try {
        showMessage('Updating context files from folder...', false);

        const response = await fetch('/api/systemmessages/update-from-folder', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ folderPath: folderPath })
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'Failed to update from folder');
        }

        const result = await response.json();

        closeUpdateFromFolderModal();

        // Show results in a popup
        showUpdateResults(result);

        // Reload current tab
        const tabType = window.getCurrentTabType();
        if (tabType) await window.loadMessages(tabType);
    } catch (error) {
        showMessage(`Error: ${error.message}`, true);
    }
};

/**
 * Show update results in a popup
 */
function showUpdateResults(result) {
    let modal = document.getElementById('updateResultsModal');
    if (!modal) {
        modal = document.createElement('div');
        modal.id = 'updateResultsModal';
        modal.className = 'modal';
        document.body.appendChild(modal);
    }

    const formatItems = (items, icon, color) => {
        if (!items || items.length === 0) return '';
        return items.map(item => `
            <div style="display: flex; align-items: flex-start; gap: 8px; padding: 8px; background: white; border-radius: 4px; margin-bottom: 4px;">
                <span style="color: ${color};"><i class="fas ${icon}"></i></span>
                <div>
                    <strong>${escapeHtml(item.contextFileName)}</strong>
                    ${item.mdFileName ? ` ? ${escapeHtml(item.mdFileName)}` : ''}
                    ${item.newVersion ? ` (v${item.newVersion})` : ''}
                    <br><small style="color: #666;">${escapeHtml(item.message)}</small>
                </div>
            </div>
        `).join('');
    };

    const totalProcessed = (result.updated?.length || 0) + (result.notFound?.length || 0) +
        (result.skipped?.length || 0) + (result.errors?.length || 0);

    modal.innerHTML = `
        <div class="modal-content" style="max-width: 700px; max-height: 80vh; overflow-y: auto;">
            <div class="modal-header">
                <h3>Update Results</h3>
                <button class="modal-close" onclick="document.getElementById('updateResultsModal').style.display='none'">&times;</button>
            </div>
            <div class="modal-body">
                <div style="display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; margin-bottom: 20px;">
                    <div style="text-align: center; padding: 12px; background: #e8f5e9; border-radius: 6px;">
                        <div style="font-size: 24px; font-weight: bold; color: #2e7d32;">${result.updated?.length || 0}</div>
                        <div style="font-size: 12px; color: #388e3c;">Updated</div>
                    </div>
                    <div style="text-align: center; padding: 12px; background: #e3f2fd; border-radius: 6px;">
                        <div style="font-size: 24px; font-weight: bold; color: #1565c0;">${result.skipped?.length || 0}</div>
                        <div style="font-size: 12px; color: #1976d2;">Unchanged</div>
                    </div>
                    <div style="text-align: center; padding: 12px; background: #fff3e0; border-radius: 6px;">
                        <div style="font-size: 24px; font-weight: bold; color: #e65100;">${result.notFound?.length || 0}</div>
                        <div style="font-size: 12px; color: #f57c00;">Not Found</div>
                    </div>
                    <div style="text-align: center; padding: 12px; background: #ffebee; border-radius: 6px;">
                        <div style="font-size: 24px; font-weight: bold; color: #c62828;">${result.errors?.length || 0}</div>
                        <div style="font-size: 12px; color: #d32f2f;">Errors</div>
                    </div>
                </div>

                ${result.updated?.length > 0 ? `
                    <div style="margin-bottom: 16px;">
                        <h4 style="margin: 0 0 8px 0; color: #2e7d32;"><i class="fas fa-check-circle"></i> Updated (${result.updated.length})</h4>
                        <div style="background: #f5f5f5; padding: 8px; border-radius: 6px; max-height: 150px; overflow-y: auto;">
                            ${formatItems(result.updated, 'fa-check', '#2e7d32')}
                        </div>
                    </div>
                ` : ''}

                ${result.skipped?.length > 0 ? `
                    <div style="margin-bottom: 16px;">
                        <h4 style="margin: 0 0 8px 0; color: #1565c0;"><i class="fas fa-forward"></i> Unchanged (${result.skipped.length})</h4>
                        <div style="background: #f5f5f5; padding: 8px; border-radius: 6px; max-height: 150px; overflow-y: auto;">
                            ${formatItems(result.skipped, 'fa-equals', '#1565c0')}
                        </div>
                    </div>
                ` : ''}

                ${result.notFound?.length > 0 ? `
                    <div style="margin-bottom: 16px;">
                        <h4 style="margin: 0 0 8px 0; color: #e65100;"><i class="fas fa-question-circle"></i> No Matching File (${result.notFound.length})</h4>
                        <div style="background: #f5f5f5; padding: 8px; border-radius: 6px; max-height: 150px; overflow-y: auto;">
                            ${formatItems(result.notFound, 'fa-question', '#e65100')}
                        </div>
                    </div>
                ` : ''}

                ${result.errors?.length > 0 ? `
                    <div style="margin-bottom: 16px;">
                        <h4 style="margin: 0 0 8px 0; color: #c62828;"><i class="fas fa-exclamation-circle"></i> Errors (${result.errors.length})</h4>
                        <div style="background: #f5f5f5; padding: 8px; border-radius: 6px; max-height: 150px; overflow-y: auto;">
                            ${formatItems(result.errors, 'fa-times', '#c62828')}
                        </div>
                    </div>
                ` : ''}
            </div>
            <div class="modal-footer">
                <button onclick="document.getElementById('updateResultsModal').style.display='none'">Close</button>
            </div>
        </div>
    `;

    modal.style.display = 'block';

    // Show summary message
    if (result.updated?.length > 0) {
        showMessage(`Successfully updated ${result.updated.length} context file(s)`, false);
    } else if (result.skipped?.length > 0 && result.notFound?.length === 0) {
        showMessage('All files are already up to date', false);
    } else {
        showMessage(`Processed ${totalProcessed} file(s). See results for details.`, false);
    }
}

/**
 * Initialize default technical messages from server constants
 */
window.initializeDefaults = async function () {
    if (!confirm('This will create default technical messages if they don\'t already exist.\n\nExisting messages will not be affected.\n\nContinue?')) {
        return;
    }

    try {
        const response = await fetch('/api/systemmessages/initialize-defaults', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' }
        });

        if (!response.ok) {
            throw new Error(`Server returned ${response.status}`);
        }

        const result = await response.json();

        // Build summary message
        const messages = [];
        if (result.created.length > 0) {
            messages.push(`✅ Created ${result.created.length} technical message(s):`);
            result.created.forEach(item => {
                messages.push(`  • ${item.name} (ID: ${item.id})`);
            });
        }
        if (result.skipped.length > 0) {
            messages.push(`⏭️ Skipped ${result.skipped.length} (already exist):`);
            result.skipped.forEach(item => {
                messages.push(`  • ${item.name}`);
            });
        }
        if (result.errors.length > 0) {
            messages.push(`❌ Errors: ${result.errors.length}`);
            result.errors.forEach(item => {
                messages.push(`  • ${item.name}: ${item.message}`);
            });
        }

        // Show results
        alert(messages.join('\n'));

        // Reload technical messages if any were created
        if (result.created.length > 0) {
            await window.loadMessages('Technical');
            showMessage(`Successfully initialized ${result.created.length} default technical message(s)`);
        } else if (result.errors.length > 0) {
            showMessage(`Initialization completed with ${result.errors.length} error(s)`, true);
        } else {
            showMessage('All default technical messages already exist');
        }
    } catch (error) {
        showMessage('Failed to initialize defaults: ' + error.message, true);
        console.error('Initialize defaults error:', error);
    }
};