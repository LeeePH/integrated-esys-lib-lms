(function () {
    'use strict';

    const config = document.getElementById('enrolled2ndSemConfig');
    if (!config) {
        console.log('[admin-enrolled-2nd-sem-details] Config element not found');
        return;
    }

    const requestId = config.dataset.requestId;
    const program = config.dataset.program;
    const firstSemRemarks = JSON.parse(config.dataset.firstSemRemarks || '{}');
    let secondSemRemarks = JSON.parse(config.dataset.secondSemRemarks || '{}');
    const prerequisites = JSON.parse(config.dataset.prerequisites || '{}');
    const calcUrl = config.dataset.calcUrl;

    console.log('[admin-enrolled-2nd-sem-details] Initialized');

    const btnCalculate = document.getElementById('btnCalculateEligibility');
    const eligibilitySection = document.getElementById('secondYearEligibilitySection');
    const eligibilityBody = document.getElementById('secondYearEligibilityBody');
    const eligibilityDataDiv = document.getElementById('eligibilityData');

    if (!btnCalculate) {
        console.error('[admin-enrolled-2nd-sem-details] Calculate button not found');
        return;
    }

    // Update secondSemRemarks when user changes radio buttons
    document.querySelectorAll('.remarks-switch input[type="radio"]').forEach(radio => {
        radio.addEventListener('change', (e) => {
            const code = e.target.name.match(/\[(.*?)\]/)[1];
            const value = e.target.value;
            secondSemRemarks[code] = value;
            console.log(`[admin-enrolled-2nd-sem-details] Updated ${code} = ${value}`);
        });
    });

    btnCalculate.addEventListener('click', async () => {
        console.log('[admin-enrolled-2nd-sem-details] Calculate button clicked');

        // ✅ Step 1: Collect 1st semester remarks from hidden inputs
        const firstSemRemarksCollected = {};
        document.querySelectorAll('input[type="hidden"][name^="firstSemRemarks"]').forEach(input => {
            const match = input.name.match(/firstSemRemarks\[(.*?)\]/);
            if (match) {
                const code = match[1];
                firstSemRemarksCollected[code] = input.value;
                console.log(`[admin-enrolled-2nd-sem-details] 1st Sem: ${code} = ${input.value}`);
            }
        });

        // ✅ Step 2: Collect 2nd semester remarks from radio buttons
        const secondSemRemarksCollected = {};
        document.querySelectorAll('.remarks-switch').forEach(group => {
            const code = group.dataset.subjectCode;
            const checkedRadio = group.querySelector('input[type="radio"]:checked');
            if (checkedRadio) {
                secondSemRemarksCollected[code] = checkedRadio.value;
                console.log(`[admin-enrolled-2nd-sem-details] 2nd Sem: ${code} = ${checkedRadio.value}`);
            }
        });

        // ✅ Step 3: Merge remarks (2nd semester overrides 1st if there's overlap)
        const allRemarks = { ...firstSemRemarksCollected, ...secondSemRemarksCollected };

        console.log('[admin-enrolled-2nd-sem-details] Merged remarks:', allRemarks);
        console.log(`[admin-enrolled-2nd-sem-details] Total remarks: ${Object.keys(allRemarks).length}`);

        // ✅ CRITICAL: Get anti-forgery token
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (!token) {
            console.error('[admin-enrolled-2nd-sem-details] Anti-forgery token not found');
            alert('Security token missing. Please refresh the page.');
            return;
        }

        // Build FormData
        const formData = new FormData();
        formData.append('id', requestId);
        formData.append('__RequestVerificationToken', token); // ✅ ADD TOKEN

        // Add subject remarks
        for (const [code, remark] of Object.entries(allRemarks)) {
            formData.append(`subjectRemarks[${code}]`, remark);
            console.log(`[admin-enrolled-2nd-sem-details] Sending: subjectRemarks[${code}] = ${remark}`);
        }

        try {
            console.log('[admin-enrolled-2nd-sem-details] Sending POST to', calcUrl);

            const response = await fetch(calcUrl, {
                method: 'POST',
                body: formData,
                headers: {
                    'RequestVerificationToken': token // ✅ ADD HEADER
                }
            });

            console.log('[admin-enrolled-2nd-sem-details] Response status:', response.status);

            if (!response.ok) {
                const errorText = await response.text();
                console.error('[admin-enrolled-2nd-sem-details] Server error:', errorText);
                throw new Error('Failed to calculate eligibility');
            }

            const data = await response.json();
            console.log('[admin-enrolled-2nd-sem-details] Received data:', data);

            if (data.error) {
                alert(data.error);
                return;
            }

            // Display results
            displayEligibilityResults(data.eligibility);
            storeEligibilityData(data.eligibility);

            // Show section
            eligibilitySection.classList.remove('d-none');
            eligibilitySection.scrollIntoView({ behavior: 'smooth' });

        } catch (error) {
            console.error('[admin-enrolled-2nd-sem-details] Eligibility calculation error:', error);
            alert('Failed to calculate eligibility. Check console for details.');
        }
    });

    function displayEligibilityResults(eligibility) {
        eligibilityBody.innerHTML = '';

        for (const [code, status] of Object.entries(eligibility)) {
            const row = document.createElement('tr');
            const isEligible = status.startsWith('Can enroll');
            const badgeClass = isEligible ? 'bg-success' : 'bg-danger';
            const prereqList = prerequisites[code] || [];
            const prereqDisplay = prereqList.length > 0 ? prereqList.join(', ') : 'None';

            row.innerHTML = `
                <td><strong>${code}</strong></td>
                <td>${code}</td>
                <td>3</td>
                <td><span class="text-muted small">${prereqDisplay}</span></td>
                <td><span class="badge ${badgeClass}">${status}</span></td>
            `;

            eligibilityBody.appendChild(row);
        }
    }

    function storeEligibilityData(eligibility) {
        eligibilityDataDiv.innerHTML = '';

        for (const [code, status] of Object.entries(eligibility)) {
            const input = document.createElement('input');
            input.type = 'hidden';
            input.name = `secondYearEligibility[${code}]`;
            input.value = status;
            eligibilityDataDiv.appendChild(input);
        }
    }
})();