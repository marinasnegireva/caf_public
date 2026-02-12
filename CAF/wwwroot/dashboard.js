/**
 * Dashboard Page Module
 * Displays system overview and statistics
 */

import {
    escapeHtml,
    showMessage,
    showError,
    API_BASE_URL
} from './js/common-utils.js';

let dashboard = null;

// Model configurations
const MODEL_OPTIONS = {
    Gemini: [
        { value: 'gemini-3-pro-preview', label: 'Gemini 3.0 Pro Preview' },
        { value: 'gemini-2.5-pro', label: 'Gemini 2.5 Pro' },
        { value: 'gemini-2.5-flash', label: 'Gemini 2.5 Flash' }
    ],
    Claude: [
        { value: 'claude-opus-4-6', label: 'Claude Opus 4.6' },
        { value: 'claude-sonnet-4-5', label: 'Claude Sonnet 4.5' },
        { value: 'claude-opus-4-5', label: 'Claude Opus 4.5' },
        { value: 'claude-3-5-sonnet-20241022', label: 'Claude 3.5 Sonnet' },
        { value: 'claude-3-5-haiku-20241022', label: 'Claude 3.5 Haiku' }
    ]
};

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    loadDashboard();
    loadLLMSettings();
    setupPolling();
});

async function loadDashboard() {
    try {
        const response = await fetch(`${API_BASE_URL}/dashboard`);
        if (!response.ok) throw new Error('Failed to load dashboard');

        dashboard = await response.json();
        renderDashboard();
    } catch (error) {
        console.error('Error loading dashboard:', error);
        showError('Failed to load dashboard');
    }
}

async function loadLLMSettings() {
    try {
        // Load LLM Provider
        const providerResponse = await fetch(`${API_BASE_URL}/settings/by-name/LLMProvider`);
        let provider = 'Gemini'; // Default
        if (providerResponse.ok) {
            const providerSetting = await providerResponse.json();
            if (providerSetting && providerSetting.value) {
                provider = providerSetting.value;
            }
        }

        // Load model based on provider
        const modelKey = provider === 'Claude' ? 'ClaudeModel' : 'GeminiModel';
        const modelResponse = await fetch(`${API_BASE_URL}/settings/by-name/${modelKey}`);
        let model = null;
        if (modelResponse.ok) {
            const modelSetting = await modelResponse.json();
            if (modelSetting && modelSetting.value) {
                model = modelSetting.value;
            }
        }

        // Update UI
        const providerSelect = document.getElementById('llmProviderSelect');
        providerSelect.value = provider;

        updateModelOptions(provider);

        if (model) {
            const modelSelect = document.getElementById('modelSelect');
            modelSelect.value = model;
        }

        updateCurrentDisplay(provider, model);
    } catch (error) {
        console.error('Error loading LLM settings:', error);
        document.getElementById('currentProviderName').textContent = 'Error loading';
        document.getElementById('currentModelName').textContent = 'Error loading';
    }
}

function updateModelOptions(provider) {
    const modelSelect = document.getElementById('modelSelect');
    const options = MODEL_OPTIONS[provider] || MODEL_OPTIONS.Gemini;

    modelSelect.innerHTML = options.map(opt =>
        `<option value="${opt.value}">${opt.label}</option>`
    ).join('');
}

function updateCurrentDisplay(provider, model) {
    document.getElementById('currentProviderName').textContent = provider || 'Gemini (default)';

    if (model) {
        // Find the label for the model
        const options = MODEL_OPTIONS[provider] || MODEL_OPTIONS.Gemini;
        const modelOption = options.find(opt => opt.value === model);
        document.getElementById('currentModelName').textContent = modelOption ? modelOption.label : model;
    } else {
        const defaultModel = MODEL_OPTIONS[provider || 'Gemini'][0];
        document.getElementById('currentModelName').textContent = `${defaultModel.label} (default)`;
    }
}

async function updateLLMProvider() {
    const providerSelect = document.getElementById('llmProviderSelect');
    const selectedProvider = providerSelect.value;

    try {
        // Update provider setting
        const providerResponse = await fetch(`${API_BASE_URL}/settings`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                name: 'LLMProvider',
                value: selectedProvider
            })
        });

        if (!providerResponse.ok) {
            const error = await providerResponse.json();
            throw new Error(error.error || 'Failed to update provider');
        }

        // Update model options dropdown
        updateModelOptions(selectedProvider);

        // Get the first model from the new provider's list
        const defaultModel = MODEL_OPTIONS[selectedProvider][0].value;
        document.getElementById('modelSelect').value = defaultModel;

        // Update the model setting for the new provider
        await updateModelSetting(selectedProvider, defaultModel);

        updateCurrentDisplay(selectedProvider, defaultModel);
        showMessage(`LLM provider switched to ${selectedProvider}`);
        await loadDashboard();
    } catch (error) {
        console.error('Error updating LLM provider:', error);
        showError('Failed to update provider: ' + error.message);
    }
}

async function updateModel() {
    const providerSelect = document.getElementById('llmProviderSelect');
    const modelSelect = document.getElementById('modelSelect');
    const selectedProvider = providerSelect.value;
    const selectedModel = modelSelect.value;

    try {
        await updateModelSetting(selectedProvider, selectedModel);
        updateCurrentDisplay(selectedProvider, selectedModel);

        const modelOption = MODEL_OPTIONS[selectedProvider].find(opt => opt.value === selectedModel);
        showMessage(`Model switched to ${modelOption ? modelOption.label : selectedModel}`);
        await loadDashboard();
    } catch (error) {
        console.error('Error updating model:', error);
        showError('Failed to update model: ' + error.message);
    }
}

async function updateModelSetting(provider, model) {
    const modelKey = provider === 'Claude' ? 'ClaudeModel' : 'GeminiModel';

    const response = await fetch(`${API_BASE_URL}/settings`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            name: modelKey,
            value: model
        })
    });

    if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || 'Failed to update model');
    }
}

function renderDashboard() {
    // Update statistics
    document.getElementById('totalFlags').textContent = dashboard.statistics.totalFlags;
    document.getElementById('totalActiveContext').textContent = dashboard.statistics.activeContextData;
    document.getElementById('totalSettings').textContent = dashboard.statistics.totalSettings;

    const sessionNumber = dashboard.activeSession?.number || 'None';
    document.getElementById('sessionNumber').textContent = sessionNumber;

    renderActiveFlags();
    renderSettings();
    renderActiveSession();
}

function renderActiveFlags() {
    const container = document.getElementById('activeFlagsList');

    if (!dashboard.activeFlags || dashboard.activeFlags.length === 0) {
        container.innerHTML = '<p class="no-data">No active flags</p>';
        return;
    }

    container.innerHTML = dashboard.activeFlags.map(flag => `
        <div class="flag-badge ${flag.constant ? 'flag-constant' : 'flag-active'}">
            <div class="flag-value">${escapeHtml(flag.value)}</div>
            <div class="flag-type">${flag.constant ? 'Every Turn' : 'Active'}</div>
        </div>
    `).join('');
}

function renderSettings() {
    const container = document.getElementById('settingsList');

    if (dashboard.settings.length === 0) {
        container.innerHTML = '<p class="no-data">No settings configured</p>';
        return;
    }

    container.innerHTML = `
        <table class="settings-table" style="font-size: 0.9rem;">
            <thead>
                <tr>
                    <th style="width: 30%;">Name</th>
                    <th>Value</th>
                </tr>
            </thead>
            <tbody>
                ${dashboard.settings.map(setting => {
                    const value = setting.value.toLowerCase();
                    const isBoolean = value === 'true' || value === 'false';
                    
                    return `
                        <tr>
                            <td><strong>${escapeHtml(setting.name)}</strong></td>
                            <td>
                                ${isBoolean ? `
                                    <div class="form-check form-switch">
                                        <input class="form-check-input" type="checkbox" 
                                            id="setting-${setting.id}" 
                                            ${value === 'true' ? 'checked' : ''}
                                            onchange="window.dashboardActions.toggleBooleanSetting(${setting.id}, '${escapeHtml(setting.name)}', this.checked)">
                                    </div>
                                ` : `
                                    <input type="text" 
                                        class="form-control form-control-sm" 
                                        id="setting-value-${setting.id}"
                                        value="${escapeHtml(setting.value)}"
                                        onblur="window.dashboardActions.updateSettingValue(${setting.id}, '${escapeHtml(setting.name)}', this.value)"
                                        onkeypress="if(event.key === 'Enter') this.blur()">
                                `}
                            </td>
                        </tr>
                    `;
                }).join('')}
            </tbody>
        </table>
    `;
}

function renderActiveSession() {
    const container = document.getElementById('activeSessionInfo');

    if (!dashboard.activeSession) {
        container.innerHTML = '<p class="no-data">No active session</p>';
        return;
    }

    const session = dashboard.activeSession;
    container.innerHTML = `
        <div class="session-info">
            <div class="info-row">
                <span class="info-label">Session #:</span>
                <span class="info-value">${session.number}</span>
            </div>
            <div class="info-row">
                <span class="info-label">Name:</span>
                <span class="info-value">${escapeHtml(session.name)}</span>
            </div>
            <div class="info-row">
                <span class="info-label">Turns:</span>
                <span class="info-value">${session.turns?.length || 0}</span>
            </div>
            <div class="info-row">
                <span class="info-label">Created:</span>
                <span class="info-value">${new Date(session.createdAt).toLocaleString()}</span>
            </div>
        </div>
        <a href="sessions.html" class="btn btn-primary" style="margin-top: 1rem;">Manage Sessions</a>
    `;
}

async function createOrUpdateSetting() {
    const name = document.getElementById('newSettingName').value.trim();
    const value = document.getElementById('newSettingValue').value.trim();

    if (!name) {
        showError('Setting name cannot be empty');
        return;
    }

    try {
        const response = await fetch(`${API_BASE_URL}/settings`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name, value })
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'Failed to save setting');
        }

        document.getElementById('newSettingName').value = '';
        document.getElementById('newSettingValue').value = '';
        await loadDashboard();
        showMessage('Setting saved successfully');
    } catch (error) {
        console.error('Error saving setting:', error);
        showError('Error: ' + error.message);
    }
}

async function deleteSetting(settingId) {
    if (!confirm('Are you sure you want to delete this setting?')) {
        return;
    }

    try {
        const response = await fetch(`${API_BASE_URL}/settings/${settingId}`, {
            method: 'DELETE'
        });

        if (!response.ok) throw new Error('Failed to delete setting');

        await loadDashboard();
        showMessage('Setting deleted successfully');
    } catch (error) {
        console.error('Error deleting setting:', error);
        showError('Error: ' + error.message);
    }
}

function setupPolling() {
    // Refresh dashboard every 30 seconds
    setInterval(loadDashboard, 30000);
}

async function toggleBooleanSetting(settingId, settingName, isChecked) {
    const newValue = isChecked ? 'true' : 'false';
    await updateSettingValue(settingId, settingName, newValue);
}

async function updateSettingValue(settingId, settingName, newValue) {
    const trimmedValue = newValue.trim();
    
    if (!trimmedValue) {
        showError('Setting value cannot be empty');
        await loadDashboard();
        return;
    }

    try {
        const response = await fetch(`${API_BASE_URL}/settings`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ 
                name: settingName, 
                value: trimmedValue 
            })
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'Failed to update setting');
        }

        await loadDashboard();
        showMessage(`Setting "${settingName}" updated successfully`);
    } catch (error) {
        console.error('Error updating setting:', error);
        showError('Error: ' + error.message);
        await loadDashboard();
    }
}

// Expose functions to window for HTML onclick handlers
window.dashboardActions = {
    updateLLMProvider,
    updateModel,
    toggleBooleanSetting,
    updateSettingValue
};