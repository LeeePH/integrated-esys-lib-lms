// Global variables
let currentBookCondition = 'Good';
let currentDamageType = '';
let lateFees = 0;
let currentReservationData = null;

// Detect current role based on URL or page context
function getCurrentRole() {
    const path = window.location.pathname;
    if (path.includes('/Admin/')) {
        return 'Admin';
    } else if (path.includes('/Librarian/')) {
        return 'Librarian';
    }
    // Fallback: check for role in page content or try to detect from URL structure
    return 'Librarian'; // Default to Librarian for backward compatibility
}

// Get the appropriate base URL for API calls
function getApiBaseUrl() {
    const role = getCurrentRole();
    return `/${role}`;
}

document.addEventListener('DOMContentLoaded', function () {
    console.log('🚀 Return Transaction JS Loaded');
    
    // Debug: Log detected role
    const currentRole = getCurrentRole();
    const apiBaseUrl = getApiBaseUrl();
    console.log(`🔍 Detected Role: ${currentRole}`);
    console.log(`🔗 API Base URL: ${apiBaseUrl}`);

    // Elements
    const searchInput = document.getElementById('book-id');
    const searchBtn = document.querySelector('.btn-search');
    const bookInfoSection = document.querySelector('.book-info-section');
    const conditionButtons = document.querySelectorAll('.condition-btn');
    const restrictBtn = document.getElementById('restrictBtn');
    const processReturnBtn = document.getElementById('processReturnBtn');
    const damageTypeSection = document.getElementById('damageTypeSection');
    const damageTypeSelect = document.getElementById('damageType');

    // Modals
    const restrictModal = document.getElementById('restrictModal');
    const restrictSuccessPopup = document.getElementById('restrictSuccessPopup');
    const confirmReturnPopup = document.getElementById('confirmReturnPopup');
    const returnSuccessPopup = document.getElementById('returnSuccessPopup');

    // Modal buttons
    const modalNoBtn = document.getElementById('modalNoBtn');
    const modalYesBtn = document.getElementById('modalYesBtn');
    const closeRestrictSuccess = document.getElementById('closeRestrictSuccess');
    const cancelReturn = document.getElementById('cancelReturn');
    const confirmReturnYes = document.getElementById('confirmReturnYes');
    const closeReturnSuccess = document.getElementById('closeReturnSuccess');

    // Initially hide book info and damage type section
    if (bookInfoSection) {
        bookInfoSection.style.display = 'none';
    }
    if (damageTypeSection) {
        damageTypeSection.style.display = 'none';
    }

    // Search functionality
    if (searchBtn) {
        searchBtn.addEventListener('click', handleSearch);
    }
    if (searchInput) {
        searchInput.addEventListener('keypress', function (e) {
            if (e.key === 'Enter') {
                handleSearch();
            }
        });
    }

    async function handleSearch() {
        const bookId = searchInput.value.trim();

        if (!bookId) {
            showNotification('Please enter a Book ID or Accession Number', 'error');
            return;
        }

        searchBtn.disabled = true;
        searchBtn.textContent = 'Searching...';

        try {
            const apiBaseUrl = getApiBaseUrl();
            const response = await fetch(`${apiBaseUrl}/SearchActiveReservation?bookId=${encodeURIComponent(bookId)}`);
            const data = await response.json();

            if (data.success) {
                currentReservationData = data.data;
                displayBookInfo(data.data);
                calculateLateFees(data.data.dueDate, data.data.minutesLate || data.data.daysLate);
            } else {
                showNotification(data.message || 'Book not found or not currently borrowed', 'error');
                hideBookInfo();
            }
        } catch (error) {
            console.error('Error:', error);
            showNotification('Failed to search for book. Please try again.', 'error');
            hideBookInfo();
        } finally {
            searchBtn.disabled = false;
            searchBtn.textContent = 'Search';
        }
    }

    function displayBookInfo(data) {
        const titleEl = document.querySelector('.book-title');
        const metaEls = document.querySelectorAll('.book-meta');
        const dateEls = document.querySelectorAll('.book-dates-grid .date-value');
        const statusBadge = document.querySelector('.status-badge');

        if (!titleEl || metaEls.length < 2 || dateEls.length < 3 || !statusBadge) {
            console.error('Missing HTML elements for book info');
            return;
        }

        titleEl.textContent = data.bookTitle;
        metaEls[0].textContent = `ISBN: ${data.isbn || 'N/A'}`;
        metaEls[1].textContent = `Borrowed by: ${data.borrowerName}`;

        const borrowDate = new Date(data.borrowDate);
        const dueDate = new Date(data.dueDate);

        dateEls[0].textContent = formatDate(borrowDate);
        dateEls[1].textContent = formatDate(dueDate);
        dateEls[2].textContent = data.daysBorrowed + ' days';

        if (data.isOverdue) {
            statusBadge.innerHTML = `
                <img src="/images/overduebooks.png" alt="Overdue Icon" class="icon-sm mr-2" />
                This Book is Overdue
            `;
            statusBadge.style.background = 'linear-gradient(135deg, #ff6b6b 0%, #ee5a6f 100%)';
        } else {
            statusBadge.innerHTML = `
                <img src="/images/grayeligible.png" alt="On Time Icon" class="icon-sm mr-2" />
                On Time
            `;
            statusBadge.style.background = 'linear-gradient(135deg, #4CAF50 0%, #45a049 100%)';
        }

        bookInfoSection.style.display = 'block';
        bookInfoSection.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }

    function hideBookInfo() {
        bookInfoSection.style.display = 'none';
        currentReservationData = null;
        if (damageTypeSection) damageTypeSection.style.display = 'none';
        if (damageTypeSelect) damageTypeSelect.value = '';
        currentDamageType = '';
        lateFees = 0;
        updatePenaltyDisplay();
    }

    function formatDate(date) {
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        const year = String(date.getFullYear()).slice(-2);
        return `${month}/${day}/${year}`;
    }

    function calculateLateFees(dueDate, minutesLate) {
        // Calculate minutes overdue if not provided
        if (!minutesLate && dueDate) {
            const due = new Date(dueDate);
            const now = new Date();
            if (now > due) {
                minutesLate = Math.ceil((now - due) / (1000 * 60)); // Convert to minutes
            } else {
                minutesLate = 0;
            }
        }
        lateFees = minutesLate * 10; // ₱10 per minute
        console.log(`📅 Late Fees Calculated: ${minutesLate} min(s) = ₱${lateFees}`);
        updatePenaltyDisplay();
    }

    // Condition button selection
    conditionButtons.forEach(btn => {
        btn.addEventListener('click', function () {
            console.log('🔘 Condition Button Clicked:', this.dataset.condition);

            conditionButtons.forEach(b => b.classList.remove('active'));
            this.classList.add('active');

            currentBookCondition = this.dataset.condition;

            // Show/hide damage type dropdown
            if (currentBookCondition === 'Damage') {
                if (damageTypeSection) {
                    damageTypeSection.style.display = 'block';
                }
            } else {
                if (damageTypeSection) {
                    damageTypeSection.style.display = 'none';
                }
                if (damageTypeSelect) {
                    damageTypeSelect.value = '';
                }
                currentDamageType = '';
            }

            // Auto-process return for Lost books (auto-restriction)
            if (currentBookCondition === 'Lost') {
                if (processReturnBtn) {
                    processReturnBtn.style.display = 'none';
                }
                // Automatically process the return for lost books
                setTimeout(() => {
                    processLostBookReturn();
                }, 500); // Small delay to ensure UI updates
            } else {
                if (processReturnBtn) {
                    processReturnBtn.style.display = 'block';
                }
            }

            updatePenaltyDisplay();
        });
    });

    // Damage type dropdown handler
    if (damageTypeSelect) {
        damageTypeSelect.addEventListener('change', function () {
            currentDamageType = this.value;
            console.log('Selected Damage Type:', currentDamageType);
            
            // Debug: Log the penalty amount from data-penalty attribute
            const selectedOption = this.querySelector(`option[value="${currentDamageType}"]`);
            if (selectedOption) {
                const penaltyAmount = selectedOption.getAttribute('data-penalty');
                console.log('Penalty amount from data-penalty:', penaltyAmount);
            }
            
            updatePenaltyDisplay();
        });
    }

    // ✅ DISPLAY-ONLY: Shows estimated penalties (backend will recalculate)
    function updatePenaltyDisplay() {
        const penaltyBreakdown = document.querySelector('#penaltyBreakdown');
        const totalPenaltyAmount = document.querySelector('#totalPenaltyAmount');

        if (!penaltyBreakdown || !totalPenaltyAmount) {
            return;
        }

        let penaltiesHTML = '';
        let totalPenalty = 0;

        // Late fees - calculate minutes from lateFees (₱10 per minute)
        if (lateFees > 0) {
            const minutesLate = lateFees / 10; // Convert back to minutes
            penaltiesHTML += `
                <div class="flex justify-between items-center penalty-row">
                    <span>Late Return Fee (${minutesLate} min${minutesLate !== 1 ? 's' : ''} = ₱${lateFees.toFixed(2)})</span>
                    <span class="font-bold">₱ ${lateFees.toFixed(2)}</span>
                </div>
            `;
            totalPenalty += lateFees;
        }

        // Damage penalty (display only - backend will calculate actual amount)
        if (currentBookCondition === 'Damage' && currentDamageType) {
            // Get penalty amount from the selected option's data-penalty attribute
            const selectedOption = damageTypeSelect.querySelector(`option[value="${currentDamageType}"]`);
            const damageAmount = selectedOption ? parseFloat(selectedOption.getAttribute('data-penalty')) || 0 : 0;
            
            console.log('Damage penalty calculation:', {
                condition: currentBookCondition,
                damageType: currentDamageType,
                selectedOption: selectedOption,
                penaltyAttribute: selectedOption?.getAttribute('data-penalty'),
                damageAmount: damageAmount
            });

            penaltiesHTML += `
                <div class="flex justify-between items-center penalty-row">
                    <span>${currentDamageType} Damage Fee</span>
                    <span class="font-bold">₱ ${damageAmount.toFixed(2)}</span>
                </div>
            `;
            totalPenalty += damageAmount;
        }

        // Lost book penalty
        if (currentBookCondition === 'Lost') {
            const lostPenalty = 2000;
            penaltiesHTML += `
                <div class="flex justify-between items-center penalty-row">
                    <span>Lost Book Fee</span>
                    <span class="font-bold">₱ ${lostPenalty.toFixed(2)}</span>
                </div>
            `;
            totalPenalty += lostPenalty;
        }

        // No penalties
        if (totalPenalty === 0) {
            penaltiesHTML = `
                <div class="flex justify-between items-center penalty-row">
                    <span>No Penalties</span>
                    <span class="font-bold">₱ 0</span>
                </div>
            `;
        }

        penaltyBreakdown.innerHTML = penaltiesHTML;
        totalPenaltyAmount.textContent = `₱ ${totalPenalty.toFixed(2)}`;
    }

    // Restrict Account functionality
    if (restrictBtn) {
        restrictBtn.addEventListener('click', function () {
            if (!currentReservationData) {
                showNotification('Please search for a book first', 'error');
                return;
            }
            restrictModal.style.display = 'flex';
        });
    }

    if (modalNoBtn) {
        modalNoBtn.addEventListener('click', function () {
            restrictModal.style.display = 'none';
        });
    }

    if (modalYesBtn) {
        modalYesBtn.addEventListener('click', async function () {
            restrictModal.style.display = 'none';

            try {
                const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
                const apiBaseUrl = getApiBaseUrl();
                const response = await fetch(`${apiBaseUrl}/RestrictAccount`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify({
                        userId: currentReservationData.userId,
                        reason: 'Overdue book or damage'
                    })
                });

                const result = await response.json();

                if (result.success) {
                    restrictSuccessPopup.style.display = 'flex';
                } else {
                    showNotification(result.message || 'Failed to restrict account', 'error');
                }
            } catch (error) {
                console.error('Restrict error:', error);
                showNotification('Error restricting account. Please try again.', 'error');
            }
        });
    }

    if (closeRestrictSuccess) {
        closeRestrictSuccess.addEventListener('click', function () {
            restrictSuccessPopup.style.display = 'none';
        });
    }

    // Process Return functionality
    if (processReturnBtn) {
        processReturnBtn.addEventListener('click', function () {
            if (!currentReservationData) {
                showNotification('Please search for a book first', 'error');
                return;
            }

            if (currentBookCondition === 'Damage' && !currentDamageType) {
                showNotification('Please select a damage type', 'error');
                return;
            }

            confirmReturnPopup.style.display = 'flex';
        });
    }

    if (cancelReturn) {
        cancelReturn.addEventListener('click', function () {
            confirmReturnPopup.style.display = 'none';
        });
    }

    if (confirmReturnYes) {
        confirmReturnYes.addEventListener('click', async function () {
            confirmReturnPopup.style.display = 'none';

            // Calculate damage penalty from dropdown data-penalty attribute
            let damagePenalty = 0;
            if (currentBookCondition === 'Damage' && currentDamageType) {
                const selectedOption = damageTypeSelect.querySelector(`option[value="${currentDamageType}"]`);
                damagePenalty = selectedOption ? parseFloat(selectedOption.getAttribute('data-penalty')) || 0 : 0;
            }

            // Calculate lost book penalty (only for Lost condition)
            const lostPenalty = currentBookCondition === 'Lost' ? 2000 : 0;
            
            // Calculate total penalty
            const totalPenalty = lateFees + damagePenalty + lostPenalty;

            // ✅ Send data with calculated penalties
            // Fix: Combine damage condition with damage type for backend compatibility
            let finalBookCondition = currentBookCondition;
            if (currentBookCondition === 'Damage' && currentDamageType) {
                finalBookCondition = `Damaged-${currentDamageType}`;
            }

            console.log('📤 Submitting Return Transaction');
            console.log('   Book Condition:', currentBookCondition);
            console.log('   Damage Type:', currentDamageType);
            console.log('   Final Book Condition:', finalBookCondition);

            const returnData = {
                reservationId: currentReservationData.reservationId,
                bookId: currentReservationData.bookId,
                userId: currentReservationData.userId,
                bookTitle: currentReservationData.bookTitle,
                borrowDate: currentReservationData.borrowDate,
                dueDate: currentReservationData.dueDate,
                returnDate: new Date().toISOString(),
                daysLate: currentReservationData.daysLate || 0,
                lateFees: lateFees,
                bookCondition: finalBookCondition,  // Fixed: Send "Damaged-Minor" instead of "Damage"
                damageType: currentDamageType || null,
                damagePenalty: damagePenalty,
                penaltyAmount: lostPenalty,  // Only lost book penalty
                totalPenalty: totalPenalty,
                paymentStatus: 'Pending'
            };

            try {
                const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
                const apiBaseUrl = getApiBaseUrl();
                const response = await fetch(`${apiBaseUrl}/ProcessReturnTransaction`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token
                    },
                    body: JSON.stringify(returnData)
                });

                const result = await response.json();

                if (result.success) {
                    returnSuccessPopup.style.display = 'flex';
                } else {
                    showNotification(result.message || 'Failed to process return', 'error');
                }
            } catch (error) {
                console.error('Return error:', error);
                showNotification('Error processing return. Please try again.', 'error');
            }
        });
    }

    if (closeReturnSuccess) {
        closeReturnSuccess.addEventListener('click', function () {
            returnSuccessPopup.style.display = 'none';

            // Clear the form and reset state
            searchInput.value = '';
            hideBookInfo();
            currentBookCondition = 'Good';
            conditionButtons.forEach(b => b.classList.remove('active'));
            conditionButtons[0].classList.add('active');
        });
    }

    // Close modals on outside click
    window.addEventListener('click', function (e) {
        if (e.target === restrictModal) restrictModal.style.display = 'none';
        if (e.target === restrictSuccessPopup) restrictSuccessPopup.style.display = 'none';
        if (e.target === confirmReturnPopup) confirmReturnPopup.style.display = 'none';
        if (e.target === returnSuccessPopup) returnSuccessPopup.style.display = 'none';
    });

    // Notification helper
    function showNotification(message, type = 'info') {
        const notification = document.createElement('div');
        notification.className = `notification notification-${type}`;
        notification.innerHTML = `
            <div style="background: ${type === 'error' ? '#ff4444' : '#4CAF50'}; 
                        color: white; 
                        padding: 16px 24px; 
                        border-radius: 8px; 
                        box-shadow: 0 4px 12px rgba(0,0,0,0.15);
                        position: fixed;
                        top: 20px;
                        right: 20px;
                        z-index: 10000;
                        max-width: 400px;
                        animation: slideIn 0.3s ease-out;">
                <div style="display: flex; align-items: center; gap: 12px;">
                    <span style="font-size: 20px;">${type === 'error' ? '⚠️' : '✓'}</span>
                    <span style="font-weight: 500;">${message}</span>
                </div>
            </div>
        `;

        document.body.appendChild(notification);

        setTimeout(() => {
            notification.style.animation = 'slideOut 0.3s ease-in';
            setTimeout(() => notification.remove(), 300);
        }, 4000);
    }

    // Add CSS for animations
    const style = document.createElement('style');
    style.textContent = `
        @keyframes slideIn {
            from { transform: translateX(400px); opacity: 0; }
            to { transform: translateX(0); opacity: 1; }
        }
        
        @keyframes slideOut {
            from { transform: translateX(0); opacity: 1; }
            to { transform: translateX(400px); opacity: 0; }
        }
        
        .condition-btn.active {
            border: 3px solid #2563eb;
            box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.2);
            transform: scale(1.05);
        }
        
        .penalty-row { padding: 8px 0; }

        #damageTypeSection { transition: all 0.3s ease; }

        #damageType {
            width: 100%;
            padding: 12px;
            border: 2px solid #e5e7eb;
            border-radius: 8px;
            font-size: 14px;
            background: white;
            transition: border-color 0.2s;
        }

        #damageType:focus {
            outline: none;
            border-color: #2563eb;
        }
    `;
    document.head.appendChild(style);

    // Reset form function
    function resetForm() {
        // Reset search
        if (searchInput) searchInput.value = '';
        if (bookInfoSection) bookInfoSection.style.display = 'none';
        
        // Reset condition
        currentBookCondition = 'Good';
        conditionButtons.forEach(btn => {
            btn.classList.remove('active');
            if (btn.dataset.condition === 'Good') {
                btn.classList.add('active');
            }
        });
        
        // Reset damage type
        currentDamageType = '';
        if (damageTypeSection) damageTypeSection.style.display = 'none';
        if (damageTypeSelect) damageTypeSelect.value = '';
        
        // Reset penalties
        lateFees = 0;
        currentReservationData = null;
        
        // Show process return button
        if (processReturnBtn) processReturnBtn.style.display = 'block';
        
        updatePenaltyDisplay();
    }

    // Auto-process return for lost books
    async function processLostBookReturn() {
        if (!currentReservationData) {
            showNotification('Please search for a book first', 'error');
            return;
        }

        // Show confirmation dialog for lost book
        const confirmed = confirm('Mark this book as LOST? This will automatically restrict the student account and cannot be undone.');
        if (!confirmed) {
            // Reset to Good condition if user cancels
            currentBookCondition = 'Good';
            conditionButtons.forEach(btn => {
                btn.classList.remove('active');
                if (btn.dataset.condition === 'Good') {
                    btn.classList.add('active');
                }
            });
            if (processReturnBtn) {
                processReturnBtn.style.display = 'block';
            }
            updatePenaltyDisplay();
            return;
        }

        // Calculate lost book penalty
        const lostPenalty = 2000;
        const totalPenalty = lateFees + lostPenalty;

        console.log('📤 Auto-processing Lost Book Return');
        console.log('   Book Condition: Lost');
        console.log('   Lost Penalty:', lostPenalty);
        console.log('   Total Penalty:', totalPenalty);

        const returnData = {
            reservationId: currentReservationData.reservationId,
            bookId: currentReservationData.bookId,
            userId: currentReservationData.userId,
            bookTitle: currentReservationData.bookTitle,
            borrowDate: currentReservationData.borrowDate,
            dueDate: currentReservationData.dueDate,
            returnDate: new Date().toISOString(),
            daysLate: currentReservationData.daysLate || 0,
            lateFees: lateFees,
            bookCondition: 'Lost',
            damageType: null,
            damagePenalty: 0,
            penaltyAmount: lostPenalty,
            totalPenalty: totalPenalty,
            paymentStatus: 'Pending'
        };

        try {
            const apiBaseUrl = getApiBaseUrl();
            const response = await fetch(`${apiBaseUrl}/ProcessReturnTransaction`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify(returnData)
            });

            const result = await response.json();

            if (result.success) {
                if (result.accountRestricted) {
                    showNotification('Book marked as LOST. Student account has been automatically restricted.', 'success');
                } else {
                    showNotification('Book marked as LOST. Warning: Failed to restrict student account.', 'warning');
                }
                
                // Reset the form
                resetForm();
            } else {
                showNotification('Error: ' + result.message, 'error');
            }
        } catch (error) {
            console.error('Error processing lost book return:', error);
            showNotification('An error occurred while processing the return', 'error');
        }
    }

    console.log('✅ Return Transaction JS Fully Initialized');
});