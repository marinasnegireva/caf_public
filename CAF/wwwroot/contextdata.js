/**
 * Context Data Management Page Module
 * Unified management for quotes, memories, insights, profiles, and data
 */

import {
    escapeHtml,
    showMessage,
    showError,
    getTimeAgo,
    formatDate
} from './js/common-utils.js';

const API_BASE_URL = '/api/contextdata';

let allData = [];
let filteredData = [];
let editModalInstance = null;
let confirmModalInstance = null;
let bulkAvailabilityModalInstance = null;
let currentTypeFilter = 'all';
let pendingAvailabilityChange = null;
let selectedIds = new Set();

// DOM Elements
const dataList = document.getElementById('dataList');
const typeTabs = document.getElementById('typeTabs');
const availabilityFilter = document.getElementById('availabilityFilter');
const embeddedFilter = document.getElementById('embeddedFilter');
const tagFilter = document.getElementById('tagFilter');
const speakerFilter = document.getElementById('speakerFilter');
const sessionFilter = document.getElementById('sessionFilter');
const sortBy = document.getElementById('sortBy');
const sortOrder = document.getElementById('sortOrder');
const searchInput = document.getElementById('searchInput');
const includeArchived = document.getElementById('includeArchived');
const importSection = document.getElementById('importSection');

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    initializeModals();
    setupEventListeners();
    loadData();
    loadStats();
});

function initializeModals() {
    const editModal = document.getElementById('editModal');
    if (editModal) {
        editModalInstance = new bootstrap.Modal(editModal, {
            backdrop: true,
            keyboard: true,
            focus: true
        });
    }
    
    const confirmModal = document.getElementById('confirmAvailabilityModal');
    if (confirmModal) {
        confirmModalInstance = new bootstrap.Modal(confirmModal);
    }
    
    const bulkAvailabilityModal = document.getElementById('bulkAvailabilityModal');
    if (bulkAvailabilityModal) {
        bulkAvailabilityModalInstance = new bootstrap.Modal(bulkAvailabilityModal);
    }
}

function setupEventListeners() {
    // Type tabs
    typeTabs.querySelectorAll('.type-tab').forEach(tab => {
        tab.addEventListener('click', () => {
            typeTabs.querySelectorAll('.type-tab').forEach(t => t.classList.remove('active'));
            tab.classList.add('active');
            currentTypeFilter = tab.dataset.type;
            applyFilters();
        });
    });
    
    // Filters
    availabilityFilter.addEventListener('change', applyFilters);
    embeddedFilter.addEventListener('change', applyFilters);
    tagFilter.addEventListener('change', applyFilters);
    speakerFilter.addEventListener('change', applyFilters);
    sessionFilter.addEventListener('change', applyFilters);
    sortBy.addEventListener('change', applyFilters);
    sortOrder.addEventListener('change', applyFilters);
    searchInput.addEventListener('input', debounce(applyFilters, 300));
    includeArchived.addEventListener('change', loadData);
    
    // Availability change in edit modal
    document.getElementById('editAvailability').addEventListener('change', handleAvailabilityFieldChange);
    
    // Confirm availability change
    document.getElementById('confirmAvailabilityBtn').addEventListener('click', confirmAvailabilityChange);
}

// ============ Data Loading ============

async function loadData() {
    try {
        const archived = includeArchived.checked;
        const response = await fetch(`${API_BASE_URL}?includeArchived=${archived}`);
        if (!response.ok) throw new Error('Failed to load data');
        
        allData = await response.json();
        
        // Populate filter dropdowns
        populateFilterDropdowns();
        
        applyFilters();
    } catch (error) {
        console.error('Error loading data:', error);
        dataList.innerHTML = '<div class="alert alert-danger">Failed to load data</div>';
    }
}

function populateFilterDropdowns() {
    // Get unique speakers
    const speakers = [...new Set(allData
        .filter(d => d.speaker)
        .map(d => d.speaker)
        .sort())];
    
    speakerFilter.innerHTML = '<option value="">All Speakers</option>' +
        speakers.map(s => `<option value="${escapeHtml(s)}">${escapeHtml(s)}</option>`).join('');
    
    // Get unique sessions
    const sessions = [...new Set(allData
        .filter(d => d.sourceSessionId)
        .map(d => d.sourceSessionId)
        .sort((a, b) => b - a))]; // Sort descending
    
    sessionFilter.innerHTML = '<option value="">All Sessions</option>' +
        sessions.map(s => `<option value="${s}">Session ${s}</option>`).join('');
}

async function loadStats() {
    try {
        const response = await fetch(`${API_BASE_URL}/stats`);
        if (!response.ok) return;
        
        const stats = await response.json();
        document.getElementById('totalCount').textContent = stats.totalCount;
        document.getElementById('activeCount').textContent = stats.activeCount;
        document.getElementById('embeddedCount').textContent = stats.embeddedCount;
    } catch (error) {
        console.error('Error loading stats:', error);
    }
}

// ============ Filtering & Sorting ============

function applyFilters() {
    let data = [...allData];
    
    // Type filter
    if (currentTypeFilter !== 'all') {
        data = data.filter(d => d.type === currentTypeFilter);
    }
    
    // Availability filter
    const avail = availabilityFilter.value;
    if (avail) {
        data = data.filter(d => d.availability === avail);
    }
    
    // Embedded status filter
    const embedded = embeddedFilter.value;
    if (embedded === 'embedded') {
        data = data.filter(d => d.inVectorDb);
    } else if (embedded === 'not-embedded') {
        data = data.filter(d => !d.inVectorDb);
    }
    
    // Tag status filter
    const tagged = tagFilter.value;
    if (tagged === 'tagged') {
        data = data.filter(d => d.tags && d.tags.length > 0);
    } else if (tagged === 'untagged') {
        data = data.filter(d => !d.tags || d.tags.length === 0);
    }
    
    // Speaker filter
    const speaker = speakerFilter.value;
    if (speaker) {
        data = data.filter(d => d.speaker === speaker);
    }
    
    // Session filter
    const session = sessionFilter.value;
    if (session) {
        data = data.filter(d => d.sourceSessionId && d.sourceSessionId.toString() === session);
    }
    
    // Search filter
    const search = searchInput.value.toLowerCase().trim();
    if (search) {
        data = data.filter(d => 
            d.name.toLowerCase().includes(search) || 
            d.content.toLowerCase().includes(search) ||
            (d.speaker && d.speaker.toLowerCase().includes(search)) ||
            (d.tags && d.tags.some(t => t.toLowerCase().includes(search)))
        );
    }
    
    // Sorting
    const sortField = sortBy.value;
    const order = sortOrder.value === 'asc' ? 1 : -1;
    
    data.sort((a, b) => {
        let aVal = a[sortField];
        let bVal = b[sortField];
        
        if (typeof aVal === 'string') {
            return order * aVal.localeCompare(bVal);
        }
        if (aVal instanceof Date || sortField.includes('At')) {
            aVal = new Date(aVal || 0).getTime();
            bVal = new Date(bVal || 0).getTime();
        }
        return order * ((aVal || 0) - (bVal || 0));
    });
    
    filteredData = data;
    renderData();
}

// ============ Rendering ============

function renderData() {
    document.getElementById('resultCount').textContent = `(${filteredData.length} items)`;
    
    if (filteredData.length === 0) {
        dataList.innerHTML = `
            <div class="no-data">
                <i class="fas fa-inbox"></i>
                <p>No data found matching your filters</p>
            </div>`;
        return;
    }
    
    // Check if any items have Trigger availability
    const hasTriggers = filteredData.some(d => d.availability === 'Trigger');
    
    const tableHtml = `
        <div class="table-responsive">
            <table class="table table-sm table-hover data-table">
                <thead>
                    <tr>
                        <th style="width: 30px;"><input type="checkbox" class="form-check-input" id="selectAllTable" onchange="window.contextDataActions.toggleSelectAll(this.checked)"></th>
                        <th style="width: 50px;">ID</th>
                        <th style="width: 200px;">Name</th>
                        <th style="width: 80px;">Type</th>
                        <th style="width: 90px;">Availability</th>
                        ${hasTriggers ? '<th style="width: 150px;">Triggers</th>' : ''}
                        <th>Content</th>
                        <th style="width: 200px;">Tags</th>
                        <th style="width: 180px;">Path</th>
                        <th style="width: 70px;">Tokens</th>
                        <th style="width: 220px;">Actions</th>
                    </tr>
                </thead>
                <tbody>
                    ${filteredData.map(item => renderDataRow(item, hasTriggers)).join('')}
                </tbody>
            </table>
        </div>`;
    
    dataList.innerHTML = tableHtml;
    updateSelectionUI();
}

function renderDataRow(item, hasTriggers) {
    const isSelected = selectedIds.has(item.id);
    const typeBadge = `<span class="badge-type badge-${item.type.toLowerCase()}">${item.type}</span>`;
    const availBadge = `<span class="badge-availability badge-${item.availability.toLowerCase()}">${item.availability}</span>`;
    
    // Tags display
    const tagsHtml = item.tags && item.tags.length > 0
        ? item.tags.map(t => `<span class="tag-item">${escapeHtml(t)}</span>`).join(' ')
        : '<span class="text-muted small">No tags</span>';
    
    // Path display
    const pathHtml = item.path 
        ? `<span class="path-cell" title="${escapeHtml(item.path)}">${escapeHtml(item.path)}</span>`
        : '<span class="text-muted small">-</span>';
    
    // Content preview with tooltip
    const contentPreview = item.content.length > 80 
        ? item.content.substring(0, 80) + '...' 
        : item.content;
    
    // Triggers display (only for Trigger availability)
    let triggersHtml = '';
    if (hasTriggers) {
        if (item.availability === 'Trigger') {
            const keywords = item.triggerKeywords || '';
            triggersHtml = `<td class="trigger-cell">
                <div class="d-flex align-items-center gap-1">
                    <span class="editable-cell trigger-keywords" data-field="triggerKeywords" data-id="${item.id}" title="Click to edit trigger keywords">
                        ${keywords ? escapeHtml(keywords) : '<span class="text-muted small">No keywords</span>'}
                    </span>
                    <button class="btn btn-sm btn-link p-0 text-primary" onclick="window.contextDataActions.manageTriggers(${item.id})" title="Manage triggers">
                        <i class="fas fa-cog"></i>
                    </button>
                </div>
            </td>`;
        } else {
            triggersHtml = '<td>-</td>';
        }
    }
    
    // Actions - compact buttons
    let actions = `
        <button class="btn btn-sm btn-outline-primary" onclick="window.contextDataActions.editItem(${item.id})" title="Edit">
            <i class="fas fa-edit"></i>
        </button>`;
    
    // Manual toggle buttons (shown for any availability)
    const useNextTitle = item.useNextTurnOnly ? 'Clear: Use Next Turn Only' : 'Set: Use Next Turn Only';
    const useEveryTitle = item.useEveryTurn ? 'Clear: Use Every Turn' : 'Set: Use Every Turn';
    const nextTurnClass = item.useNextTurnOnly ? 'btn-warning' : 'btn-outline-secondary';
    const everyTurnClass = item.useEveryTurn ? 'btn-success' : 'btn-outline-secondary';
    
    actions += `
        <button class="btn btn-sm ${nextTurnClass}" 
                onclick="window.contextDataActions.toggleNextTurn(${item.id})" 
                title="${useNextTitle}">
            <i class="fas fa-play"></i>
        </button>
        <button class="btn btn-sm ${everyTurnClass}" 
                onclick="window.contextDataActions.toggleEveryTurn(${item.id}, ${!item.useEveryTurn})" 
                title="${useEveryTitle}">
            <i class="fas fa-repeat"></i>
        </button>`;
    
    if (item.availability === 'Semantic') {
        actions += `
            <button class="btn btn-sm btn-outline-info" onclick="window.contextDataActions.generateTags(${item.id})" title="Generate Tags & Relevance">
                <i class="fas fa-tags"></i>
            </button>`;
        
        if (item.tags && item.tags.length > 0) {
            actions += item.inVectorDb
                ? `<button class="btn btn-sm btn-outline-danger" onclick="window.contextDataActions.unembed(${item.id})" title="Unembed">
                       <i class="fas fa-times"></i>
                   </button>`
                : `<button class="btn btn-sm btn-outline-success" onclick="window.contextDataActions.embed(${item.id})" title="Embed">
                       <i class="fas fa-vector-square"></i>
                   </button>`;
        }
    }
    
    actions += !item.isArchived
        ? `<button class="btn btn-sm btn-outline-warning" onclick="window.contextDataActions.archive(${item.id})" title="Archive">
               <i class="fas fa-archive"></i>
           </button>`
        : `<button class="btn btn-sm btn-outline-success" onclick="window.contextDataActions.restore(${item.id})" title="Restore">
               <i class="fas fa-undo"></i>
           </button>`;
    
    // Reload button (only if path is set)
    if (item.path) {
        actions += `<button class="btn btn-sm btn-outline-info" onclick="window.contextDataActions.reloadFromDisk(${item.id})" title="Reload from disk">
               <i class="fas fa-sync-alt"></i>
           </button>`;
    }
    
    // Token count display
    let tokenCountHtml = '-';
    if (item.tokenCount != null && item.tokenCount > 0) {
        const tokenTitle = item.tokenCountUpdatedAt 
            ? `Last counted: ${new Date(item.tokenCountUpdatedAt).toLocaleString()}`
            : 'Token count';
        tokenCountHtml = `<span class="text-muted small" title="${tokenTitle}">${item.tokenCount}</span>`;
    } else {
        tokenCountHtml = `<button class="btn btn-sm btn-link p-0 text-muted" onclick="window.contextDataActions.countTokens(${item.id})" title="Count tokens">
            <i class="fas fa-calculator"></i>
        </button>`;
    }
    
    return `
        <tr class="${item.isArchived ? 'archived' : ''} ${isSelected ? 'selected' : ''}" data-id="${item.id}">
            <td>
                <input type="checkbox" class="form-check-input data-checkbox" 
                       ${isSelected ? 'checked' : ''} 
                       onchange="window.contextDataActions.toggleSelection(${item.id}, this.checked)">
            </td>
            <td class="text-muted small">#${item.id}</td>
            <td class="editable-cell" data-field="name" data-id="${item.id}" title="Click to edit name">
                <div class="small fw-semibold editable-value">${escapeHtml(item.name)}</div>
                ${item.speaker ? `<div class="text-muted" style="font-size: 10px;"><i class="fas fa-user"></i> ${escapeHtml(item.speaker)}</div>` : ''}
            </td>
            <td class="editable-cell" data-field="type" data-id="${item.id}" title="Click to change type">
                ${typeBadge}
            </td>
            <td class="editable-cell" data-field="availability" data-id="${item.id}" title="Click to change availability">
                ${availBadge}
            </td>
            ${triggersHtml}
            <td class="editable-cell" data-field="content" data-id="${item.id}" title="Click to edit content">
                <div class="content-cell editable-value">${escapeHtml(contentPreview)}</div>
            </td>
            <td class="tags-cell">${tagsHtml}</td>
            <td class="path-cell editable-cell" data-field="path" data-id="${item.id}" title="${escapeHtml(item.path || 'Click to set path')}">${pathHtml}</td>
            <td class="text-center">${tokenCountHtml}</td>
            <td class="action-btns">${actions}</td>
        </tr>`;
}

function renderDataCard(item) {
    const typeBadge = `<span class="badge-type badge-${item.type.toLowerCase()}">${item.type}</span>`;
    const availBadge = `<span class="badge-availability badge-${item.availability.toLowerCase()}">${item.availability}</span>`;
    const embeddedBadge = item.inVectorDb 
        ? '<span class="badge-embedded"><i class="fas fa-vector-square"></i> Embedded</span>'
        : '<span class="badge-not-embedded">Not Embedded</span>';
    
    // Trigger keywords display
    let triggerHtml = '';
    if (item.availability === 'Trigger' && item.triggerKeywords) {
        const keywords = item.triggerKeywords.split(',').map(k => k.trim()).filter(k => k);
        triggerHtml = `
            <div class="trigger-keywords">
                <small class="text-muted me-2"><i class="fas fa-bolt"></i> Triggers:</small>
                ${keywords.map(k => `<span class="trigger-keyword">${escapeHtml(k)}</span>`).join('')}
            </div>`;
    }
    
    // Tags display
    let tagsHtml = '';
    if (item.tags && item.tags.length > 0) {
        tagsHtml = `
            <div class="tag-list">
                <small class="text-muted me-2"><i class="fas fa-tags"></i></small>
                ${item.tags.map(t => `<span class="tag-item">${escapeHtml(t)}</span>`).join('')}
            </div>`;
    }
    
    // Manual toggle display
    let manualHtml = '';
    if (item.availability === 'Manual') {
        manualHtml = `
            <div class="manual-toggles">
                <label class="form-check form-check-inline mb-0">
                    <input type="checkbox" class="form-check-input" 
                           ${item.useNextTurnOnly ? 'checked' : ''} 
                           onchange="window.contextDataActions.toggleNextTurn(${item.id}, this.checked)">
                    <span class="form-check-label small">Next Turn Only</span>
                </label>
                <label class="form-check form-check-inline mb-0">
                    <input type="checkbox" class="form-check-input" 
                           ${item.useEveryTurn ? 'checked' : ''} 
                           onchange="window.contextDataActions.toggleEveryTurn(${item.id}, this.checked)">
                    <span class="form-check-label small">Every Turn</span>
                </label>
            </div>`;
    }
    
    // Actions based on availability
    let semanticActions = '';
    if (item.availability === 'Semantic') {
        if (item.tags && item.tags.length > 0) {
            semanticActions = item.inVectorDb
                ? `<button class="btn btn-outline-danger btn-sm" onclick="window.contextDataActions.unembed(${item.id})">
                       <i class="fas fa-times"></i> Unembed
                   </button>`
                : `<button class="btn btn-outline-success btn-sm" onclick="window.contextDataActions.embed(${item.id})">
                       <i class="fas fa-vector-square"></i> Embed
                   </button>`;
        }
        semanticActions += `
            <button class="btn btn-outline-info btn-sm" onclick="window.contextDataActions.generateTags(${item.id})">
                <i class="fas fa-tags"></i> ${item.tags && item.tags.length > 0 ? 'Regen Tags' : 'Gen Tags'}
            </button>
            <button class="btn btn-outline-secondary btn-sm" onclick="window.contextDataActions.generateRelevance(${item.id})">
                <i class="fas fa-star"></i> Relevance
            </button>`;
    }
    
    const timeAgo = getTimeAgo(item.modifiedAt || item.createdAt);
    const isSelected = selectedIds.has(item.id);
    
    return `
        <div class="data-card ${item.isArchived ? 'archived' : ''} ${isSelected ? 'selected' : ''}" data-id="${item.id}">
            <div class="data-header">
                <div class="data-title">
                    <input type="checkbox" class="form-check-input data-checkbox me-2" 
                           ${isSelected ? 'checked' : ''} 
                           onchange="window.contextDataActions.toggleSelection(${item.id}, this.checked)">
                    <span class="text-muted small">#${item.id}</span>
                    ${escapeHtml(item.name)}
                </div>
                <div class="data-badges">
                    ${typeBadge}
                    ${availBadge}
                    ${item.availability === 'Semantic' ? embeddedBadge : ''}
                    ${item.isUser ? '<span class="badge bg-primary">User</span>' : ''}
                </div>
            </div>
            
            <div class="data-content" id="content-${item.id}">
                ${escapeHtml(item.content)}
            </div>
            <button class="btn btn-link btn-sm text-muted p-0" onclick="window.contextDataActions.toggleContent(${item.id})">
                <small>Show more/less</small>
            </button>
            
            ${triggerHtml}
            ${tagsHtml}
            ${manualHtml}
            
            <div class="data-meta">
                <span class="meta-item"><i class="fas fa-clock"></i> ${timeAgo}</span>
                ${item.usageCount > 0 ? `<span class="meta-item"><i class="fas fa-chart-bar"></i> Used ${item.usageCount}x</span>` : ''}
                ${item.relevanceScore > 0 ? `<span class="meta-item"><i class="fas fa-star"></i> Relevance: ${item.relevanceScore}</span>` : ''}
                ${item.speaker ? `<span class="meta-item"><i class="fas fa-user"></i> ${escapeHtml(item.speaker)}</span>` : ''}
                ${item.sourceSessionId ? `<span class="meta-item"><i class="fas fa-folder"></i> Session ${item.sourceSessionId}</span>` : ''}
            </div>
            
            <div class="data-actions">
                <button class="btn btn-primary btn-sm" onclick="window.contextDataActions.editItem(${item.id})">
                    <i class="fas fa-edit"></i> Edit
                </button>
                ${semanticActions}
                ${!item.isArchived 
                    ? `<button class="btn btn-warning btn-sm" onclick="window.contextDataActions.archive(${item.id})">
                           <i class="fas fa-archive"></i> Archive
                       </button>`
                    : `<button class="btn btn-success btn-sm" onclick="window.contextDataActions.restore(${item.id})">
                           <i class="fas fa-undo"></i> Restore
                       </button>`
                }
                <button class="btn btn-danger btn-sm" onclick="window.contextDataActions.deleteItem(${item.id})">
                    <i class="fas fa-trash"></i>
                </button>
            </div>
        </div>`;
}

// ============ CRUD Operations ============

function showCreateModal() {
    document.getElementById('modalTitle').textContent = 'Add Context Data';
    document.getElementById('editId').value = '';
    document.getElementById('editName').value = '';
    document.getElementById('editType').value = 'Memory';
    document.getElementById('editAvailability').value = 'Semantic';
    document.getElementById('editContent').value = '';
    document.getElementById('editSummary').value = '';
    document.getElementById('editTriggerKeywords').value = '';
    document.getElementById('editTriggerLookback').value = '3';
    document.getElementById('editTriggerMinMatch').value = '1';
    document.getElementById('editUseNextTurnOnly').checked = false;
    document.getElementById('editUseEveryTurn').checked = false;
    document.getElementById('editSpeaker').value = '';
    document.getElementById('editSubtype').value = '';
    document.getElementById('editPath').value = '';
    document.getElementById('editTags').value = '';
    document.getElementById('editSortOrder').value = '0';
    document.getElementById('editDescription').value = '';
    document.getElementById('editIsEnabled').checked = true;
    document.getElementById('editIsUser').checked = false;
    
    handleAvailabilityFieldChange();
    editModalInstance.show();
}

async function editItem(id) {
const item = allData.find(d => d.id === id);
if (!item) return;
    
document.getElementById('modalTitle').textContent = 'Edit Context Data';
document.getElementById('editId').value = item.id;
document.getElementById('editName').value = item.name;
document.getElementById('editType').value = item.type;
document.getElementById('editAvailability').value = item.availability;
document.getElementById('editContent').value = item.content;
document.getElementById('editSummary').value = item.summary || '';
document.getElementById('editTriggerKeywords').value = item.triggerKeywords || '';
document.getElementById('editTriggerLookback').value = item.triggerLookbackTurns || 3;
document.getElementById('editTriggerMinMatch').value = item.triggerMinMatchCount || 1;
document.getElementById('editUseNextTurnOnly').checked = item.useNextTurnOnly;
document.getElementById('editUseEveryTurn').checked = item.useEveryTurn;
document.getElementById('editSpeaker').value = item.speaker || '';
document.getElementById('editSubtype').value = item.subtype || '';
document.getElementById('editPath').value = item.path || '';
document.getElementById('editTags').value = (item.tags || []).join(', ');
document.getElementById('editSortOrder').value = item.sortOrder || 0;
document.getElementById('editDescription').value = item.description || '';
document.getElementById('editIsEnabled').checked = item.isEnabled;
document.getElementById('editIsUser').checked = item.isUser;
    
handleAvailabilityFieldChange();
editModalInstance.show();
}

async function saveItem() {
const id = document.getElementById('editId').value;
const isNew = !id;
    
// Build the payload - use exact property names from C# entity (PascalCase)
const payload = {
    name: document.getElementById('editName').value.trim(),
    type: document.getElementById('editType').value,
    availability: document.getElementById('editAvailability').value,
    content: document.getElementById('editContent').value.trim(),
    summary: document.getElementById('editSummary').value.trim() || null,
    triggerKeywords: document.getElementById('editTriggerKeywords').value.trim() || null,
    triggerLookbackTurns: parseInt(document.getElementById('editTriggerLookback').value) || 3,
    triggerMinMatchCount: parseInt(document.getElementById('editTriggerMinMatch').value) || 1,
    useNextTurnOnly: document.getElementById('editUseNextTurnOnly').checked,
    useEveryTurn: document.getElementById('editUseEveryTurn').checked,
    speaker: document.getElementById('editSpeaker').value.trim() || null,
    subtype: document.getElementById('editSubtype').value.trim() || null,
    path: document.getElementById('editPath').value.trim() || null,
    tags: document.getElementById('editTags').value.split(',').map(t => t.trim()).filter(t => t),
    sortOrder: parseInt(document.getElementById('editSortOrder').value) || 0,
    description: document.getElementById('editDescription').value.trim() || null,
    isEnabled: document.getElementById('editIsEnabled').checked,
    isUser: document.getElementById('editIsUser').checked
};
    
    
if (!payload.name || !payload.content) {
    showError('Name and Content are required');
    return;
}
    
try {
    const url = isNew ? API_BASE_URL : `${API_BASE_URL}/${id}`;
    const method = isNew ? 'POST' : 'PUT';
        
    console.log('Saving item:', { id, url, method, payload });
        
    const response = await fetch(url, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });
        
        if (!response.ok) {
            let errorMessage = 'Failed to save';
            try {
                const err = await response.json();
                console.error('Save failed with error:', err);
                
                // Handle ASP.NET Core validation errors
                if (err.errors && typeof err.errors === 'object') {
                    const validationErrors = [];
                    for (const [field, messages] of Object.entries(err.errors)) {
                        if (Array.isArray(messages)) {
                            validationErrors.push(`${field}: ${messages.join(', ')}`);
                        } else {
                            validationErrors.push(`${field}: ${messages}`);
                        }
                    }
                    errorMessage = validationErrors.length > 0 
                        ? validationErrors.join('\n') 
                        : (err.title || 'Validation failed');
                } else {
                    errorMessage = err.error || err.message || err.title || JSON.stringify(err);
                }
            } catch (parseError) {
                // If JSON parsing fails, get the text response
                const errorText = await response.text();
                errorMessage = errorText || `HTTP ${response.status}: ${response.statusText}`;
                console.error('Save failed, raw response:', errorText);
            }
            throw new Error(errorMessage);
        }
        
        editModalInstance.hide();
        showMessage(isNew ? 'Item created successfully' : 'Item updated successfully');
        await loadData();
        loadStats();
    } catch (error) {
        console.error('Error in saveItem:', error);
        showError(error.message);
    }
}

async function deleteItem(id) {
    if (!confirm('Are you sure you want to delete this item? This cannot be undone.')) return;
    
    try {
        const response = await fetch(`${API_BASE_URL}/${id}`, { method: 'DELETE' });
        if (!response.ok) throw new Error('Failed to delete');
        
        showMessage('Item deleted');
        await loadData();
        loadStats();
    } catch (error) {
        showError(error.message);
    }
}

async function archive(id) {
    try {
        const response = await fetch(`${API_BASE_URL}/${id}/archive`, { method: 'POST' });
        if (!response.ok) throw new Error('Failed to archive');
        
        showMessage('Item archived');
        await loadData();
        loadStats();
    } catch (error) {
        showError(error.message);
    }
}

async function restore(id) {
    try {
        const response = await fetch(`${API_BASE_URL}/${id}/restore`, { method: 'POST' });
        if (!response.ok) throw new Error('Failed to restore');
        
        showMessage('Item restored');
        await loadData();
        loadStats();
    } catch (error) {
        showError(error.message);
    }
}

// ============ Availability & Manual Toggles ============

function handleAvailabilityFieldChange() {
    const availability = document.getElementById('editAvailability').value;
    
    document.querySelectorAll('.trigger-fields').forEach(el => {
        el.style.display = availability === 'Trigger' ? 'block' : 'none';
    });
    
    document.querySelectorAll('.manual-fields').forEach(el => {
        el.style.display = availability === 'Manual' ? 'block' : 'none';
    });
}

async function changeAvailability(id, newAvailability, confirmUnembed = false) {
    const item = allData.find(d => d.id === id);
    if (!item) return;
    
    // Check if changing from Semantic and item is embedded
    if (item.availability === 'Semantic' && newAvailability !== 'Semantic' && item.inVectorDb && !confirmUnembed) {
        pendingAvailabilityChange = { id, newAvailability };
        document.getElementById('availabilityWarningText').textContent = 
            `You are changing "${item.name}" from Semantic to ${newAvailability}.`;
        confirmModalInstance.show();
        return;
    }
    
    try {
        const response = await fetch(`${API_BASE_URL}/${id}/availability`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ availability: newAvailability, confirmUnembed })
        });
        
        const result = await response.json();
        
        if (!result.success) {
            showError(result.message);
            return;
        }
        
        if (result.wasUnembedded) {
            showMessage('Availability changed and item was unembedded', 'info');
        } else {
            showMessage('Availability changed');
        }
        
        await loadData();
        loadStats();
    } catch (error) {
        showError(error.message);
    }
}

function confirmAvailabilityChange() {
    if (pendingAvailabilityChange) {
        changeAvailability(pendingAvailabilityChange.id, pendingAvailabilityChange.newAvailability, true);
        pendingAvailabilityChange = null;
    }
    confirmModalInstance.hide();
}

// ============ Trigger Management ============

async function manageTriggers(contextDataId) {
    const item = allData.find(d => d.id === contextDataId);
    if (!item) return;
    
    // Show modal or inline editor for advanced trigger management
    const keywords = item.triggerKeywords || '';
    const lookback = item.triggerLookbackTurns || 3;
    const minMatch = item.triggerMinMatchCount || 1;
    
    const newKeywords = prompt(
        `Edit trigger keywords for "${item.name}"\n\nEnter keywords separated by commas:\n(Lookback: ${lookback} turns, Min matches: ${minMatch})`,
        keywords
    );
    
    if (newKeywords === null) return; // Cancelled
    
    try {
        // Build a clean update payload
        const updatePayload = {
            name: item.name,
            type: item.type,
            availability: item.availability,
            content: item.content,
            summary: item.summary || null,
            triggerKeywords: newKeywords.trim(),
            triggerLookbackTurns: item.triggerLookbackTurns || 3,
            triggerMinMatchCount: item.triggerMinMatchCount || 1,
            useNextTurnOnly: item.useNextTurnOnly || false,
            useEveryTurn: item.useEveryTurn || false,
            speaker: item.speaker || null,
            subtype: item.subtype || null,
            path: item.path || null,
            tags: item.tags || [],
            sortOrder: item.sortOrder || 0,
            description: item.description || null,
            isEnabled: item.isEnabled !== false,
            isUser: item.isUser || false
        };
        
        const response = await fetch(`${API_BASE_URL}/${contextDataId}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(updatePayload)
        });
        
        if (!response.ok) throw new Error('Failed to update trigger keywords');
        
        showMessage('Trigger keywords updated');
        await loadData();
    } catch (error) {
        showError(error.message);
    }
}

async function updateTrigger(id, keywords) {
    try {
        const item = allData.find(d => d.id === id);
        if (!item) return;
        
        // Build a clean update payload
        const updatePayload = {
            name: item.name,
            type: item.type,
            availability: item.availability,
            content: item.content,
            summary: item.summary || null,
            triggerKeywords: keywords,
            triggerLookbackTurns: item.triggerLookbackTurns || 3,
            triggerMinMatchCount: item.triggerMinMatchCount || 1,
            useNextTurnOnly: item.useNextTurnOnly || false,
            useEveryTurn: item.useEveryTurn || false,
            speaker: item.speaker || null,
            subtype: item.subtype || null,
            path: item.path || null,
            tags: item.tags || [],
            sortOrder: item.sortOrder || 0,
            description: item.description || null,
            isEnabled: item.isEnabled !== false,
            isUser: item.isUser || false
        };
        
        const response = await fetch(`${API_BASE_URL}/${id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(updatePayload)
        });
        
        if (!response.ok) throw new Error('Failed to update trigger');
        
        showMessage('Trigger keywords updated');
        await loadData();
    } catch (error) {
        showError(error.message);
    }
}

// ============ Tags & Embedding ============

async function generateTags(id) {
    try {
        showMessage('Generating tags and relevance...', 'info');
        
        const response = await fetch(`${API_BASE_URL}/${id}/generate-tags`, { method: 'POST' });
        const result = await response.json();
        
        if (!result.success) {
            showError(result.message);
            return;
        }
        
        showMessage(`Generated ${result.tags.length} tags, relevance score: ${result.relevanceScore}`);
        await loadData();
        loadStats();
    } catch (error) {
        showError(error.message);
    }
}

async function embed(id) {
    try {
        showMessage('Embedding...', 'info');
        
        const response = await fetch(`${API_BASE_URL}/${id}/embed`, { method: 'POST' });
        const result = await response.json();
        
        if (!result.success) {
            showError(result.message);
            return;
        }
        
        showMessage('Successfully embedded');
        await loadData();
        loadStats();
    } catch (error) {
        showError(error.message);
    }
}

async function unembed(id) {
    if (!confirm('Are you sure you want to remove this item from the vector database?')) return;
    
    try {
        const response = await fetch(`${API_BASE_URL}/${id}/unembed`, { method: 'POST' });
        const result = await response.json();
        
        if (!result.success) {
            showError(result.message);
            return;
        }
        
        showMessage('Removed from vector database');
        await loadData();
        loadStats();
    } catch (error) {
        showError(error.message);
    }
}

async function bulkGenerateTags() {
    const semanticWithoutTags = filteredData.filter(d => 
        d.availability === 'Semantic' && (!d.tags || d.tags.length === 0)
    );
    
    if (semanticWithoutTags.length === 0) {
        showMessage('No Semantic items without tags found', 'info');
        return;
    }
    
    if (!confirm(`Generate tags for ${semanticWithoutTags.length} items using parallel processing? This may take a few minutes.`)) return;
    
    try {
        showMessage(`Starting parallel tag generation for ${semanticWithoutTags.length} items...`, 'info');
        
        const response = await fetch(`${API_BASE_URL}/generate-tags-bulk`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(semanticWithoutTags.map(d => d.id))
        });
        
        const result = await response.json();
        
        if (!response.ok) {
            showError(result.message || 'Failed to generate tags');
            if (result.errors && result.errors.length > 0) {
                console.error('Tag generation errors:', result.errors);
            }
            return;
        }
        
        showMessage(result.message);
        
        if (result.errors && result.errors.length > 0) {
            console.warn('Some items failed:', result.errors);
        }
        
        if (result.processedItems && result.processedItems.length > 0) {
            console.log('Successfully processed:', result.processedItems);
        }
        
        await loadData();
    } catch (error) {
        showError(error.message);
    }
}

async function bulkEmbed() {
    const typeFilter = currentTypeFilter === 'all' ? null : currentTypeFilter;
    const url = typeFilter 
        ? `${API_BASE_URL}/embed-all?type=${typeFilter}` 
        : `${API_BASE_URL}/embed-all`;
    
    if (!confirm('Embed all tagged Semantic items that are not yet embedded? This uses optimized bulk processing.')) return;
    
    try {
        showMessage('Bulk embedding (optimized)...', 'info');
        
        const response = await fetch(url, { method: 'POST' });
        const result = await response.json();
        
        showMessage(result.message);
        
        if (result.errors && result.errors.length > 0) {
            console.warn('Embed errors:', result.errors);
        }
        
        await loadData();
        loadStats();
    } catch (error) {
        showError(error.message);
    }
}

// ============ Import Operations ============

function toggleImportSection() {
    importSection.classList.toggle('active');
}

async function importQuotesTsv() {
    const content = document.getElementById('tsvQuotesContent').value.trim();
    if (!content) {
        showError('Please paste TSV content');
        return;
    }
    
    const speaker = document.getElementById('tsvQuotesSpeaker').value.trim();
    
    const request = {
        content,
        dataType: 'Quote', // Fixed to Quote
        defaultAvailability: document.getElementById('tsvQuotesAvailability').value,
        hasHeader: document.getElementById('tsvQuotesHasHeader').checked,
        speaker: speaker || null
    };
    
    try {
        showMessage('Importing quotes...', 'info');
        
        const response = await fetch(`${API_BASE_URL}/import/tsv`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(request)
        });
        
        const result = await response.json();
        showMessage(result.message);
        
        if (result.errors && result.errors.length > 0) {
            console.warn('Import errors:', result.errors);
        }
        
        document.getElementById('tsvQuotesContent').value = '';
        document.getElementById('tsvQuotesSpeaker').value = '';
        await loadData();
        loadStats();
    } catch (error) {
        showError(error.message);
    }
}

async function importVoiceTsv() {
    const content = document.getElementById('tsvVoiceContent').value.trim();
    if (!content) {
        showError('Please paste TSV content');
        return;
    }
    
    const speaker = document.getElementById('tsvVoiceSpeaker').value.trim();
    
    const request = {
        content,
        defaultAvailability: document.getElementById('tsvVoiceAvailability').value,
        hasHeader: document.getElementById('tsvVoiceHasHeader').checked,
        speaker: speaker || null
    };
    
    try {
        showMessage('Importing voice samples...', 'info');
        
        const response = await fetch(`${API_BASE_URL}/import/voice-samples-tsv`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(request)
        });
        
        const result = await response.json();
        showMessage(result.message);
        
        if (result.errors && result.errors.length > 0) {
            console.warn('Import errors:', result.errors);
        }
        
        document.getElementById('tsvVoiceContent').value = '';
        document.getElementById('tsvVoiceSpeaker').value = '';
        await loadData();
        loadStats();
    } catch (error) {
        showError(error.message);
    }
}

async function importMarkdown() {
    const content = document.getElementById('markdownContent').value.trim();
    if (!content) {
        showError('Please paste Markdown content');
        return;
    }
    
    const request = {
        content,
        dataType: document.getElementById('markdownDataType').value,
        defaultAvailability: document.getElementById('markdownAvailability').value
    };
    
    try {
        showMessage('Importing...', 'info');
        
        const response = await fetch(`${API_BASE_URL}/import/markdown`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(request)
        });
        
        const result = await response.json();
        showMessage(result.message);
        
        document.getElementById('markdownContent').value = '';
        await loadData();
        loadStats();
    } catch (error) {
        showError(error.message);
    }
}

async function importFolder() {
    const folderPath = document.getElementById('folderPath').value.trim();
    if (!folderPath) {
        showError('Please enter a folder path');
        return;
    }
    
    const extensions = document.getElementById('folderExtensions').value
        .split(',')
        .map(e => e.trim())
        .filter(e => e);
    
    const updateExisting = document.getElementById('folderUpdateExisting')?.checked || false;
    
    const request = {
        folderPath,
        dataType: document.getElementById('folderDataType').value,
        defaultAvailability: document.getElementById('folderAvailability').value,
        extensions: extensions.length > 0 ? extensions : null,
        updateExisting
    };
    
    try {
        if (updateExisting) {
            showMessage('Looking for existing items to update...', 'info');
        } else {
            showMessage('Importing new items...', 'info');
        }
        
        const response = await fetch(`${API_BASE_URL}/import/folder`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(request)
        });
        
        const result = await response.json();
        
        if (updateExisting && result.updated > 0) {
            showMessage(`${result.message} (Updated: ${result.updated}, Not found: ${result.notFound || 0})`);
        } else {
            showMessage(result.message);
        }
        
        await loadData();
        loadStats();
    } catch (error) {
        showError(error.message);
    }
}


// ============ Utility Functions ============

function toggleContent(id) {
    const el = document.getElementById(`content-${id}`);
    if (el) {
        el.classList.toggle('expanded');
    }
}

function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

// ============ Selection Functions ============

function toggleSelection(id, isSelected) {
    if (isSelected) {
        selectedIds.add(id);
    } else {
        selectedIds.delete(id);
    }
    updateSelectionUI();
}

function toggleSelectAll(isSelected) {
    selectedIds.clear();
    if (isSelected) {
        filteredData.forEach(item => selectedIds.add(item.id));
    }
    renderData();
    updateSelectionUI();
}

function clearSelection() {
    selectedIds.clear();
    document.getElementById('selectAll').checked = false;
    renderData();
    updateSelectionUI();
}

function updateSelectionUI() {
    const toolbar = document.getElementById('selectionToolbar');
    const countEl = document.getElementById('selectedCount');
    const selectAllCheckbox = document.getElementById('selectAll');
    
    if (selectedIds.size > 0) {
        toolbar.style.display = 'block';
        countEl.textContent = selectedIds.size;
    } else {
        toolbar.style.display = 'none';
    }
    
    // Update select all checkbox state
    if (filteredData.length > 0 && selectedIds.size === filteredData.length) {
        selectAllCheckbox.checked = true;
        selectAllCheckbox.indeterminate = false;
    } else if (selectedIds.size > 0) {
        selectAllCheckbox.checked = false;
        selectAllCheckbox.indeterminate = true;
    } else {
        selectAllCheckbox.checked = false;
        selectAllCheckbox.indeterminate = false;
    }
    
    // Update card visual state
    document.querySelectorAll('.data-card').forEach(card => {
        const id = parseInt(card.dataset.id);
        const checkbox = card.querySelector('.data-checkbox');
        if (selectedIds.has(id)) {
            card.classList.add('selected');
            if (checkbox) checkbox.checked = true;
        } else {
            card.classList.remove('selected');
            if (checkbox) checkbox.checked = false;
        }
    });
}

function bulkChangeAvailability() {
    if (selectedIds.size === 0) {
        showError('No items selected');
        return;
    }
    document.getElementById('bulkAvailabilityCount').textContent = selectedIds.size;
    bulkAvailabilityModalInstance.show();
}

async function confirmBulkAvailability() {
    const newAvailability = document.getElementById('bulkAvailabilitySelect').value;
    const ids = Array.from(selectedIds);
    
    bulkAvailabilityModalInstance.hide();
    
    let successCount = 0;
    let errorCount = 0;
    
    for (const id of ids) {
        try {
            const response = await fetch(`${API_BASE_URL}/${id}/availability`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ availability: newAvailability, confirmUnembed: true })
            });
            if (response.ok) {
                successCount++;
            } else {
                errorCount++;
            }
        } catch (error) {
            errorCount++;
        }
    }
    
    showMessage(`Availability changed: ${successCount} succeeded, ${errorCount} failed`);
    clearSelection();
    await loadData();
    loadStats();
}

async function bulkTagSelected() {
    if (selectedIds.size === 0) {
        showError('No items selected');
        return;
    }
    
    const ids = Array.from(selectedIds);
    
    if (!confirm(`Generate tags for ${ids.length} selected items using parallel processing? This may take a few minutes.`)) {
        return;
    }
    
    try {
        showMessage(`Starting parallel tag generation for ${ids.length} items...`, 'info');
        
        const response = await fetch(`${API_BASE_URL}/generate-tags-bulk`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(ids)
        });
        
        const result = await response.json();
        
        if (!response.ok) {
            showError(result.message || 'Failed to generate tags');
            if (result.errors && result.errors.length > 0) {
                console.error('Tag generation errors:', result.errors);
            }
            return;
        }
        
        showMessage(result.message);
        
        if (result.errors && result.errors.length > 0) {
            console.warn('Some items failed:', result.errors);
        }
        
        if (result.processedItems && result.processedItems.length > 0) {
            console.log('Successfully processed:', result.processedItems);
        }
        
        clearSelection();
        await loadData();
    } catch (error) {
        showError(error.message);
    }
}

async function bulkEmbedSelected() {
    if (selectedIds.size === 0) {
        showError('No items selected');
        return;
    }
    
    const ids = Array.from(selectedIds);
    
    if (!confirm(`Embed ${ids.length} selected items? This will use bulk embedding for better performance.`)) {
        return;
    }
    
    try {
        showMessage(`Embedding ${ids.length} items in bulk...`, 'info');
        
        const response = await fetch(`${API_BASE_URL}/embed-selected`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(ids)
        });
        
        const result = await response.json();
        
        if (!response.ok) {
            showError(result.message || 'Failed to embed selected items');
            return;
        }
        
        showMessage(result.message);
        
        if (result.errors && result.errors.length > 0) {
            console.warn('Embed errors:', result.errors);
        }
        
        clearSelection();
        await loadData();
        loadStats();
    } catch (error) {
        showError(error.message);
    }
}

async function bulkUnembedSelected() {
    if (selectedIds.size === 0) {
        showError('No items selected');
        return;
    }
    
    const ids = Array.from(selectedIds);
    
    if (!confirm(`Unembed ${ids.length} selected items? This will remove them from the vector database.`)) {
        return;
    }
    
    try {
        showMessage(`Unembedding ${ids.length} items in bulk...`, 'info');
        
        const response = await fetch(`${API_BASE_URL}/unembed-selected`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(ids)
        });
        
        const result = await response.json();
        
        if (!response.ok) {
            showError(result.message || 'Failed to unembed selected items');
            return;
        }
        
        showMessage(result.message);
        
        if (result.errors && result.errors.length > 0) {
            console.warn('Unembed errors:', result.errors);
        }
        
        clearSelection();
        await loadData();
        loadStats();
    } catch (error) {
        showError(error.message);
    }
}

async function bulkArchiveSelected() {
    if (selectedIds.size === 0) {
        showError('No items selected');
        return;
    }
    
    if (!confirm(`Archive ${selectedIds.size} selected items?`)) {
        return;
    }
    
    const ids = Array.from(selectedIds);
    let successCount = 0;
    let errorCount = 0;
    
    for (const id of ids) {
        try {
            const response = await fetch(`${API_BASE_URL}/${id}/archive`, { method: 'POST' });
            if (response.ok) {
                successCount++;
            } else {
                errorCount++;
            }
        } catch (error) {
            errorCount++;
        }
    }
    
    showMessage(`Archived: ${successCount} succeeded, ${errorCount} failed`);
    clearSelection();
    await loadData();
    loadStats();
}

// ============ Token Counting ============

async function countTokens(id) {
    try {
        showMessage('Counting tokens...', 'info');
        
        const response = await fetch(`${API_BASE_URL}/${id}/count-tokens`, { 
            method: 'POST' 
        });
        
        if (!response.ok) {
            throw new Error('Failed to count tokens');
        }
        
        const result = await response.json();
        showMessage(`${result.name}: ${result.tokenCount} tokens (${result.contentLength} chars)`);
    } catch (error) {
        showError(error.message);
    }
}

async function bulkCountTokens() {
    if (selectedIds.size === 0) {
        showError('No items selected');
        return;
    }
    
    const ids = Array.from(selectedIds);
    
    try {
        showMessage(`Counting tokens for ${ids.length} items...`, 'info');
        
        const response = await fetch(`${API_BASE_URL}/count-tokens-bulk`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(ids)
        });
        
        if (!response.ok) {
            throw new Error('Failed to count tokens');
        }
        
        const results = await response.json();
        const totalTokens = results.reduce((sum, r) => sum + r.tokenCount, 0);
        const totalChars = results.reduce((sum, r) => sum + r.contentLength, 0);
        
        showMessage(`Total: ${totalTokens} tokens across ${results.length} items (${totalChars} chars)`);
        console.log('Token counts:', results);
    } catch (error) {
        showError(error.message);
    }
}

// ============ Manual Toggles ============

async function toggleNextTurn(id) {
    try {
        const item = allData.find(d => d.id === id);
        if (!item) return;
        
        // Toggling ON: set UseNextTurnOnly
        if (!item.useNextTurnOnly) {
            showMessage('Setting to use next turn only...', 'info');
            const response = await fetch(`${API_BASE_URL}/${id}/use-next-turn`, { 
                method: 'POST' 
            });
            
            if (!response.ok) throw new Error('Failed to set use next turn');
            
            showMessage('Will be used in next turn only');
        } else {
            // Toggling OFF: clear all manual flags and restore previous availability
            showMessage('Clearing manual flag...', 'info');
            const response = await fetch(`${API_BASE_URL}/${id}/clear-manual`, { 
                method: 'POST' 
            });
            
            if (!response.ok) throw new Error('Failed to clear manual flag');
            
            showMessage('Manual flag cleared');
        }
        
        await loadData();
        loadStats();
    } catch (error) {
        showError(error.message);
    }
}

async function toggleEveryTurn(id, enabled) {
    try {
        const item = allData.find(d => d.id === id);
        if (!item) return;
        
        showMessage(enabled ? 'Setting to use every turn...' : 'Clearing manual flag...', 'info');
        
        const response = await fetch(`${API_BASE_URL}/${id}/use-every-turn?enabled=${enabled}`, { 
            method: 'POST' 
        });
        
        if (!response.ok) throw new Error('Failed to toggle use every turn');
        
        showMessage(enabled ? 'Will be used every turn' : 'Manual flag cleared');
        await loadData();
        loadStats();
    } catch (error) {
        showError(error.message);
    }
}

// ============ Reload from Disk ============

async function reloadFromDisk(id) {
    try {
        const item = allData.find(d => d.id === id);
        if (!item || !item.path) {
            showError('No path set for this item');
            return;
        }
        
        showMessage('Reloading content from disk...', 'info');
        
        const response = await fetch(`${API_BASE_URL}/${id}/reload-from-disk`, {
            method: 'POST'
        });
        
        if (!response.ok) {
            const error = await response.text();
            throw new Error(error || 'Failed to reload from disk');
        }
        
        showMessage('Content reloaded successfully');
        await loadData();
    } catch (error) {
        showError(error.message);
    }
}

async function bulkReloadFromDisk() {
    if (!confirm('Reload all items with paths from disk? This will update their content.')) return;
    
    try {
        showMessage('Reloading all items from disk...', 'info');
        
        const response = await fetch(`${API_BASE_URL}/bulk-reload-from-disk`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' }
        });
        
        if (!response.ok) {
            const error = await response.text();
            throw new Error(error || 'Failed to reload from disk');
        }
        
        const result = await response.json();
        showMessage(`Reloaded ${result.reloaded} items successfully`);
        await loadData();
    } catch (error) {
        showError(error.message);
    }
}

async function bulkReloadSelectedFromDisk() {
    if (selectedIds.size === 0) {
        showError('No items selected');
        return;
    }
    
    const itemsWithPaths = Array.from(selectedIds).filter(id => {
        const item = allData.find(d => d.id === id);
        return item && item.path;
    });
    
    if (itemsWithPaths.length === 0) {
        showError('None of the selected items have paths set');
        return;
    }
    
    if (!confirm(`Reload ${itemsWithPaths.length} selected items from disk?`)) return;
    
    try {
        showMessage('Reloading selected items from disk...', 'info');
        
        const response = await fetch(`${API_BASE_URL}/bulk-reload-selected-from-disk`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ ids: itemsWithPaths })
        });
        
        if (!response.ok) {
            const error = await response.text();
            throw new Error(error || 'Failed to reload from disk');
        }
        
        const result = await response.json();
        showMessage(`Reloaded ${result.reloaded} items successfully`);
        await loadData();
        clearSelection();
    } catch (error) {
        showError(error.message);
    }
}

// ============ Export to window ============

window.contextDataActions = {
    showCreateModal,
    editItem,
    saveItem,
    deleteItem,
    archive,
    restore,
    toggleNextTurn,
    toggleEveryTurn,
    updateTrigger,
    manageTriggers,
    generateTags,
    embed,
    unembed,
    bulkGenerateTags,
    bulkEmbed,
    toggleImportSection,
    importQuotesTsv,
    importVoiceTsv,
    importMarkdown,
    importFolder,
    toggleContent,
    changeAvailability,
    countTokens,
    bulkCountTokens,
    reloadFromDisk,
    bulkReloadFromDisk,
    bulkReloadSelectedFromDisk,
    // Selection functions
    toggleSelection,
    toggleSelectAll,
    clearSelection,
    bulkChangeAvailability,
    confirmBulkAvailability,
    bulkTagSelected,
    bulkEmbedSelected,
    bulkUnembedSelected,
    bulkArchiveSelected
};

// ============ Inline Editing ============

document.addEventListener('click', async function(e) {
    const cell = e.target.closest('.editable-cell');
    if (!cell) return;
    
    // Don't edit if already editing
    if (cell.querySelector('input, select, textarea')) return;
    
    const itemId = parseInt(cell.dataset.id);
    const field = cell.dataset.field;
    const item = allData.find(d => d.id === itemId);
    if (!item) return;
    
    let currentValue;
    if (field === 'triggerKeywords') {
        currentValue = item.triggerKeywords || '';
    } else {
        const valueElement = cell.querySelector('.editable-value');
        currentValue = valueElement ? valueElement.textContent.trim() : cell.textContent.trim();
    }
    
    // Create appropriate input based on field
    let inputHtml = '';
    
    switch (field) {
        case 'type':
            inputHtml = `
                <select class="form-select form-select-sm inline-edit-input">
                    <option value="Quote" ${item.type === 'Quote' ? 'selected' : ''}>Quote</option>
                    <option value="Memory" ${item.type === 'Memory' ? 'selected' : ''}>Memory</option>
                    <option value="Insight" ${item.type === 'Insight' ? 'selected' : ''}>Insight</option>
                    <option value="CharacterProfile" ${item.type === 'CharacterProfile' ? 'selected' : ''}>Character Profile</option>
                    <option value="PersonaVoiceSample" ${item.type === 'PersonaVoiceSample' ? 'selected' : ''}>Voice Sample</option>
                    <option value="Generic" ${item.type === 'Generic' ? 'selected' : ''}>Generic Data</option>
                </select>`;
            break;
            
        case 'availability':
            inputHtml = `
                <select class="form-select form-select-sm inline-edit-input">
                    <option value="AlwaysOn" ${item.availability === 'AlwaysOn' ? 'selected' : ''}>Always On</option>
                    <option value="Manual" ${item.availability === 'Manual' ? 'selected' : ''}>Manual</option>
                    <option value="Semantic" ${item.availability === 'Semantic' ? 'selected' : ''}>Semantic</option>
                    <option value="Trigger" ${item.availability === 'Trigger' ? 'selected' : ''}>Trigger</option>
                    <option value="Archive" ${item.availability === 'Archive' ? 'selected' : ''}>Archive</option>
                </select>`;
            break;
            
        case 'content':
            inputHtml = `<textarea class="form-control form-control-sm inline-edit-input" rows="3">${escapeHtml(item.content)}</textarea>`;
            break;
            
        case 'name':
            inputHtml = `<input type="text" class="form-control form-control-sm inline-edit-input" value="${escapeHtml(item.name)}">`;
            break;
            
        case 'triggerKeywords':
            inputHtml = `<input type="text" class="form-control form-control-sm inline-edit-input" value="${escapeHtml(currentValue)}" placeholder="keyword1, keyword2">`;
            break;
            
        case 'path':
            inputHtml = `<input type="text" class="form-control form-control-sm inline-edit-input" value="${escapeHtml(item.path || '')}" placeholder="Path to file">`;
            break;
            
        default:
            return;
    }
    
    // Replace cell content with input
    const originalHtml = cell.innerHTML;
    cell.innerHTML = inputHtml;
    
    const input = cell.querySelector('.inline-edit-input');
    input.focus();
    if (input.select) input.select();
    
    // Handle save on blur or Enter
    const saveEdit = async () => {
        const newValue = input.value.trim();
        
        // Restore original if no change (allow empty for trigger keywords)
        if (newValue === currentValue || (field !== 'triggerKeywords' && field !== 'content' && !newValue)) {
            cell.innerHTML = originalHtml;
            return;
        }
        
        try {
            // Build a clean update payload with only the necessary fields
            const updatePayload = {
                name: item.name,
                type: item.type,
                availability: item.availability,
                content: item.content,
                summary: item.summary || null,
                triggerKeywords: item.triggerKeywords || null,
                triggerLookbackTurns: item.triggerLookbackTurns || 3,
                triggerMinMatchCount: item.triggerMinMatchCount || 1,
                useNextTurnOnly: item.useNextTurnOnly || false,
                useEveryTurn: item.useEveryTurn || false,
                speaker: item.speaker || null,
                subtype: item.subtype || null,
                path: item.path || null,
                tags: item.tags || [],
                sortOrder: item.sortOrder || 0,
                description: item.description || null,
                isEnabled: item.isEnabled !== false,
                isUser: item.isUser || false
            };
            
            // Update the specific field that changed
            switch (field) {
                case 'type':
                    updatePayload.type = newValue;
                    break;
                case 'availability':
                    // Use the changeAvailability function which handles unembedding
                    cell.innerHTML = originalHtml;
                    await changeAvailability(itemId, newValue);
                    return; // Don't proceed with normal update
                case 'content':
                    updatePayload.content = newValue;
                    break;
                case 'name':
                    updatePayload.name = newValue;
                    break;
                case 'triggerKeywords':
                    updatePayload.triggerKeywords = newValue;
                    break;
                case 'path':
                    updatePayload.path = newValue || null;
                    break;
            }
            
            // Save to backend
            const response = await fetch(`${API_BASE_URL}/${itemId}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(updatePayload)
            });
            
            if (!response.ok) {
                let errorMessage = 'Failed to update';
                try {
                    const err = await response.json();
                    if (err.errors && typeof err.errors === 'object') {
                        const validationErrors = [];
                        for (const [field, messages] of Object.entries(err.errors)) {
                            if (Array.isArray(messages)) {
                                validationErrors.push(`${field}: ${messages.join(', ')}`);
                            } else {
                                validationErrors.push(`${field}: ${messages}`);
                            }
                        }
                        errorMessage = validationErrors.join('\n') || err.title || 'Validation failed';
                    } else {
                        errorMessage = err.error || err.message || err.title || JSON.stringify(err);
                    }
                } catch (parseError) {
                    errorMessage = await response.text() || `HTTP ${response.status}: ${response.statusText}`;
                }
                throw new Error(errorMessage);
            }
            
            showMessage(`${field === 'triggerKeywords' ? 'Trigger keywords' : field} updated successfully`);
            await loadData();
        } catch (error) {
            showError(error.message);
            cell.innerHTML = originalHtml;
        }
    };
    
    input.addEventListener('blur', saveEdit);
    input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && field !== 'content') {
            e.preventDefault();
            input.blur();
        }
        if (e.key === 'Escape') {
            cell.innerHTML = originalHtml;
        }
    });
});

