/**
 * Message Card Rendering Module
 * Handles rendering of system message cards with expand/collapse preview
 */

import { escapeHtml, formatDate, joinTags, TYPE_COLORS } from './utils.js';

/**
 * Get badge class for message type
 */
function getBadgeClass(type) {
    const typeMap = {
        'Persona': 'persona',
        'Perception': 'technical',
        'Technical': 'technical',
        'ContextFile': 'context'
    };
    return typeMap[type] || type.toLowerCase();
}

/**
 * Render a system message card with collapsible preview
 * @param {Object} message - Message object
 * @param {string} type - Message type (Persona, Perception, Technical, ContextFile)
 * @returns {string} HTML string
 */
export function renderMessageCard(message, type) {
    if (!message || !type) {
        console.error('renderMessageCard: missing message or type', { message, type });
        return '';
    }

    const userProfileCheckbox = renderUserProfileCheckbox(message);
    const preview = getContentPreview(message.content, 150);
    const isArchived = message.isArchived || false;
    const badgeClass = getBadgeClass(type);

    return `
        <div class="message-card ${message.isActive ? 'active' : ''} ${isArchived ? 'archived' : ''}"
             data-message-id="${message.id}">
            ${renderMessageHeader(message, preview, badgeClass, type)}
            ${renderMessagePreview(message, preview)}
            ${renderMessageContent(message)}
            ${renderMessageMetaFields(message, userProfileCheckbox)}
            ${renderMessageActions(message)}
            <div id="diff-view-${message.id}" class="diff-view" style="display: none;"></div>
        </div>
    `;
}

/**
 * Get a short preview of the content
 */
function getContentPreview(content, maxLength = 150) {
    if (!content) return '[Empty]';
    const cleaned = content.trim().replace(/\n+/g, ' ').substring(0, maxLength);
    return cleaned.length < content.length ? cleaned + '...' : cleaned;
}

/**
 * Render collapsible preview section
 */
function renderMessagePreview(message, preview) {
    const cardId = `preview-${message.id}`;
    return `
        <div class="message-preview" id="${cardId}">
            <div class="preview-text">${escapeHtml(preview)}</div>
        </div>
    `;
}

/**
 * Render message header with expand button
 */
function renderMessageHeader(message, preview, badgeClass, type) {
    const isArchived = message.isArchived || false;
    const statusClass = isArchived ? 'status-archived' : message.isActive ? 'status-active' : 'status-inactive';
    const statusText = isArchived ? 'ARCHIVED' : message.isActive ? 'ACTIVE' : 'INACTIVE';

    // Priority badge for context files
    let priorityBadge = '';
    if (type === 'ContextFile' && message.priority !== undefined) {
        const isHot = message.priority <= 1;
        const priorityLabel = isHot ? `?? P${message.priority}` : `?? P${message.priority}`;
        const priorityStyle = isHot
            ? 'background: #ffebee; color: #c62828;'
            : 'background: #e3f2fd; color: #1565c0;';
        priorityBadge = `<span class="status-badge" style="${priorityStyle}">${priorityLabel}</span>`;
    }

    return `
        <div class="message-header" onclick="toggleMessageExpand(event, ${message.id})">
            <div style="flex: 1; display: flex; align-items: center; gap: 12px;">
                <span class="expand-icon" id="expand-icon-${message.id}">
                    <i class="fas fa-chevron-down"></i>
                </span>
                <div style="flex: 1;">
                    <div class="message-title">
                        <span class="message-badge badge-${badgeClass}">${type === 'ContextFile' ? 'Context' : type}</span>
                        <span class="status-badge ${statusClass}">${statusText}</span>
                        ${priorityBadge}
                        ${message.isUserProfile ? '<span class="status-badge" style="background: #c8e6c9; color: #2e7d32;">USER PROFILE</span>' : ''}
                        ${message.version > 1 ? `<span class="status-badge" style="background: #e1f5fe; color: #0277bd;">v${message.version}</span>` : ''}
                    </div>
                    <div style="font-weight: 600; color: #333; margin-top: 4px;">${escapeHtml(message.name)}</div>
                    <div class="message-meta">
                        <span class="meta-item">
                            <i class="fas fa-id-badge"></i> ID: ${message.id}
                        </span>
                        <span class="meta-item">
                            <i class="fas fa-calendar"></i> ${formatDate(message.createdAt)}
                        </span>
                        ${message.modifiedAt ? `
                            <span class="meta-item">
                                <i class="fas fa-edit"></i> ${formatDate(message.modifiedAt)}
                            </span>
                        ` : ''}
                    </div>
                </div>
            </div>
            <div class="message-actions">
                <button class="btn-expand" onclick="event.stopPropagation(); toggleMessageExpand(event, ${message.id})" title="Expand/collapse preview">
                    <i class="fas fa-expand"></i> Expand
                </button>
                <button class="btn-edit" onclick="event.stopPropagation(); window.showEditModal(${message.id}, '${type}')" title="Edit message">
                    <i class="fas fa-edit"></i> Edit
                </button>
                <button class="btn-archive" onclick="event.stopPropagation(); archiveMessage(${message.id})" title="Archive message">
                    <i class="fas fa-archive"></i> Archive
                </button>
            </div>
        </div>
    `;
}

/**
 * Render message content area (hidden by default)
 */
function renderMessageContent(message) {
    return `
        <div class="message-content-wrapper" id="content-${message.id}" style="display: none;">
            <label style="color: var(--text-secondary); font-size: 12px; margin-bottom: 8px; display: block; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px;">
                Full Content:
            </label>
            <textarea class="message-content-edit"
                      data-message-id="${message.id}"
                      data-field="content"
                      onchange="markAsModified(${message.id})"></textarea>
        </div>
    `;
}

/**
 * Render message meta fields (tags, notes, checkboxes)
 */
function renderMessageMetaFields(message, userProfileCheckbox) {
    return `
        <div class="message-meta-fields" id="meta-${message.id}" style="display: none;">
            <div class="form-group-inline">
                <label>Description:</label>
                <input type="text" class="inline-description-edit" placeholder="Optional description"
                       value="${escapeHtml(message.description || '')}"
                       data-message-id="${message.id}" data-field="description"
                       onchange="markAsModified(${message.id})">
            </div>
            <div class="form-group-inline">
                <label>Tags (comma-separated):</label>
                <input type="text" class="inline-tags-edit"
                       value="${joinTags(message.tags || [])}"
                       data-message-id="${message.id}" data-field="tags"
                       onchange="markAsModified(${message.id})">
            </div>
            <div class="form-group-inline">
                <label>Notes:</label>
                <textarea class="inline-notes-edit"
                          data-message-id="${message.id}" data-field="notes"
                          onchange="markAsModified(${message.id})">${escapeHtml(message.notes || '')}</textarea>
            </div>
            <div class="form-group-inline">
                <label>
                    <input type="checkbox"
                           ${message.isActive ? 'checked' : ''}
                           data-message-id="${message.id}"
                           data-field="isActive"
                           onchange="markAsModified(${message.id})">
                    Active
                </label>
            </div>
            ${userProfileCheckbox}
        </div>
    `;
}

/**
 * Render user profile checkbox for context files
 */
function renderUserProfileCheckbox(message) {
    if (message.type !== 'ContextFile') return '';

    return `
        <div class="form-group-inline">
            <label title="Use this context file's Name field as the user name in conversations">
                <input type="checkbox"
                       ${message.isUserProfile ? 'checked' : ''}
                       data-message-id="${message.id}"
                       data-field="isUserProfile"
                       onchange="markAsModified(${message.id})">
                User Profile
            </label>
        </div>
    `;
}

/**
 * Render message action buttons (hidden by default, shown when expanded)
 */
function renderMessageActions(message) {
    return `
        <div class="message-actions" id="actions-${message.id}" style="display: none; flex-direction: column; gap: 8px;">
            <div style="display: flex; gap: 8px; flex-wrap: wrap;">
                <button class="success modified-indicator"
                        id="update-btn-${message.id}"
                        onclick="event.stopPropagation(); updateMessage(${message.id})"
                        disabled>
                    <i class="fas fa-save"></i> Save Changes
                </button>
                <button class="secondary" onclick="event.stopPropagation(); resetChanges(${message.id})">
                    <i class="fas fa-undo"></i> Reset
                </button>
                <button class="secondary" onclick="event.stopPropagation(); toggleDiff(${message.id})">
                    <i class="fas fa-code-branch"></i> Compare
                </button>
                ${!message.isActive ?
            `<button class="success" onclick="event.stopPropagation(); setActive(${message.id})">
                        <i class="fas fa-check"></i> Set Active
                    </button>`
            : ''
        }
            </div>
            <div style="display: flex; gap: 8px; flex-wrap: wrap;">
                <button class="secondary" onclick="event.stopPropagation(); createVersion(${message.id})">
                    <i class="fas fa-star"></i> New Version
                </button>
                <button class="secondary" onclick="event.stopPropagation(); viewVersions(${message.id})">
                    <i class="fas fa-history"></i> Version History
                </button>
                <button class="btn-delete" onclick="event.stopPropagation(); deleteMessage(${message.id})">
                    <i class="fas fa-trash"></i> Delete
                </button>
            </div>
        </div>
    `;
}

/**
 * Toggle message expansion (expose globally)
 */
window.toggleMessageExpand = function (event, messageId) {
    event.stopPropagation();
    const preview = document.getElementById(`preview-${messageId}`);
    const content = document.getElementById(`content-${messageId}`);
    const meta = document.getElementById(`meta-${messageId}`);
    const actions = document.getElementById(`actions-${messageId}`);
    const icon = document.getElementById(`expand-icon-${messageId}`);

    const isExpanded = preview?.classList.contains('expanded');

    if (preview) preview.classList.toggle('expanded');
    if (content) content.style.display = isExpanded ? 'none' : 'block';
    if (meta) meta.style.display = isExpanded ? 'none' : 'block';
    if (actions) actions.style.display = isExpanded ? 'none' : 'flex';
    if (icon) icon.classList.toggle('open');
};

/**
 * Archive message (expose globally)
 */
window.archiveMessage = async function (messageId) {
    try {
        const response = await fetch(`/api/system-messages/${messageId}/archive`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' }
        });

        if (!response.ok) throw new Error('Failed to archive message');

        // Reload messages
        const tabType = window.getCurrentTabType?.();
        if (tabType && window.loadMessages) {
            await window.loadMessages(tabType);
        }

        window.showMessage?.('Message archived successfully', 'success');
    } catch (error) {
        console.error('Archive error:', error);
        window.showMessage?.(`Error: ${error.message}`, 'error');
    }
};

/**
 * Show edit modal (expose globally)
 */
window.showEditModal = function (messageId, type) {
    console.log('Edit modal requested:', { messageId, type });
    if (window.modalManager?.showEditModal) {
        window.modalManager.showEditModal(messageId, type);
    } else {
        console.error('Modal manager not available');
    }
};

export default {
    renderMessageCard
};