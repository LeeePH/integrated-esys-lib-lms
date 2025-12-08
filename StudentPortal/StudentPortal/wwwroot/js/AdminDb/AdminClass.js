document.addEventListener('DOMContentLoaded', () => {

    // ------------------ ELEMENT REFERENCES -------------------
    const userProfile = document.getElementById('userProfile');
    const userPopup = document.getElementById('userPopup');
    const menuCircle = document.getElementById('menuCircle');
    const radialActions = document.getElementById('radialActions');
    const actions = radialActions ? radialActions.querySelectorAll('.action') : [];
    const addContentBtn = document.getElementById('addContentBtn');
    const contentRadial = document.getElementById('contentRadial');
    const contentActions = contentRadial ? Array.from(contentRadial.querySelectorAll('.content-action')) : [];
    const contentTooltip = contentRadial ? contentRadial.querySelector('.content-tooltip') : null;
    const modalBackdrop = document.getElementById('modalBackdrop');
    const contentModal = document.getElementById('contentModal');
    const modalBody = document.getElementById('modalBody');
    const modalSubmit = document.getElementById('modalSubmit');
    const modalCancel = document.getElementById('modalCancel');
    const classContent = document.getElementById('classContent');
    const toast = document.getElementById('toast');
    const announcementCard = document.getElementById('createAnnouncementCard');
    const announcementInput = document.getElementById('announcementInput');
    const announceBtn = document.getElementById('announceBtn');
    const backButton = document.querySelector('.back-button');
    const manageButtons = document.querySelectorAll('.manage-btn');

   
    let currentPage = 'class';
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
                (icon.classList.contains('fa-clipboard-question') && currentPage === 'assessment')
            ) {
                showToast("🏠 You're already here.");
                radialActions.classList.remove('show');
                menuOpen = false;
                return;
            }

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

    function navigateWithAnimation(url, message) {
        showToast(message);
        radialActions?.classList.remove('show');
        menuOpen = false;
        setTimeout(() => { window.location.href = url; }, 600);
    }

    // ------------------ CONTENT RADIAL -----------------------
    let contentRadialOpen = false;
    addContentBtn?.addEventListener('click', (e) => {
        e.stopPropagation();
        contentRadialOpen = !contentRadialOpen;
        contentRadial?.classList.toggle('show', contentRadialOpen);
        addContentBtn?.classList.toggle('selected', contentRadialOpen);
    });
    document.addEventListener('click', (e) => {
        if (!contentRadial?.contains(e.target) && e.target !== addContentBtn) {
            contentRadialOpen = false;
            contentRadial?.classList.remove('show');
            addContentBtn?.classList.remove('selected');
        }
    });

    contentActions.forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            openContentModal(btn.dataset.type);
        });
        btn.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                btn.click();
            }
        });
    });

    // ------------------ MODAL HANDLING -----------------------
    let currentCreateType = null;

    function openContentModal(type) {
        currentCreateType = type;
        document.getElementById('modalTitle').textContent = {
            material: 'Upload Material',
            task: 'Create Task',
            assessment: 'Create Assessment'
        }[type] || 'Create';

        modalBody.innerHTML = '';
        const titleInput = createElement('input', { type: 'text', id: 'c_title', placeholder: 'Content title' });
        const desc = createElement('textarea', { id: 'c_desc', placeholder: 'Content description (optional)' });
        const file = createElement('input', { type: 'file', id: 'c_file' });
        modalBody.append(titleInput, desc, file);

        if (type === 'task' || type === 'assessment') {
            const deadline = createElement('input', { type: 'date', id: 'c_deadline' });
            modalBody.appendChild(deadline);
        }

        if (type === 'task') {
            const maxGradeLabel = createElement('label', { htmlFor: 'c_maxgrade' });
            maxGradeLabel.textContent = 'Maximum grade';
            const maxGradeSelect = createElement('select', { id: 'c_maxgrade' });
            ['10','50','100'].forEach(v => {
                const opt = createElement('option', { value: v });
                opt.textContent = `${v}/${v}`;
                maxGradeSelect.appendChild(opt);
            });
            modalBody.append(maxGradeLabel, maxGradeSelect);
        }

        if (type === 'assessment') {
            const link = createElement('input', { type: 'url', id: 'c_link', placeholder: 'Link to assessment (optional)' });
            modalBody.appendChild(link);
        }

        modalBackdrop.hidden = false;
        modalBackdrop.style.display = 'flex';
        modalBackdrop.setAttribute('aria-hidden', 'false');
        modalSubmit.textContent = type === 'material' ? 'Upload' : 'Create';
        modalSubmit.dataset.type = type;
    }

    function createElement(tag, attrs = {}) {
        const el = document.createElement(tag);
        Object.entries(attrs).forEach(([k, v]) => {
            if (k in el) el[k] = v;
            else el.setAttribute(k, v);
        });
        return el;
    }

    function closeModal() {
        modalBackdrop.hidden = true;
        modalBackdrop.style.display = 'none';
        modalBackdrop.setAttribute('aria-hidden', 'true');
        modalBody.innerHTML = '';
        currentCreateType = null;
        addContentBtn.classList.remove('selected');
        contentRadial.classList.remove('show');
        contentRadialOpen = false;
    }

    modalBackdrop?.addEventListener('click', (ev) => {
        if (ev.target === modalBackdrop) closeModal();
    });

    modalCancel?.addEventListener('click', closeModal);

    // ------------------ CREATE / UPLOAD CONTENT -------------
    modalSubmit?.addEventListener('click', async () => {
        const type = modalSubmit.dataset.type;
        const title = (document.getElementById('c_title') || {}).value || '';
        const desc = (document.getElementById('c_desc') || {}).value || '';
        const deadlineVal = (document.getElementById('c_deadline') || {}).value || '';
        const linkVal = (document.getElementById('c_link') || {}).value || '';
        const fileInput = document.getElementById('c_file');
        const file = fileInput?.files[0];

        if (!title.trim()) {
            showToast('⚠️ Please add a title.', 'warning');
            return;
        }

        try {
            // Get class code from URL
            const pathParts = window.location.pathname.split('/').filter(part => part);
            const classCode = pathParts[pathParts.length - 1];

            console.log('Using Class Code from URL:', classCode);

            if (!classCode) {
                showToast('❌ Cannot find class code. Please refresh the page.', 'error');
                return;
            }

            // If there's a file, upload it first, then create content
            if (file) {
                await uploadFileAndCreateContent(file, type, title, desc, deadlineVal, linkVal, classCode);
            } else {
                // No file, just create content
                await createContentWithoutFile(type, title, desc, deadlineVal, linkVal, classCode);
            }

        } catch (error) {
            console.error('Error creating content:', error);
            showToast('❌ Failed to save content to database: ' + error.message, 'error');
        }
    });

    async function uploadFileAndCreateContent(file, type, title, desc, deadlineVal, linkVal, classCode) {
        // First, upload the file
        const fileFormData = new FormData();
        fileFormData.append('file', file);
        fileFormData.append('classCode', classCode);
        fileFormData.append('type', type);

        console.log('Uploading file:', file.name);

        const uploadResponse = await fetch('/AdminClass/UploadFile', {
            method: 'POST',
            body: fileFormData
        });

        if (!uploadResponse.ok) {
            const errorText = await uploadResponse.text();
            throw new Error(`Failed to upload file: ${uploadResponse.status} ${errorText}`);
        }

        const uploadResult = await uploadResponse.json();
        console.log('File upload result:', uploadResult);

        if (!uploadResult.success) {
            throw new Error(uploadResult.message);
        }

        // Now create the content with the file information
        const contentData = {
            type: type,
            title: title,
            description: desc,
            deadline: deadlineVal,
            link: linkVal,
            classId: classCode,
            fileName: file.name,
            fileUrl: uploadResult.fileUrl || '', // URL where the file is stored
            fileSize: file.size,
            maxGrade: type === 'task' ? parseInt((document.getElementById('c_maxgrade') || {}).value || '100', 10) : 0
        };

        await createContentInDatabase(contentData);
    }

    async function createContentWithoutFile(type, title, desc, deadlineVal, linkVal, classCode) {
        const contentData = {
            type: type,
            title: title,
            description: desc,
            deadline: deadlineVal,
            link: linkVal,
            classId: classCode,
            maxGrade: type === 'task' ? parseInt((document.getElementById('c_maxgrade') || {}).value || '100', 10) : 0
        };

        await createContentInDatabase(contentData);
    }

    async function createContentInDatabase(contentData) {
        console.log('Sending content data:', contentData);

        const response = await fetch('/AdminClass/CreateContent', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(contentData)
        });

        console.log('Response status:', response.status);

        if (!response.ok) {
            const errorText = await response.text();
            console.error('Server response:', errorText);
            throw new Error(`Failed to save content: ${response.status} ${errorText}`);
        }

        const savedContent = await response.json();
        console.log('Saved content:', savedContent);

        showToast('✅ Content created and saved.', 'success');
        closeModal();

        // Refresh the page to show the new content
        setTimeout(() => {
            window.location.reload();
        }, 1000);
    }

    // ------------------ ANNOUNCEMENTS ------------------------
    announceBtn?.addEventListener('click', async () => {
        const txt = announcementInput.value.trim();
        if (!txt) { showToast('⚠️ Please type an announcement.', 'warning'); return; }

        try {
            // FIX: Get class code from URL (same method as above)
            const pathParts = window.location.pathname.split('/').filter(part => part);
            const classCode = pathParts[pathParts.length - 1];

            console.log('Creating announcement for class:', classCode);

            if (!classCode) {
                showToast('❌ Cannot find class code for announcement.', 'error');
                return;
            }

            const res = await fetch('/AdminClass/AddAnnouncement', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    classId: classCode,  // Send Class Code, not Class ID
                    text: txt
                })
            });

            console.log('Announcement response status:', res.status);

            if (!res.ok) {
                const errorText = await res.text();
                throw new Error(`Failed to save announcement: ${res.status} ${errorText}`);
            }

            const data = await res.json();
            console.log('Announcement saved:', data);

            showToast('📢 Announcement posted.', 'success');
            collapseAnnouncement();

            // Refresh to show the new announcement
            setTimeout(() => {
                window.location.reload();
            }, 1000);

        } catch (err) {
            console.error(err);
            showToast('❌ Could not save announcement: ' + err.message, 'error');
        }
    });

    function createContentCard(type, title, desc, deadline, link) {
        const card = createElement('article', { className: `content-card ${type}` });
        const left = createElement('div', { className: 'content-left' });
        const icon = createElement('i', {
            className: {
                material: 'fa-solid fa-book-open-reader',
                task: 'fa-solid fa-file-pen',
                assessment: 'fa-solid fa-circle-question'
            }[type]
        });
        left.appendChild(icon);

        const info = createElement('div', { className: 'content-info' });
        const h = createElement('h4', { className: 'content-title', textContent: title });
        const meta = createElement('p', { className: 'content-meta', textContent: generateMeta(desc, deadline, link) });
        info.append(h, meta);
        left.appendChild(info);
        card.appendChild(left);

        if ((type === 'task' || type === 'assessment') && deadline) {
            const rightFlag = createElement('div', { className: 'content-right urgency' });
            const days = diffDays(new Date(), new Date(deadline));
            if (days <= 2) rightFlag.classList.add('red');
            else if (days <= 7) rightFlag.classList.add('yellow');
            else rightFlag.classList.add('green');
            card.appendChild(rightFlag);
        }

        // give new card same click behavior
        card.addEventListener('click', () => {
            const targetPage = card.dataset.target;
            if (!targetPage) return;
            showToast('Opening...');
            navigateWithDelay(targetPage);
        });

        return card;
    }

    function generateMeta(desc, deadline, link) {
        let text = `Posted: ${new Date().toLocaleDateString()}`;
        if (deadline) text += ` | Deadline: ${deadline}`;
        if (desc) text += `\n${desc}`;
        if (link) text += `\nLink: ${link}`;
        return text;
    }

    function insertCard(card) {
        const firstInsert = document.querySelector('.create-announcement');
        if (firstInsert && firstInsert.parentNode) firstInsert.parentNode.insertBefore(card, firstInsert.nextSibling);
        else classContent.prepend(card);
    }

    // ------------------ CONTENT-CARD NAVIGATION --------------
    const contentCards = document.querySelectorAll('.content-card');
    contentCards.forEach(card => {
        card.style.cursor = 'pointer';
        card.addEventListener('click', () => {
            const targetPage = card.dataset.target;
            if (!targetPage) return;
            showToast('Opening...');
            navigateWithDelay(targetPage);
        });
    });

    // ------------------ ANNOUNCEMENTS ------------------------
    function autoResize(el) { el.style.height = 'auto'; el.style.height = (el.scrollHeight) + 'px'; }
    function expandAnnouncement() {
        announcementCard.classList.add('active');
        announcementCard.querySelector('.announcement-actions').hidden = false;
        autoResize(announcementInput);
        announcementInput.focus();
    }
    function collapseAnnouncement() {
        announcementCard.classList.remove('active');
        announcementCard.querySelector('.announcement-actions').hidden = true;
        announcementInput.style.height = 'auto';
        announcementInput.value = '';
    }

    announcementInput?.addEventListener('focus', expandAnnouncement);
    announcementInput?.addEventListener('click', expandAnnouncement);
    announcementInput?.addEventListener('input', () => autoResize(announcementInput));

    announceBtn?.addEventListener('click', async () => {
        const txt = announcementInput.value.trim();
        if (!txt) { showToast('⚠️ Please type an announcement.', 'warning'); return; }

        try {
            // FIX: Use the same method to get classId
            let classId = null;
            const hiddenClassId = document.querySelector('input[name="classId"], input[id="classId"]');
            if (hiddenClassId) {
                classId = hiddenClassId.value;
            }
            if (!classId && document.body.dataset.classId) {
                classId = document.body.dataset.classId;
            }
            if (!classId) {
                const classContentElement = document.getElementById('classContent');
                classId = classContentElement?.dataset.classId;
            }

            console.log('Creating announcement for class:', classId);

            if (!classId) {
                showToast('❌ Cannot find class ID for announcement.', 'error');
                return;
            }

            const res = await fetch('/AdminClass/AddAnnouncement', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ classId, text: txt })
            });

            console.log('Announcement response status:', res.status);

            if (!res.ok) {
                const errorText = await res.text();
                throw new Error(`Failed to save announcement: ${res.status} ${errorText}`);
            }

            const data = await res.json();
            console.log('Announcement saved:', data);

            // Safely get values
            const description = data.Description || txt;
            const uploadedBy = data.UploadedBy || 'Admin';
            const createdAt = data.CreatedAt ? new Date(data.CreatedAt) : new Date();

            const card = document.createElement('article');
            card.className = 'content-card announcement';
            card.innerHTML = `
    <div class="content-left">
        <i class="fa-solid fa-bullhorn"></i>
        <div class="content-info">
            <h4 class="content-title">Announcement</h4>
            <p class="content-meta">${description}</p>
            <p class="content-meta-small">By ${uploadedBy} | ${createdAt.toLocaleString()}</p>
        </div>
    </div>`;

            insertCard(card);

            showToast('📢 Announcement posted.', 'success');
            collapseAnnouncement();
        } catch (err) {
            console.error(err);
            showToast('❌ Could not save announcement: ' + err.message, 'error');
        }
    });

    // ------------------ GLOBAL ESC HANDLING ------------------
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            closeModal();
            radialActions?.classList.remove('show');
            contentRadial?.classList.remove('show');
            addContentBtn?.classList.remove('selected');
            menuOpen = false;
        }
    });

    // ------------------ BACK BUTTON ---------------------------
    backButton?.addEventListener('click', () => {
        const classCodeFromPath = window.location.pathname.split('/').filter(Boolean).pop();
        const classCode = (window.adminBottomBarData && window.adminBottomBarData.classCode) || classCodeFromPath || '';
        window.showToast ? window.showToast('Returning to dashboard...') : null;
        const target = '/professordb/ProfessorDb';
        setTimeout(() => { window.location.href = target; }, 600);
    });

    // ------------------ MANAGE BUTTONS -------------------------
    manageButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const targetPage = btn.getAttribute('data-target');
            if (!targetPage) return;

            if (btn.classList.contains('manage-class')) {
                showToast('🧑‍🏫 Opening Manage Class...');
            } else if (btn.classList.contains('manage-grades')) {
                showToast('🎓 Opening Manage Grades...');
            } else {
                showToast('Opening page...');
            }

            setTimeout(() => {
                window.location.href = targetPage;
            }, 600);
        });
    });

    // ------------------ TOAST --------------------------------
    function showToast(message, type = '') {
        if (!toast) return;
        toast.textContent = message;
        toast.className = `toast show ${type}`;
        setTimeout(() => toast.classList.remove('show'), 2800);
    }

    // ------------------ HELPERS ------------------------------
    function diffDays(d1, d2) {
        const one = 24 * 60 * 60 * 1000;
        return Math.round((+d2 - +d1) / one);
    }

    function navigateWithDelay(url, delay = 600) {
        setTimeout(() => window.location.href = url, delay);
    }

});

