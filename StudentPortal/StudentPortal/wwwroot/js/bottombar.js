// --- ELEMENT REFERENCES ---
const userProfile = document.getElementById('userProfile');
const userPopup = document.getElementById('userPopup');
const menuCircle = document.getElementById('menuCircle');
const radialActions = document.getElementById('radialActions');
const actions = radialActions.querySelectorAll('.action');

// --- USER PROFILE DROPDOWN ---
userProfile.addEventListener('click', () => {
    userPopup.classList.toggle('show');
});

document.addEventListener('click', (e) => {
    if (!userProfile.contains(e.target)) {
        userPopup.classList.remove('show');
    }
});

// --- RADIAL MENU TOGGLE ---
let menuOpen = false;
menuCircle.addEventListener('click', () => {
    menuOpen = !menuOpen;
    radialActions.classList.toggle('show', menuOpen);
});

// --- PAGE SELECTION + NAVIGATION ---
let currentPage = 'home'; // current page is studentdb.html (home)

function setActivePage(page) {
    actions.forEach((a) => a.classList.toggle('selected', a.dataset.page === page));
    currentPage = page;
}
setActivePage(currentPage);

// --- RADIAL ACTION CLICK HANDLER ---
actions.forEach((action) => {
    action.addEventListener('click', () => {
        const page = action.dataset.page;
        const icon = action.querySelector('i');

        // --- VALIDATION: prevent navigating to same page ---
        if (
            (icon.classList.contains('fa-house') && currentPage === 'home') ||
            (icon.classList.contains('fa-book') && currentPage === 'subjects') ||
            (icon.classList.contains('fa-clipboard-question') && currentPage === 'assessment')
        ) {
            showToast("🏠 You're already here.");
            radialActions.classList.remove('show');
            menuOpen = false;
            return;
        }

        // --- Otherwise navigate normally ---
        setActivePage(page);

        if (icon.classList.contains('fa-house')) {
            navigateWithAnimation('admindb.html', 'Going to dashboard...');
        }
        else if (icon.classList.contains('fa-book')) {
            navigateWithAnimation('adminsubject.html', 'Opening subjects...');
        }
        else if (icon.classList.contains('fa-pencil')) {
            navigateWithAnimation('adminassessmentlist.html', 'Opening assessments...');
        }
    });
});

// --- NAVIGATION FUNCTION ---
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
    if (!menuCircle.contains(e.target) && !radialActions.contains(e.target)) {
        menuOpen = false;
        radialActions.classList.remove('show');
    }
});