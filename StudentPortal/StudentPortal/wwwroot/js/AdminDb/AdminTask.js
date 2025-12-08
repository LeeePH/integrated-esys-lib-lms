// admintask.js — client logic adapted for MVC view

document.addEventListener('DOMContentLoaded', () => {
    const userProfile = document.getElementById('userProfile');
    const userPopup = document.getElementById('userPopup');
    const menuCircle = document.getElementById('menuCircle');
    const radialActions = document.getElementById('radialActions');
    const actions = radialActions ? radialActions.querySelectorAll('.action') : [];
  const toast = document.getElementById('toast');
  const backButton = document.querySelector('.back-button');

    const makeChangesButton = document.getElementById('makeChangesButton');
    const adminActions = document.getElementById('adminActions');
    const deleteModal = document.getElementById('deleteModal');
    const confirmDelete = document.getElementById('confirmDelete');
    const cancelDelete = document.getElementById('cancelDelete');

    const taskTitle = document.getElementById('taskTitle');
    const taskDescription = document.getElementById('taskDescription');
    const attachmentList = document.getElementById('attachmentList');
    const editControls = document.getElementById('editControls');
    const saveEditBtn = document.getElementById('saveEditBtn');
    const cancelEditBtn = document.getElementById('cancelEditBtn');
    const dateInfo = document.getElementById('dateInfo');

    // submissions list refs
    const searchStudents = document.getElementById('searchStudents');
    const submittedList = document.getElementById('submittedList');
    const submittedCountEl = document.getElementById('submittedCount');
    const totalStudentsEl = document.getElementById('totalStudents');

    // --- initial
    if (editControls) { editControls.style.display = 'none'; editControls.classList.remove('show'); }

    // Bottom bar interactions are managed by AdminBottomBar.js

    // back button -> AdminClass (by classCode) or dashboard
    backButton?.addEventListener('click', () => {
        const classCode = document.body?.dataset?.classCode || '';
        showToast('Returning...');
        const target = classCode ? `/AdminClass/${classCode}` : '/admindb/AdminDb';
        setTimeout(() => window.location.href = target, 800);
    });

  // toast
  function showToast(message) {
        if (!toast) return;
        toast.textContent = message;
        toast.className = 'toast show';
        setTimeout(() => toast.classList.remove('show'), 2500);
    }

  // admin actions menu
    let adminMenuOpen = false;
    makeChangesButton?.addEventListener('click', (e) => {
        adminMenuOpen = !adminMenuOpen;
        adminActions?.classList.toggle('show', adminMenuOpen);
        e.stopPropagation();
    });

    document.addEventListener('click', (e) => {
        if (!adminActions?.contains(e.target) && !makeChangesButton?.contains(e.target)) {
            adminActions?.classList.remove('show');
            adminMenuOpen = false;
        }
    });

    adminActions?.addEventListener('click', (e) => {
        const action = e.target.closest('.admin-action');
        if (!action) return;

        // LOCKED BUTTON FOR ADMIN ACTIONS
        if (action.classList.contains('locked')) {
            showToast("Coming soon");
            adminActions?.classList.remove('show');
            adminMenuOpen = false;
            return;
        }

        const type = action.dataset.action;
        adminActions.classList.remove('show');
        adminMenuOpen = false;

        if (type === 'edit') enterEditMode();
        else if (type === 'delete') showDeleteModal();
    });

    // delete flow
    function showDeleteModal() { if (!deleteModal) return; deleteModal.classList.add('show'); }
    function getTaskIdFromForm() {
        const form = document.getElementById('adminCommentForm');
        const input = form ? form.querySelector('input[name="taskId"]') : null;
        return input ? input.value : '';
    }

    confirmDelete?.addEventListener('click', async () => {
        if (!deleteModal) return;
        deleteModal.classList.remove('show');
        try {
            const taskId = getTaskIdFromForm();
            const res = await fetch('/AdminTask/DeleteTask', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ taskId })
            });
            if (!res.ok) throw new Error('Delete request failed');
            const data = await res.json();
            if (data && data.success) {
                showToast('🗑️ Task deleted.');
                const classCode = document.body?.dataset?.classCode || '';
                setTimeout(() => { window.location.href = classCode ? `/AdminClass/${classCode}` : '/admindb/AdminDb'; }, 800);
            } else {
                throw new Error(data?.message || 'Delete failed');
            }
        } catch (e) {
            showToast('❌ Failed to delete task');
        }
    });
    cancelDelete?.addEventListener('click', () => { if (!deleteModal) return; deleteModal.classList.remove('show'); });
    document.addEventListener('click', (e) => {
        if (deleteModal && deleteModal.classList.contains('show') && e.target === deleteModal) deleteModal.classList.remove('show');
    });

    // edit mode
    let inEditMode = false;
    let originalDateText = '';

    function enterEditMode() {
        if (inEditMode) return;
        inEditMode = true;
        showToast('✏️ Editing task...');

        if (taskTitle) { taskTitle.contentEditable = 'true'; taskTitle.focus(); placeCaretAtEnd(taskTitle); }
        if (taskDescription) taskDescription.contentEditable = 'true';

        originalDateText = dateInfo.textContent;
        const parts = dateInfo.textContent.split('|').map(p => p.trim());
        const postedText = parts.find(p => p.startsWith('Posted:')) || '';
        const deadlineText = parts.find(p => p.startsWith('Deadline:'));
        const currentDeadline = deadlineText ? deadlineText.replace('Deadline:', '').trim() : '';

        const deadlineInput = document.createElement('input');
        deadlineInput.type = 'date';
        deadlineInput.id = 'deadlineInput';
        if (currentDeadline) {
            const d = new Date(currentDeadline);
            if (!isNaN(d)) deadlineInput.value = d.toISOString().split('T')[0];
        }

        dateInfo.innerHTML = `${postedText} | Deadline: `;
        dateInfo.appendChild(deadlineInput);

        revealEditControls();
    }

    function revealEditControls() {
        if (!editControls) return;
        editControls.style.display = 'flex';
        requestAnimationFrame(() => editControls.classList.add('show'));
    }

    function hideEditControls() {
        if (!editControls) return;
        editControls.classList.remove('show');
        setTimeout(() => editControls.style.display = 'none', 320);
    }

    function placeCaretAtEnd(el) {
        try {
            const range = document.createRange();
            const sel = window.getSelection();
            range.selectNodeContents(el);
            range.collapse(false);
            sel.removeAllRanges();
            sel.addRange(range);
        } catch (err) { }
    }

    function exitEditMode(saveChanges = false) {
        if (!inEditMode) return;
        inEditMode = false;

        if (taskTitle) taskTitle.contentEditable = 'false';
        if (taskDescription) taskDescription.contentEditable = 'false';

        const deadlineInput = document.getElementById('deadlineInput');
        const newDeadline = deadlineInput ? deadlineInput.value : '';
        if (deadlineInput) deadlineInput.remove();

        if (saveChanges) {
            (async () => {
                try {
                    const taskId = getTaskIdFromForm();
                    const title = taskTitle?.textContent?.trim() || '';
                    const description = taskDescription?.textContent?.trim() || '';
                    const deadline = newDeadline ? new Date(newDeadline).toISOString() : null;
                    const res = await fetch('/AdminTask/UpdateTask', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ taskId, title, description, deadline })
                    });
                    if (!res.ok) throw new Error('Update request failed');
                    const data = await res.json();
                    if (data && data.success) {
                        const today = new Date().toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
                        const formattedDeadline = newDeadline ? new Date(newDeadline).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) : 'N/A';
                        dateInfo.textContent = `Posted: ${originalDateText.split('|')[0].trim()} | Edited: ${today} | Deadline: ${formattedDeadline}`;
                        showToast('✅ Changes saved.');
                    } else {
                        throw new Error(data?.message || 'Update failed');
                    }
                } catch (e) {
                    showToast('❌ Failed to save changes');
                }
            })();
        } else {
            dateInfo.textContent = originalDateText;
            showToast('✖️ Edit cancelled.');
        }

        hideEditControls();
    }

    // ------------------ REPLACE ATTACHMENT (upload + link) -----
    const replaceFileBtn = document.getElementById('replaceFileBtn');
    const replaceFileInput = document.getElementById('replaceFileInput');

    replaceFileBtn?.addEventListener('click', (e) => {
        e.preventDefault();
        if (!inEditMode) { showToast('Enable edit mode first'); return; }
        replaceFileInput?.click();
    });

    replaceFileInput?.addEventListener('change', async () => {
        const file = replaceFileInput.files && replaceFileInput.files[0];
        if (!file) return;
        try {
            const classCode = document.body?.dataset?.classCode || '';
            const fd = new FormData();
            fd.append('file', file);
            fd.append('classCode', classCode);
            fd.append('type', 'task');
            const up = await fetch('/AdminClass/UploadFile', { method: 'POST', body: fd });
            if (!up.ok) throw new Error('Upload failed');
            const upRes = await up.json();
            if (!upRes.success) throw new Error(upRes.message || 'Upload failed');

            const taskId = getTaskIdFromForm();
            const linkRes = await fetch('/AdminTask/ReplaceAttachment', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ taskId, fileName: file.name, fileUrl: upRes.fileUrl })
            });
            if (!linkRes.ok) throw new Error('Replace failed');
            const linkJson = await linkRes.json();
            if (!linkJson.success) throw new Error(linkJson.message || 'Replace failed');

            // Update UI attachment list if present
            const attList = document.getElementById('attachmentList');
            if (attList) {
                attList.innerHTML = '';
                const box = document.createElement('div');
                box.className = 'attachment-box';
                box.innerHTML = `<i class=\"fa-solid fa-file\"></i> ${file.name}`;
                attList.appendChild(box);
            }
            showToast('✅ File replaced');
        } catch (err) {
            console.error(err);
            showToast('❌ ' + (err.message || 'Could not replace file'));
        } finally {
            try { replaceFileInput.value = ''; } catch {}
        }
    });

    saveEditBtn?.addEventListener('click', () => exitEditMode(true));
    cancelEditBtn?.addEventListener('click', () => exitEditMode(false));

    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && inEditMode) {
            exitEditMode(false);
        }
    });

    // --- submissions rendering & search ---
    function parseSubmissionsFromDom() {
        const rows = Array.from(document.querySelectorAll('.submitted-row')).filter(r => !r.classList.contains('header'));
        return rows.map(r => {
            const id = r.dataset.id;
            const name = (r.querySelector('.name-col') || r.querySelector('.col.name-col')).textContent.trim();
            const statusText = (r.querySelector('.status-col') || r.querySelector('.col.status-col')).textContent.trim();
            return { id, name, statusText, row: r };
        });
    }

    let submissionsData = parseSubmissionsFromDom();
    function updateSubmissionCounters() {
        const submittedCount = submissionsData.filter(s => /submitted/i.test(s.statusText)).length;
        const total = submissionsData.length;
        if (submittedCountEl) submittedCountEl.textContent = submittedCount;
        if (totalStudentsEl) totalStudentsEl.textContent = total;
    }
    updateSubmissionCounters();

    searchStudents?.addEventListener('input', (e) => {
        const q = (e.target.value || '').trim().toLowerCase();
        submissionsData.forEach(item => {
            const visible = item.name.toLowerCase().includes(q);
            item.row.style.display = visible ? '' : 'none';
        });
    });

    submittedList?.addEventListener('click', async (e) => {
        const row = e.target.closest('.submitted-row');
        if (!row || row.classList.contains('header')) return;
        const id = row.dataset.id;
        await openSubmissionModal(id, row.querySelector('.name-col')?.textContent?.trim() || 'Student');
    });

    // ==========================
    // ADMIN PUBLIC COMMENTS
    // ==========================
    const adminCommentContainer = document.getElementById('adminCommentContainer');
    const adminCommentForm = document.getElementById('adminCommentForm');
    const adminCommentText = document.getElementById('adminCommentText');
    const postAdminCommentBtn = document.getElementById('postAdminCommentBtn');
    const adminCommentList = document.getElementById('adminCommentList');

    function getAdminAntiForgeryToken() {
        const el = adminCommentForm ? adminCommentForm.querySelector('input[name="__RequestVerificationToken"]') : null;
        return el ? el.value : '';
    }

    function getTaskId() {
        const input = adminCommentForm ? adminCommentForm.querySelector('input[name="taskId"]') : null;
        return input ? input.value : '';
    }

    function getClassCode() {
        const input = adminCommentForm ? adminCommentForm.querySelector('input[name="classCode"]') : null;
        return input ? input.value : '';
    }

    async function loadAdminComments() {
        const taskId = getTaskId();
        if (!taskId || !adminCommentList) return;
        try {
            const res = await fetch('/AdminTask/GetComments?taskId=' + encodeURIComponent(taskId), { credentials: 'same-origin' });
            const data = await res.json();
            if (!data || !data.success) return;
            adminCommentList.innerHTML = '';
            data.comments.forEach(renderAdminComment);
        } catch {}
    }

    function renderAdminComment(c) {
        const box = document.createElement('div');
        box.className = 'comment-box';
        box.dataset.id = c.id;
        const nameHtml = `<div class="student-name">${escapeHtml(c.authorName)}${c.role ? ` • ${escapeHtml(c.role)}` : ''}</div>`;
        const textHtml = `<div class="comment-text">${escapeHtml(c.text)}</div>`;
        const dateHtml = `<div class="comment-datetime">${new Date(c.createdAt).toLocaleString()}</div>`;
        box.innerHTML = `${nameHtml}${textHtml}${dateHtml}`;
        if (Array.isArray(c.replies)) {
            c.replies.forEach(r => {
                const rdiv = document.createElement('div');
                rdiv.className = 'instructor-reply';
                rdiv.innerHTML = `<div><i class="fa-solid fa-reply"></i> <span class="instructor-name">${escapeHtml(r.authorName)}${r.role ? ` • ${escapeHtml(r.role)}` : ''}</span></div><div class="reply-text">${escapeHtml(r.text)}</div><div class="reply-datetime">${new Date(r.createdAt).toLocaleString()}</div>`;
                box.appendChild(rdiv);
            });
        }
        const replyToggle = document.createElement('div');
        replyToggle.className = 'reply-option';
        replyToggle.innerHTML = '<i class="fa-solid fa-reply"></i> Reply';
        box.appendChild(replyToggle);

        const replyArea = document.createElement('div');
        replyArea.className = 'reply-box-area';
        replyArea.innerHTML = `<textarea class="reply-box" placeholder="Write a reply..."></textarea><button class="reply-submit-btn">Reply</button>`;
        box.appendChild(replyArea);

        replyToggle.addEventListener('click', () => {
            replyArea.style.display = (replyArea.style.display === 'none' || replyArea.style.display === '') ? 'flex' : 'none';
        });

        const submitBtn = replyArea.querySelector('.reply-submit-btn');
        submitBtn.addEventListener('click', async () => {
            const text = (replyArea.querySelector('.reply-box').value || '').trim();
            if (!text) return;
            const token = getAdminAntiForgeryToken();
            try {
                const res = await fetch('/AdminTask/PostReply', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token },
                    body: new URLSearchParams({ commentId: c.id, text })
                });
                const data = await res.json();
                if (data && data.success && data.reply) {
                    loadAdminComments();
                }
            } catch {}
        });

        adminCommentList.appendChild(box);
    }

    postAdminCommentBtn?.addEventListener('click', async () => {
        const text = (adminCommentText?.value || '').trim();
        if (!text) return;
        const token = getAdminAntiForgeryToken();
        const taskId = getTaskId();
        const classCode = getClassCode();
        try {
            const res = await fetch('/AdminTask/PostComment', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token },
                body: new URLSearchParams({ taskId, classCode, text })
            });
            const data = await res.json();
            if (data && data.success) {
                adminCommentText.value = '';
                loadAdminComments();
            }
        } catch {}
    });

    loadAdminComments();
});

// ==========================
// CHECK SUBMISSION MODAL
// ==========================
const checkSubmissionModal = document.getElementById('checkSubmissionModal');
const modalStudentName = document.getElementById('modalStudentName');
const modalAttachments = document.getElementById('modalAttachments');
const privateCommentDisplay = document.getElementById('privateCommentDisplay');
const closeSubmissionModal = document.getElementById('closeSubmissionModal');
const pointsEarnedInput = document.getElementById('pointsEarned');
const pointsMaxSpan = document.getElementById('pointsMax');
const remarksInput = document.getElementById('remarks');
const adminGradeForm = document.getElementById('adminGradeForm');

async function openSubmissionModal(submissionId, studentName) {
    try {
        modalStudentName.textContent = studentName || 'Student';
        const res = await fetch('/AdminTask/GetSubmission?submissionId=' + encodeURIComponent(submissionId), { credentials: 'same-origin' });
        const data = await res.json();
        if (!data || !data.success || !data.submission) { checkSubmissionModal.classList.add('show'); return; }

        const s = data.submission;
        const taskMax = parseInt(document.body?.dataset?.taskMax || '100', 10) || 100;
        pointsMaxSpan.textContent = String(taskMax);
        pointsEarnedInput.max = String(taskMax);
        // Normalize grade value into points for input: support "x/y" and "%"
        let gradeText = (s.grade || '').trim();
        let pointsValue = '';
        if (gradeText) {
            if (gradeText.includes('/')) {
                const parts = gradeText.split('/');
                if (parts.length === 2) { pointsValue = parts[0].trim(); }
            } else if (gradeText.endsWith('%')) {
                const pct = parseFloat(gradeText.slice(0, -1));
                if (!isNaN(pct)) { pointsValue = String(Math.round(taskMax * (pct / 100))); }
            } else {
                pointsValue = gradeText;
            }
        }
        pointsEarnedInput.value = pointsValue;
        remarksInput.value = s.feedback || '';

        privateCommentDisplay.textContent = s.feedback ? escapeHtml(s.feedback) : 'No comment provided.';

        modalAttachments.innerHTML = '';
        if (s.fileName) {
            const file = document.createElement('a');
            file.className = 'attachment-box';
            file.href = s.fileUrl || '#';
            file.target = '_blank';
            file.rel = 'noopener';
            const size = s.fileSize ? ` (${Math.round(s.fileSize/1024)} KB)` : '';
            file.innerHTML = `<i class="fa-solid fa-file"></i> ${escapeHtml(s.fileName)}${size}`;
            modalAttachments.appendChild(file);
            modalAttachments.classList.remove('empty');
        } else {
            modalAttachments.classList.add('empty');
        }

        checkSubmissionModal.classList.add('show');

        const saveBtn = document.querySelector('.save-grade');
        saveBtn.onclick = async () => {
            const tokenEl = adminGradeForm ? adminGradeForm.querySelector('input[name="__RequestVerificationToken"]') : null;
            const token = tokenEl ? tokenEl.value : '';
            const grade = (pointsEarnedInput.value || '').trim();
            const feedback = (remarksInput.value || '').trim();
            const approve = true;
            const pass = (() => { const n = parseFloat(grade); return !isNaN(n) ? (taskMax > 0 ? (n / taskMax) * 100 : n) >= 75 : false; })();
            const res2 = await fetch('/AdminTask/GradeSubmission', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token },
                body: new URLSearchParams({ submissionId, grade, feedback, approve: String(approve), pass: String(pass) })
            });
            const d2 = await res2.json();
            if (d2 && d2.success) {
                showToast('✅ Grade saved');
                checkSubmissionModal.classList.remove('show');
            } else {
                showToast('Failed to save');
            }
        };
    } catch (err) {
        checkSubmissionModal.classList.add('show');
    }
}

closeSubmissionModal?.addEventListener('click', () => {
    checkSubmissionModal.classList.remove('show');
});

checkSubmissionModal?.addEventListener('click', (e) => {
    if (e.target === checkSubmissionModal) checkSubmissionModal.classList.remove('show');
});

function escapeHtml(s) {
    try { return s.replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c])); } catch { return s; }
}
  // Library modal elements (same as Student Task)
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
  // ==========================
  // Library Module
  // ==========================
  (function libraryModule() {
    const books = [
      { id: 1, title: 'Introduction to Algorithms', author: 'Thomas H. Cormen', category: 'Computer Science', description: 'A comprehensive introduction to modern algorithm design and analysis...' },
      { id: 2, title: 'Clean Code', author: 'Robert C. Martin', category: 'Software Engineering', description: 'Guidelines and best practices for writing clean, maintainable, and testable code...' },
      { id: 3, title: 'Database System Concepts', author: 'Abraham Silberschatz', category: 'Databases', description: 'Core concepts of relational databases, SQL, query optimization, and database design.' }
    ];

    let filtered = [...books];
    let selectedId = null;

    function renderList() {
      if (!libraryListEl) return;
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
        li.dataset.id = book.id;
        li.setAttribute('role', 'option');
        li.className = selectedId === book.id ? 'selected' : '';
        li.innerHTML = `<div class="book-title">${escapeHtml(book.title)}</div><div class="book-category">Category: ${escapeHtml(book.category)}</div>`;
        li.addEventListener('click', () => selectBook(book.id));
        libraryListEl.appendChild(li);
      });
    }

    function selectBook(id) {
      const book = books.find(b => b.id === id);
      if (!book) return;
      selectedId = id;
      libraryListEl?.querySelectorAll('li').forEach(it => { it.classList.toggle('selected', Number(it.dataset.id) === id); });
      if (bookInfoDefault) bookInfoDefault.hidden = true;
      if (bookDetail) bookDetail.hidden = false;
      if (detailTitle) detailTitle.textContent = book.title;
      if (detailAuthor) detailAuthor.textContent = `Author: ${book.author}`;
      if (detailCategory) detailCategory.textContent = book.category;
      if (detailDescription) detailDescription.value = book.description;
      if (reserveBtn) reserveBtn.disabled = false;
    }

    function filterBooks(query) {
      query = (query || '').trim().toLowerCase();
      filtered = query ? books.filter(b => b.title.toLowerCase().includes(query) || b.category.toLowerCase().includes(query) || b.description.toLowerCase().includes(query)) : [...books];
      if (selectedId && !filtered.find(b => b.id === selectedId)) {
        selectedId = null;
        if (bookInfoDefault) bookInfoDefault.hidden = false;
        if (bookDetail) bookDetail.hidden = true;
        if (reserveBtn) reserveBtn.disabled = true;
      }
      renderList();
    }

    function openLibrary() {
      showToast('Opening Library…');
      setTimeout(() => {
        if (libraryBackdrop) libraryBackdrop.hidden = false;
        filtered = [...books];
        renderList();
        librarySearch?.focus();
      }, 500);
    }

    function closeLibrary() { if (libraryBackdrop) libraryBackdrop.hidden = true; }

    libraryBtn?.addEventListener('click', (e) => { e.preventDefault(); openLibrary(); });
    closeLibraryBtn?.addEventListener('click', closeLibrary);
    libraryBackdrop?.addEventListener('click', (e) => { if (e.target === libraryBackdrop) closeLibrary(); });
    librarySearch?.addEventListener('input', (e) => filterBooks(e.target.value));
    reserveBtn?.addEventListener('click', () => { if (!selectedId) return; reserveBackdrop.hidden = false; });
    reserveYesBtn?.addEventListener('click', () => { reserveBackdrop.hidden = true; showToast('Redirecting to Library…'); setTimeout(() => (window.location.href = '/Library/ReserveSuccess'), 900); });
    reserveNoBtn?.addEventListener('click', () => { reserveBackdrop.hidden = true; });
    reserveBackdrop?.addEventListener('click', (e) => { if (e.target === reserveBackdrop) reserveBackdrop.hidden = true; });
    document.addEventListener('keydown', (e) => { if (e.key === 'Escape') { if (!libraryBackdrop?.hidden) closeLibrary(); if (!reserveBackdrop?.hidden) reserveBackdrop.hidden = true; } });
  })();
