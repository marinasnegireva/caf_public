/**
 * Version Manager Module
 * Handles version history for system messages
 */

import { showMessage, getCurrentTabType } from './utils.js';
import * as apiClient from './api-client.js';
import * as modalManager from './modal-manager.js';

/**
 * View version history for a message
 * @param {number} messageId - Message ID
 */
export async function viewVersions(messageId) {
    try {
        const versions = await apiClient.getVersions(messageId);

        if (versions.length === 0) {
            showMessage('No version history available for this message');
            return;
        }

        modalManager.showVersionHistoryModal(versions);
    } catch (error) {
        // Error already shown by apiClient
    }
}

/**
 * Create a new version of a message
 * @param {number} messageId - Message ID
 * @param {Function} reloadCallback - Callback to reload messages
 */
export async function createVersion(messageId, reloadCallback) {
    try {
        // Get the original message to check if it's a context file
        const originalMessage = await apiClient.loadMessageById(messageId);
        if (!originalMessage) {
            showMessage('Failed to load original message', true);
            return;
        }

        // Create the new version
        const newVersion = await apiClient.createVersion(messageId);

        // Automatically activate the new version
        await apiClient.setActive(newVersion.id);

        // If this is a context file, attach it to the active persona
        if (originalMessage.type === 'ContextFile' || originalMessage.Type === 'ContextFile') {
            const activePersona = await apiClient.getActivePersona();

            if (activePersona) {
                // Get current attachments from the original message
                const attachedToPersonas = originalMessage.attachedToPersonas || originalMessage.AttachedToPersonas || [];
                const attachedToPerceptions = originalMessage.attachedToPerceptions || originalMessage.AttachedToPerceptions || [];

                // If the original was attached to the active persona, attach the new version too
                // Or if not attached to anyone, attach to active persona by default
                if (attachedToPersonas.includes(activePersona.id) || attachedToPersonas.length === 0) {
                    const updatedAttachments = [...new Set([...attachedToPersonas, activePersona.id])];
                    await apiClient.updateAttachments(newVersion.id, {
                        attachedToPersonas: updatedAttachments,
                        attachedToPerceptions: attachedToPerceptions
                    });
                    showMessage(`New version created, activated, and attached to ${activePersona.name}`);
                } else {
                    // Keep the original attachments
                    await apiClient.updateAttachments(newVersion.id, {
                        attachedToPersonas: attachedToPersonas,
                        attachedToPerceptions: attachedToPerceptions
                    });
                    showMessage('New version created and activated');
                }
            } else {
                showMessage('New version created and activated (no active persona found)');
            }
        } else {
            showMessage('New version created and activated');
        }

        if (reloadCallback) {
            const tabType = getCurrentTabType();
            if (tabType) await reloadCallback(tabType);
        }
    } catch (error) {
        // Error already shown by apiClient
    }
}

export default {
    viewVersions,
    createVersion
};