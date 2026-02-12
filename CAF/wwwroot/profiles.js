const API_BASE = '/api';

let profiles = [];
let currentProfileId = null;
let selectedMemories = new Set();
let selectedTriggers = new Set();
let selectedMessages = new Set();

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    loadProfiles();
    setupEventListeners();
});

function setupEventListeners() {
    document.getElementById('createProfileBtn').addEventListener('click', () => openProfileModal());
    document.getElementById('refreshBtn').addEventListener('click', loadProfiles);
    document.getElementById('testApiBtn').addEventListener('click', testApiConnection);
    document.getElementById('profileForm').addEventListener('submit', handleProfileSubmit);

    // Modal close buttons
    document.querySelectorAll('.modal .close, .modal .cancel').forEach(btn => {
        btn.addEventListener('click', closeModals);
    });

    // Tab switching
    document.querySelectorAll('.tab-button').forEach(btn => {
        btn.addEventListener('click', (e) => switchTab(e.target.dataset.tab));
    });

    // Move entities button
    document.getElementById('moveEntitiesBtn').addEventListener('click', handleMoveEntities);
}

async function loadProfiles() {
    showLoading(true);
    hideError();

    try {
        console.log('Loading profiles from:', `${API_BASE}/profiles`);
        const response = await fetch(`${API_BASE}/profiles`);

        if (!response.ok) {
            console.error('Failed to load profiles. Status:', response.status);
            const errorText = await response.text();
            throw new Error(`HTTP ${response.status}: ${errorText}`);
        }

        profiles = await response.json();
        console.log('Loaded profiles:', profiles);
        console.log('Number of profiles:', profiles.length);
        console.log('Active profile:', profiles.find(p => p.isActive));
        console.log('Inactive profiles:', profiles.filter(p => !p.isActive));

        showDebugInfo(profiles);
        renderProfiles();
    } catch (error) {
        console.error('Error loading profiles:', error);
        showError('Failed to load profiles: ' + error.message);
    } finally {
        showLoading(false);
    }
}

function renderProfiles() {
    console.log('=== RENDER PROFILES DEBUG ===');
    console.log('Total profiles:', profiles.length);
    console.log('Profiles array:', JSON.stringify(profiles, null, 2));

    const activeProfile = profiles.find(p => p.isActive);
    const inactiveProfiles = profiles.filter(p => !p.isActive);

    console.log('Active profile found:', activeProfile ? activeProfile.name : 'NONE');
    console.log('Inactive profiles:', inactiveProfiles.length);

    // Render active profile
    const activeContainer = document.getElementById('activeProfile');
    if (activeProfile) {
        activeContainer.innerHTML = `
            <div class="profile-card active" style="border-left: 4px solid ${activeProfile.color || '#007bff'}">
                <div class="profile-header">
                    <div>
                        <h3>${activeProfile.name}</h3>
                        <span class="badge active-badge">ACTIVE</span>
                    </div>
                    <div class="profile-actions">
                        <button onclick="openProfileModal(${activeProfile.id})" class="icon-btn" title="Edit"><i class="fas fa-edit"></i></button>
                        <button onclick="openMoveEntitiesModal(${activeProfile.id})" class="icon-btn" title="Manage Entities"><i class="fas fa-exchange-alt"></i></button>
                        <button onclick="duplicateProfile(${activeProfile.id})" class="icon-btn" title="Duplicate"><i class="fas fa-copy"></i></button>
                    </div>
                </div>
                <p class="profile-description">${activeProfile.description || 'No description'}</p>
                <div class="profile-stats">
                    <span><i class="fas fa-bolt"></i> ${activeProfile.contextTriggersCount || 0} triggers</span>
                    <span><i class="fas fa-brain"></i> ${activeProfile.memoriesCount || 0} memories</span>
                    <span><i class="fas fa-file-alt"></i> ${activeProfile.systemMessagesCount || 0} messages</span>
                    <span><i class="fas fa-folder"></i> ${activeProfile.sessionsCount || 0} sessions</span>
                    <span><i class="fas fa-cog"></i> ${activeProfile.settingsCount || 0} settings</span>
                    <span><i class="fas fa-flag"></i> ${activeProfile.flagsCount || 0} flags</span>
                </div>
                <small>Last activated: ${formatDate(activeProfile.lastActivatedAt)}</small>
            </div>
        `;
    } else if (profiles.length === 0) {
        activeContainer.innerHTML = `
            <div class="empty-state-card">
                <h3>No Profiles Yet</h3>
                <p>Create profiles to organize your contexts, triggers, memories, and system messages.</p>
                <button onclick="createDefaultProfile()" class="primary" style="margin-top: 16px;">
                    <i class="fas fa-magic"></i> Create Default Profile & Assign All Entities
                </button>
                <p style="margin-top: 12px; font-size: 0.9rem; color: var(--text-muted);">
                    Or create a custom profile using the "+ New Profile" button above
                </p>
            </div>
        `;
    } else {
        activeContainer.innerHTML = '<p class="empty-state">No active profile. Click "Activate" on a profile below.</p>';
    }

    // Render inactive profiles
    const listContainer = document.getElementById('profilesList');
    if (inactiveProfiles.length === 0 && profiles.length > 0) {
        listContainer.innerHTML = '<p class="empty-state">No other profiles</p>';
        return;
    } else if (profiles.length === 0) {
        listContainer.innerHTML = '';
        return;
    }

    listContainer.innerHTML = inactiveProfiles.map(profile => `
        <div class="profile-card" style="border-left: 4px solid ${profile.color || '#6c757d'}">
            <div class="profile-header">
                <h3>${profile.name}</h3>
                <div class="profile-actions">
                    <button onclick="activateProfile(${profile.id})" class="btn-small primary" title="Activate"><i class="fas fa-check"></i> Activate</button>
                    <button onclick="openProfileModal(${profile.id})" class="icon-btn" title="Edit"><i class="fas fa-edit"></i></button>
                    <button onclick="openMoveEntitiesModal(${profile.id})" class="icon-btn" title="Manage Entities"><i class="fas fa-exchange-alt"></i></button>
                    <button onclick="duplicateProfile(${profile.id})" class="icon-btn" title="Duplicate"><i class="fas fa-copy"></i></button>
                    <button onclick="deleteProfile(${profile.id})" class="icon-btn danger" title="Delete"><i class="fas fa-trash"></i></button>
                </div>
            </div>
            <p class="profile-description">${profile.description || 'No description'}</p>
            <div class="profile-stats">
                <span><i class="fas fa-bolt"></i> ${profile.contextTriggersCount || 0} triggers</span>
                <span><i class="fas fa-brain"></i> ${profile.memoriesCount || 0} memories</span>
                <span><i class="fas fa-file-alt"></i> ${profile.systemMessagesCount || 0} messages</span>
                <span><i class="fas fa-folder"></i> ${profile.sessionsCount || 0} sessions</span>
                <span><i class="fas fa-cog"></i> ${profile.settingsCount || 0} settings</span>
                <span><i class="fas fa-flag"></i> ${profile.flagsCount || 0} flags</span>
            </div>
            <small>Created: ${formatDate(profile.createdAt)}</small>
        </div>
    `).join('');
}

function openProfileModal(profileId = null) {
    const modal = document.getElementById('profileModal');
    const form = document.getElementById('profileForm');
    const title = document.getElementById('modalTitle');

    form.reset();

    if (profileId) {
        const profile = profiles.find(p => p.id === profileId);
        if (profile) {
            title.textContent = 'Edit Profile';
            document.getElementById('profileId').value = profile.id;
            document.getElementById('profileName').value = profile.name;
            document.getElementById('profileDescription').value = profile.description || '';
            document.getElementById('profileColor').value = profile.color || '#007bff';
        }
    } else {
        title.textContent = 'Create Profile';
    }

    modal.style.display = 'block';
}

async function handleProfileSubmit(e) {
    e.preventDefault();

    const profileId = document.getElementById('profileId').value;
    const profile = {
        id: profileId ? parseInt(profileId) : 0,
        name: document.getElementById('profileName').value,
        description: document.getElementById('profileDescription').value,
        color: document.getElementById('profileColor').value
    };

    try {
        const url = profileId ? `${API_BASE}/profiles/${profileId}` : `${API_BASE}/profiles`;
        const method = profileId ? 'PUT' : 'POST';

        const response = await fetch(url, {
            method,
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(profile)
        });

        if (response.ok) {
            closeModals();
            await loadProfiles();
        } else {
            alert('Failed to save profile');
        }
    } catch (error) {
        console.error('Error saving profile:', error);
        alert('Failed to save profile');
    }
}

async function activateProfile(profileId) {
    if (!confirm('Activate this profile? This will deactivate the current profile.')) return;

    try {
        const response = await fetch(`${API_BASE}/profiles/${profileId}/activate`, {
            method: 'POST'
        });

        if (response.ok) {
            await loadProfiles();
        } else {
            alert('Failed to activate profile');
        }
    } catch (error) {
        console.error('Error activating profile:', error);
        alert('Failed to activate profile');
    }
}

async function deleteProfile(profileId) {
    const profile = profiles.find(p => p.id === profileId);
    if (!confirm(`Delete profile "${profile.name}"? Entities will be unassigned, not deleted.`)) return;

    try {
        const response = await fetch(`${API_BASE}/profiles/${profileId}`, {
            method: 'DELETE'
        });

        if (response.ok) {
            await loadProfiles();
        } else {
            const error = await response.text();
            alert(`Failed to delete profile: ${error}`);
        }
    } catch (error) {
        console.error('Error deleting profile:', error);
        alert('Failed to delete profile');
    }
}

async function duplicateProfile(profileId) {
    const profile = profiles.find(p => p.id === profileId);
    const newName = prompt(`Duplicate profile "${profile.name}". Enter new name:`, `${profile.name} (Copy)`);

    if (!newName) return;

    try {
        const response = await fetch(`${API_BASE}/profiles/${profileId}/duplicate`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ newName })
        });

        if (response.ok) {
            await loadProfiles();
        } else {
            alert('Failed to duplicate profile');
        }
    } catch (error) {
        console.error('Error duplicating profile:', error);
        alert('Failed to duplicate profile');
    }
}

async function openMoveEntitiesModal(profileId) {
    currentProfileId = profileId;
    const profile = profiles.find(p => p.id === profileId);

    document.getElementById('targetProfileName').textContent = `Moving entities to: ${profile.name}`;

    // Clear selections
    selectedMemories.clear();
    selectedTriggers.clear();
    selectedMessages.clear();

    // Load entities (only those not already in this profile)
    await loadEntitiesForMove(profileId);

    document.getElementById('moveEntitiesModal').style.display = 'block';
}

async function createDefaultProfile() {
    if (!confirm('Create a default profile and assign all existing entities to it?')) return;

    try {
        const response = await fetch(`${API_BASE}/profiles/create-default`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name: 'Default Profile' })
        });

        if (response.ok) {
            await loadProfiles();
            alert('Default profile created successfully!');
        } else {
            alert('Failed to create default profile');
        }
    } catch (error) {
        console.error('Error creating default profile:', error);
        alert('Failed to create default profile');
    }
}

async function loadEntitiesForMove(profileId) {
    try {
        // Load ALL memories (from all profiles) - we want to see what we can move
        const memoriesResponse = await fetch(`${API_BASE}/corememories`);
        const memories = await memoriesResponse.json();
        const availableMemories = memories.filter(m => m.profileId !== profileId);

        const memoriesList = document.getElementById('memoriesList');
        memoriesList.innerHTML = availableMemories.length === 0 ?
            '<p class="empty-state">No available memories</p>' :
            availableMemories.map(m => `
                <label class="entity-item">
                    <input type="checkbox" value="${m.id}" data-type="memory">
                    <span>${m.name || 'Unnamed Memory'} ${m.profileId ? `(Profile: ${m.profileId})` : '(Global)'}</span>
                </label>
            `).join('');

        // Load ALL triggers (from all profiles) - we want to see what we can move
        const triggersResponse = await fetch(`${API_BASE}/contexttriggers`);
        const triggers = await triggersResponse.json();
        const availableTriggers = triggers.filter(t => t.profileId !== profileId);

        const triggersList = document.getElementById('triggersList');
        triggersList.innerHTML = availableTriggers.length === 0 ?
            '<p class="empty-state">No available triggers</p>' :
            availableTriggers.map(t => `
                <label class="entity-item">
                    <input type="checkbox" value="${t.id}" data-type="trigger">
                    <span>${t.name} ${t.profileId ? `(Profile: ${t.profileId})` : '(Global)'}</span>
                </label>
            `).join('');

        // Load ALL system messages (from all profiles) - we want to see what we can move
        const messagesResponse = await fetch(`${API_BASE}/systemmessages`);
        const messages = await messagesResponse.json();
        const availableMessages = messages.filter(sm => sm.profileId !== profileId);

        const messagesList = document.getElementById('messagesList');
        messagesList.innerHTML = availableMessages.length === 0 ?
            '<p class="empty-state">No available system messages</p>' :
            availableMessages.map(sm => `
                <label class="entity-item">
                    <input type="checkbox" value="${sm.id}" data-type="message">
                    <span>${sm.name} - ${sm.type} ${sm.profileId ? `(Profile: ${sm.profileId})` : '(Global)'}</span>
                </label>
            `).join('');

        // Setup checkbox listeners
        document.querySelectorAll('#moveEntitiesModal input[type="checkbox"]').forEach(cb => {
            cb.addEventListener('change', (e) => {
                const id = parseInt(e.target.value);
                const type = e.target.dataset.type;
                const set = type === 'memory' ? selectedMemories :
                    type === 'trigger' ? selectedTriggers : selectedMessages;

                if (e.target.checked) {
                    set.add(id);
                } else {
                    set.delete(id);
                }
            });
        });
    } catch (error) {
        console.error('Error loading entities:', error);
        alert('Failed to load entities');
    }
}

async function handleMoveEntities() {
    const memoryIds = Array.from(selectedMemories);
    const contextTriggerIds = Array.from(selectedTriggers);
    const systemMessageIds = Array.from(selectedMessages);

    if (memoryIds.length === 0 && contextTriggerIds.length === 0 && systemMessageIds.length === 0) {
        alert('Please select at least one entity to move');
        return;
    }

    try {
        const response = await fetch(`${API_BASE}/profiles/${currentProfileId}/move-entities`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                memoryIds: memoryIds.length > 0 ? memoryIds : null,
                contextTriggerIds: contextTriggerIds.length > 0 ? contextTriggerIds : null,
                systemMessageIds: systemMessageIds.length > 0 ? systemMessageIds : null
            })
        });

        if (response.ok) {
            const result = await response.json();
            alert(`Moved ${result.movedCount} entities successfully`);
            closeModals();
            await loadProfiles();
        } else {
            alert('Failed to move entities');
        }
    } catch (error) {
        console.error('Error moving entities:', error);
        alert('Failed to move entities');
    }
}

function switchTab(tabName) {
    // Update tab buttons
    document.querySelectorAll('.tab-button').forEach(btn => {
        btn.classList.toggle('active', btn.dataset.tab === tabName);
    });

    // Update tab panes
    document.querySelectorAll('.tab-pane').forEach(pane => {
        pane.classList.toggle('active', pane.id === `${tabName}-tab`);
    });
}

function closeModals() {
    document.querySelectorAll('.modal').forEach(modal => {
        modal.style.display = 'none';
    });
}

function formatDate(dateString) {
    if (!dateString) return 'Never';
    const date = new Date(dateString);
    return date.toLocaleString();
}

// Helper functions for UI feedback
function showLoading(show) {
    const loader = document.getElementById('loadingIndicator');
    if (loader) {
        loader.style.display = show ? 'block' : 'none';
    }
}

function showError(message) {
    const errorDiv = document.getElementById('errorDisplay');
    const errorMsg = document.getElementById('errorMessage');
    if (errorDiv && errorMsg) {
        errorMsg.textContent = message;
        errorDiv.style.display = 'block';
    }
}

function hideError() {
    const errorDiv = document.getElementById('errorDisplay');
    if (errorDiv) {
        errorDiv.style.display = 'none';
    }
}

function showDebugInfo(data) {
    const debugDiv = document.getElementById('debugInfo');
    const debugContent = document.getElementById('debugContent');
    if (debugDiv && debugContent) {
        const activeProfile = data.find(p => p.isActive);
        const debugData = {
            totalProfiles: data.length,
            hasActiveProfile: !!activeProfile,
            activeProfileName: activeProfile ? activeProfile.name : 'NONE',
            activeProfileId: activeProfile ? activeProfile.id : null,
            allProfiles: data.map(p => ({
                id: p.id,
                name: p.name,
                isActive: p.isActive,
                triggers: p.contextTriggersCount || 0,
                memories: p.memoriesCount || 0,
                messages: p.systemMessagesCount || 0
            }))
        };
        debugContent.textContent = JSON.stringify(debugData, null, 2);
        debugDiv.style.display = 'block';
    }
}

async function testApiConnection() {
    console.log('Testing API connection...');
    showLoading(true);
    hideError();

    try {
        const response = await fetch(`${API_BASE}/profiles`);
        console.log('Response status:', response.status);
        console.log('Response headers:', [...response.headers.entries()]);

        const text = await response.text();
        console.log('Response body (raw):', text);

        if (response.ok) {
            const data = JSON.parse(text);
            alert(`API Connection OK!\n\nFound ${data.length} profiles\n\nCheck console for details.`);
            showDebugInfo(data);
        } else {
            alert(`API Error: ${response.status}\n\n${text}`);
            showError(`API returned status ${response.status}`);
        }
    } catch (error) {
        console.error('API Test Error:', error);
        alert(`Connection Failed: ${error.message}`);
        showError(`Failed to connect: ${error.message}`);
    } finally {
        showLoading(false);
    }
}

// Close modal when clicking outside
window.addEventListener('click', (e) => {
    if (e.target.classList.contains('modal')) {
        closeModals();
    }
});