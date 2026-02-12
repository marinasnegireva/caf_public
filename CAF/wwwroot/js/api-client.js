/**
 * API Client Module
 * Handles all API calls for system messages
 */

import { showMessage, getCurrentTabType, TYPE_MAP } from './utils.js';

const API_BASE = '/api/systemmessages';
let activeProfileId = null;

/**
 * Fetch the active profile ID
 */
export async function fetchActiveProfile() {
    try {
        const response = await fetch('/api/profiles/active');
        if (response.ok) {
            const profile = await response.json();
            activeProfileId = profile.id;
            console.log('Active profile:', profile.name, 'ID:', activeProfileId);
        } else if (response.status === 404) {
            // No active profile - this is OK, use profile 0 (global)
            console.log('No active profile found - using global profile (ID: 0)');
            activeProfileId = 0;
        } else {
            console.warn('Error response when fetching profile:', response.status);
            activeProfileId = 0;
        }
    } catch (error) {
        console.error('Error fetching active profile:', error);
        activeProfileId = 0; // Fallback to global
    }
    return activeProfileId;
}

function normalizeMessage(m) {
    return {
        ...m,
        id: m.id ?? m.id ?? m.Id,
        name: m.name ?? m.Name ?? '',
        content: m.content ?? m.Content ?? '',
        type: m.type ?? m.Type ?? '',
        description: m.description ?? m.Description ?? '',
        tags: m.tags ?? m.Tags ?? [],
        notes: m.notes ?? m.Notes ?? '',
        isActive: m.isActive ?? m.IsActive ?? false,
        isUserProfile: m.isUserProfile ?? m.IsUserProfile ?? false,
        attachedToPersonas: m.attachedToPersonas ?? m.AttachedToPersonas ?? [],
        attachedToPerceptions: m.attachedToPerceptions ?? m.AttachedToPerceptions ?? [],
        createdAt: m.createdAt ?? m.CreatedAt ?? null,
        modifiedAt: m.modifiedAt ?? m.ModifiedAt ?? null,
        version: m.version ?? m.Version ?? 1
    };
}

/**
 * Load messages by type
 * @param {string} type - Message type (Persona, Perception, Technical, ContextFile or lowercase variants)
 * @returns {Promise<Array>} Array of messages
 */
export async function loadMessages(type) {
    try {
        // Ensure we have the active profile loaded (will be 0 if no active profile)
        if (activeProfileId === null) {
            await fetchActiveProfile();
        }

        // Create a reverse map for capitalized types
        const typeNormalizationMap = {
            'Persona': 'persona',
            'Perception': 'perception',
            'Technical': 'technical',
            'ContextFile': 'context'
        };

        // Normalize type: use the map if type is capitalized, otherwise treat as lowercase
        const normalizedType = typeNormalizationMap[type] || type.toLowerCase();
        const apiType = TYPE_MAP[normalizedType];

        if (!apiType) {
            throw new Error(`Invalid type: ${type} (normalized to: ${normalizedType})`);
        }

        console.log(`Loading messages for type: ${type} (normalized: ${normalizedType}, api: ${apiType})`);

        // Build URL with profileId filter (always include, even if 0)
        let url = `${API_BASE}?type=${apiType}&includeArchived=false`;
        if (activeProfileId !== null && activeProfileId !== 0) {
            url += `&profileId=${activeProfileId}`;
            console.log(`Filtering by profileId: ${activeProfileId}`);
        } else {
            console.log(`Using global profile (profileId: 0)`);
        }

        const response = await fetch(url);
        if (!response.ok) throw new Error('Failed to load messages');
        const raw = await response.json();
        if (!Array.isArray(raw)) return [];
        return raw.map(normalizeMessage);
    } catch (error) {
        showMessage(`Failed to load messages: ${error.message}`, true);
        throw error;
    }
}

/**
 * Load a single message by ID
 * @param {number} messageId - Message ID
 * @returns {Promise<Object|null>} Message object or null
 */
export async function loadMessageById(messageId) {
    try {
        const response = await fetch(`${API_BASE}/${messageId}`);
        if (!response.ok) throw new Error('Failed to load message');
        const raw = await response.json();
        return normalizeMessage(raw);
    } catch (error) {
        showMessage(`Failed to load message: ${error.message}`, true);
        throw error;
    }
}

/**
 * Create a new message
 * @param {Object} message - Message object
 * @returns {Promise<Object>} Created message
 */
export async function createMessage(message) {
    try {
        const response = await fetch(API_BASE, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(message)
        });

        if (!response.ok) throw new Error('Failed to save message');
        const created = await response.json();
        return normalizeMessage(created);
    } catch (error) {
        showMessage(`Failed to save: ${error.message}`, true);
        throw error;
    }
}

/**
 * Update an existing message
 * @param {number} messageId - Message ID
 * @param {Object} message - Updated message object
 * @returns {Promise<Object>} Updated message
 */
export async function updateMessage(messageId, message) {
    try {
        const response = await fetch(`${API_BASE}/${messageId}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(message)
        });

        if (!response.ok) throw new Error('Failed to update message');
        const updated = await response.json();
        return normalizeMessage(updated);
    } catch (error) {
        showMessage(`Failed to update: ${error.message}`, true);
        throw error;
    }
}

/**
 * Delete a message
 * @param {number} messageId - Message ID
 * @returns {Promise<void>}
 */
export async function deleteMessage(messageId) {
    try {
        const response = await fetch(`${API_BASE}/${messageId}`, {
            method: 'DELETE'
        });

        if (!response.ok) throw new Error('Failed to delete message');

        showMessage('Message deleted successfully');
    } catch (error) {
        showMessage(`Failed to delete: ${error.message}`, true);
        throw error;
    }
}

/**
 * Set message as active
 * @param {number} messageId - Message ID
 * @returns {Promise<void>}
 */
export async function setActive(messageId) {
    try {
        const response = await fetch(`${API_BASE}/${messageId}/activate`, {
            method: 'POST'
        });

        if (!response.ok) throw new Error('Failed to set active');

        // Don't show message here - let caller handle it
    } catch (error) {
        showMessage(`Failed to set active: ${error.message}`, true);
        throw error;
    }
}

/**
 * Create a new version of a message
 * @param {number} messageId - Message ID
 * @returns {Promise<Object>} Created version object
 */
export async function createVersion(messageId) {
    try {
        const response = await fetch(`${API_BASE}/${messageId}/version`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({})
        });

        if (!response.ok) throw new Error('Failed to create version');

        const created = await response.json();
        return normalizeMessage(created);
    } catch (error) {
        showMessage(`Failed to create version: ${error.message}`, true);
        throw error;
    }
}

/**
 * Get version history for a message
 * @param {number} messageId - Message ID
 * @returns {Promise<Array>} Array of versions
 */
export async function getVersions(messageId) {
    try {
        const response = await fetch(`${API_BASE}/${messageId}/versions`);
        if (!response.ok) throw new Error('Failed to load version history');

        const raw = await response.json();
        return Array.isArray(raw) ? raw.map(normalizeMessage) : [];
    } catch (error) {
        showMessage(`Failed to load version history: ${error.message}`, true);
        throw error;
    }
}

/**
 * Update message attachments
 * @param {number} messageId - Message ID
 * @param {Object} attachments - Attachments object with persona and perception IDs
 * @returns {Promise<void>}
 */
export async function updateAttachments(messageId, attachments) {
    try {
        const response = await fetch(`${API_BASE}/${messageId}/attachments`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(attachments)
        });

        if (!response.ok) throw new Error('Failed to save attachments');

        // Don't show message here - let caller handle it
    } catch (error) {
        showMessage(`Failed to save attachments: ${error.message}`, true);
        throw error;
    }
}

/**
 * Load preview with active persona and user names
 * @returns {Promise<Object>} Preview object
 */
export async function loadPreview() {
    try {
        const response = await fetch(`${API_BASE}/preview`);
        if (!response.ok) throw new Error('Failed to load preview');

        const raw = await response.json();
        return {
            completeMessage:
                raw.completeMessage ??
                raw.CompleteMessage ??
                raw.systemMessage ??
                raw.SystemMessage ??
                '',
            hasPersona: raw.hasPersona ?? raw.HasPersona ?? false,
            perceptionCount: raw.perceptionCount ?? raw.PerceptionCount ?? 0,
            hasTechnical: raw.hasTechnical ?? raw.HasTechnical ?? false,
            contextCount: raw.contextCount ?? raw.ContextCount ?? 0,
            tokenCount: raw.tokenCount ?? raw.TokenCount ?? 0,
            items: (raw.items ?? raw.Items ?? []).map(item => ({
                name: item.name ?? item.Name ?? '',
                type: item.type ?? item.Type ?? '',
                tokenCount: item.tokenCount ?? item.TokenCount ?? 0
            }))
        };
    } catch (error) {
        showMessage(`Failed to load preview: ${error.message}`, true);
        throw error;
    }
}

/**
 * Get active persona
 * @returns {Promise<Object|null>} Active persona or null
 */
export async function getActivePersona() {
    try {
        const response = await fetch(`${API_BASE}?type=Persona&includeArchived=false`);
        if (!response.ok) throw new Error('Failed to load persona');

        const raw = await response.json();
        const personas = Array.isArray(raw) ? raw.map(normalizeMessage) : [];
        return personas.find(p => p.isActive) || null;
    } catch (error) {
        return null;
    }
}

/**
 * Get active user profile
 * @returns {Promise<Object|null>} Active user profile or null
 */
export async function getActiveUserProfile() {
    try {
        const response = await fetch(`${API_BASE}?type=ContextFile&includeArchived=false`);
        if (!response.ok) return null;

        const raw = await response.json();
        const contextFiles = Array.isArray(raw) ? raw.map(normalizeMessage) : [];
        const userProfileContext = contextFiles.find(c => c.isUserProfile && c.isActive);

        if (userProfileContext) {
            return { name: userProfileContext.name, source: 'context' };
        }

        const profileResponse = await fetch('/api/userprofiles/active');
        if (!profileResponse.ok) return null;

        const profile = await profileResponse.json();
        return { name: profile.name, source: 'profile' };
    } catch (error) {
        return null;
    }
}

export default {
    loadMessages,
    loadMessageById,
    createMessage,
    updateMessage,
    deleteMessage,
    setActive,
    createVersion,
    getVersions,
    updateAttachments,
    loadPreview,
    getActivePersona,
    getActiveUserProfile
};