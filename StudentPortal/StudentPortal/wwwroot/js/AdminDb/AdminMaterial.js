document.addEventListener('DOMContentLoaded', () => {
    // ------------------ ELEMENT REFERENCES -------------------
    const userProfile = document.getElementById('userProfile');
    const userPopup = document.getElementById('userPopup');
    const menuCircle = document.getElementById('menuCircle');
    const radialActions = document.getElementById('radialActions');
    const actions = radialActions ? radialActions.querySelectorAll('.action') : [];
    const toast = document.getElementById('toast');
    const backButton = document.querySelector('.back-button');

    const materialContainer = document.getElementById('materialContainer');
    const materialName = document.getElementById('materialName');
    const materialDescription = document.getElementById('materialDescription');
    const attachmentList = document.getElementById('attachmentList');
    const editControls = document.getElementById('editControls');
    const saveEditBtn = document.getElementById('saveEditBtn');
    const cancelEditBtn = document.getElementById('cancelEditBtn');
    const makeChangesButton = document.getElementById('makeChangesButton');
    const adminActions = document.getElementById('adminActions');
    const deleteModal = document.getElementById('deleteModal');
    const confirmDelete = document.getElementById('confirmDelete');
    const cancelDelete = document.getElementById('cancelDelete');
    const dateInfo = document.getElementById('dateInfo');

    // ------------------ STATE VARIABLES -------------------
    let isEditing = false;
    let originalName = '';
    let originalDescription = '';
    let menuOpen = false;
    let adminMenuOpen = false;

    // ------------------ INITIAL SETUP -------------------
    if (editControls) {
        editControls.style.display = 'none';
        editControls.classList.remove('visible');
    }

    // Initialize download buttons for existing files
    initializeDownloadButtons();

    // Bottom bar interactions are managed by AdminBottomBar.js

    // ------------------ BACK BUTTON ---------------------------
    backButton?.addEventListener('click', () => {
        const classCode = document.body?.dataset?.classCode || '';
        showToast('Returning...');
        const target = classCode ? `/AdminClass/${classCode}` : '/admindb/AdminDb';
        setTimeout(() => (window.location.href = target), 800);
    });

    // ------------------ ADMIN ACTIONS MENU --------------------
    makeChangesButton?.addEventListener('click', (e) => {
        e.stopPropagation();
        adminMenuOpen = !adminMenuOpen;
        adminActions?.classList.toggle('show', adminMenuOpen);
    });

    document.addEventListener('click', (e) => {
        if (!adminActions?.contains(e.target) && e.target !== makeChangesButton) {
            adminActions?.classList.remove('show');
            adminMenuOpen = false;
        }
    });

    // Admin Actions
    adminActions?.querySelectorAll('.admin-action').forEach(action => {
        action.addEventListener('click', (e) => {
            e.stopPropagation();
            const actionType = action.dataset.action;

            if (actionType === 'edit') {
                startEditing();
            } else if (actionType === 'delete') {
                showDeleteModal();
            }

            adminActions.classList.remove('show');
            adminMenuOpen = false;
        });
    });

    // ------------------ EDITING FUNCTIONALITY -----------------
    function startEditing() {
        isEditing = true;
        originalName = materialName.textContent;
        originalDescription = materialDescription.textContent;

        materialName.contentEditable = true;
        materialDescription.contentEditable = true;

        materialName.focus();
        placeCaretAtEnd(materialName);
        revealEditControls();

        showToast('✏️ Editing mode enabled');
    }

    function cancelEditing() {
        isEditing = false;
        materialName.textContent = originalName;
        materialDescription.textContent = originalDescription;

        materialName.contentEditable = false;
        materialDescription.contentEditable = false;

        hideEditControls();
        showToast('Editing cancelled');
    }

    async function saveEditing() {
        const newName = materialName.textContent.trim();
        const newDescription = materialDescription.textContent.trim();

        if (!newName) {
            showToast('⚠️ Material name cannot be empty', 'warning');
            materialName.textContent = originalName;
            return;
        }

        try {
            const materialId = materialContainer.dataset.materialId;
            const response = await fetch('/AdminMaterial/UpdateMaterial', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    materialId: materialId,
                    title: newName,
                    description: newDescription
                })
            });

            if (!response.ok) {
                throw new Error('Failed to update material');
            }

            const result = await response.json();

            if (result.success) {
                isEditing = false;
                materialName.contentEditable = false;
                materialDescription.contentEditable = false;

                hideEditControls();

                // Update date info
                const today = new Date().toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
                if (dateInfo) {
                    const postedPart = dateInfo.textContent.split('|')[0].trim();
                    dateInfo.textContent = `${postedPart} | Edited: ${today}`;
                }

                showToast('✅ Material updated successfully');
            } else {
                throw new Error(result.message);
            }
        } catch (error) {
            console.error('Error updating material:', error);
            showToast('❌ Failed to update material: ' + error.message, 'error');
            cancelEditing();
        }
    }

    function revealEditControls() {
        if (!editControls) return;
        editControls.style.display = 'flex';
        requestAnimationFrame(() => editControls.classList.add('visible'));
    }

    function hideEditControls() {
        if (!editControls) return;
        editControls.classList.remove('visible');
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
        } catch (err) { /* ignore */ }
    }

    // ------------------ FILE DOWNLOAD FUNCTIONALITY ------------
    function initializeDownloadButtons() {
        const downloadButtons = attachmentList?.querySelectorAll('.download-btn');
        downloadButtons?.forEach(btn => {
            btn.addEventListener('click', async (e) => {
                e.preventDefault();
                const fileName = btn.dataset.filename;
                const materialId = materialContainer.dataset.materialId;
                await downloadFile(fileName, materialId);
            });
        });
    }

    // ------------------ REPLACE ATTACHMENT (upload + link) -----
    const replaceFileBtn = document.getElementById('replaceFileBtn');
    const replaceFileInput = document.getElementById('replaceFileInput');

    replaceFileBtn?.addEventListener('click', (e) => {
        e.preventDefault();
        if (!isEditing) { showToast('Enable edit mode first'); return; }
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
            fd.append('type', 'material');
            const up = await fetch('/AdminClass/UploadFile', { method: 'POST', body: fd });
            if (!up.ok) throw new Error('Upload failed');
            const upRes = await up.json();
            if (!upRes.success) throw new Error(upRes.message || 'Upload failed');

            const materialId = materialContainer?.dataset?.materialId || '';
            const linkRes = await fetch('/AdminMaterial/ReplaceAttachment', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ materialId, fileName: file.name, fileUrl: upRes.fileUrl })
            });
            if (!linkRes.ok) throw new Error('Replace failed');
            const linkJson = await linkRes.json();
            if (!linkJson.success) throw new Error(linkJson.message || 'Replace failed');

            // Update UI: show single attachment
            if (attachmentList) {
                attachmentList.innerHTML = '';
                const box = document.createElement('div');
                box.className = 'attachment-box';
                box.setAttribute('data-filename', file.name);
                box.innerHTML = `<i class="fa-solid fa-file"></i><span class="attachment-name">${file.name}</span>
                                 <button class="download-btn" data-filename="${file.name}" title="Download ${file.name}"><i class="fa-solid fa-download"></i></button>`;
                attachmentList.appendChild(box);
                initializeDownloadButtons();
            }
            showToast('✅ File replaced');
        } catch (err) {
            console.error(err);
            showToast('❌ ' + (err.message || 'Could not replace file'));
        } finally {
            try { replaceFileInput.value = ''; } catch {}
        }
    });

    async function downloadFile(fileName, materialId) {
        try {
            showToast('📥 Preparing download: ' + fileName);

            const response = await fetch(`/AdminMaterial/DownloadFile/${encodeURIComponent(fileName)}?contentId=${materialId}`, {
                method: 'GET'
            });

            if (!response.ok) {
                throw new Error('Failed to download file');
            }

            const result = await response.json();

            if (result.success) {
                showToast('✅ Download ready: ' + fileName);
                // Here you would typically trigger the actual file download
                // For now, we'll just show a success message
            } else {
                throw new Error(result.message);
            }
        } catch (error) {
            console.error('Error downloading file:', error);
            showToast('❌ Failed to download file: ' + error.message, 'error');
        }
    }

    // ------------------ DELETE FUNCTIONALITY -----------------
    function showDeleteModal() {
        if (!deleteModal) return;
        deleteModal.style.display = 'flex';
        deleteModal.classList.add('show');
    }

    function hideDeleteModal() {
        if (!deleteModal) return;
        deleteModal.style.display = 'none';
        deleteModal.classList.remove('show');
    }

    async function deleteMaterial() {
        try {
            const materialId = materialContainer.dataset.materialId;
            const response = await fetch('/AdminMaterial/DeleteMaterial', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ materialId: materialId })
            });

            if (!response.ok) {
                throw new Error('Failed to delete material');
            }

            const result = await response.json();

            if (result.success) {
                showToast('✅ Material deleted successfully');
                setTimeout(() => {
                    // Redirect back to class page
                    const classCode = document.body.dataset.classCode;
                    window.location.href = classCode ? `/AdminClass/${classCode}` : '/AdminClass';
                }, 1000);
            } else {
                throw new Error(result.message);
            }
        } catch (error) {
            console.error('Error deleting material:', error);
            showToast('❌ Failed to delete material: ' + error.message, 'error');
        } finally {
            hideDeleteModal();
        }
    }

    // ------------------ EVENT LISTENERS ----------------------
    saveEditBtn?.addEventListener('click', saveEditing);
    cancelEditBtn?.addEventListener('click', cancelEditing);
    confirmDelete?.addEventListener('click', deleteMaterial);
    cancelDelete?.addEventListener('click', hideDeleteModal);

    // ------------------ TOAST FUNCTION -----------------------
    function showToast(message, type = '') {
        if (!toast) return;
        toast.textContent = message;
        toast.className = `toast show ${type}`;
        setTimeout(() => toast.classList.remove('show'), 3000);
    }

    // ------------------ EVENT HANDLERS -----------------------
    // Close modal when clicking outside
    deleteModal?.addEventListener('click', (e) => {
        if (e.target === deleteModal) {
            hideDeleteModal();
        }
    });

    // Escape key handling
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            if (isEditing) {
                cancelEditing();
            }
            if (deleteModal.style.display === 'flex' || deleteModal.classList.contains('show')) {
                hideDeleteModal();
            }
            radialActions?.classList.remove('show');
            adminActions?.classList.remove('show');
            menuOpen = false;
            adminMenuOpen = false;
        }
    });

    const adminCommentContainer = document.getElementById('adminCommentContainer');
    const adminCommentForm = document.getElementById('adminCommentForm');
    const adminCommentText = document.getElementById('adminCommentText');
    const postAdminCommentBtn = document.getElementById('postAdminCommentBtn');
    const adminCommentList = document.getElementById('adminCommentList');

    function getAdminAntiForgeryToken() {
        const el = adminCommentForm ? adminCommentForm.querySelector('input[name="__RequestVerificationToken"]') : null;
        return el ? el.value : '';
    }

    function getContentId() {
        const input = adminCommentForm ? adminCommentForm.querySelector('input[name="contentId"]') : null;
        return input ? input.value : '';
    }

    function getClassCode() {
        const input = adminCommentForm ? adminCommentForm.querySelector('input[name="classCode"]') : null;
        return input ? input.value : '';
    }

    async function loadAdminComments() {
        const contentId = getContentId();
        if (!contentId || !adminCommentList) return;
        try {
            const res = await fetch('/StudentMaterial/GetComments?contentId=' + encodeURIComponent(contentId), { credentials: 'same-origin' });
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
        const nameHtml = '<div class="student-name">' + escapeHtml(c.authorName) + (c.role ? ' • ' + escapeHtml(c.role) : '') + '</div>';
        const textHtml = '<div class="comment-text">' + escapeHtml(c.text) + '</div>';
        const dateHtml = '<div class="comment-datetime">' + new Date(c.createdAt).toLocaleString() + '</div>';
        box.innerHTML = nameHtml + textHtml + dateHtml;
        if (Array.isArray(c.replies)) {
            c.replies.forEach(r => {
                const rdiv = document.createElement('div');
                rdiv.className = 'instructor-reply';
                rdiv.innerHTML = '<div><i class="fa-solid fa-reply"></i> <span class="instructor-name">' + escapeHtml(r.authorName) + (r.role ? ' • ' + escapeHtml(r.role) : '') + '</span></div><div class="reply-text">' + escapeHtml(r.text) + '</div><div class="reply-datetime">' + new Date(r.createdAt).toLocaleString() + '</div>';
                box.appendChild(rdiv);
            });
        }
        const replyToggle = document.createElement('div');
        replyToggle.className = 'reply-option';
        replyToggle.innerHTML = '<i class="fa-solid fa-reply"></i> Reply';
        box.appendChild(replyToggle);

        const replyArea = document.createElement('div');
        replyArea.className = 'reply-box-area';
        replyArea.innerHTML = '<textarea class="reply-box" placeholder="Write a reply..."></textarea><button class="reply-submit-btn">Reply</button>';
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
                const res = await fetch('/StudentMaterial/PostReply', {
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
        const contentId = getContentId();
        const classCode = getClassCode();
        try {
            const res = await fetch('/StudentMaterial/PostComment', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token },
                body: new URLSearchParams({ contentId, classCode, text })
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

function escapeHtml(s) {
    try { return s.replace(/[&<>"']/g, function(c){ return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]; }); } catch { return s; }
}
