// ==========================================================
// studenttodo.js — Handles StudentTodo/Index.cshtml
// ==========================================================

const userProfile = document.getElementById('userProfile');
const userPopup = document.getElementById('userPopup');
const menuCircle = document.getElementById('menuCircle');
const radialActions = document.getElementById('radialActions');
const actions = radialActions?.querySelectorAll('.action') || [];
const toggleButton = document.getElementById('toggleButton');
const toastEl = document.getElementById('toast');


// --- RADIAL MENU TOGGLE ---
let menuOpen = false;
menuCircle?.addEventListener('click', () => {
    menuOpen = !menuOpen;
    radialActions?.classList.toggle('show', menuOpen);
});

// --- PAGE SELECTION + NAVIGATION ---
let currentPage = 'todo'; // current page is studenttodo

function setActivePage(page) {
    actions.forEach((a) => a.classList.toggle('selected', a.dataset.page === page));
    currentPage = page;
}
setActivePage(currentPage);

actions.forEach((action) => {
    action.addEventListener('click', () => {
        const page = action.dataset.page;
        const icon = action.querySelector('i');

        if (
            (icon.classList.contains('fa-house') && currentPage === 'home') ||
            (icon.classList.contains('fa-book') && currentPage === 'subjects') ||
            (icon.classList.contains('fa-pencil') && currentPage === 'todo')
        ) {
            showToast("🏠 You're already here.");
            radialActions.classList.remove('show');
            menuOpen = false;
            return;
        }

        setActivePage(page);

        if (icon.classList.contains('fa-house')) {
            navigateWithAnimation('/StudentDb/StudentDb', 'Going to dashboard...');
        }
        else if (icon.classList.contains('fa-book')) {
            navigateWithAnimation('/StudentClass', 'Opening subjects...');
        }
        else if (icon.classList.contains('fa-pencil')) {
            navigateWithAnimation('/StudentDb/StudentTodo', 'Opening to-do list...');
        }
    });
});

function navigateWithAnimation(url, message) {
    showToast(message);
    radialActions.classList.remove('show');
    menuOpen = false;
    setTimeout(() => {
        window.location.href = url;
    }, 600);
}

// --- HIDE RADIAL WHEN CLICKING OUTSIDE ---
document.addEventListener('click', (e) => {
    if (!menuCircle?.contains(e.target) && !radialActions?.contains(e.target)) {
        menuOpen = false;
        radialActions?.classList.remove('show');
    }
});

// --- TOGGLE BUTTON FOR TASK STATUS ---
const toggleOptions = document.querySelectorAll('.toggle-button .option');

function applyFilter(view) {
    const allTasks = document.querySelectorAll('.task-card');
    allTasks.forEach(task => {
        // If a task has no data-status, show it by default
        const status = task.dataset.status || 'todo';
        task.style.display = (status === view) ? 'flex' : 'none';
    });
}

toggleOptions.forEach(option => {
    option.addEventListener('click', () => {
        // Toggle active class
        toggleOptions.forEach(opt => opt.classList.remove('active'));
        option.classList.add('active');

        const view = option.dataset.view;
        applyFilter(view);
    });
});

// On load, show the default filter (todo)
document.addEventListener('DOMContentLoaded', () => {
    applyFilter('todo');
});

// --- TOAST NOTIFICATION ---
function showToast(message, type = '') {
    const toast = toastEl;
    if (!toast) return;
    toast.textContent = message;
    toast.className = `toast show ${type}`;
    setTimeout(() => {
        toast.classList.remove('show');
    }, 2800);
}
