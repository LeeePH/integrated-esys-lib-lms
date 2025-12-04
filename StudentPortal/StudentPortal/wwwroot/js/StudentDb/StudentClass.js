// ==========================================================
// studentclass.js — Handles interactivity for StudentClass/Index.cshtml
// ==========================================================

// --- ELEMENT REFERENCES ---
const userProfile = document.getElementById('userProfile');
const userPopup = document.getElementById('userPopup');
const menuCircle = document.getElementById('menuCircle');
const radialActions = document.getElementById('radialActions');
const actions = radialActions?.querySelectorAll('.action') || [];
const backButton = document.querySelector('.back-button');
const toast = document.getElementById('toast');
const contentCards = document.querySelectorAll('.content-card');



// --- RADIAL MENU ---
let menuOpen = false;
menuCircle?.addEventListener('click', () => {
    menuOpen = !menuOpen;
    radialActions?.classList.toggle('show', menuOpen);
});

document.addEventListener('click', (e) => {
    if (!menuCircle?.contains(e.target) && !radialActions?.contains(e.target)) {
        menuOpen = false;
        radialActions?.classList.remove('show');
    }
});

// --- PAGE SELECTION + NAVIGATION ---
let currentPage = 'subjects'; // current page context: StudentClass

function setActivePage(page) {
    actions.forEach((a) => a.classList.toggle('selected', a.dataset.page === page));
    currentPage = page;
}
setActivePage(currentPage);

actions.forEach((action) => {
    action.addEventListener('click', () => {
        const page = action.dataset.page;
        if (page === currentPage) {
            showToast("📚 You're already here.");
            radialActions.classList.remove('show');
            menuOpen = false;
            return;
        }

        let url = null;
        switch (page) {
            case 'home':
                url = '/StudentDb/StudentDb'; // MVC: goes to StudentDbController.Index
                showToast('Going to dashboard...');
                break;
            case 'subjects':
                url = '/StudentClass'; // MVC: goes to StudentClassController.Index
                showToast('Opening subjects...');
                break;
            case 'todo':
                url = '/StudentTodo'; // MVC: goes to StudentTodoController.Index
                showToast('Opening to-do list...');
                break;
        }

        radialActions.classList.remove('show');
        menuOpen = false;
        if (url) setTimeout(() => (window.location.href = url), 600);
    });
});

// --- BACK BUTTON ---
backButton?.addEventListener('click', () => {
    showToast('Returning to dashboard...');
    setTimeout(() => (window.location.href = '/StudentDb/StudentDb'), 800);
});

// --- ESC CLOSE MENUS ---
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        userPopup?.classList.remove('show');
        radialActions?.classList.remove('show');
    }
});

// --- TOAST FUNCTION ---
function showToast(message) {
    if (!toast) return;
    toast.textContent = message;
    toast.className = 'toast show';
    setTimeout(() => toast.classList.remove('show'), 2500);
}

// --- CONTENT CARD NAVIGATION ---
contentCards.forEach((card) => {
    card.addEventListener('click', () => {
        const targetUrl = card.dataset.target;
        if (!targetUrl) return;

        showToast('Opening...');
        setTimeout(() => (window.location.href = targetUrl), 600);
    });

    card.style.cursor = 'pointer';
});

// --- RESPONSIVE HEIGHT SAFETY ---
window.addEventListener('resize', () => {
    const info = document.querySelector('.class-info');
    if (info) info.style.height = `calc(100vh - 145px)`; // matches layout spacing
});
