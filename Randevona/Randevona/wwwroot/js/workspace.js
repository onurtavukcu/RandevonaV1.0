(() => {
    const toggle = document.querySelector('.sidebar-toggle');
    const sidebar = document.querySelector('.workspace-sidebar');
    const backdrop = document.querySelector('.sidebar-backdrop');
    if (!toggle || !sidebar) return;
    const mobile = window.matchMedia('(max-width: 991px)');
    const setOpen = (open, restoreFocus = false) => {
        document.body.classList.toggle('sidebar-open', open);
        toggle.setAttribute('aria-expanded', String(open));
        toggle.setAttribute('aria-label', open ? 'Close navigation' : 'Open navigation');
        sidebar.inert = mobile.matches && !open;
        if (open) sidebar.querySelector('a')?.focus();
        if (restoreFocus) toggle.focus();
    };
    toggle.addEventListener('click', () => setOpen(!document.body.classList.contains('sidebar-open')));
    backdrop?.addEventListener('click', () => setOpen(false, true));
    document.addEventListener('keydown', event => {
        if (!document.body.classList.contains('sidebar-open')) return;
        if (event.key === 'Escape') setOpen(false, true);
        if (event.key === 'Tab') {
            const items = [...sidebar.querySelectorAll('a, summary, button')].filter(x => x.getClientRects().length);
            const first = items[0], last = items.at(-1);
            if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
            else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
        }
    });
    mobile.addEventListener('change', () => setOpen(false));
    setOpen(false);
})();
