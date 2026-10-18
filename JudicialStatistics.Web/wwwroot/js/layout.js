(function () {
    // 1. Mobile Sidebar Functions
    window.toggleMobileSidebar = function () {
        const sidebar = document.querySelector('.app-sidebar');
        const overlay = document.getElementById('mobileOverlay');
        if (!sidebar) return;
        const isOpen = sidebar.classList.contains('open');
        if (isOpen) {
            sidebar.classList.remove('open');
            if (overlay) overlay.classList.remove('show');
            document.body.style.overflow = '';
        } else {
            sidebar.classList.add('open');
            if (overlay) overlay.classList.add('show');
            document.body.style.overflow = 'hidden';
        }
    };

    window.closeMobileSidebar = function () {
        const sidebar = document.querySelector('.app-sidebar');
        const overlay = document.getElementById('mobileOverlay');
        if (sidebar) sidebar.classList.remove('open');
        if (overlay) overlay.classList.remove('show');
        document.body.style.overflow = '';
    };

    // 2. Desktop Sidebar Collapse Function
    window.toggleDesktopSidebar = function () {
        const layout = document.querySelector('.app-layout');
        const toggleIcon = document.getElementById('sidebarToggleIcon');
        const toggleLabel = document.getElementById('sidebarToggleLabel');
        if (!layout) return;
        const isCollapsed = layout.classList.toggle('sidebar-collapsed');
        if (toggleIcon) toggleIcon.textContent = isCollapsed ? '»' : '«';
        if (toggleLabel) toggleLabel.textContent = isCollapsed ? 'توسيع القائمة' : 'تصغير القائمة';
        try {
            localStorage.setItem('sidebar_collapsed', isCollapsed ? 'true' : 'false');
        } catch (e) { }
    };

    function restoreSidebarState() {
        if (window.innerWidth > 992) {
            try {
                if (localStorage.getItem('sidebar_collapsed') === 'true') {
                    const layout = document.querySelector('.app-layout');
                    const toggleIcon = document.getElementById('sidebarToggleIcon');
                    const toggleLabel = document.getElementById('sidebarToggleLabel');
                    if (layout) layout.classList.add('sidebar-collapsed');
                    if (toggleIcon) toggleIcon.textContent = '»';
                    if (toggleLabel) toggleLabel.textContent = 'توسيع القائمة';
                }
            } catch (e) { }
        } else {
            const overlay = document.getElementById('mobileOverlay');
            const sidebar = document.querySelector('.app-sidebar');
            if (sidebar && !sidebar.classList.contains('open')) {
                if (overlay) overlay.classList.remove('show');
                document.body.style.overflow = '';
            }
        }
    }

    // 3. Document Event Delegation (Works reliably across page navigations)
    document.addEventListener('click', function (e) {
        // Mobile hamburger button
        if (e.target.closest('.mobile-menu-btn')) {
            e.preventDefault();
            e.stopPropagation();
            window.toggleMobileSidebar();
            return;
        }

        // Mobile close button inside sidebar
        if (e.target.closest('.mobile-sidebar-close-btn')) {
            e.preventDefault();
            e.stopPropagation();
            window.closeMobileSidebar();
            return;
        }

        // Mobile overlay
        if (e.target.closest('#mobileOverlay')) {
            e.preventDefault();
            e.stopPropagation();
            window.closeMobileSidebar();
            return;
        }

        // Desktop collapse button
        if (e.target.closest('#sidebarCollapseBtn')) {
            e.preventDefault();
            e.stopPropagation();
            window.toggleDesktopSidebar();
            return;
        }

        // Clicking any navigation link in the sidebar on mobile closes the drawer
        if (e.target.closest('.app-sidebar a') && window.innerWidth <= 992) {
            window.closeMobileSidebar();
            return;
        }
    });

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', restoreSidebarState);
    } else {
        restoreSidebarState();
    }

    window.addEventListener('popstate', restoreSidebarState);
})();
