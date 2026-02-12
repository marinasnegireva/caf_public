/**
 * Common Utilities Module
 * Shared functions used across all CAF UI pages
 */

/**
 * Escape HTML to prevent XSS
 * @param {string} text - Text to escape
 * @returns {string} Escaped HTML
 */
export function escapeHtml(text) {
    if (text === null || text === undefined) return '';
    const div = document.createElement('div');
    div.textContent = String(text);
    return div.innerHTML;
}

/**
 * Format date to locale string
 * @param {string|Date} date - Date to format
 * @returns {string} Formatted date
 */
export function formatDate(dateString) {
    if (!dateString) return '';
    const date = new Date(dateString);
    return date.toLocaleDateString();
}

/**
 * Format datetime to locale string
 * @param {string|Date} date - Date to format
 * @returns {string} Formatted datetime
 */
export function formatDateTime(dateString) {
    if (!dateString) return '';
    const date = new Date(dateString);
    return date.toLocaleString();
}

/**
 * Display a toast notification message
 * @param {string} text - Message text
 * @param {string} type - Message type ('success', 'error', 'info')
 */
export function showMessage(text, type = 'success') {
    const toast = document.createElement('div');
    toast.className = 'toast-notification';
    toast.textContent = text;

    const bgColor = type === 'error' ? '#ef4444' : type === 'info' ? '#3b82f6' : '#10b981';
    toast.style.cssText = `
        position: fixed;
        bottom: 20px;
        right: 20px;
        background: ${bgColor};
        color: white;
        padding: 1rem 1.5rem;
        border-radius: 8px;
        box-shadow: 0 4px 6px rgba(0,0,0,0.1);
        z-index: 1000;
        transition: opacity 0.3s;
    `;

    document.body.appendChild(toast);

    setTimeout(() => {
        toast.style.opacity = '0';
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

/**
 * Show error message (convenience wrapper)
 * @param {string} text - Error message text
 */
export function showError(text) {
    showMessage(text, 'error');
}

/**
 * Calculate time ago from date
 * @param {string|Date} date - Date to calculate from
 * @returns {string} Human-readable time ago string
 */
export function getTimeAgo(date) {
    const seconds = Math.floor((new Date() - new Date(date)) / 1000);

    if (seconds < 60) return 'just now';
    if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
    if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`;
    if (seconds < 604800) return `${Math.floor(seconds / 86400)}d ago`;
    if (seconds < 2592000) return `${Math.floor(seconds / 604800)}w ago`;

    return formatDate(date);
}

/**
 * Normalize text (handle null/undefined)
 * @param {any} value - Value to normalize
 * @returns {string} Normalized string
 */
export function normalizeText(value) {
    if (value === null || value === undefined) return '';
    return String(value);
}

/**
 * Check if value has text content
 * @param {any} value - Value to check
 * @returns {boolean} True if has text
 */
export function hasText(value) {
    return normalizeText(value).trim().length > 0;
}

/**
 * Confirm action with user
 * @param {string} message - Confirmation message
 * @returns {boolean} User's choice
 */
export function confirmAction(message) {
    return confirm(message);
}

/**
 * API base URL constant
 */
export const API_BASE_URL = '/api';

export default {
    escapeHtml,
    formatDate,
    formatDateTime,
    showMessage,
    showError,
    getTimeAgo,
    normalizeText,
    hasText,
    confirmAction,
    API_BASE_URL
};