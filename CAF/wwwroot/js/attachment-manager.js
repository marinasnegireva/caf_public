/**
 * Attachment Manager Module
 * Handles context file attachments to personas and perceptions
 */

import { showMessage } from './utils.js';
import * as apiClient from './api-client.js';
import * as modalManager from './modal-manager.js';

/**
 * Manage attachments for a context file
 * @param {number} messageId - Message ID
 * @param {Object} currentMessages - Current messages object
 * @param {Function} reloadCallback - Callback to reload messages
 */
export async function manageAttachments(messageId, currentMessages, reloadCallback) {
    // Look for the message in ContextFile messages
    const message = currentMessages['ContextFile']?.find(m => m.id === messageId);
    if (!message) {
        showMessage('Context file not found', true);
        console.error('Message not found. currentMessages:', currentMessages);
        return;
    }

    try {
        // Load personas and perceptions
        const [personas, perceptions] = await Promise.all([
            apiClient.loadMessages('Persona'),
            apiClient.loadMessages('Perception')
        ]);

        // Show attachment modal
        modalManager.showAttachmentModal(messageId, message, personas, perceptions);
    } catch (error) {
        showMessage(`Failed to load attachments: ${error.message}`, true);
    }
}

/**
 * Save attachments for a context file
 * @param {number} messageId - Message ID
 * @param {Function} closeModalCallback - Callback to close modal
 * @param {Function} reloadCallback - Callback to reload messages
 */
export async function saveAttachments(messageId, closeModalCallback, reloadCallback) {
    const selectedPersonas = Array.from(
        document.querySelectorAll('.attachment-persona-checkbox:checked')
    ).map(cb => parseInt(cb.value));

    const selectedPerceptions = Array.from(
        document.querySelectorAll('.attachment-perception-checkbox:checked')
    ).map(cb => parseInt(cb.value));

    try {
        await apiClient.updateAttachments(messageId, {
            attachedToPersonas: selectedPersonas,
            attachedToPerceptions: selectedPerceptions
        });

        if (closeModalCallback) closeModalCallback();
        if (reloadCallback) await reloadCallback('ContextFile');
    } catch (error) {
        // Error already shown by apiClient
    }
}

export default {
    manageAttachments,
    saveAttachments
};