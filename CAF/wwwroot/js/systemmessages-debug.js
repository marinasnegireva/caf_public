import { setupAutoResize } from './utils.js';

export function populateContentFields(messages) {
    console.log('Populating content fields for messages', messages);
    Object.values(messages || {}).forEach(list => {
        list.forEach(m => {
            const textarea = document.querySelector(`textarea[data-message-id="${m.id}"][data-field="content"]`);
            if (textarea) {
                textarea.value = m.content || '';
                textarea.dispatchEvent(new Event('input'));
            }
        });
    });

    // Re-apply auto-resize
    setupAutoResize();
}