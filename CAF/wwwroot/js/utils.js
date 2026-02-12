/**
 * Shared Utilities for CAF UI
 * Common functions used across all wwwroot JavaScript files
 */

/**
 * Display a toast message
 * @param {string} text - Message text
 * @param {boolean} isError - Whether this is an error message
 */
export function showMessage(text, isError = false) {
    const messageEl = document.getElementById('message');
    if (!messageEl) {
        console.error('Message element not found');
        return;
    }

    messageEl.textContent = text;
    messageEl.className = `message-message ${isError ? 'error' : 'success'} show`;
    setTimeout(() => messageEl.classList.remove('show'), 5000);
}

/**
 * Escape HTML to prevent XSS
 * @param {string} text - Text to escape
 * @returns {string} Escaped HTML
 */
export function escapeHtml(text) {
    if (!text) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

/**
 * Format date to locale string
 * @param {string|Date} date - Date to format
 * @returns {string} Formatted date
 */
export function formatDate(date) {
    if (!date) return '';
    return new Date(date).toLocaleDateString();
}

/**
 * Format datetime to locale string
 * @param {string|Date} date - Date to format
 * @returns {string} Formatted datetime
 */
export function formatDateTime(date) {
    if (!date) return '';
    return new Date(date).toLocaleString();
}

/**
 * Parse tags from comma-separated string
 * @param {string} tagsString - Comma-separated tags
 * @returns {string[]} Array of tags
 */
export function parseTags(tagsString) {
    if (!tagsString) return [];
    return tagsString.split(',').map(t => t.trim()).filter(t => t);
}

/**
 * Join tags into comma-separated string
 * @param {string[]} tags - Array of tags
 * @returns {string} Comma-separated tags
 */
export function joinTags(tags) {
    if (!tags || !Array.isArray(tags)) return '';
    return tags.join(', ');
}

/**
 * Get current active tab type
 * @returns {string|null} Tab type (persona, perception, technical, context)
 */
export function getCurrentTabType() {
    const currentTab = document.querySelector('.tab.active');
    if (!currentTab) return null;

    const text = currentTab.textContent.toLowerCase();
    if (text.includes('persona')) return 'persona';
    if (text.includes('perception')) return 'perception';
    if (text.includes('technical')) return 'technical';
    if (text.includes('context')) return 'context';
    return null;
}

/**
 * Type mapping for API calls
 */
export const TYPE_MAP = {
    'persona': 'Persona',
    'perception': 'Perception',
    'technical': 'Technical',
    'context': 'ContextFile'
};

/**
 * Color mapping for message types
 */
export const TYPE_COLORS = {
    'Persona': 'persona',
    'Perception': 'perception',
    'Technical': 'technical',
    'ContextFile': 'context'
};

/**
 * Auto-resize textarea based on content
 * @param {HTMLTextAreaElement} textarea - Textarea element
 * @param {number} minHeight - Minimum height in pixels
 */
export function autoResizeTextarea(textarea, minHeight = 120) {
    if (!textarea) return;

    textarea.style.height = 'auto';
    const contentHeight = textarea.scrollHeight;
    textarea.style.height = Math.max(minHeight, contentHeight) + 'px';
}

/**
 * Setup auto-resize for all textareas matching selector
 * @param {string} selector - CSS selector for textareas
 */
export function setupAutoResize(selector = '.message-content-edit, .inline-notes-edit') {
    document.querySelectorAll(selector).forEach(textarea => {
        autoResizeTextarea(textarea);
        textarea.addEventListener('input', () => autoResizeTextarea(textarea));
    });
}

/**
 * Debounce function calls
 * @param {Function} func - Function to debounce
 * @param {number} wait - Wait time in milliseconds
 * @returns {Function} Debounced function
 */
export function debounce(func, wait = 300) {
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

/**
 * Create a DOM element from HTML string
 * @param {string} html - HTML string
 * @returns {Element} DOM element
 */
export function createElementFromHTML(html) {
    const div = document.createElement('div');
    div.innerHTML = html.trim();
    return div.firstElementChild;
}

/**
 * Remove element from DOM
 * @param {string} id - Element ID
 */
export function removeElement(id) {
    const element = document.getElementById(id);
    if (element) {
        element.remove();
    }
}

/**
 * Confirm action with user
 * @param {string} message - Confirmation message
 * @returns {boolean} User's choice
 */
export function confirmAction(message) {
    return confirm(message);
}

export default {
    showMessage,
    escapeHtml,
    formatDate,
    formatDateTime,
    parseTags,
    joinTags,
    getCurrentTabType,
    TYPE_MAP,
    TYPE_COLORS,
    autoResizeTextarea,
    setupAutoResize,
    debounce,
    createElementFromHTML,
    removeElement,
    confirmAction
};