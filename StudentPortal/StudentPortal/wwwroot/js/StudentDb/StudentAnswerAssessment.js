// wwwroot/js/studentanswerassessment.js
// UI logic for StudentAnswerAssessment page + submission to server

const userProfile = document.getElementById('userProfile');
const userPopup = document.getElementById('userPopup');
const menuCircle = document.getElementById('menuCircle');
const radialActions = document.getElementById('radialActions');
const actions = radialActions?.querySelectorAll('.action') || [];
const submitBtn = document.getElementById('submitAssessment');
const toast = document.getElementById('toast');



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

// --- PAGE SELECTION ---
let currentPage = 'subjects'; // this page is within subject flow
function setActivePage(page) {
    actions.forEach((a) => a.classList.toggle('selected', a.dataset.page === page));
    currentPage = page;
}
setActivePage(currentPage);

// --- RADIAL ACTION CLICK HANDLER ---
actions.forEach((action) => {
    action.addEventListener('click', () => {
        const page = action.dataset.page;

        if (page === currentPage) {
            showToast("You're already here.");
            return;
        }

        let message = "";
        let url = "";
        if (page === "home") {
            message = "Going to dashboard...";
            url = "/studentdb/StudentDb"; // MVC route (explicit) — adjust if different
        } else if (page === "subjects") {
            message = "Opening subjects...";
            url = "/StudentClass";
        } else if (page === "todo") {
            message = "Opening to-do list...";
            url = "/StudentTodo";
        }

        showToast(message);
        setTimeout(() => {
            if (url) window.location.href = url;
        }, 600);
    });
});

// --- COLLECT ANSWERS FROM DOM
function collectAnswers() {
    const answers = [];

    // Radio groups (multiple choice): select all radio inputs and group by name
    const radios = document.querySelectorAll('input[type="radio"]');
    const radioNames = new Set();
    radios.forEach(r => radioNames.add(r.name));
    radioNames.forEach(name => {
        const checked = document.querySelector(`input[type="radio"][name="${name}"]:checked`);
        answers.push({
            questionId: name,
            response: checked ? checked.parentElement?.innerText?.trim() ?? checked.value : ""
        });
    });

    // Short/long textareas
    const textareas = document.querySelectorAll('textarea.long-answer, textarea');
    textareas.forEach((ta, idx) => {
        // try to find an id/name nearby, else create one
        const qId = ta.getAttribute('data-question-id') || ta.name || `ta_${idx}`;
        answers.push({
            questionId: qId,
            response: ta.value.trim()
        });
    });

    return answers;
}

// helper: try to get a short anti-cheat summary
async function gatherAntiCheatSummary() {
    try {
        if (window.AntiCheat && typeof window.AntiCheat.info === 'function') {
            const info = window.AntiCheat.info();
            // give small summary — controller will log it for now
            return JSON.stringify({ loadedAt: info.loadedAt, modules: info.modules });
        }
        return "";
    } catch (err) {
        console.warn("anti-cheat summary failed:", err);
        return "";
    }
}

// --- SUBMIT BUTTON (POST to MVC JSON endpoint) ---
submitBtn?.addEventListener('click', async (e) => {
    e.preventDefault();
    showToast("Submitting assessment...");

    const answers = collectAnswers();
    const antiSummary = await gatherAntiCheatSummary();

    const payload = {
        StudentId: null, // set if you have current user id
        Answers: answers,
        AntiCheatSummary: antiSummary
    };

    try {
        const resp = await fetch('/StudentAnswerAssessment/SubmitJson', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        if (!resp.ok) {
            const text = await resp.text();
            console.error('submit failed', resp.status, text);
            showToast("Submission failed. Try again.");
            return;
        }

        const data = await resp.json().catch(() => ({}));
        showToast(data?.message || "✅ Assessment submitted successfully!");
        setTimeout(() => {
            // redirect to student todo / results page
            window.location.href = "/StudentTodo";
        }, 1200);
    } catch (err) {
        console.error('submit error', err);
        showToast("Network error — submission failed.");
    }
});

// --- TOAST NOTIFICATION ---
function showToast(message) {
    if (!toast) return;
    toast.textContent = message;
    toast.className = "toast show";
    setTimeout(() => toast.classList.remove("show"), 2500);
}
