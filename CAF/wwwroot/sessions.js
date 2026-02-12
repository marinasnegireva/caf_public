/**
 * Sessions Page Module
 * Manages conversation sessions
 */

import {
    escapeHtml,
    formatDateTime,
    showMessage,
    showError,
    API_BASE_URL
} from './js/common-utils.js';

let sessions = [];
let editingSessionId = null;
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
const sessionsList = document.getElementById('sessionsList');
const activeSessionCard = document.getElementById('activeSessionCard');
const createSessionBtn = document.getElementById('createSessionBtn');
const sessionModal = document.getElementById('sessionModal');
const modalTitle = document.getElementById('modalTitle');
const sessionForm = document.getElementById('sessionForm');
const sessionNameInput = document.getElementById('sessionName');
const duplicateTurnsCheckbox = document.getElementById('duplicateTurnsCheckbox');
const duplicateTurnsOptions = document.getElementById('duplicateTurnsOptions');
const sourceSessionSelect = document.getElementById('sourceSessionSelect');
const turnCountInput = document.getElementById('turnCountInput');
const closeBtn = document.querySelector('.close');
const cancelBtn = document.querySelector('.cancel-btn');

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    loadSessions();
    setupEventListeners();
});

function setupEventListeners() {
    createSessionBtn.addEventListener('click', openCreateModal);
    closeBtn.addEventListener('click', closeModal);
    cancelBtn.addEventListener('click', closeModal);
    sessionForm.addEventListener('submit', handleSubmit);
    
    // Toggle duplicate turns options
    duplicateTurnsCheckbox.addEventListener('change', (e) => {
        duplicateTurnsOptions.style.display = e.target.checked ? 'block' : 'none';
        if (e.target.checked) {
            populateSourceSessionsDropdown();
        }
    });

    window.addEventListener('click', (e) => {
        if (e.target === sessionModal) {
            closeModal();
        }
    });
}

async function loadSessions() {
    try {
        // Ensure we have the active profile loaded
        if (activeProfileId === null) {
            await fetchActiveProfile();
        }

        // Build URL with profileId filter if active profile exists
        let url = `${API_BASE_URL}/sessions`;
        if (activeProfileId !== null) {
            url += `?profileId=${activeProfileId}`;
            console.log(`Filtering sessions by profileId: ${activeProfileId}`);
        }

        const response = await fetch(url);
        if (!response.ok) throw new Error('Failed to load sessions');

        sessions = await response.json();
        renderSessions();
        renderActiveSession();
    } catch (error) {
        console.error('Error loading sessions:', error);
        sessionsList.innerHTML = '<p class="error">Failed to load sessions</p>';
    }
}

function renderSessions() {
    if (sessions.length === 0) {
        sessionsList.innerHTML = '<p class="no-data">No sessions yet. Create your first session!</p>';
        return;
    }

    sessionsList.innerHTML = sessions.map(session => `
        <div class="session-card ${session.isActive ? 'active' : ''}">
            <div class="session-header">
                <div>
                    <span class="session-number">#${session.number}</span>
                    <span class="session-name">${escapeHtml(session.name)}</span>
                    ${session.isActive ? '<span class="badge badge-active">Active</span>' : ''}
                </div>
                <div class="session-actions">
                    ${!session.isActive ? `<button class="btn btn-small" onclick="window.sessionActions.activateSession(${session.id})">Activate</button>` : ''}
                    <button class="btn btn-small" onclick="window.sessionActions.editSession(${session.id})">Edit</button>
                    <button class="btn btn-small btn-danger" onclick="window.sessionActions.deleteSession(${session.id})">Delete</button>
                </div>
            </div>
            <div class="session-meta">
                <span>Created: ${formatDateTime(session.createdAt)}</span>
                ${session.modifiedAt ? `<span>Modified: ${formatDateTime(session.modifiedAt)}</span>` : ''}
                <span>Turns: ${session.turns ? session.turns.length : 0}</span>
            </div>
        </div>
    `).join('');
}

function renderActiveSession() {
    const activeSession = sessions.find(s => s.isActive);

    if (!activeSession) {
        activeSessionCard.innerHTML = '<p class="no-data">No active session. Activate a session to start a conversation.</p>';
        return;
    }

    activeSessionCard.innerHTML = `
        <div class="session-card active">
            <div class="session-header">
                <div>
                    <span class="session-number">#${activeSession.number}</span>
                    <span class="session-name">${escapeHtml(activeSession.name)}</span>
                    <span class="badge badge-active">Active</span>
                </div>
                <div class="session-actions">
                    <a href="conversation.html" class="btn btn-primary">Open Conversation</a>
                </div>
            </div>
            <div class="session-meta">
                <span>Created: ${formatDateTime(activeSession.createdAt)}</span>
                ${activeSession.modifiedAt ? `<span>Modified: ${formatDateTime(activeSession.modifiedAt)}</span>` : ''}
                <span>Turns: ${activeSession.turns ? activeSession.turns.length : 0}</span>
            </div>
        </div>
    `;
}

function openCreateModal() {
    editingSessionId = null;
    modalTitle.textContent = 'Create Session';
    sessionNameInput.value = '';
    duplicateTurnsCheckbox.checked = false;
    duplicateTurnsOptions.style.display = 'none';
    turnCountInput.value = '5';
    sessionModal.style.display = 'block';
}

function populateSourceSessionsDropdown() {
    // Clear existing options except the first one
    sourceSessionSelect.innerHTML = '<option value="">Select a session...</option>';
    
    // Add sessions sorted by most recent first
    const sortedSessions = [...sessions].sort((a, b) => 
        new Date(b.createdAt) - new Date(a.createdAt)
    );
    
    sortedSessions.forEach(session => {
        const option = document.createElement('option');
        option.value = session.id;
        const turnCount = session.turns ? session.turns.length : 0;
        option.textContent = `#${session.number} - ${session.name} (${turnCount} turns)`;
        sourceSessionSelect.appendChild(option);
    });
}

function editSession(id) {
    const session = sessions.find(s => s.id === id);
    if (!session) return;

    editingSessionId = id;
    modalTitle.textContent = 'Edit Session';
    sessionNameInput.value = session.name;
    sessionModal.style.display = 'block';
}

function closeModal() {
    sessionModal.style.display = 'none';
    editingSessionId = null;
    sessionForm.reset();
}

async function handleSubmit(e) {
    e.preventDefault();

    const name = sessionNameInput.value.trim();
    if (!name) return;
    
    // Validate duplicate turns options if checkbox is checked
    if (duplicateTurnsCheckbox.checked && !sourceSessionSelect.value) {
        showError('Please select a source session to duplicate turns from');
        return;
    }

    try {
        if (editingSessionId) {
            await updateSession(editingSessionId, name);
        } else {
            await createSession(name);
        }

        closeModal();
        await loadSessions();
    } catch (error) {
        console.error('Error saving session:', error);
        showError('Failed to save session');
    }
}

async function createSession(name) {
    const sessionData = { name };

    // Include profileId if active profile exists
    if (activeProfileId !== null) {
        sessionData.profileId = activeProfileId;
    }
    
    // Include duplicate turns parameters if checkbox is checked
    if (duplicateTurnsCheckbox.checked && sourceSessionSelect.value) {
        sessionData.sourceSessionId = parseInt(sourceSessionSelect.value);
        sessionData.turnCount = parseInt(turnCountInput.value);
    }

    const response = await fetch(`${API_BASE_URL}/sessions`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(sessionData)
    });

    if (!response.ok) throw new Error('Failed to create session');
    showMessage('Session created successfully');
}

async function updateSession(id, name) {
    const session = sessions.find(s => s.id === id);

    const response = await fetch(`${API_BASE_URL}/sessions/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            name,
            isActive: session.isActive
        })
    });

    if (!response.ok) throw new Error('Failed to update session');
    showMessage('Session updated successfully');
}

async function activateSession(id) {
    try {
        const response = await fetch(`${API_BASE_URL}/sessions/${id}/activate`, {
            method: 'POST'
        });

        if (!response.ok) throw new Error('Failed to activate session');

        await loadSessions();
        showMessage('Session activated successfully');
    } catch (error) {
        console.error('Error activating session:', error);
        showError('Failed to activate session');
    }
}

async function deleteSession(id) {
    if (!confirm('Are you sure you want to delete this session? This will also delete all turns.')) {
        return;
    }

    try {
        const response = await fetch(`${API_BASE_URL}/sessions/${id}`, {
            method: 'DELETE'
        });

        if (!response.ok) throw new Error('Failed to delete session');

        await loadSessions();
        showMessage('Session deleted successfully');
    } catch (error) {
        console.error('Error deleting session:', error);
        showError('Failed to delete session');
    }
}

// Expose functions to window for HTML onclick handlers
window.sessionActions = {
    editSession,
    activateSession,
    deleteSession
};