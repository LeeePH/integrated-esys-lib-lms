function setupStudentBottomBar() {
    const userProfile = document.getElementById('userProfile');
    const userPopup = document.getElementById('userPopup');
    const menuCircle = document.getElementById('menuCircle');
    const radialActions = document.getElementById('radialActions');
    const actions = radialActions ? radialActions.querySelectorAll('.action') : [];
    const backButton = document.querySelector('.back-button');
    const data = window.studentBottomBarData || {};
    const classCode = data.classCode || document.body.dataset?.classCode || '';
    const currentPage = data.currentPage || '';

    // Popup
    userProfile?.addEventListener('click', (e) => {
        e.stopPropagation();
        userPopup?.classList.toggle('show');
    });
    document.addEventListener('click', (e) => {
        if (!userProfile?.contains(e.target)) userPopup?.classList.remove('show');
    });

    // Exit • Sign out
    document.getElementById('signOutBtn')?.addEventListener('click', (e) => {
        e.stopPropagation();
        console.log('Student logout clicked');
        window.showToast ? window.showToast('Signing out...') : null;
        userPopup?.classList.remove('show');
        setTimeout(() => window.location.href = '/Account/Logout', 300);
    });

    // Delegated fallback
    document.addEventListener('click', (e) => {
        const target = e.target;
        if (target && (target.id === 'signOutBtn' || (target.closest && target.closest('#signOutBtn')))) {
            e.stopPropagation();
            console.log('Student logout clicked (delegated)');
            window.showToast ? window.showToast('Signing out...') : null;
            userPopup?.classList.remove('show');
            setTimeout(() => window.location.href = '/Account/Logout', 300);
        }
    }, true);

    // Radial menu toggle
    let menuOpen = false;
    menuCircle?.addEventListener('click', () => {
        menuOpen = !menuOpen;
        radialActions?.classList.toggle('show', menuOpen);
    });

    function setActive(page) {
        actions.forEach(a => a.classList.toggle('selected', a.dataset.page === page));
    }
    setActive(currentPage);

    actions.forEach(action => {
        action.addEventListener('click', () => {
            if (action.classList.contains('locked')) {
                window.showToast ? window.showToast('Coming soon') : null;
                radialActions?.classList.remove('show');
                menuOpen = false;
                return;
            }
            const page = action.dataset.page;
            const icon = action.querySelector('i');
            if (!icon) return;

            if (
                (icon.classList.contains('fa-house') && currentPage === 'home') ||
                (icon.classList.contains('fa-book') && currentPage === 'subjects') ||
                (icon.classList.contains('fa-pencil') && currentPage === 'todo')
            ) {
                window.showToast ? window.showToast("You're already here.") : null;
                radialActions?.classList.remove('show');
                menuOpen = false;
                return;
            }

            setActive(page);

            if (icon.classList.contains('fa-house')) {
                navigateWithAnimation('/studentdb/StudentDb', 'Going to dashboard...');
            } else if (icon.classList.contains('fa-book')) {
                navigateWithAnimation('/studentdb/StudentDb', 'Opening classes...');
            } else if (icon.classList.contains('fa-pencil')) {
                navigateWithAnimation('/StudentTodo', 'Opening to-do...');
            }
        });
    });

    // Back button
    backButton?.addEventListener('click', () => {
        window.showToast ? window.showToast('Going back...') : null;
        setTimeout(() => window.location.href = '/studentdb/StudentDb', 500);
    });

    function navigateWithAnimation(url, message) {
        window.showToast ? window.showToast(message) : null;
        radialActions?.classList.remove('show');
        menuOpen = false;
        setTimeout(() => window.location.href = url, 600);
    }
}
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', setupStudentBottomBar);
} else {
    setupStudentBottomBar();
}


