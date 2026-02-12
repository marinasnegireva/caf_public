/**
 * Message Operations Module
 * Handles message CRUD operations and state management
 */

import { showMessage, getCurrentTabType, parseTags, TYPE_MAP } from './utils.js';
import * as apiClient from './api-client.js';

/**
 * Mark message as modified
 * @param {number} messageId - Message ID
 */
export function markAsModified(messageId) {
    const updateBtn = document.getElementById(`update-btn-${messageId}`);
    if (updateBtn) {
        updateBtn.disabled = false;
    }
}

/**
 * Reset changes for a message
 * @param {number} messageId - Message ID
 * @param {Object} currentMessages - Current messages object
 */
export function resetChanges(messageId, currentMessages) {
    const tabType = getCurrentTabType();
    if (!tabType) return;

    // Convert to storage key format
    const storageKey = TYPE_MAP[tabType];
    if (!storageKey) {
        showMessage('Invalid tab type', true);
        console.error('Unknown tab type:', tabType);
        return;
    }

    // Safety check
    if (!currentMessages || !currentMessages[storageKey]) {
        showMessage('Message state not available', true);
        console.error('currentMessages not properly initialized');
        return;
    }

    const message = currentMessages[storageKey].find(m => m.id === messageId);
    if (!message) return;

    const card = document.querySelector(`[data-message-id="${messageId}"]`);
    if (!card) return;

    // Reset all fields (name is not editable inline, skip it)
    const nameField = card.querySelector('[data-field="name"]');
    if (nameField) nameField.value = message.name;

    const descriptionField = card.querySelector('[data-field="description"]');
    if (descriptionField) descriptionField.value = message.description || '';

    const contentField = card.querySelector('[data-field="content"]');
    if (contentField) contentField.value = message.content;

    const tagsField = card.querySelector('[data-field="tags"]');
    if (tagsField) tagsField.value = (message.tags || []).join(', ');

    const notesField = card.querySelector('[data-field="notes"]');
    if (notesField) notesField.value = message.notes || '';

    const isActiveField = card.querySelector('[data-field="isActive"]');
    if (isActiveField) isActiveField.checked = message.isActive;

    // Reset isUserProfile if it exists
    const isUserProfileCheckbox = card.querySelector('[data-field="isUserProfile"]');
    if (isUserProfileCheckbox) {
        isUserProfileCheckbox.checked = message.isUserProfile || false;
    }

    // Disable update button
    const updateBtn = document.getElementById(`update-btn-${messageId}`);
    if (updateBtn) updateBtn.disabled = true;

    // Hide diff view
    const diffView = document.getElementById(`diff-view-${messageId}`);
    if (diffView) diffView.style.display = 'none';

    showMessage('Changes reset');
}

/**
 * Get message data from card
 * @param {number} messageId - Message ID
 * @param {Object} originalMessage - Original message object
 * @returns {Object} Updated message object
 */
export function getMessageData(messageId, originalMessage) {
    const card = document.querySelector(`[data-message-id="${messageId}"]`);
    if (!card) return null;

    // Name is not editable inline, use original
    const nameField = card.querySelector('[data-field="name"]');
    const descriptionField = card.querySelector('[data-field="description"]');
    const contentField = card.querySelector('[data-field="content"]');
    const tagsField = card.querySelector('[data-field="tags"]');
    const notesField = card.querySelector('[data-field="notes"]');
    const isActiveField = card.querySelector('[data-field="isActive"]');

    const message = {
        name: nameField ? nameField.value.trim() : originalMessage.name,
        content: contentField ? contentField.value.trim() : originalMessage.content,
        type: originalMessage.type,
        description: descriptionField ? descriptionField.value.trim() : (originalMessage.description || ''),
        tags: tagsField ? parseTags(tagsField.value) : (originalMessage.tags || []),
        notes: notesField ? notesField.value.trim() : (originalMessage.notes || ''),
        isActive: isActiveField ? isActiveField.checked : originalMessage.isActive,
        attachedToPersonas: originalMessage.attachedToPersonas || [],
        attachedToPerceptions: originalMessage.attachedToPerceptions || []
    };

    // Add isUserProfile if this is a context file
    const isUserProfileCheckbox = card.querySelector('[data-field="isUserProfile"]');
    if (isUserProfileCheckbox) {
        message.isUserProfile = isUserProfileCheckbox.checked;
    } else if (originalMessage.type === 'ContextFile') {
        message.isUserProfile = originalMessage.isUserProfile || false;
    }

    return message;
}

/**
 * Validate message data
 * @param {Object} message - Message object
 * @returns {boolean} True if valid
 */
export function validateMessage(message) {
    if (!message.name || !message.content) {
        showMessage('Name and Content are required', true);
        return false;
    }
    return true;
}

/**
 * Update message
 * @param {number} messageId - Message ID
 * @param {Object} currentMessages - Current messages object
 * @param {Function} reloadCallback - Callback to reload messages
 */
export async function updateMessage(messageId, currentMessages, reloadCallback) {
    const tabType = getCurrentTabType();
    if (!tabType) {
        showMessage('Could not determine current tab type', true);
        return;
    }

    // Convert to storage key format
    const storageKey = TYPE_MAP[tabType];
    if (!storageKey) {
        showMessage('Invalid tab type', true);
        console.error('Unknown tab type:', tabType);
        return;
    }

    // Safety check for currentMessages
    if (!currentMessages) {
        showMessage('Message state not available. Please reload the page.', true);
        console.error('currentMessages is undefined');
        return;
    }

    if (!currentMessages[storageKey]) {
        showMessage(`No ${storageKey} messages loaded. Please reload the page.`, true);
        console.error(`currentMessages.${storageKey} is undefined`);
        return;
    }

    const originalMessage = currentMessages[storageKey].find(m => m.id === messageId);
    if (!originalMessage) {
        showMessage('Original message not found', true);
        return;
    }

    const updatedMessage = getMessageData(messageId, originalMessage);
    if (!updatedMessage || !validateMessage(updatedMessage)) return;

    try {
        await apiClient.updateMessage(messageId, updatedMessage);

        showMessage('Message updated successfully');

        // Disable update button
        const updateBtn = document.getElementById(`update-btn-${messageId}`);
        if (updateBtn) updateBtn.disabled = true;

        // Hide diff view
        const diffView = document.getElementById(`diff-view-${messageId}`);
        if (diffView) diffView.style.display = 'none';

        // Reload messages with storage key format
        if (reloadCallback) await reloadCallback(storageKey);
    } catch (error) {
        // Error already shown by apiClient
    }
}

/**
 * Save new message
 * @param {Object} formData - Form data object
 * @param {Function} closeModalCallback - Callback to close modal
 * @param {Function} reloadCallback - Callback to reload messages
 */
export async function saveMessage(formData, closeModalCallback, reloadCallback) {
    if (!validateMessage(formData)) return;

    try {
        await apiClient.createMessage(formData);

        showMessage('Message created successfully');
        if (closeModalCallback) closeModalCallback();

        // Use the actual type from formData for reloading
        if (reloadCallback) await reloadCallback(formData.type);
    } catch (error) {
        // Error already shown by apiClient
    }
}

export default {
    markAsModified,
    resetChanges,
    getMessageData,
    validateMessage,
    updateMessage,
    saveMessage
};