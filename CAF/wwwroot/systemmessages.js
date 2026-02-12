const API_BASE = '/api/systemmessages';
let currentMessages = {};
let activeProfileId = null;

// Fetch active profile on page load
async function fetchActiveProfile() {
    try {
        const response = await fetch('/api/profiles/active');
        if (response.ok) {
            const profile = await response.json();
            activeProfileId = profile.id;
            console.log('Active profile:', profile.name, 'ID:', activeProfileId);
        } else {
            console.log('No active profile found');
            activeProfileId = null;
        }
    } catch (error) {
        console.error('Error fetching active profile:', error);
        activeProfileId = null;
    }
}

function showMessage(text, isError = false) {
    const messageEl = document.getElementById('message');
    messageEl.textContent = text;
    messageEl.className = `message-message ${isError ? 'error' : 'success'} show`;
    setTimeout(() => messageEl.classList.remove('show'), 5000);
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

async function switchTab(tab) {
    document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
    document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));

    event.target.closest('.tab').classList.add('active');
    document.getElementById(`${tab}-tab`).classList.add('active');

    if (tab !== 'preview') {
        await loadMessages(tab);
    }
}

async function loadMessages(type) {
    const typeMap = {
        'persona': 'Persona',
        'perception': 'Perception',
        'technical': 'Technical',
        'context': 'ContextFile'
    };

    const container = document.getElementById(`${type}-list`);
    container.innerHTML = '<div class="loading">Loading...</div>';

    try {
        // Ensure we have the active profile loaded
        if (activeProfileId === null) {
            await fetchActiveProfile();
        }

        // Build URL with profileId filter if active profile exists
        let url = `${API_BASE}?type=${typeMap[type]}&includeArchived=false`;
        if (activeProfileId !== null) {
            url += `&profileId=${activeProfileId}`;
        }

        const response = await fetch(url);
        if (!response.ok) throw new Error('Failed to load messages');

        const messages = await response.json();
        currentMessages[type] = messages;

        if (messages.length === 0) {
            container.innerHTML = '<div class="empty-state">No messages found. Create your first one!</div>';
            return;
        }

        container.innerHTML = messages.map(m => renderMessageCard(m, type)).join('');
    } catch (error) {
        container.innerHTML = `<div class="empty-state">Error: ${error.message}</div>`;
    }
}

function renderMessageCard(message, type) {
    const typeColors = {
        'Persona': 'persona',
        'Perception': 'perception',
        'Technical': 'technical',
        'ContextFile': 'context'
    };

    const attachments = message.type === 'ContextFile' && (message.attachedToPersonas?.length > 0 || message.attachedToPerceptions?.length > 0) ? `
        <div class="context-attachments" style="background: var(--bg-primary); padding: 16px; border-radius: 8px; margin-bottom: 18px; border: 1px solid var(--border-color);">
            <h4 style="color: var(--text-secondary); font-size: 12px; margin-bottom: 10px; text-transform: uppercase; letter-spacing: 0.5px;">Attached To:</h4>
            <div class="attachment-list" style="display: flex; flex-wrap: wrap; gap: 6px;">
                ${(message.attachedToPersonas || []).map(id => `<span class="badge badge-persona">Persona #${id}</span>`).join('')}
                ${(message.attachedToPerceptions || []).map(id => `<span class="badge badge-perception">Perception #${id}</span>`).join('')}
                ${(message.attachedToPersonas?.length === 0 && message.attachedToPerceptions?.length === 0) ? '<span style="color: var(--text-muted); font-style: italic;">Not attached to any system messages</span>' : ''}
            </div>
        </div>
    ` : '';

    return `
        <div class="message-card ${message.isActive ? 'active' : ''}" data-message-id="${message.id}">
            <div class="message-header">
                <div style="flex: 1;">
                    <div class="message-title">
                        <span class="badge badge-${typeColors[message.type]}">${message.type}</span>
                        ${message.isActive ? '<span class="badge badge-active">ACTIVE</span>' : ''}
                        ${message.isUserProfile ? '<span class="badge badge-success">USER PROFILE</span>' : ''}
                        ${message.version > 1 ? `<span class="badge badge-version">v${message.version}</span>` : ''}
                        <input type="text" class="inline-name-edit" value="${escapeHtml(message.name)}"
                               data-message-id="${message.id}" data-field="name"
                               onchange="markAsModified(${message.id})">
                    </div>
                    <div class="message-meta">
                        ID: ${message.id} | Created: ${new Date(message.createdAt).toLocaleDateString()}
                        ${message.modifiedAt ? ` | Modified: ${new Date(message.modifiedAt).toLocaleDateString()}` : ''}
                    </div>
                    <div class="message-meta-edit">
                        <input type="text" class="inline-description-edit" placeholder="Description (optional)"
                               value="${escapeHtml(message.description || '')}"
                               data-message-id="${message.id}" data-field="description"
                               onchange="markAsModified(${message.id})">
                    </div>
                </div>
            </div>

            <div class="message-content-wrapper">
                <label style="color: var(--text-secondary); font-size: 12px; margin-bottom: 8px; display: block; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px;">Content:</label>
                <textarea class="message-content-edit"
                          data-message-id="${message.id}"
                          data-field="content"
                          data-original="${escapeHtml(message.content)}"
                          onchange="markAsModified(${message.id})">${escapeHtml(message.content)}</textarea>
            </div>

            <div class="message-meta-fields">
                <div class="form-group-inline">
                    <label>Tags (comma-separated):</label>
                    <input type="text" class="inline-tags-edit"
                           value="${(message.tags || []).join(', ')}"
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
                ${message.type === 'ContextFile' ? `
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
                ` : ''}
            </div>

            ${attachments}

            <div class="message-actions">
                <button class="success modified-indicator"
                        id="update-btn-${message.id}"
                        onclick="updateMessage(${message.id})"
                        style="display: none;">
                    Save Changes
                </button>
                <button class="secondary" onclick="resetChanges(${message.id})">
                    Reset
                </button>
                <button class="secondary"
                        onclick="toggleDiff(${message.id})">
                    Compare
                </button>
                ${!message.isActive ? `<button class="success" onclick="setActive(${message.id})">Set Active</button>` : ''}
                <button class="warning" onclick="createVersion(${message.id})">New Version</button>
                <button class="secondary" onclick="viewVersions(${message.id})">Version History</button>
                ${message.type === 'ContextFile' ? `<button class="secondary" onclick="manageAttachments(${message.id})">Manage Attachments</button>` : ''}
                <button class="danger" onclick="deleteMessage(${message.id})">Delete</button>
            </div>

            <div id="diff-view-${message.id}" class="diff-view" style="display: none;"></div>
        </div>
    `;
}

function markAsModified(messageId) {
    const updateBtn = document.getElementById(`update-btn-${messageId}`);
    const diffBtn = document.getElementById(`diff-btn-${messageId}`);
    if (updateBtn) updateBtn.style.display = 'inline-block';
    if (diffBtn) diffBtn.style.display = 'inline-block';
}

function resetChanges(messageId) {
    // Find the message in currentMessages
    const currentTab = document.querySelector('.tab.active').textContent.toLowerCase();
    const tabType = currentTab.includes('persona') ? 'persona' :
        currentTab.includes('perception') ? 'perception' :
            currentTab.includes('technical') ? 'technical' : 'context';

    const message = currentMessages[tabType]?.find(m => m.id === messageId);
    if (!message) return;

    // Reset all fields
    const card = document.querySelector(`[data-message-id="${messageId}"]`);
    if (!card) return;

    card.querySelector('[data-field="name"]').value = message.name;
    card.querySelector('[data-field="description"]').value = message.description || '';
    card.querySelector('[data-field="content"]').value = message.content;
    card.querySelector('[data-field="tags"]').value = (message.tags || []).join(', ');
    card.querySelector('[data-field="notes"]').value = message.notes || '';
    card.querySelector('[data-field="isActive"]').checked = message.isActive;

    // Reset isUserProfile if it exists
    const isUserProfileCheckbox = card.querySelector('[data-field="isUserProfile"]');
    if (isUserProfileCheckbox) {
        isUserProfileCheckbox.checked = message.isUserProfile || false;
    }

    // Hide update/diff buttons
    document.getElementById(`update-btn-${messageId}`).style.display = 'none';
    document.getElementById(`diff-btn-${messageId}`).style.display = 'none';

    // Hide diff view
    const diffView = document.getElementById(`diff-view-${messageId}`);
    if (diffView) diffView.style.display = 'none';

    showMessage('Changes reset');
}

function showDiff(messageId) {
    const currentTab = document.querySelector('.tab.active').textContent.toLowerCase();
    const tabType = currentTab.includes('persona') ? 'persona' :
        currentTab.includes('perception') ? 'perception' :
            currentTab.includes('technical') ? 'technical' : 'context';

    const message = currentMessages[tabType]?.find(m => m.id === messageId);
    if (!message) return;

    const card = document.querySelector(`[data-message-id="${messageId}"]`);
    if (!card) return;

    const originalContent = message.content;
    const newContent = card.querySelector('[data-field="content"]').value;

    const diffView = document.getElementById(`diff-view-${messageId}`);
    if (!diffView) return;

    if (originalContent === newContent) {
        diffView.innerHTML = `
            <div class="diff-header" style="text-align: center; padding: 24px; color: var(--text-secondary);">
                <div style="font-size: 48px; margin-bottom: 12px;">?</div>
                <div>No changes detected - content is identical to the original</div>
            </div>
        `;
    } else {
        const diffHtml = generateLineDiff(originalContent, newContent);
        diffView.innerHTML = `
            <div class="diff-header" style="margin-bottom: 16px; padding-bottom: 12px; border-bottom: 1px solid var(--border-color);">
                Content Comparison: Original vs Modified
            </div>
            ${diffHtml}
        `;
    }

    diffView.style.display = 'block';
}

function generateLineDiff(original, modified) {
    const originalLines = original.split('\n');
    const modifiedLines = modified.split('\n');

    // Simple LCS-based diff
    const diff = computeDiff(originalLines, modifiedLines);

    let html = '<div class="diff-container" style="display: flex; gap: 1px; background: var(--border-color); border: 1px solid var(--border-color); border-radius: 8px; overflow: hidden; max-height: 800px; overflow-y: auto;">';

    // Left side - Original
    html += '<div class="diff-side" style="flex: 1; background: var(--bg-primary); min-width: 0;">';
    html += '<div style="background: var(--bg-tertiary); padding: 12px 16px; font-weight: 600; color: var(--text-primary); border-bottom: 1px solid var(--border-color); text-align: left; position: sticky; top: 0; z-index: 1;">ORIGINAL</div>';
    html += '<div style="font-family: \'JetBrains Mono\', \'Fira Code\', \'Courier New\', monospace; font-size: 13px; line-height: 1.6;">';

    diff.forEach((item, index) => {
        if (item.type === 'removed' || item.type === 'unchanged') {
            const bgColor = item.type === 'removed' ? 'rgba(248, 113, 113, 0.15)' : 'transparent';
            const textColor = item.type === 'removed' ? 'var(--accent-red)' : 'var(--text-primary)';
            const lineNum = item.originalLine !== undefined ? item.originalLine + 1 : '';
            html += `<div style="display: flex; background: ${bgColor}; padding: 2px 0; min-height: 22px;">`;
            html += `<div style="min-width: 50px; padding: 2px 12px; text-align: right; color: var(--text-muted); user-select: none; border-right: 1px solid var(--border-color); flex-shrink: 0;">${lineNum}</div>`;
            html += `<div style="flex: 1; padding: 2px 12px; color: ${textColor}; white-space: pre-wrap; word-wrap: break-word; text-align: left; overflow-wrap: anywhere;">${escapeHtml(item.value)}</div>`;
            html += `</div>`;
        } else if (item.type === 'added') {
            // Show empty line for added content
            html += `<div style="display: flex; background: rgba(113, 128, 150, 0.05); padding: 2px 0; min-height: 22px;">`;
            html += `<div style="min-width: 50px; padding: 2px 12px; text-align: right; color: var(--text-muted); user-select: none; border-right: 1px solid var(--border-color); flex-shrink: 0;"></div>`;
            html += `<div style="flex: 1; padding: 2px 12px; color: var(--text-muted); opacity: 0.3; text-align: left;">·</div>`;
            html += `</div>`;
        }
    });

    html += '</div></div>';

    // Right side - Modified
    html += '<div class="diff-side" style="flex: 1; background: var(--bg-primary); min-width: 0;">';
    html += '<div style="background: var(--bg-tertiary); padding: 12px 16px; font-weight: 600; color: var(--text-primary); border-bottom: 1px solid var(--border-color); text-align: left; position: sticky; top: 0; z-index: 1;">MODIFIED</div>';
    html += '<div style="font-family: \'JetBrains Mono\', \'Fira Code\', \'Courier New\', monospace; font-size: 13px; line-height: 1.6;">';

    diff.forEach((item, index) => {
        if (item.type === 'added' || item.type === 'unchanged') {
            const bgColor = item.type === 'added' ? 'rgba(74, 222, 128, 0.15)' : 'transparent';
            const textColor = item.type === 'added' ? 'var(--accent-green)' : 'var(--text-primary)';
            const lineNum = item.modifiedLine !== undefined ? item.modifiedLine + 1 : '';
            html += `<div style="display: flex; background: ${bgColor}; padding: 2px 0; min-height: 22px;">`;
            html += `<div style="min-width: 50px; padding: 2px 12px; text-align: right; color: var(--text-muted); user-select: none; border-right: 1px solid var(--border-color); flex-shrink: 0;">${lineNum}</div>`;
            html += `<div style="flex: 1; padding: 2px 12px; color: ${textColor}; white-space: pre-wrap; word-wrap: break-word; text-align: left; overflow-wrap: anywhere;">${escapeHtml(item.value)}</div>`;
            html += `</div>`;
        } else if (item.type === 'removed') {
            // Show empty line for removed content
            html += `<div style="display: flex; background: rgba(113, 128, 150, 0.05); padding: 2px 0; min-height: 22px;">`;
            html += `<div style="min-width: 50px; padding: 2px 12px; text-align: right; color: var(--text-muted); user-select: none; border-right: 1px solid var(--border-color); flex-shrink: 0;"></div>`;
            html += `<div style="flex: 1; padding: 2px 12px; color: var(--text-muted); opacity: 0.3; text-align: left;">·</div>`;
            html += `</div>`;
        }
    });

    html += '</div></div>';
    html += '</div>';

    return html;
}

function computeDiff(original, modified) {
    const diff = [];
    let i = 0, j = 0;

    // Simple diff algorithm using longest common subsequence
    const lcs = longestCommonSubsequence(original, modified);
    const lcsSet = new Set(lcs.map((item, idx) => `${item.origIdx}-${item.modIdx}`));

    while (i < original.length || j < modified.length) {
        if (i < original.length && j < modified.length &&
            lcsSet.has(`${i}-${j}`) && original[i] === modified[j]) {
            // Unchanged line
            diff.push({
                type: 'unchanged',
                value: original[i],
                originalLine: i,
                modifiedLine: j
            });
            i++;
            j++;
        } else if (i < original.length &&
            (j >= modified.length || !lcsSet.has(`${i}-${j}`))) {
            // Line removed from original
            diff.push({
                type: 'removed',
                value: original[i],
                originalLine: i
            });
            i++;
        } else if (j < modified.length) {
            // Line added in modified
            diff.push({
                type: 'added',
                value: modified[j],
                modifiedLine: j
            });
            j++;
        }
    }

    return diff;
}

function longestCommonSubsequence(arr1, arr2) {
    const m = arr1.length;
    const n = arr2.length;
    const dp = Array(m + 1).fill(null).map(() => Array(n + 1).fill(0));

    // Build LCS length table
    for (let i = 1; i <= m; i++) {
        for (let j = 1; j <= n; j++) {
            if (arr1[i - 1] === arr2[j - 1]) {
                dp[i][j] = dp[i - 1][j - 1] + 1;
            } else {
                dp[i][j] = Math.max(dp[i - 1][j], dp[i][j - 1]);
            }
        }
    }

    // Backtrack to find LCS
    const lcs = [];
    let i = m, j = n;
    while (i > 0 && j > 0) {
        if (arr1[i - 1] === arr2[j - 1]) {
            lcs.unshift({ origIdx: i - 1, modIdx: j - 1 });
            i--;
            j--;
        } else if (dp[i - 1][j] > dp[i][j - 1]) {
            i--;
        } else {
            j--;
        }
    }

    return lcs;
}

function toggleDiff(messageId) {
    const diffView = document.getElementById(`diff-view-${messageId}`);
    if (!diffView) return;

    if (diffView.style.display === 'none' || !diffView.style.display) {
        showDiff(messageId);
    } else {
        diffView.style.display = 'none';
    }
}

async function updateMessage(messageId) {
    const card = document.querySelector(`[data-message-id="${messageId}"]`);
    if (!card) return;

    const currentTab = document.querySelector('.tab.active').textContent.toLowerCase();
    const tabType = currentTab.includes('persona') ? 'persona' :
        currentTab.includes('perception') ? 'perception' :
            currentTab.includes('technical') ? 'technical' : 'context';

    const originalMessage = currentMessages[tabType]?.find(m => m.id === messageId);
    if (!originalMessage) return;

    const updatedMessage = {
        name: card.querySelector('[data-field="name"]').value.trim(),
        content: card.querySelector('[data-field="content"]').value.trim(),
        type: originalMessage.type,
        description: card.querySelector('[data-field="description"]').value.trim(),
        tags: card.querySelector('[data-field="tags"]').value.split(',').map(t => t.trim()).filter(t => t),
        notes: card.querySelector('[data-field="notes"]').value.trim(),
        isActive: card.querySelector('[data-field="isActive"]').checked,
        profileId: originalMessage.profileId, // Preserve ProfileId
        attachedToPersonas: originalMessage.attachedToPersonas || [],
        attachedToPerceptions: originalMessage.attachedToPerceptions || []
    };

    // Add isUserProfile if this is a context file
    const isUserProfileCheckbox = card.querySelector('[data-field="isUserProfile"]');
    if (isUserProfileCheckbox) {
        updatedMessage.isUserProfile = isUserProfileCheckbox.checked;
    }

    if (!updatedMessage.name || !updatedMessage.content) {
        showMessage('Name and Content are required', true);
        return;
    }

    try {
        const response = await fetch(`${API_BASE}/${messageId}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(updatedMessage)
        });

        if (!response.ok) throw new Error('Failed to update message');

        showMessage('Message updated successfully');

        // Hide update/diff buttons
        document.getElementById(`update-btn-${messageId}`).style.display = 'none';
        document.getElementById(`diff-btn-${messageId}`).style.display = 'none';

        // Hide diff view
        const diffView = document.getElementById(`diff-view-${messageId}`);
        if (diffView) diffView.style.display = 'none';

        // Reload to get fresh data
        await loadMessages(tabType);
    } catch (error) {
        showMessage('Failed to update: ' + error.message, true);
    }
}

function showCreateModal(type) {
    document.getElementById('modalTitle').textContent = `Create New ${type}`;
    document.getElementById('editId').value = '';
    document.getElementById('editType').value = type;
    document.getElementById('editName').value = '';
    document.getElementById('editContent').value = '';
    document.getElementById('editDescription').value = '';
    document.getElementById('editTags').value = '';
    document.getElementById('editNotes').value = '';
    document.getElementById('editIsActive').checked = false;

    // Show/hide user profile checkbox based on type
    const userProfileGroup = document.getElementById('userProfileGroup');
    if (type === 'ContextFile') {
        userProfileGroup.style.display = 'block';
        document.getElementById('editIsUserProfile').checked = false;
    } else {
        userProfileGroup.style.display = 'none';
    }

    document.getElementById('editModal').classList.add('show');
}

function closeModal() {
    document.getElementById('editModal').classList.remove('show');
}

async function saveMessage() {
    const id = document.getElementById('editId').value;
    const type = document.getElementById('editType').value;

    const message = {
        name: document.getElementById('editName').value.trim(),
        content: document.getElementById('editContent').value.trim(),
        type: type,
        description: document.getElementById('editDescription').value.trim(),
        tags: document.getElementById('editTags').value.split(',').map(t => t.trim()).filter(t => t),
        notes: document.getElementById('editNotes').value.trim(),
        isActive: document.getElementById('editIsActive').checked,
        attachedToPersonas: [],
        attachedToPerceptions: []
    };

    // Add isUserProfile for context files
    if (type === 'ContextFile') {
        message.isUserProfile = document.getElementById('editIsUserProfile').checked;
    }

    if (!message.name || !message.content) {
        showMessage('Name and Content are required', true);
        return;
    }

    try {
        // Only create, not update (update is done inline now)
        const response = await fetch(API_BASE, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(message)
        });

        if (!response.ok) throw new Error('Failed to save message');

        showMessage('Message created successfully');
        closeModal();

        const tabType = type.toLowerCase().replace('file', '');
        await loadMessages(tabType === 'contextfile' ? 'context' : tabType);
    } catch (error) {
        showMessage('Failed to save: ' + error.message, true);
    }
}

async function setActive(messageId) {
    try {
        const response = await fetch(`${API_BASE}/${messageId}/activate`, {
            method: 'POST'
        });

        if (!response.ok) throw new Error('Failed to set active');

        showMessage('Message set as active');

        const currentTab = document.querySelector('.tab.active').textContent.toLowerCase();
        const tabType = currentTab.includes('persona') ? 'persona' :
            currentTab.includes('perception') ? 'perception' :
                currentTab.includes('technical') ? 'technical' : 'context';

        await loadMessages(tabType);
    } catch (error) {
        showMessage('Failed to set active: ' + error.message, true);
    }
}

async function deleteMessage(messageId) {
    if (!confirm('Are you sure you want to delete this message?')) return;

    try {
        const response = await fetch(`${API_BASE}/${messageId}`, {
            method: 'DELETE'
        });

        if (!response.ok) throw new Error('Failed to delete message');

        showMessage('Message deleted successfully');

        const currentTab = document.querySelector('.tab.active').textContent.toLowerCase();
        const tabType = currentTab.includes('persona') ? 'persona' :
            currentTab.includes('perception') ? 'perception' :
                currentTab.includes('technical') ? 'technical' : 'context';

        await loadMessages(tabType);
    } catch (error) {
        showMessage('Failed to delete: ' + error.message, true);
    }
}

async function createVersion(messageId) {
    try {
        const response = await fetch(`${API_BASE}/${messageId}/version`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({})
        });

        if (!response.ok) throw new Error('Failed to create version');

        showMessage('New version created successfully');

        const currentTab = document.querySelector('.tab.active').textContent.toLowerCase();
        const tabType = currentTab.includes('persona') ? 'persona' :
            currentTab.includes('perception') ? 'perception' :
                currentTab.includes('technical') ? 'technical' : 'context';

        await loadMessages(tabType);
    } catch (error) {
        showMessage('Failed to create version: ' + error.message, true);
    }
}

function viewVersions(messageId) {
    showMessage('Version history feature coming soon!');
}

function manageAttachments(messageId) {
    showMessage('Attachment management coming soon!');
}

async function loadPreview() {
    const previewContent = document.getElementById('preview-content');
    previewContent.innerHTML = '<div class="loading">Loading preview...</div>';

    try {
        const response = await fetch(`${API_BASE}/preview`);
        if (!response.ok) throw new Error('Failed to load preview');

        const preview = await response.json();

        previewContent.innerHTML = `
            <div class="message-content" style="max-height: none;">
                <h3 style="margin-top: 0; color: #4a9eff;">Complete System Message</h3>
                <pre style="white-space: pre-wrap; word-wrap: break-word; line-height: 1.6;">${escapeHtml(preview.completeMessage || 'No active messages configured')}</pre>

                <h4 style="margin-top: 30px; color: #888;">Components:</h4>
                <ul style="color: #888;">
                    <li>Active Persona: ${preview.hasPersona ? '?' : '?'}</li>
                    <li>Active Perceptions: ${preview.perceptionCount || 0}</li>
                    <li>Active Technical: ${preview.hasTechnical ? '?' : '?'}</li>
                    <li>Context Files: ${preview.contextCount || 0}</li>
                </ul>
            </div>
        `;
    } catch (error) {
        previewContent.innerHTML = `<div class="empty-state">Error: ${error.message}</div>`;
    }
}

// Auto-adjust textarea height based on content
function autoResizeTextarea(textarea) {
    textarea.style.height = 'auto';
    // Set a minimum height and calculate based on content
    const minHeight = 120;
    const contentHeight = textarea.scrollHeight;
    textarea.style.height = Math.max(minHeight, contentHeight) + 'px';
}

// Apply auto-resize to all textareas
function setupAutoResize() {
    document.querySelectorAll('.message-content-edit, .inline-notes-edit').forEach(textarea => {
        autoResizeTextarea(textarea);
        textarea.addEventListener('input', () => autoResizeTextarea(textarea));
    });
}

// Initialize on page load
document.addEventListener('DOMContentLoaded', async () => {
    console.log('System Messages Manager initialized');

    // Load persona messages by default
    await loadMessages('persona');

    // Setup textarea auto-resize
    setupAutoResize();

    // Re-apply auto-resize when messages are loaded
    const observer = new MutationObserver(() => {
        setupAutoResize();
    });

    observer.observe(document.getElementById('persona-list'), { childList: true, subtree: true });
    observer.observe(document.getElementById('perception-list'), { childList: true, subtree: true });
    observer.observe(document.getElementById('technical-list'), { childList: true, subtree: true });
    observer.observe(document.getElementById('context-list'), { childList: true, subtree: true });
});

async function manageAttachments(messageId) {
    const currentTab = document.querySelector('.tab.active').textContent.toLowerCase();
    const tabType = currentTab.includes('context') ? 'context' : null;

    if (!tabType) {
        showMessage('This feature is only available for context files', true);
        return;
    }

    const message = currentMessages[tabType]?.find(m => m.id === messageId);
    if (!message) return;

    // Fetch all personas and perceptions
    try {
        const [personasResponse, perceptionsResponse] = await Promise.all([
            fetch(`${API_BASE}?type=Persona&includeArchived=false`),
            fetch(`${API_BASE}?type=Perception&includeArchived=false`)
        ]);

        if (!personasResponse.ok || !perceptionsResponse.ok) {
            throw new Error('Failed to load system messages');
        }

        const personas = await personasResponse.json();
        const perceptions = await perceptionsResponse.json();

        // Create attachment modal
        const attachedToPersonas = message.attachedToPersonas || [];
        const attachedToPerceptions = message.attachedToPerceptions || [];

        const modalHtml = `
            <div id="attachmentModal" class="modal show">
                <div class="modal-content">
                    <div class="modal-header">
                        <h3>Manage Attachments: ${escapeHtml(message.name)}</h3>
                        <button class="modal-close" onclick="closeAttachmentModal()">&times;</button>
                    </div>
                    <div class="modal-body">
                        <p style="color: var(--text-secondary); margin-bottom: 20px;">
                            Select which personas and perceptions this context file should be attached to.
                            Attached contexts will be included when these system messages are active.
                        </p>

                        <div class="form-group">
                            <label>Attach to Personas:</label>
                            <div style="max-height: 200px; overflow-y: auto; background: var(--bg-primary); padding: 12px; border-radius: 8px; border: 1px solid var(--border-color);">
                                ${personas.length > 0 ? personas.map(p => `
                                    <div style="margin-bottom: 8px;">
                                        <label style="display: flex; align-items: center; cursor: pointer; font-weight: normal;">
                                            <input type="checkbox"
                                                   value="${p.id}"
                                                   class="attachment-persona-checkbox"
                                                   ${attachedToPersonas.includes(p.id) ? 'checked' : ''}
                                                   style="margin-right: 8px; width: 18px; height: 18px;">
                                            <span class="badge badge-persona" style="margin-right: 8px;">${p.isActive ? 'ACTIVE' : ''}</span>
                                            ${escapeHtml(p.name)}
                                        </label>
                                    </div>
                                `).join('') : '<div style="color: var(--text-muted); font-style: italic;">No personas available</div>'}
                            </div>
                        </div>

                        <div class="form-group">
                            <label>Attach to Perceptions:</label>
                            <div style="max-height: 200px; overflow-y: auto; background: var(--bg-primary); padding: 12px; border-radius: 8px; border: 1px solid var(--border-color);">
                                ${perceptions.length > 0 ? perceptions.map(p => `
                                    <div style="margin-bottom: 8px;">
                                        <label style="display: flex; align-items: center; cursor: pointer; font-weight: normal;">
                                            <input type="checkbox"
                                                   value="${p.id}"
                                                   class="attachment-perception-checkbox"
                                                   ${attachedToPerceptions.includes(p.id) ? 'checked' : ''}
                                                   style="margin-right: 8px; width: 18px; height: 18px;">
                                            <span class="badge badge-perception" style="margin-right: 8px;">${p.isActive ? 'ACTIVE' : ''}</span>
                                            ${escapeHtml(p.name)}
                                        </label>
                                    </div>
                                `).join('') : '<div style="color: var(--text-muted); font-style: italic;">No perceptions available</div>'}
                            </div>
                        </div>
                    </div>
                    <div class="modal-footer">
                        <button class="secondary" onclick="closeAttachmentModal()">Cancel</button>
                        <button class="success" onclick="saveAttachments(${messageId})">Save Attachments</button>
                    </div>
                </div>
            </div>
        `;

        // Add modal to body
        const tempDiv = document.createElement('div');
        tempDiv.innerHTML = modalHtml;
        document.body.appendChild(tempDiv.firstElementChild);
    } catch (error) {
        showMessage('Failed to load attachments: ' + error.message, true);
    }
}

function closeAttachmentModal() {
    const modal = document.getElementById('attachmentModal');
    if (modal) {
        modal.remove();
    }
}

async function saveAttachments(messageId) {
    const selectedPersonas = Array.from(document.querySelectorAll('.attachment-persona-checkbox:checked')).map(cb => parseInt(cb.value));
    const selectedPerceptions = Array.from(document.querySelectorAll('.attachment-perception-checkbox:checked')).map(cb => parseInt(cb.value));

    try {
        const response = await fetch(`${API_BASE}/${messageId}/attachments`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                attachedToPersonas: selectedPersonas,
                attachedToPerceptions: selectedPerceptions
            })
        });

        if (!response.ok) throw new Error('Failed to save attachments');

        showMessage('Attachments saved successfully');
        closeAttachmentModal();

        // Reload context files
        await loadMessages('context');
    } catch (error) {
        showMessage('Failed to save attachments: ' + error.message, true);
    }
}

async function viewVersions(messageId) {
    try {
        const response = await fetch(`${API_BASE}/${messageId}/versions`);
        if (!response.ok) throw new Error('Failed to load version history');

        const versions = await response.json();

        if (versions.length === 0) {
            showMessage('No version history available for this message');
            return;
        }

        const modalHtml = `
            <div id="versionHistoryModal" class="modal show">
                <div class="modal-content" style="max-width: 1200px;">
                    <div class="modal-header">
                        <h3>Version History</h3>
                        <button class="modal-close" onclick="closeVersionHistoryModal()">&times;</button>
                    </div>
                    <div class="modal-body">
                        <p style="color: var(--text-secondary); margin-bottom: 20px;">
                            View all versions of this system message. Click "Set as Active" to make a version active.
                        </p>

                        <div style="max-height: 500px; overflow-y: auto;">
                            ${versions.map(version => `
                                <div class="message-card ${version.isActive ? 'active' : ''}" style="margin-bottom: 16px;">
                                    <div class="message-header">
                                        <div style="flex: 1;">
                                            <div class="message-title">
                                                <span class="badge badge-version">v${version.version}</span>
                                                ${version.isActive ? '<span class="badge badge-active">ACTIVE</span>' : ''}
                                                ${escapeHtml(version.name)}
                                            </div>
                                            <div class="message-meta">
                                                Created: ${new Date(version.createdAt).toLocaleString()}
                                                ${version.modifiedBy ? ` | By: ${escapeHtml(version.modifiedBy)}` : ''}
                                            </div>
                                            ${version.description ? `<div class="message-meta" style="margin-top: 4px;">${escapeHtml(version.description)}</div>` : ''}
                                        </div>
                                    </div>
                                    <div class="message-content" style="max-height: 150px; overflow-y: auto; margin-top: 12px;">
                                        ${escapeHtml(version.content)}
                                    </div>
                                    ${!version.isActive ? `
                                        <div style="margin-top: 12px;">
                                            <button class="success" onclick="setActive(${version.id}); closeVersionHistoryModal();">
                                                Set as Active
                                            </button>
                                        </div>
                                    ` : ''}
                                </div>
                            `).join('')}
                        </div>
                    </div>
                    <div class="modal-footer">
                        <button class="secondary" onclick="closeVersionHistoryModal()">Close</button>
                    </div>
                </div>
            </div>
        `;

        const tempDiv = document.createElement('div');
        tempDiv.innerHTML = modalHtml;
        document.body.appendChild(tempDiv.firstElementChild);
    } catch (error) {
        showMessage('Failed to load version history: ' + error.message, true);
    }
}

function closeVersionHistoryModal() {
    const modal = document.getElementById('versionHistoryModal');
    if (modal) {
        modal.remove();
    }
}

/**
 * Initialize default technical messages from server constants
 */
async function initializeDefaults() {
    if (!confirm('This will create default technical messages if they don\'t already exist.\n\nExisting messages will not be affected.\n\nContinue?')) {
        return;
    }

    try {
        const response = await fetch(`${API_BASE}/initialize-defaults`, {
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
            await loadMessages('technical');
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
}