(function () {
// --- ELEMENT REFERENCES ---
const userProfile = document.getElementById('userProfile');
const userPopup = document.getElementById('userPopup');
const menuCircle = document.getElementById('menuCircle');
const radialActions = document.getElementById('radialActions');
const actions = radialActions ? radialActions.querySelectorAll('.action') : [];
const createBtn = document.getElementById('createBtn');
const modalBackdrop = document.getElementById('modalBackdrop');
const subjectNameInput = document.getElementById('subjectName');
const subjectCodeInput = document.getElementById('subjectCode');
const createForm = document.getElementById('createForm');
const cancelCreate = document.getElementById('cancelCreate');
const sectionInput = document.getElementById('section');
const createModal = document.getElementById('createModal');
const courseSelect = document.getElementById('courseSelect');
const yearSelect = document.getElementById('yearSelect');
const semesterSelect = document.getElementById('semesterSelect');
const courseValueInput = document.getElementById('courseValue');
const yearValueInput = document.getElementById('yearValue');
const semesterValueInput = document.getElementById('semesterValue');

// --- USER PROFILE DROPDOWN ---
userProfile?.addEventListener('click', () => {
    userPopup?.classList.toggle('show');
});
document.addEventListener('click', (e) => {
    if (!userProfile?.contains(e.target)) {
        userPopup?.classList.remove('show');
    }
});

// --- RADIAL MENU TOGGLE ---
let menuOpen = false;
menuCircle?.addEventListener('click', () => {
    menuOpen = !menuOpen;
    radialActions?.classList.toggle('show', menuOpen);
});

// --- PAGE SELECTION + NAVIGATION ---
let currentPage = 'home';

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
            navigateWithAnimation('/AdminDb', 'Going to dashboard...');
        }
        else if (icon.classList.contains('fa-book')) {
            navigateWithAnimation('/AdminSubject', 'Opening subjects...');
        }
        else if (icon.classList.contains('fa-clipboard-question')) {
            navigateWithAnimation('/AdminAssessmentList', 'Opening assessments...');
        }
    });
});

// --- NAVIGATION FUNCTION ---
function navigateWithAnimation(url, message) {
    showToast(message);
    radialActions?.classList.remove('show');
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

// --- Clickable class cards (data-href) ---
document.querySelectorAll('.class-left[data-href]').forEach(el => {
    el.style.cursor = 'pointer';
    el.addEventListener('click', () => {
        const href = el.getAttribute('data-href');
        if (href) {
            showToast('Opening class...');
            setTimeout(() => window.location.href = href, 350);
        }
    });
});

// --- CREATE CLASS MODAL ---
function openCreateModal(event) {
    event?.preventDefault();
    event?.stopPropagation();
    if (!createBtn || !modalBackdrop) {
        return;
    }
    createBtn.classList.add('selected');
    modalBackdrop?.classList.add('show');
    modalBackdrop?.setAttribute('aria-hidden', 'false');
}
window.openCreateModal = openCreateModal;

modalBackdrop?.addEventListener('click', (e) => {
    if (e.target === modalBackdrop) {
        modalBackdrop.classList.remove('show');
        createBtn.classList.remove('selected');
        modalBackdrop?.setAttribute('aria-hidden', 'true');
    }
});

createModal?.addEventListener('click', (event) => {
    event.stopPropagation();
});

cancelCreate?.addEventListener('click', () => {
    modalBackdrop.classList.remove('show');
    createBtn.classList.remove('selected');
    modalBackdrop?.setAttribute('aria-hidden', 'true');
});

// client-side validation, then submit form to server
createForm?.addEventListener('submit', function (e) {
    const name = subjectNameInput?.value?.trim();
    const code = subjectCodeInput?.value?.trim();
    const course = courseValueInput?.value?.trim();
    const year = yearValueInput?.value?.trim();
    const semester = semesterValueInput?.value?.trim();
    let section = sectionInput?.value?.trim();

    if (sectionInput) {
        section = section?.toUpperCase();
        sectionInput.value = section ?? '';
    }

    if (!course || !year || !semester) {
        e.preventDefault();
        showToast('⚠️ Please select course, year, and semester.', 'warning');
        return;
    }

    if (!section || section.length !== 1 || !/[A-Z]/.test(section)) {
        e.preventDefault();
        showToast('⚠️ Section must be a single letter (A-Z).', 'warning');
        return;
    }

    if (!name || !code) {
        e.preventDefault();
        showToast('⚠️ Please fill in both fields.', 'warning');
        return;
    }
    // optionally show immediate feedback before redirect
    showToast(`Creating "${name}"...`);
    // let the form submit naturally (server will redirect and set TempData)
});

// --- ESC KEY CLOSE ---
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        modalBackdrop?.classList.remove('show');
        createBtn?.classList.remove('selected');
        userPopup?.classList.remove('show');
        radialActions?.classList.remove('show');
    }
});

// --- TOAST NOTIFICATION ---
function showToast(message, type = '') {
    const t = document.getElementById('toast');
    if (!t) return;
    t.textContent = message;
    t.className = `toast show ${type}`;
    setTimeout(() => {
        t.classList.remove('show');
    }, 2800);
}

// --- CUSTOM SELECT INITIALIZATION ---
const customSelects = [
    { container: courseSelect, input: courseValueInput, placeholder: 'Select Course' },
    { container: yearSelect, input: yearValueInput, placeholder: 'Select Year' },
    { container: semesterSelect, input: semesterValueInput, placeholder: 'Select Semester' }
];

function closeOtherSelects(except) {
    customSelects.forEach(({ container }) => {
        if (container && container !== except) {
            container.classList.remove('open');
        }
    });
}

customSelects.forEach(({ container, input, placeholder }) => {
    if (!container || !input) return;

    const selectedDisplay = container.querySelector('.selected');
    const options = container.querySelectorAll('.options div[data-value]');

    const setValue = (value, label) => {
        container.dataset.value = value ?? '';
        input.value = value ?? '';
        if (selectedDisplay) {
            selectedDisplay.textContent = label ?? placeholder;
        }
    };

    // Initialize display if value already present
    if (input.value) {
        const match = Array.from(options).find(opt => opt.dataset.value === input.value);
        setValue(input.value, match ? match.textContent : input.value);
    } else if (container.dataset.value) {
        const match = Array.from(options).find(opt => opt.dataset.value === container.dataset.value);
        setValue(container.dataset.value, match ? match.textContent : container.dataset.value);
    } else {
        setValue('', placeholder);
    }

    selectedDisplay?.addEventListener('click', (event) => {
        event.preventDefault();
        const isOpen = container.classList.contains('open');
        closeOtherSelects(container);
        container.classList.toggle('open', !isOpen);
    });

    options.forEach(option => {
        option.addEventListener('click', (event) => {
            event.preventDefault();
            event.stopPropagation();
            const value = option.dataset.value || '';
            const label = option.textContent || placeholder;
            setValue(value, label);
            container.classList.remove('open');
        });
    });
});

document.addEventListener('click', (event) => {
    customSelects.forEach(({ container }) => {
        if (container && !container.contains(event.target)) {
            container.classList.remove('open');
        }
    });
});
})();
