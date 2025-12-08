// ==========================================================
// studenttask.js — Cleaned & Optimized
// Handles: Submit / Mark Done / Unsubmit flows,
// Attachment List Modal, Reply Boxes, Library Modal
// ==========================================================

document.addEventListener('DOMContentLoaded', () => {

    // --- GLOBAL ELEMENTS ---
    const userProfile = document.getElementById('userProfile');
    const userPopup = document.getElementById('userPopup');
    const menuCircle = document.getElementById('menuCircle');
    const radialActions = document.getElementById('radialActions');
    const backButton = document.querySelector('.back-button');
    const toast = document.getElementById('toast');

    const fileInput = document.getElementById('fileInput');
    const markDoneBtn = document.querySelector('.mark-done-btn');
    const confirmModal = document.getElementById('confirmModal');
    const confirmYes = document.getElementById('confirmYes');
    const confirmNo = document.getElementById('confirmNo');

    const attachmentListBtn = document.getElementById('attachmentListBtn');
    const attachmentListModal = document.getElementById('attachmentListModal');
    const attachmentListContent = document.getElementById('attachmentListContent');
    const closeAttachmentList = document.getElementById('closeAttachmentList');

    const submitBtn = document.getElementById('submitBtn');
    const submitConfirmModal = document.getElementById('submitConfirmModal');
    const submitConfirmYes = document.getElementById('submitConfirmYes');
    const submitConfirmNo = document.getElementById('submitConfirmNo');

    const submitContainer = document.getElementById('submitContainer');
    const formEl = document.getElementById('taskForm');

    function getAntiForgeryToken() {
        const el = formEl ? formEl.querySelector('input[name="__RequestVerificationToken"]') : null;
        return el ? el.value : '';
    }

    const unsubmitConfirmModal = document.getElementById('unsubmitConfirmModal');
    const unsubmitYes = document.getElementById('unsubmitYes');
    const unsubmitNo = document.getElementById('unsubmitNo');

    const submittedFileData = window.__submittedFileData || null;
    let clientObjectUrls = [];

    // Library modal elements (kept)
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


    // --- VARIABLES ---
    let uploadedFiles = [];
    let originalSubmitHTML = submitContainer ? submitContainer.innerHTML : null;
    const totalPoints = window.__TOTAL_POINTS || 'N';
    const instructorRemarks = window.__INSTRUCTOR_REMARKS || '';


    // ==========================================================
    // Toast
    // ==========================================================
    let toastTimeout = null;
    function showToast(message, duration = 1500) {
        toast.textContent = message;
        toast.classList.add('show');
        clearTimeout(toastTimeout);
        toastTimeout = setTimeout(() => toast.classList.remove('show'), duration);
    }


    // ==========================================================
    // User menu + radial menu
    // ==========================================================
    userProfile?.addEventListener('click', () => {
        userPopup.classList.toggle('show');
    });

    document.addEventListener('click', (e) => {
        if (!userProfile.contains(e.target)) userPopup.classList.remove('show');
    });

    let menuOpen = false;
    menuCircle?.addEventListener('click', () => {
        menuOpen = !menuOpen;
        radialActions.classList.toggle('show', menuOpen);
    });

    document.addEventListener('click', (e) => {
        if (!menuCircle.contains(e.target) && !radialActions.contains(e.target)) {
            menuOpen = false;
            radialActions.classList.remove('show');
        }
    });


    // ==========================================================
    // Back button
    // ==========================================================
    backButton?.addEventListener('click', () => {
        showToast('Returning to class...');
        setTimeout(() => (window.location.href = '/StudentClass'), 800);
    });

    // ===== SHOW / HIDE REPLY BOX =====
    document.querySelectorAll(".reply-option").forEach(button => {
        button.addEventListener("click", () => {
            const commentBox = button.closest(".comment-box");
            const replyArea = commentBox.querySelector(".reply-box-area");

            if (!replyArea) return;

            replyArea.style.display =
                (replyArea.style.display === "none" || replyArea.style.display === "")
                    ? "flex"
                    : "none";
        });
    });

    // ===== SUBMIT A REPLY =====
    document.querySelectorAll(".reply-submit-btn").forEach(btn => {
        btn.addEventListener("click", () => {

            const replyArea = btn.closest(".reply-box-area");
            const commentBox = btn.closest(".comment-box");
            const textarea = replyArea.querySelector(".reply-box");

            let replyText = textarea.value.trim();
            if (replyText === "") return;

            // Placeholder instructor name (replace if dynamic)
            const instructorName = "Instructor Santos";

            // Timestamp
            const now = new Date();
            const dateTimeString = now.toLocaleString("en-US", {
                month: "short",
                day: "numeric",
                year: "numeric",
                hour: "numeric",
                minute: "2-digit"
            });

            // ===== INSERT REPLY INTO COMMENT =====
            let replyHtml = `
            <div class="instructor-reply">
                <div><i class="fa-solid fa-reply"></i> <span class="instructor-name">${instructorName}</span></div>
                <div class="reply-text">${replyText}</div>
                <div class="reply-datetime">${dateTimeString}</div>
            </div>
        `;

            // Insert reply BEFORE the reply-option (so structure stays clean)
            const replyBtn = commentBox.querySelector(".reply-option");
            replyBtn.insertAdjacentHTML("beforebegin", replyHtml);

            // Reset input
            textarea.value = "";
            replyArea.style.display = "none";
        });
    });




    // ==========================================================
    // Attachment Handling
    // ==========================================================
    fileInput?.addEventListener('change', (e) => {
        uploadedFiles = Array.from(e.target.files || []);
    });

    function populateAttachmentList() {
        attachmentListContent.innerHTML = '';

        if (submittedFileData) {
            const div = document.createElement('div');
            div.className = 'attachment-box';
            const name = submittedFileData.fileName || '';
            const url = submittedFileData.fileUrl || '';
            const sizeText = submittedFileData.sizeText || '';
            div.innerHTML = `<i class="fa-solid fa-file"></i> ${escapeHtml(name)}${url ? ` <a href="${escapeHtml(url)}" class="download-link" style="margin-left:auto" download title="Download"><i class="fa-solid fa-download"></i></a>` : ''}${sizeText ? ` <span class="file-size" style="margin-left:8px; color:#64748b;">${escapeHtml(sizeText)}</span>` : ''}`;
            attachmentListContent.appendChild(div);
        }

        clientObjectUrls.forEach(u => { try { URL.revokeObjectURL(u); } catch {} });
        clientObjectUrls = [];

        uploadedFiles.forEach(f => {
            const div = document.createElement('div');
            div.className = 'attachment-box';
            const sizeText = typeof f.size === 'number' ? formatSize(f.size) : '';
            const url = URL.createObjectURL(f);
            clientObjectUrls.push(url);
            div.innerHTML = `<i class="fa-solid fa-file"></i> ${escapeHtml(f.name)} <a href="${url}" class="download-link client-file" style="margin-left:auto" download title="Download"><i class="fa-solid fa-download"></i></a>${sizeText ? ` <span class="file-size" style="margin-left:8px; color:#64748b;">${escapeHtml(sizeText)}</span>` : ''}`;
            attachmentListContent.appendChild(div);
        });

        if (!attachmentListContent.children.length) {
            const p = document.createElement('div');
            p.style.color = '#475569';
            p.style.fontSize = '0.95rem';
            p.textContent = 'No attachments';
            attachmentListContent.appendChild(p);
        }
    }

    function downloadAttachments() {
        const toClick = [];
        if (submittedFileData && submittedFileData.fileUrl) {
            const a = document.createElement('a');
            a.href = submittedFileData.fileUrl;
            a.download = submittedFileData.fileName || '';
            document.body.appendChild(a);
            toClick.push(a);
        }
        clientObjectUrls.forEach(u => { try { URL.revokeObjectURL(u); } catch {} });
        clientObjectUrls = [];
        uploadedFiles.forEach(f => {
            const url = URL.createObjectURL(f);
            clientObjectUrls.push(url);
            const a = document.createElement('a');
            a.href = url;
            a.download = f.name || 'file';
            document.body.appendChild(a);
            toClick.push(a);
        });
        if (!toClick.length) return false;
        toClick.forEach(a => { try { a.click(); } catch {} a.remove(); });
        setTimeout(() => { clientObjectUrls.forEach(u => { try { URL.revokeObjectURL(u); } catch {} }); clientObjectUrls = []; }, 1500);
        return true;
    }

    attachmentListBtn?.addEventListener('click', () => {
        const ok = downloadAttachments();
        if (!ok) {
            showToast('there is no file');
            return;
        }
    });
    closeAttachmentList?.addEventListener('click', () => {
        attachmentListModal.hidden = true;
        clientObjectUrls.forEach(u => { try { URL.revokeObjectURL(u); } catch {} });
        clientObjectUrls = [];
    });
    attachmentListModal?.addEventListener('click', (e) => {
        if (e.target === attachmentListModal) {
            attachmentListModal.hidden = true;
            clientObjectUrls.forEach(u => { try { URL.revokeObjectURL(u); } catch {} });
            clientObjectUrls = [];
        }
    });


    // ==========================================================
    // Mark Done Logic
    // ==========================================================
    markDoneBtn?.addEventListener('click', (e) => {
        e.preventDefault();
        if (!fileInput.files.length) {
            if (confirmModal) {
                confirmModal.removeAttribute('hidden');
                confirmModal.setAttribute('aria-hidden', 'false');
            }
        } else {
            submitMarkAsDone();
        }
    });

    confirmYes?.addEventListener('click', () => {
        if (confirmModal) confirmModal.setAttribute('aria-hidden', 'true');
        submitMarkAsDone();
    });

    confirmNo?.addEventListener('click', () => {
        if (confirmModal) confirmModal.setAttribute('aria-hidden', 'true');
    });

    confirmModal?.addEventListener('click', (e) => {
        if (e.target === confirmModal) confirmModal.setAttribute('aria-hidden', 'true');
    });
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && confirmModal && confirmModal.getAttribute('aria-hidden') === 'false') {
            confirmModal.setAttribute('aria-hidden', 'true');
        }
    });

    async function submitMarkAsDone() {
        if (!formEl) return;
        const fd = new FormData(formEl);
        const token = getAntiForgeryToken();
        try {
            const res = await fetch('/StudentTask/MarkAsDone', {
                method: 'POST',
                body: fd,
                credentials: 'same-origin',
                headers: { 'RequestVerificationToken': token }
            });
            const data = await res.json();
            if (data && data.success) {
                showToast("Marked as done");
                setTimeout(() => location.reload(), 800);
            } else {
                showToast((data && data.message) || 'Action failed');
            }
        } catch (err) {
            showToast('Network error');
        }
    }


    // ==========================================================
    // Submit Workflow → Inject Result Container
    // ==========================================================
    submitBtn?.addEventListener('click', (e) => {
        e.preventDefault();
        submitConfirmModal.hidden = false;
    });

    submitConfirmNo?.addEventListener('click', () => {
        submitConfirmModal.hidden = true;
    });

    submitConfirmModal?.addEventListener('click', (e) => {
        if (e.target === submitConfirmModal) submitConfirmModal.hidden = true;
    });

    submitConfirmYes?.addEventListener('click', async () => {
        submitConfirmModal.hidden = true;
        if (!formEl) return;
        const hasFile = fileInput && fileInput.files && fileInput.files.length > 0;
        if (!hasFile) { showToast('Please attach a file to submit'); return; }
        const fd = new FormData(formEl);
        const token = getAntiForgeryToken();
        try {
            const res = await fetch('/StudentTask/SubmitTask', {
                method: 'POST',
                body: fd,
                credentials: 'same-origin',
                headers: { 'RequestVerificationToken': token }
            });
            const data = await res.json();
            if (data && data.success) {
                showToast('Submitted');
                setTimeout(() => location.reload(), 800);
            } else {
                showToast((data && data.message) || 'Submission failed');
            }
        } catch (err) {
            showToast('Network error');
        }
    });


    function wireResultEvents() {
        const btnList = document.getElementById('resultAttachmentListBtn');
        const unsubmitBtn = document.getElementById('unsubmitBtn');
        const backBtn = document.getElementById('resultBackButton');

        btnList?.addEventListener('click', () => {
            const ok = downloadAttachments();
            if (!ok) {
                showToast('there is no file');
                return;
            }
        });

        unsubmitBtn?.addEventListener('click', () => {
            unsubmitConfirmModal.hidden = false;
        });

        unsubmitYes?.addEventListener('click', () => {
            if (originalSubmitHTML) {
                submitContainer.innerHTML = originalSubmitHTML;
                location.reload(); // easiest way to rebind
            } else {
                showToast("Cannot unsubmit (client error).");
                unsubmitConfirmModal.hidden = true;
            }
        });

        unsubmitNo?.addEventListener('click', () => {
            unsubmitConfirmModal.hidden = true;
        });

        backBtn?.addEventListener('click', () => {
            showToast('Returning to class...');
            setTimeout(() => (window.location.href = '/StudentClass'), 800);
        });
    }


    // ==========================================================
    // Library Modal - now backed by Library System via /Library/ApiSearch and /Library/ReserveBook
    // ==========================================================
    (function libraryModule() {

        let books = [];
        let filtered = [];
        let selectedId = null;

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
                li.dataset.id = book.id;
                li.setAttribute('role', 'option');
                li.className = selectedId === book.id ? 'selected' : '';
                li.innerHTML = `
                    <div class="book-title">${escapeHtml(book.title)}</div>
                    <div class="book-category">Category: ${escapeHtml(book.category)}</div>
                `;
                li.addEventListener('click', () => selectBook(book.id));
                libraryListEl.appendChild(li);
            });
        }

        function selectBook(id) {
            const book = books.find(b => b.id === id);
            if (!book) return;

            selectedId = id;

            libraryListEl.querySelectorAll('li').forEach(it => {
                it.classList.toggle('selected', Number(it.dataset.id) === id);
            });

            bookInfoDefault.hidden = true;
            bookDetail.hidden = false;

            detailTitle.textContent = book.title;
            detailAuthor.textContent = `Author: ${book.author}`;
            detailCategory.textContent = book.category;
            detailDescription.value = book.description;

            reserveBtn.disabled = false;
        }

        async function loadBooks(query) {
            try {
                const url = '/Library/ApiSearch?q=' + encodeURIComponent(query || '');
                console.log('[StudentTask] Loading books from:', url);
                const res = await fetch(url, { credentials: 'same-origin' });
                const data = await res.json();
                console.log('[StudentTask] API response:', data);
                
                if (!data || !data.success) {
                    console.warn('[StudentTask] API returned error:', data?.message || 'Unknown error');
                    books = [];
                    filtered = [];
                    renderList();
                    return;
                }
                books = data.books || [];
                filtered = [...books];
                console.log(`[StudentTask] Loaded ${books.length} books`);

                if (selectedId && !filtered.find(b => String(b.id) === String(selectedId))) {
                    selectedId = null;
                    bookInfoDefault.hidden = false;
                    bookDetail.hidden = true;
                    reserveBtn.disabled = true;
                }

                renderList();
            } catch (err) {
                console.error('[StudentTask] Error loading books:', err);
                books = [];
                filtered = [];
                renderList();
            }
        }

        function filterBooks(query) {
            loadBooks(query || '');
        }

        function openLibrary() {
            showToast("Opening Library…", 900);
            setTimeout(() => {
                libraryBackdrop.hidden = false;
                loadBooks('');
                librarySearch && librarySearch.focus();
            }, 700);
        }

        function closeLibrary() {
            libraryBackdrop.hidden = true;
        }


        libraryBtn?.addEventListener('click', (e) => {
            e.preventDefault();
            openLibrary();
        });

        closeLibraryBtn?.addEventListener('click', closeLibrary);
        libraryBackdrop?.addEventListener('click', (e) => {
            if (e.target === libraryBackdrop) closeLibrary();
        });

        librarySearch?.addEventListener('input', (e) => filterBooks(e.target.value));


        // Reserve modal
        reserveBtn?.addEventListener('click', () => {
            if (!selectedId) return;
            reserveBackdrop.hidden = false;
        });

        reserveYesBtn?.addEventListener('click', async () => {
            reserveBackdrop.hidden = true;
            if (!selectedId) return;
            try {
                const res = await fetch('/Library/ReserveBook', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: 'bookId=' + encodeURIComponent(selectedId),
                    credentials: 'same-origin'
                });
                const data = await res.json();
                showToast((data && data.message) || 'Reservation request sent.', 1500);
                if (data && data.success) {
                    reserveBtn.disabled = true;
                }
            } catch {
                showToast('Error reserving book. Please try again.', 1500);
            }
        });

        reserveNoBtn?.addEventListener('click', () => {
            reserveBackdrop.hidden = true;
        });

        reserveBackdrop?.addEventListener('click', (e) => {
            if (e.target === reserveBackdrop) reserveBackdrop.hidden = true;
        });

        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                if (!libraryBackdrop.hidden) closeLibrary();
                if (!reserveBackdrop.hidden) reserveBackdrop.hidden = true;
            }
        });

    })();


    // ==========================================================
    // Escape HTML
    // ==========================================================
    function escapeHtml(s) {
        return String(s || '').replace(/[&<>"']/g, m => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[m]));
    }

});
document.addEventListener('DOMContentLoaded', () => {
  const form = document.getElementById('taskForm');
  const submissionForm = document.getElementById('submissionForm');
  const resubmissionSection = document.getElementById('resubmissionSection');
  const resubmitBtn = document.getElementById('resubmitBtn');
  const submitBtn = document.getElementById('submitBtn');
  const markDoneBtn = document.getElementById('markDoneBtn');
  const confirmModal = document.getElementById('confirmModal');
  const confirmYes = document.getElementById('confirmYes');
  const confirmNo = document.getElementById('confirmNo');
  const fileInput = document.getElementById('fileInput');
  const fileNameSpan = document.getElementById('fileName');
  const filePreview = document.getElementById('filePreview');
  const privateComment = document.getElementById('privateComment');
  const commentPopup = document.getElementById('commentPopup');
  const commentInput = document.getElementById('commentInput');
  const addComment = document.getElementById('addComment');
  const cancelComment = document.getElementById('cancelComment');
  const hiddenComment = document.getElementById('hiddenComment');
  const toast = document.getElementById('toast');
  const libraryBtn = document.getElementById('libraryBtn');
  const publicCommentText = document.getElementById('publicCommentText');
  const postPublicCommentBtn = document.getElementById('postPublicCommentBtn');
  const publicCommentList = document.getElementById('publicCommentList');
  const taskIdInput = document.querySelector('#taskForm input[name="taskId"]');
  const classCodeInput = document.querySelector('#taskForm input[name="classCode"]');
  const serverAttachments = document.getElementById('serverAttachments');

  let toastTimeout2 = null;
  function showToast2(message, duration = 1500) {
    if (!toast) return;
    toast.textContent = message;
    toast.classList.add('show');
    clearTimeout(toastTimeout2);
    toastTimeout2 = setTimeout(() => toast.classList.remove('show'), duration);
  }

  function getAntiForgeryToken() {
    const el = form ? form.querySelector('input[name="__RequestVerificationToken"]') : null;
    return el ? el.value : '';
  }

  function formatSize(bytes) {
    if (!bytes && bytes !== 0) return '';
    const kb = bytes / 1024;
    if (kb < 1024) return `${kb.toFixed(1)} KB`;
    return `${(kb / 1024).toFixed(2)} MB`;
  }

  function escapeHtml(s) {
    return String(s || '').replace(/[&<>"']/g, m => ({
      '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[m]));
  }

  if (confirmModal) confirmModal.style.display = 'none';
  if (commentPopup) commentPopup.style.display = 'none';

  if (submitBtn) {
    const newSubmit = submitBtn.cloneNode(true);
    submitBtn.parentNode.replaceChild(newSubmit, submitBtn);
    newSubmit.addEventListener('click', async (e) => {
      e.preventDefault();
      if (!form) return;
      const hasFile = fileInput && fileInput.files && fileInput.files.length > 0;
      if (!hasFile) { showToast2('Please attach a file to submit'); return; }
      const fd = new FormData(form);
      const token = getAntiForgeryToken();
      try {
        const res = await fetch('/StudentTask/SubmitTask', {
          method: 'POST',
          body: fd,
          credentials: 'same-origin',
          headers: { 'RequestVerificationToken': token }
        });
        const data = await res.json();
        if (data && data.success) {
          showToast2('Submitted');
          setTimeout(() => location.reload(), 800);
        } else {
          showToast2((data && data.message) || 'Submission failed');
        }
      } catch (err) {
        showToast2('Network error');
      }
    });
  }

  if (markDoneBtn) {
    const newMark = markDoneBtn.cloneNode(true);
    markDoneBtn.parentNode.replaceChild(newMark, markDoneBtn);
    newMark.addEventListener('click', (e) => {
      e.preventDefault();
      const hasFile = fileInput && fileInput.files && fileInput.files.length > 0;
      if (!hasFile) {
        if (confirmModal) {
          confirmModal.removeAttribute('hidden');
          confirmModal.setAttribute('aria-hidden', 'false');
        }
      } else {
        postMarkAsDone2();
      }
    });
  }

  confirmYes?.addEventListener('click', () => {
    if (confirmModal) confirmModal.setAttribute('aria-hidden', 'true');
    postMarkAsDone2();
  });

  confirmNo?.addEventListener('click', () => {
    if (confirmModal) confirmModal.setAttribute('aria-hidden', 'true');
  });

  async function postMarkAsDone2() {
    if (!form) return;
    const fd = new FormData(form);
    const token = getAntiForgeryToken();
    try {
      const res = await fetch('/StudentTask/MarkAsDone', {
        method: 'POST',
        body: fd,
        credentials: 'same-origin',
        headers: { 'RequestVerificationToken': token }
      });
      const data = await res.json();
      if (data && data.success) {
        showToast2('Marked as done');
        setTimeout(() => location.reload(), 800);
      } else {
        showToast2((data && data.message) || 'Action failed');
      }
    } catch (err) {
      showToast2('Network error');
    }
  }

  fileInput?.addEventListener('change', () => {
    const f = fileInput.files && fileInput.files[0];
    if (!f) {
      if (fileNameSpan) fileNameSpan.textContent = 'Attach File';
      if (filePreview) filePreview.innerHTML = '';
      return;
    }
    if (fileNameSpan) fileNameSpan.textContent = f.name;
    if (filePreview) filePreview.innerHTML = `<div class="preview-row"><i class="fa-solid fa-file"></i> <span>${escapeHtml(f.name)}</span> <span class="size">${formatSize(f.size)}</span></div>`;
  });

  privateComment?.addEventListener('click', () => {
    if (commentPopup) commentPopup.style.display = 'flex';
    if (commentInput) commentInput.value = hiddenComment?.value || '';
  });

  cancelComment?.addEventListener('click', () => {
    if (commentPopup) commentPopup.style.display = 'none';
  });

  addComment?.addEventListener('click', () => {
    const val = commentInput ? commentInput.value.trim() : '';
    if (hiddenComment) hiddenComment.value = val;
    const preview = document.getElementById('commentPreview');
    if (preview) preview.textContent = val || 'Add Private Comment';
    if (commentPopup) commentPopup.style.display = 'none';
  });

  resubmitBtn?.addEventListener('click', () => {
    if (submissionForm) submissionForm.style.display = '';
    if (resubmissionSection) resubmissionSection.style.display = 'none';
  });

  libraryBtn?.addEventListener('click', (e) => {
    e.preventDefault();
    const backdrop = document.getElementById('libraryBackdrop');
    const searchInput = document.getElementById('librarySearch');
    if (backdrop) {
      backdrop.hidden = false;
      if (searchInput) setTimeout(() => searchInput.focus(), 0);
    } else {
      window.location.href = '/Library';
    }
  });

  async function loadComments() {
    const taskId = taskIdInput?.value || '';
    if (!taskId || !publicCommentList) return;
    const res = await fetch(`/StudentTask/GetComments?taskId=${encodeURIComponent(taskId)}`, { credentials: 'same-origin' });
    const data = await res.json();
    if (!data?.success) return;
    publicCommentList.innerHTML = '';
    data.comments.forEach(renderComment);
  }

  function renderComment(c) {
    const box = document.createElement('div');
    box.className = 'comment-box';
    const dt = new Date(c.createdAt).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });
    box.innerHTML = `
      <div class="student-name">${escapeHtml(c.authorName)}${c.role ? ' (' + escapeHtml(c.role) + ')' : ''}</div>
      <div class="comment-text">${escapeHtml(c.text)}</div>
      <div class="comment-datetime">${dt}</div>
      <div class="reply-option" role="button"><i class="fa-solid fa-comment-dots"></i> Reply</div>
      <div class="reply-box-area" hidden>
        <textarea class="reply-box" placeholder="Write a reply..."></textarea>
        <button class="reply-submit-btn">Post Reply</button>
      </div>
    `;
    if (Array.isArray(c.replies)) {
      c.replies.forEach(r => {
        const rdt = new Date(r.createdAt).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });
        const reply = document.createElement('div');
        reply.className = 'instructor-reply';
        reply.innerHTML = `
          <div><i class="fa-solid fa-reply"></i> <span class="instructor-name">${escapeHtml(r.authorName)}${r.role ? ' (' + escapeHtml(r.role) + ')' : ''}</span></div>
          <div class="reply-text">${escapeHtml(r.text)}</div>
          <div class="reply-datetime">${rdt}</div>
        `;
        const insertBefore = box.querySelector('.reply-option');
        insertBefore?.insertAdjacentElement('beforebegin', reply);
      });
    }
    publicCommentList.appendChild(box);
    const replyToggle = box.querySelector('.reply-option');
    const replyArea = box.querySelector('.reply-box-area');
    const replyBtn = box.querySelector('.reply-submit-btn');
    const replyBox = box.querySelector('.reply-box');
    replyToggle?.addEventListener('click', () => {
      if (!replyArea) return;
      const hidden = replyArea.getAttribute('hidden') !== null || replyArea.style.display === 'none' || replyArea.style.display === '';
      replyArea.style.display = hidden ? 'flex' : 'none';
      if (hidden) replyArea.removeAttribute('hidden');
    });
    replyBtn?.addEventListener('click', async () => {
      const val = (replyBox?.value || '').trim();
      if (!val) return;
      const token = getAntiForgeryToken();
      const fd = new FormData(form);
      fd.append('commentId', c.id);
      fd.append('text', val);
      const res = await fetch('/StudentTask/PostReply', { method: 'POST', body: fd, headers: { 'RequestVerificationToken': token }, credentials: 'same-origin' });
      const data = await res.json();
      if (data?.success && data.reply) {
        const rdt = new Date(data.reply.createdAt).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });
        const reply = document.createElement('div');
        reply.className = 'instructor-reply';
        reply.innerHTML = `
          <div><i class="fa-solid fa-reply"></i> <span class="instructor-name">${escapeHtml(data.reply.authorName)}${data.reply.role ? ' (' + escapeHtml(data.reply.role) + ')' : ''}</span></div>
          <div class="reply-text">${escapeHtml(data.reply.text)}</div>
          <div class="reply-datetime">${rdt}</div>
        `;
        const insertBefore = box.querySelector('.reply-option');
        insertBefore?.insertAdjacentElement('beforebegin', reply);
        if (replyBox) replyBox.value = '';
        if (replyArea) replyArea.style.display = 'none';
      }
    });
  }

  postPublicCommentBtn?.addEventListener('click', () => {
    const text = (publicCommentText?.value || '').trim();
    const taskId = taskIdInput?.value || '';
    const classCode = classCodeInput?.value || '';
    if (!text || !taskId || !classCode || !publicCommentList) return;
    const token = getAntiForgeryToken();
    const fd = new FormData(form);
    fd.append('text', text);
    try {
      fetch('/StudentTask/PostComment', { method: 'POST', body: fd, headers: { 'RequestVerificationToken': token }, credentials: 'same-origin' })
        .then(r => r.json())
        .then(d => {
          if (d?.success && d.comment) {
            renderComment(d.comment);
            publicCommentText.value = '';
          }
        });
    } catch {}
  });

  loadComments();
});
    function formatSize(bytes) {
        if (!bytes && bytes !== 0) return '';
        const kb = bytes / 1024;
        if (kb < 1024) return `${kb.toFixed(1)} KB`;
        return `${(kb / 1024).toFixed(2)} MB`;
    }
