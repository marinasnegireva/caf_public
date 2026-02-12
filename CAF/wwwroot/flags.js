/**
 * Flags Page Module
 * Manages conversation flags
 */

import {
    escapeHtml,
    showMessage,
    showError,
    getTimeAgo,
    formatDate,
    API_BASE_URL
} from './js/common-utils.js';

let flags = [];
let editModalInstance = null;
let activeProfileId = null;

// Fetch active profile
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

// DOM Elements
const flagsList = document.getElementById('flagsList');
const newFlagValue = document.getElementById('newFlagValue');

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    loadFlags();
    setupEventListeners();
    initializeModal();
});

function initializeModal() {
    const editModal = document.getElementById('editModal');
    if (editModal) {
        editModalInstance = new bootstrap.Modal(editModal, {
            backdrop: true,
            keyboard: true,
            focus: true
        });
    }
}

function setupEventListeners() {
    if (newFlagValue) {
        newFlagValue.addEventListener('keypress', (e) => {
            if (e.which === 13 || e.keyCode === 13) {
                createFlag();
            }
        });
    }
}

async function loadFlags() {
    try {
        // Ensure we have the active profile loaded
        if (activeProfileId === null) {
            await fetchActiveProfile();
        }

        // Build URL with profileId filter if active profile exists
        let url = `${API_BASE_URL}/flags`;
        if (activeProfileId !== null) {
            url += `?profileId=${activeProfileId}`;
            console.log(`Filtering flags by profileId: ${activeProfileId}`);
        }

        const response = await fetch(url);
        if (!response.ok) throw new Error('Failed to load flags');

        flags = await response.json();
        renderFlags();
    } catch (error) {
        console.error('Error loading flags:', error);
        flagsList.innerHTML = '<p class="error">Failed to load flags</p>';
    }
}

function renderFlags() {
    if (flags.length === 0) {
        flagsList.innerHTML = '<p class="no-data">No flags yet. Create your first flag!</p>';
        return;
    }

    const sortedFlags = [...flags].sort((a, b) => {
        if (a.active !== b.active) {
            return b.active ? 1 : -1;
        }

        const dateA = new Date(a.lastUsedAt || a.createdAt);
        const dateB = new Date(b.lastUsedAt || b.createdAt);
        return dateB - dateA;
    });

    flagsList.innerHTML = sortedFlags.map(flag => {
        const lastUsed = flag.lastUsedAt ? new Date(flag.lastUsedAt) : null;
        const created = new Date(flag.createdAt);
        const displayDate = lastUsed || created;
        const timeAgo = getTimeAgo(displayDate);
        const usageLabel = lastUsed ? 'Used' : 'Created';

        return `
        <div class="flag-card" style="border-left: 4px solid ${flag.active ? '#27ae60' : '#dee2e6'};">
            <div class="flag-header">
                <div class="flag-info">
                    <span class="flag-id">#${flag.id}</span>
                    <span class="flag-value">${escapeHtml(flag.value)}</span>
                    <span style="font-size: 11px; color: #6c757d; margin-left: 8px;" title="${displayDate.toLocaleString()}">
                        ${usageLabel} ${timeAgo}
                    </span>
                </div>
                <div class="flag-actions">
                    <button class="btn btn-small ${flag.active ? 'btn-success' : 'btn-outline'}"
                            onclick="window.flagActions.toggleActive(${flag.id})">
                        ${flag.active ? '? Active' : '? Inactive'}
                    </button>
                    <button class="btn btn-small ${flag.constant ? 'btn-warning' : 'btn-outline'}"
                            onclick="window.flagActions.toggleConstant(${flag.id})">
                        ${flag.constant ? '?? Every Turn' : '? Next Turn'}
                    </button>
                    <button class="btn btn-small btn-info" onclick="window.flagActions.editFlag(${flag.id})">
                        Edit
                    </button>
                    <button class="btn btn-small btn-danger" onclick="window.flagActions.deleteFlag(${flag.id})">
                        Delete
                    </button>
                </div>
            </div>
        </div>
    `;
    }).join('');
}

async function createFlag() {
    const value = newFlagValue.value.trim();

    if (!value) {
        showError('Flag value cannot be empty');
        return;
    }

    try {
        const response = await fetch(`${API_BASE_URL}/flags`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ value })
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'Failed to create flag');
        }

        newFlagValue.value = '';
        await loadFlags();
        showMessage('Flag created successfully');
    } catch (error) {
        console.error('Error creating flag:', error);
        showError('Error: ' + error.message);
    }
}

function editFlag(flagId) {
    const flag = flags.find(f => f.id === flagId);
    if (!flag) return;

    const editFlagId = document.getElementById('editFlagId');
    const editFlagValue = document.getElementById('editFlagValue');

    if (editFlagId) editFlagId.value = flagId;
    if (editFlagValue) editFlagValue.value = flag.value;

    if (editModalInstance) {
        editModalInstance.show();
    }
}

async function saveEdit() {
    const editFlagId = document.getElementById('editFlagId');
    const editFlagValue = document.getElementById('editFlagValue');

    const flagId = parseInt(editFlagId.value);
    const value = editFlagValue.value.trim();

    if (!value) {
        showError('Flag value cannot be empty');
        return;
    }

    try {
        const response = await fetch(`${API_BASE_URL}/flags/${flagId}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ value })
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'Failed to update flag');
        }

        if (editModalInstance) {
            editModalInstance.hide();
        }

        await loadFlags();
        showMessage('Flag updated successfully');
    } catch (error) {
        console.error('Error updating flag:', error);
        showError('Error: ' + error.message);
    }
}

async function toggleActive(flagId) {
    try {
        const response = await fetch(`${API_BASE_URL}/flags/${flagId}/toggle-active`, {
            method: 'POST'
        });

        if (!response.ok) throw new Error('Failed to toggle active');

        await loadFlags();
        showMessage('Flag active state toggled');
    } catch (error) {
        console.error('Error toggling active:', error);
        showError('Error: ' + error.message);
    }
}

async function toggleConstant(flagId) {
    try {
        const response = await fetch(`${API_BASE_URL}/flags/${flagId}/toggle-constant`, {
            method: 'POST'
        });

        if (!response.ok) throw new Error('Failed to toggle constant');

        await loadFlags();
        showMessage('Flag constant state toggled');
    } catch (error) {
        console.error('Error toggling constant:', error);
        showError('Error: ' + error.message);
    }
}

async function deleteFlag(flagId) {
    if (!confirm('Are you sure you want to delete this flag?')) {
        return;
    }

    try {
        const response = await fetch(`${API_BASE_URL}/flags/${flagId}`, {
            method: 'DELETE'
        });

        if (!response.ok) throw new Error('Failed to delete flag');

        await loadFlags();
        showMessage('Flag deleted successfully');
    } catch (error) {
        console.error('Error deleting flag:', error);
        showError('Error: ' + error.message);
    }
}

function closeModal() {
    if (editModalInstance) {
        editModalInstance.hide();
    }
}

// Expose functions to window for HTML onclick handlers
window.flagActions = {
    createFlag,
    editFlag,
    saveEdit,
    toggleActive,
    toggleConstant,
    deleteFlag,
    closeModal
};