// ==========================================================
// studentmaterial.js — Student Material Page Interactivity
// ==========================================================

// --- ELEMENT REFERENCES ---
const userProfile = document.getElementById('userProfile');
const userPopup = document.getElementById('userPopup');
const menuCircle = document.getElementById('menuCircle');
const radialActions = document.getElementById('radialActions');
const actions = radialActions?.querySelectorAll('.action') || [];
const backButton = document.querySelector('.back-button');
const toast = document.getElementById('toast');



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

// --- PAGE SELECTION ---
let currentPage = 'subjects';
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
                url = '/studentdb/StudentDb'; // ✅ FIXED PATH
                showToast('Going to dashboard...');
                break;
            case 'subjects':
                url = '/StudentClass'; // ✅ Correct controller path
                showToast('Opening subjects...');
                break;
            case 'todo':
                url = '/StudentTodo'; // ✅ FIXED PATH
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
    showToast('Returning to class...');
    setTimeout(() => (window.location.href = '/StudentClass'), 800);
});

// --- ESC CLOSE ---
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        userPopup?.classList.remove('show');
        radialActions?.classList.remove('show');
    }
});

// --- TOAST ---
function showToast(message) {
    toast.textContent = message;
    toast.className = 'toast show';
    setTimeout(() => toast.classList.remove('show'), 2500);
}

// Library Modal - now backed by real Library System data via /Library/ApiSearch and /Library/ReserveBook
const libraryBtn = document.getElementById('libraryBtn');
const libraryBackdrop = document.getElementById('libraryBackdrop');
const closeLibraryBtn = document.getElementById('closeLibraryBtn');
const librarySearch = document.getElementById('librarySearch');
const libraryListEl = document.getElementById('libraryList');
const bookInfoDefault = document.getElementById('bookInfoDefault');
const bookDetail = document.getElementById('bookDetail');
const detailTitle = document.getElementById('detailTitle');
const detailAuthor = document.getElementById('detailAuthor');
const detailCategory = document.getElementById('detailCategory');
const detailDescription = document.getElementById('detailDescription');
const reserveBtn = document.getElementById('reserveBtn');
const reserveBackdrop = document.getElementById('reserveBackdrop');
const reserveYesBtn = document.getElementById('reserveYesBtn');
const reserveNoBtn = document.getElementById('reserveNoBtn');

let books = [];
let filtered = [];
let selectedBookId = null;

function renderList() {
    libraryListEl.innerHTML = '';
    if (!filtered.length) {
        const li = document.createElement('li');
        li.textContent = 'No books found.';
        li.className = 'empty';
        libraryListEl.appendChild(li);
        return;
    }
    filtered.forEach(book => {
        const li = document.createElement('li');
        li.dataset.id = String(book.id);
        li.setAttribute('role', 'option');
        li.className = selectedBookId === book.id ? 'selected' : '';
        li.innerHTML = '<div class="book-title">' + (book.title || '') + '</div><div class="book-category">Subject: ' + (book.category || '') + '</div>';
        li.addEventListener('click', function() { selectBook(book.id); });
        libraryListEl.appendChild(li);
    });
}

function selectBook(id) {
    const book = books.find(function(b) { return String(b.id) === String(id); });
    if (!book) return;
    selectedBookId = book.id;
    bookInfoDefault.hidden = true;
    bookDetail.hidden = false;
    detailTitle.textContent = book.title || '';
    detailAuthor.textContent = book.author ? ('Author: ' + book.author) : '';
    detailCategory.textContent = book.category || '';
    detailDescription.value = book.description || '';
    reserveBtn.disabled = false;
}

async function loadBooks(query) {
    try {
        const url = '/Library/ApiSearch?q=' + encodeURIComponent(query || '');
        console.log('[StudentMaterial] Loading books from:', url);
        const res = await fetch(url, { credentials: 'same-origin' });
        const data = await res.json();
        console.log('[StudentMaterial] API response:', data);
        
        if (!data || !data.success) {
            console.warn('[StudentMaterial] API returned error:', data?.message || 'Unknown error');
            books = [];
            filtered = [];
            renderList();
            return;
        }
        books = data.books || [];
        filtered = books.slice();
        console.log(`[StudentMaterial] Loaded ${books.length} books`);
        
        if (selectedBookId && !filtered.find(function (b) { return String(b.id) === String(selectedBookId); })) {
            selectedBookId = null;
            bookInfoDefault.hidden = false;
            bookDetail.hidden = true;
            reserveBtn.disabled = true;
        }
        renderList();
    } catch (err) {
        console.error('[StudentMaterial] Error loading books:', err);
        books = [];
        filtered = [];
        renderList();
    }
}

function filterBooks(q) {
    // Reload from server on each query for accurate search
    loadBooks(q || '');
}

function openLibrary() {
    showToast('Opening Library…');
    setTimeout(function() {
        libraryBackdrop.hidden = false;
        loadBooks('');
        if (librarySearch) librarySearch.focus();
    }, 700);
}

function closeLibrary() { libraryBackdrop.hidden = true; }

libraryBtn && libraryBtn.addEventListener('click', function(e) { e.preventDefault(); openLibrary(); });
closeLibraryBtn && closeLibraryBtn.addEventListener('click', closeLibrary);
libraryBackdrop && libraryBackdrop.addEventListener('click', function(e) { if (e.target === libraryBackdrop) closeLibrary(); });
librarySearch && librarySearch.addEventListener('input', function(e) { filterBooks(e.target.value); });

reserveBtn && reserveBtn.addEventListener('click', async function() {
    if (!selectedBookId) return;
    reserveBackdrop.hidden = false;
});
reserveYesBtn && reserveYesBtn.addEventListener('click', async function() {
    reserveBackdrop.hidden = true;
    if (!selectedBookId) return;
    try {
        const res = await fetch('/Library/ReserveBook', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: 'bookId=' + encodeURIComponent(selectedBookId),
            credentials: 'same-origin'
        });
        const data = await res.json();
        showToast(data && data.message ? data.message : 'Reservation request sent.');
        if (data && data.success) {
            reserveBtn.disabled = true;
        }
    } catch {
        showToast('Error reserving book. Please try again.');
    }
});
reserveNoBtn && reserveNoBtn.addEventListener('click', function() { reserveBackdrop.hidden = true; });
reserveBackdrop && reserveBackdrop.addEventListener('click', function(e) { if (e.target === reserveBackdrop) reserveBackdrop.hidden = true; });
document.addEventListener('keydown', function(e) {
    if (e.key === 'Escape') {
        if (libraryBackdrop && !libraryBackdrop.hidden) closeLibrary();
        if (reserveBackdrop && !reserveBackdrop.hidden) reserveBackdrop.hidden = true;
    }
});

// Public Comments
const publicCommentText = document.getElementById('publicCommentText');
const postPublicCommentBtn = document.getElementById('postPublicCommentBtn');
const publicCommentList = document.getElementById('publicCommentList');
const materialForm = document.getElementById('materialForm');

function getAntiForgeryToken() {
    const el = materialForm ? materialForm.querySelector('input[name="__RequestVerificationToken"]') : null;
    return el ? el.value : '';
}

function escapeHtml(s) {
    return String(s || '').replace(/[&<>"']/g, function(m) { return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[m]; });
}

async function loadComments() {
    const idInput = materialForm ? materialForm.querySelector('input[name="contentId"]') : null;
    const contentId = idInput ? idInput.value : '';
    if (!contentId || !publicCommentList) return;
    const res = await fetch('/StudentMaterial/GetComments?contentId=' + encodeURIComponent(contentId), { credentials: 'same-origin' });
    const data = await res.json();
    if (!data || !data.success) return;
    publicCommentList.innerHTML = '';
    data.comments.forEach(renderComment);
}

function renderComment(c) {
    const box = document.createElement('div');
    box.className = 'comment-box';
    const dt = new Date(c.createdAt).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });
    box.innerHTML = '<div class="student-name">' + escapeHtml(c.authorName) + (c.role ? ' (' + escapeHtml(c.role) + ')' : '') + '</div>' +
        '<div class="comment-text">' + escapeHtml(c.text) + '</div>' +
        '<div class="comment-datetime">' + dt + '</div>' +
        '<div class="reply-option" role="button"><i class="fa-solid fa-comment-dots"></i> Reply</div>' +
        '<div class="reply-box-area" hidden>' +
          '<textarea class="reply-box" placeholder="Write a reply..."></textarea>' +
          '<button class="reply-submit-btn">Post Reply</button>' +
        '</div>';

    if (Array.isArray(c.replies)) {
        c.replies.forEach(function(r) {
            const rdt = new Date(r.createdAt).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });
            const reply = document.createElement('div');
            reply.className = 'instructor-reply';
            reply.innerHTML = '<div><i class="fa-solid fa-reply"></i> <span class="instructor-name">' + escapeHtml(r.authorName) + (r.role ? ' (' + escapeHtml(r.role) + ')' : '') + '</span></div>' +
                '<div class="reply-text">' + escapeHtml(r.text) + '</div>' +
                '<div class="reply-datetime">' + rdt + '</div>';
            const insertBefore = box.querySelector('.reply-option');
            if (insertBefore) insertBefore.insertAdjacentElement('beforebegin', reply);
        });
    }

    publicCommentList.appendChild(box);
    const replyToggle = box.querySelector('.reply-option');
    const replyArea = box.querySelector('.reply-box-area');
    const replyBtn = box.querySelector('.reply-submit-btn');
    const replyBox = box.querySelector('.reply-box');
    replyToggle && replyToggle.addEventListener('click', function() {
        if (!replyArea) return;
        const hidden = replyArea.getAttribute('hidden') !== null || replyArea.style.display === 'none' || replyArea.style.display === '';
        replyArea.style.display = hidden ? 'flex' : 'none';
        if (hidden) replyArea.removeAttribute('hidden');
    });
    replyBtn && replyBtn.addEventListener('click', async function() {
        const val = (replyBox && replyBox.value || '').trim();
        if (!val) return;
        const token = getAntiForgeryToken();
        const fd = new FormData(materialForm || undefined);
        fd.append('commentId', c.id);
        fd.append('text', val);
        const res = await fetch('/StudentMaterial/PostReply', { method: 'POST', body: fd, headers: { 'RequestVerificationToken': token }, credentials: 'same-origin' });
        const data = await res.json();
        if (data && data.success && data.reply) {
            const rdt = new Date(data.reply.createdAt).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });
            const reply = document.createElement('div');
            reply.className = 'instructor-reply';
            reply.innerHTML = '<div><i class="fa-solid fa-reply"></i> <span class="instructor-name">' + escapeHtml(data.reply.authorName) + (data.reply.role ? ' (' + escapeHtml(data.reply.role) + ')' : '') + '</span></div>' +
                '<div class="reply-text">' + escapeHtml(data.reply.text) + '</div>' +
                '<div class="reply-datetime">' + rdt + '</div>';
            const insertBefore = box.querySelector('.reply-option');
            if (insertBefore) insertBefore.insertAdjacentElement('beforebegin', reply);
            if (replyBox) replyBox.value = '';
            if (replyArea) replyArea.style.display = 'none';
        }
    });
}

postPublicCommentBtn && postPublicCommentBtn.addEventListener('click', function() {
    const text = (publicCommentText && publicCommentText.value || '').trim();
    const idInput = materialForm ? materialForm.querySelector('input[name="contentId"]') : null;
    const classInput = materialForm ? materialForm.querySelector('input[name="classCode"]') : null;
    const contentId = idInput ? idInput.value : '';
    const classCode = classInput ? classInput.value : '';
    if (!text || !contentId || !classCode || !publicCommentList) return;
    const token = getAntiForgeryToken();
    const fd = new FormData(materialForm || undefined);
    fd.append('text', text);
    fetch('/StudentMaterial/PostComment', { method: 'POST', body: fd, headers: { 'RequestVerificationToken': token }, credentials: 'same-origin' })
        .then(function(r) { return r.json(); })
        .then(function(d) {
            if (d && d.success && d.comment) {
                renderComment(d.comment);
                if (publicCommentText) publicCommentText.value = '';
            }
        })
        .catch(function() {});
});

loadComments();
