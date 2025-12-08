function setupAdminBottomBar() {
    const userProfile = document.getElementById('userProfile');
    const userPopup = document.getElementById('userPopup');
    const menuCircle = document.getElementById('menuCircle');
    const radialActions = document.getElementById('radialActions');
    const actions = radialActions ? radialActions.querySelectorAll('.action') : [];
    const signOutBtn = document.getElementById('signOutBtn');

    const pageData = window.adminBottomBarData || {};
    const currentClassCode = pageData.classCode || document.body?.dataset?.classCode || '';
    const currentPage = pageData.currentPage || '';

    userProfile?.addEventListener('click', (e) => {
        e.stopPropagation();
        userPopup?.classList.toggle('show');
    });

    document.addEventListener('click', (e) => {
        if (!userProfile?.contains(e.target)) userPopup?.classList.remove('show');
    });

    signOutBtn?.addEventListener('click', (e) => {
        e.stopPropagation();
        console.log('Admin logout clicked');
        window.showToast('Signing out...');
        userPopup?.classList.remove('show');
        setTimeout(() => {
            window.location.href = '/Account/Logout';
        }, 300);
    });

    // Delegated fallback: ensure logout works even if element is re-rendered later
    document.addEventListener('click', (e) => {
        const target = e.target;
        if (target && (target.id === 'signOutBtn' || (target.closest && target.closest('#signOutBtn')))) {
            e.stopPropagation();
            console.log('Admin logout clicked (delegated)');
            window.showToast('Signing out...');
            userPopup?.classList.remove('show');
            setTimeout(() => {
                window.location.href = '/Account/Logout';
            }, 300);
        }
    }, true);

    let menuOpen = false;
    menuCircle?.addEventListener('click', () => {
        menuOpen = !menuOpen;
        radialActions?.classList.toggle('show', menuOpen);
    });

    function setActivePage(page) {
        actions.forEach(a => a.classList.toggle('selected', a.dataset.page === page));
    }

    setActivePage(currentPage);

    actions.forEach(action => {
        action.addEventListener('click', () => {
            const page = action.dataset.page;
            const icon = action.querySelector('i');
            if (!icon) return;

            if (action.classList.contains('locked')) {
                window.showToast('Coming soon');
                radialActions?.classList.remove('show');
                menuOpen = false;
                return;
            }

            if (
                (icon.classList.contains('fa-house') && currentPage === 'home') ||
                (icon.classList.contains('fa-book') && currentPage === 'subjects') ||
                (icon.classList.contains('fa-clipboard-question') && currentPage === 'assessment')
            ) {
                window.showToast("🏠 You're already here.");
                radialActions?.classList.remove('show');
                menuOpen = false;
                return;
            }

            setActivePage(page);

            if (icon.classList.contains('fa-house')) {
                navigateWithAnimation('/professordb/ProfessorDb', 'Going to dashboard...');
            } else if (icon.classList.contains('fa-book')) {
                navigateWithAnimation('/AdminSubject', 'Opening subjects...');
            } else if (icon.classList.contains('fa-clipboard-question')) {
                const target = currentClassCode ? `/AdminAssessmentList?classCode=${encodeURIComponent(currentClassCode)}` : '/AdminAssessmentList';
                navigateWithAnimation(target, 'Opening assessments...');
            }
        });
    });

    function navigateWithAnimation(url, message) {
        window.showToast(message);
        radialActions?.classList.remove('show');
        menuOpen = false;
        setTimeout(() => {
            window.location.href = url;
        }, 600);
    }

    document.addEventListener('click', (e) => {
        if (!menuCircle?.contains(e.target) && !radialActions?.contains(e.target)) {
            menuOpen = false;
            radialActions?.classList.remove('show');
        }
    });
}
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', setupAdminBottomBar);
} else {
    setupAdminBottomBar();
}

window.showToast = function(message) {
    const toastEl = document.getElementById('toast') || document.querySelector('.toast');
    if (!toastEl) {
        const newToast = document.createElement('div');
        newToast.id = 'toast';
        newToast.className = 'toast';
        newToast.setAttribute('aria-live', 'polite');
        newToast.setAttribute('role', 'status');
        document.body.appendChild(newToast);
        newToast.textContent = message;
        newToast.classList.add('show');
        setTimeout(() => {
            newToast.classList.remove('show');
            setTimeout(() => newToast.remove(), 300);
        }, 3000);
        return;
    }
    toastEl.textContent = message;
    toastEl.classList.add('show');
    setTimeout(() => toastEl.classList.remove('show'), 3000);
};

