/**
 * Shared Sidebar Loader
 * Dynamically loads the sidebar and sets the active menu item based on current page
 */

(function () {
    'use strict';

    /**
     * Gets the current page name from the URL
     */
    function getCurrentPage() {
        const path = window.location.pathname;
        const page = path.substring(path.lastIndexOf('/') + 1);
        // Remove .html extension
        return page.replace('.html', '') || 'dashboard';
    }

    /**
     * Loads the sidebar HTML and initializes it
     */
    async function loadSidebar() {
        const sidebarContainer = document.getElementById('sidebar-container');
        if (!sidebarContainer) {
            console.warn('Sidebar container not found');
            return;
        }

        try {
            const response = await fetch('/shared-sidebar.html');
            if (!response.ok) {
                throw new Error(`Failed to load sidebar: ${response.status}`);
            }

            const html = await response.text();
            sidebarContainer.innerHTML = html;

            // Set active menu item
            setActiveMenuItem();
        } catch (error) {
            console.error('Error loading sidebar:', error);
            // Fallback: show basic navigation
            sidebarContainer.innerHTML = `
                <nav class="sidebar">
                    <div class="sidebar-header">
                        <h2><i class="fas fa-brain"></i> CAF</h2>
                    </div>
                    <div class="sidebar-menu">
                        <a href="dashboard.html" class="menu-item"><i class="fas fa-home"></i> Dashboard</a>
                        <a href="quotes.html" class="menu-item"><i class="fas fa-quote-left"></i> Quotes</a>
                    </div>
                </nav>
            `;
        }
    }

    /**
     * Sets the active class on the current menu item
     */
    function setActiveMenuItem() {
        const currentPage = getCurrentPage();
        const menuItems = document.querySelectorAll('.sidebar .menu-item');

        menuItems.forEach(item => {
            const itemPage = item.getAttribute('data-page');
            if (itemPage === currentPage) {
                item.classList.add('active');
            } else {
                item.classList.remove('active');
            }
        });
    }

    // Auto-load sidebar when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', loadSidebar);
    } else {
        loadSidebar();
    }

    // Export for manual use if needed
    window.SidebarLoader = {
        load: loadSidebar,
        setActive: setActiveMenuItem
    };
})();