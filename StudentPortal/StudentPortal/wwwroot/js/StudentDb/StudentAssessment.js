// ==========================================================
// studentassessment.js — Handles StudentAssessment/Index.cshtml (MVC)
// ==========================================================

// --- ELEMENT REFERENCES ---
const userProfile = document.getElementById('userProfile');
const userPopup = document.getElementById('userPopup');
const menuCircle = document.getElementById('menuCircle');
const radialActions = document.getElementById('radialActions');
const actions = radialActions?.querySelectorAll('.action') || [];
const backButton = document.querySelector('.back-button');
const toast = document.getElementById('toast');
const markDoneBtn = document.getElementById('markDoneBtn');
const confirmModal = document.getElementById('confirmModal');
const confirmYes = document.getElementById('confirmYes');
const confirmNo = document.getElementById('confirmNo');

// NEW: Open quiz element that should redirect to the answering page
const openQuiz = document.getElementById('openQuiz');

// --- PRIVATE COMMENT ELEMENTS (added) ---
const privateComment = document.getElementById('privateComment');
const commentPopup = document.getElementById('commentPopup');
const cancelComment = document.getElementById('cancelComment');
const addComment = document.getElementById('addComment');
const commentInput = document.getElementById('commentInput');
const commentPreview = document.getElementById('commentPreview');

let privateCommentData = "";

// submitted param toast
try {
    const params = new URLSearchParams(window.location.search || "");
    if (params.get('submitted') === '1') {
        if (toast) {
            toast.textContent = 'You already answered this assessment.';
            toast.className = 'toast show';
            setTimeout(() => toast.classList.remove('show'), 2500);
        }
    }
} catch (_) {}


// --- PAGE SELECTION + NAVIGATION ---
let currentPage = 'subjects';

function setActivePage(page) {
    actions.forEach((a) => a.classList.toggle('selected', a.dataset.page === page));
    currentPage = page;
}
setActivePage(currentPage);

actions.forEach((action) => {
    action.addEventListener('click', () => {
        const page = action.dataset.page;
        if (page === currentPage) {
            showToast("📝 You're already here.");
            radialActions.classList.remove('show');
            menuOpen = false;
            return;
        }

        // --- LOCKED BUTTON BEHAVIOR ---
        if (action.classList.contains('locked')) {
            showToast("Coming soon");
            radialActions.classList.remove('show');
            menuOpen = false;
            return;
        }

        let url = null;
        switch (page) {
            case 'home':
                url = '/StudentDb/StudentDb';
                showToast('Going to dashboard...');
                break;
            case 'todo':
                url = '/StudentDb/StudentTodo';
                showToast('Opening to-do list...');
                break;
        }

        radialActions.classList.remove('show');
        menuOpen = false;
        if (url) setTimeout(() => (window.location.href = url), 600);
    });
});

// --- OPEN QUIZ: redirect to StudentAnswerAssessment ---
openQuiz?.addEventListener('click', () => {
    const presetUrl = openQuiz?.dataset.url || '';
    if (presetUrl) {
        showToast('Opening assessment...');
        setTimeout(() => { window.location.href = presetUrl; }, 600);
        return;
    }

    let classCode = openQuiz?.dataset.classCode || '';
    let contentId = openQuiz?.dataset.contentId || '';

    if (!classCode || !contentId) {
        // Fallback: parse current path /StudentAssessment/{classCode}/{contentId}
        const parts = (window.location.pathname || '').split('/').filter(Boolean);
        const idx = parts.findIndex(p => p.toLowerCase() === 'studentassessment');
        if (idx >= 0 && parts.length >= idx + 3) {
            classCode = parts[idx + 1];
            contentId = parts[idx + 2];
        }
    }

    if (!classCode || !contentId) {
        showToast('Cannot open quiz: missing identifiers.');
        return;
    }

    showToast('Opening assessment...');
    setTimeout(() => { window.location.href = `/StudentAnswerAssessment/${classCode}/${contentId}`; }, 600);
});

// ==========================================================
// PRIVATE COMMENT POPUP (added)
// ==========================================================
privateComment?.addEventListener('click', () => {
    // load existing data, open popup and focus input
    if (commentInput) commentInput.value = privateCommentData;
    commentPopup?.classList.add('show');
    commentInput?.focus();
});

// close popup when clicking the overlay backdrop
commentPopup?.addEventListener('click', (e) => {
    if (e.target === commentPopup) commentPopup.classList.remove('show');
});

// cancel button
cancelComment?.addEventListener('click', () => {
    commentPopup?.classList.remove('show');
});

// save comment (Add)
addComment?.addEventListener('click', () => {
    const text = commentInput?.value?.trim() || "";
    if (text) {
        privateCommentData = text;
        if (commentPreview) commentPreview.textContent = text;
        privateComment?.classList.add('saved');
    } else {
        privateCommentData = "";
        if (commentPreview) commentPreview.textContent = "Add Private Comment";
        privateComment?.classList.remove('saved');
    }
    commentPopup?.classList.remove('show');
});

// ==========================================================
// BACK BUTTON
// ==========================================================
backButton?.addEventListener('click', () => {
    showToast('Returning to class...');
    setTimeout(() => (window.location.href = '/StudentClass'), 800);
});

// --- TOAST FUNCTION ---
function showToast(message) {
    if (!toast) return;
    toast.textContent = message;
    toast.className = 'toast show';
    setTimeout(() => toast.classList.remove('show'), 2500);
}

// --- MARK AS DONE (modal) ---
markDoneBtn?.addEventListener('click', () => {
    confirmModal?.classList.add('show');
});

confirmYes?.addEventListener('click', () => {
    confirmModal?.classList.remove('show');
    // submit the surrounding form (the form in the view uses asp-action="MarkAsDone")
    const form = document.querySelector('form');
    if (form) form.submit();
});

confirmNo?.addEventListener('click', () => confirmModal?.classList.remove('show'));
confirmModal?.addEventListener('click', e => {
    if (e.target === confirmModal) confirmModal.classList.remove('show');
});

// --- ESCAPE KEY: close popups & menus ---
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        commentPopup?.classList.remove('show');
        confirmModal?.classList.remove('show');
        userPopup?.classList.remove('show');
        radialActions?.classList.remove('show');
        menuOpen = false;
    }
});
