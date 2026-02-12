/**
 * Diff Viewer Module
 * Handles content comparison and diff visualization
 */

import { escapeHtml, getCurrentTabType, TYPE_MAP } from './utils.js';

/**
 * Toggle diff view visibility
 * @param {number} messageId - Message ID
 * @param {Object} currentMessages - Current messages object
 */
export function toggleDiff(messageId, currentMessages) {
    const diffView = document.getElementById(`diff-view-${messageId}`);
    if (!diffView) return;

    if (diffView.style.display === 'none' || !diffView.style.display) {
        showDiff(messageId, currentMessages);
    } else {
        diffView.style.display = 'none';
    }
}

/**
 * Show diff for a message
 * @param {number} messageId - Message ID
 * @param {Object} currentMessages - Current messages object
 */
export function showDiff(messageId, currentMessages) {
    const tabType = getCurrentTabType();
    if (!tabType) return;

    // Convert to storage key format
    const storageKey = TYPE_MAP[tabType];
    if (!storageKey) return;

    const message = currentMessages[storageKey]?.find(m => m.id === messageId);
    if (!message) return;

    const card = document.querySelector(`[data-message-id="${messageId}"]`);
    if (!card) return;

    const originalContent = message.content;
    const newContent = card.querySelector('[data-field="content"]').value;

    const diffView = document.getElementById(`diff-view-${messageId}`);
    if (!diffView) return;

    if (originalContent === newContent) {
        diffView.innerHTML = renderNoChanges();
    } else {
        const diffHtml = generateLineDiff(originalContent, newContent);
        diffView.innerHTML = `
            <div class="diff-header" style="margin-bottom: 16px; padding-bottom: 12px; border-bottom: 1px solid var(--border-color);">
                Content Comparison: Original vs Modified
            </div>
            ${diffHtml}
        `;
    }

    diffView.style.display = 'block';
}

/**
 * Render "no changes" message
 */
function renderNoChanges() {
    return `
        <div class="diff-header" style="text-align: center; padding: 24px; color: var(--text-secondary);">
            <div style="font-size: 48px; margin-bottom: 12px;">?</div>
            <div>No changes detected - content is identical to the original</div>
        </div>
    `;
}

/**
 * Generate line-by-line diff HTML
 * @param {string} original - Original content
 * @param {string} modified - Modified content
 * @returns {string} HTML string
 */
export function generateLineDiff(original, modified) {
    const originalLines = original.split('\n');
    const modifiedLines = modified.split('\n');

    const diff = computeDiff(originalLines, modifiedLines);

    return `
        <div class="diff-container" style="display: flex; gap: 1px; background: var(--border-color); border: 1px solid var(--border-color); border-radius: 8px; overflow: hidden; max-height: 800px; overflow-y: auto;">
            ${renderDiffSide('ORIGINAL', diff, true)}
            ${renderDiffSide('MODIFIED', diff, false)}
        </div>
    `;
}

/**
 * Render one side of the diff
 * @param {string} title - Side title
 * @param {Array} diff - Diff array
 * @param {boolean} isOriginal - Whether this is the original side
 */
function renderDiffSide(title, diff, isOriginal) {
    let html = `
        <div class="diff-side" style="flex: 1; background: var(--bg-primary); min-width: 0;">
            <div style="background: var(--bg-tertiary); padding: 12px 16px; font-weight: 600; color: var(--text-primary); border-bottom: 1px solid var(--border-color); text-align: left; position: sticky; top: 0; z-index: 1;">
                ${title}
            </div>
            <div style="font-family: 'JetBrains Mono', 'Fira Code', 'Courier New', monospace; font-size: 13px; line-height: 1.6;">
    `;

    diff.forEach(item => {
        if (isOriginal) {
            html += renderOriginalDiffLine(item);
        } else {
            html += renderModifiedDiffLine(item);
        }
    });

    html += '</div></div>';
    return html;
}

/**
 * Render original side diff line
 */
function renderOriginalDiffLine(item) {
    if (item.type === 'removed' || item.type === 'unchanged') {
        const bgColor = item.type === 'removed' ? 'rgba(248, 113, 113, 0.15)' : 'transparent';
        const textColor = item.type === 'removed' ? 'var(--accent-red)' : 'var(--text-primary)';
        const lineNum = item.originalLine !== undefined ? item.originalLine + 1 : '';

        return `
            <div style="display: flex; background: ${bgColor}; padding: 2px 0; min-height: 22px;">
                <div style="min-width: 50px; padding: 2px 12px; text-align: right; color: var(--text-muted); user-select: none; border-right: 1px solid var(--border-color); flex-shrink: 0;">
                    ${lineNum}
                </div>
                <div style="flex: 1; padding: 2px 12px; color: ${textColor}; white-space: pre-wrap; word-wrap: break-word; text-align: left; overflow-wrap: anywhere;">
                    ${escapeHtml(item.value)}
                </div>
            </div>
        `;
    } else if (item.type === 'added') {
        return `
            <div style="display: flex; background: rgba(113, 128, 150, 0.05); padding: 2px 0; min-height: 22px;">
                <div style="min-width: 50px; padding: 2px 12px; text-align: right; color: var(--text-muted); user-select: none; border-right: 1px solid var(--border-color); flex-shrink: 0;"></div>
                <div style="flex: 1; padding: 2px 12px; color: var(--text-muted); opacity: 0.3; text-align: left;">·</div>
            </div>
        `;
    }
    return '';
}

/**
 * Render modified side diff line
 */
function renderModifiedDiffLine(item) {
    if (item.type === 'added' || item.type === 'unchanged') {
        const bgColor = item.type === 'added' ? 'rgba(74, 222, 128, 0.15)' : 'transparent';
        const textColor = item.type === 'added' ? 'var(--accent-green)' : 'var(--text-primary)';
        const lineNum = item.modifiedLine !== undefined ? item.modifiedLine + 1 : '';

        return `
            <div style="display: flex; background: ${bgColor}; padding: 2px 0; min-height: 22px;">
                <div style="min-width: 50px; padding: 2px 12px; text-align: right; color: var(--text-muted); user-select: none; border-right: 1px solid var(--border-color); flex-shrink: 0;">
                    ${lineNum}
                </div>
                <div style="flex: 1; padding: 2px 12px; color: ${textColor}; white-space: pre-wrap; word-wrap: break-word; text-align: left; overflow-wrap: anywhere;">
                    ${escapeHtml(item.value)}
                </div>
            </div>
        `;
    } else if (item.type === 'removed') {
        return `
            <div style="display: flex; background: rgba(113, 128, 150, 0.05); padding: 2px 0; min-height: 22px;">
                <div style="min-width: 50px; padding: 2px 12px; text-align: right; color: var(--text-muted); user-select: none; border-right: 1px solid var(--border-color); flex-shrink: 0;"></div>
                <div style="flex: 1; padding: 2px 12px; color: var(--text-muted); opacity: 0.3; text-align: left;">·</div>
            </div>
        `;
    }
    return '';
}

/**
 * Compute diff between two arrays of lines
 * @param {string[]} original - Original lines
 * @param {string[]} modified - Modified lines
 * @returns {Array} Diff array
 */
function computeDiff(original, modified) {
    const diff = [];
    let i = 0, j = 0;

    const lcs = longestCommonSubsequence(original, modified);
    const lcsSet = new Set(lcs.map((item, idx) => `${item.origIdx}-${item.modIdx}`));

    while (i < original.length || j < modified.length) {
        if (i < original.length && j < modified.length &&
            lcsSet.has(`${i}-${j}`) && original[i] === modified[j]) {
            diff.push({
                type: 'unchanged',
                value: original[i],
                originalLine: i,
                modifiedLine: j
            });
            i++;
            j++;
        } else if (i < original.length &&
            (j >= modified.length || !lcsSet.has(`${i}-${j}`))) {
            diff.push({
                type: 'removed',
                value: original[i],
                originalLine: i
            });
            i++;
        } else if (j < modified.length) {
            diff.push({
                type: 'added',
                value: modified[j],
                modifiedLine: j
            });
            j++;
        }
    }

    return diff;
}

/**
 * Compute longest common subsequence
 * @param {string[]} arr1 - First array
 * @param {string[]} arr2 - Second array
 * @returns {Array} LCS array
 */
function longestCommonSubsequence(arr1, arr2) {
    const m = arr1.length;
    const n = arr2.length;
    const dp = Array(m + 1).fill(null).map(() => Array(n + 1).fill(0));

    for (let i = 1; i <= m; i++) {
        for (let j = 1; j <= n; j++) {
            if (arr1[i - 1] === arr2[j - 1]) {
                dp[i][j] = dp[i - 1][j - 1] + 1;
            } else {
                dp[i][j] = Math.max(dp[i - 1][j], dp[i][j - 1]);
            }
        }
    }

    const lcs = [];
    let i = m, j = n;
    while (i > 0 && j > 0) {
        if (arr1[i - 1] === arr2[j - 1]) {
            lcs.unshift({ origIdx: i - 1, modIdx: j - 1 });
            i--;
            j--;
        } else if (dp[i - 1][j] > dp[i][j - 1]) {
            i--;
        } else {
            j--;
        }
    }

    return lcs;
}

export default {
    toggleDiff,
    showDiff,
    generateLineDiff
};