// antiCheat.js
// Unified AntiCheat combining mouse.js, interact.js, fullscreen.js, focus.js
// Exposes window.AntiCheat for diagnostics. Each subsystem is isolated.

(function (global) {
    if (!global) return;
    const NS = global.AntiCheat = global.AntiCheat || {};

    /* ------------------ Shared utilities & styles ------------------ */
    const utils = {
        nowMs: () => Date.now(),
        formatTimeMs: (ms) => {
            const s = Math.floor(ms / 1000);
            const m = Math.floor(s / 60);
            const remS = s % 60;
            return `${m}m, ${remS}s`;
        },
        formatTimeSec: (sec) => {
            const m = Math.floor(sec / 60);
            const s = sec % 60;
            return `${m}m, ${s}s`;
        },
        attachStyles(cssText) {
            try {
                const st = document.createElement('style');
                st.dataset.ac = 'true';
                st.innerHTML = cssText;
                document.head.appendChild(st);
            } catch (e) {
                // silently fail - styles not critical
                console.warn('AntiCheat: failed to attach styles', e);
            }
        },
        safeAppend(el) {
            try {
                document.body.appendChild(el);
            } catch (e) {
                console.warn('AntiCheat: append failed', e);
            }
        }
    };

    // Combined CSS (keeps original look but scoped with ac- prefixes to reduce collision)
    utils.attachStyles(`
    /* General */
    .ac-scoreboard, .ac-status-scoreboard { position: fixed; z-index: 10000; font-family: sans-serif; color: #fff; box-shadow: 0 4px 12px rgba(0,0,0,0.3); }
    /* mouse.js styles */
    body { cursor: default; }
    .ac-afk-radius, .ac-afk-confirmed { position: fixed; border-radius: 50%; pointer-events: none; z-index: 9998; display: none; visibility: hidden; }
    .ac-afk-radius { border: 3px dashed transparent; }
    .ac-afk-confirmed { border: 3px dashed transparent; }
    .ac-status-scoreboard { bottom: 100px; right: 20px; padding: 16px 24px; border-radius: 12px; background: #222; font-size: 14px; line-height: 1.5; white-space: pre-line; }
  
    /* interact.js popup */
    .ac-interact-popup { position: fixed; top: 10px; right: 10px; background: rgba(0,0,0,0.8); color: white; padding: 20px; border-radius: 10px; z-index: 9999; font-family: Arial, sans-serif; font-size: 14px; box-shadow: 0 0 10px rgba(0,0,0,0.5); }
  
    /* fullscreen.js overlay & stats */
    .ac-unlock-screen { position: fixed; top:0; left:0; right:0; bottom:0; background: rgba(255,255,255,0.5); backdrop-filter: blur(7px); display: none; z-index: 9998; align-items: center; justify-content: center; }
    .ac-fullscreen-stats { position: fixed; top: 20px; left: 20px; background: rgba(0,0,0,0.7); color: white; padding: 10px 15px; font-size: 14px; border-radius: 10px; z-index: 9999; font-family: sans-serif; }
    .ac-unlock-screen .ac-button { background-color: #1f2937; color: white; padding: .75rem 2rem; border-radius: 1rem; border: none; cursor: pointer; font-weight:600; }
  
    /* focus.js overlay + boxes */
    .ac-screenPromptOverlay { position: fixed; top:0; left:0; width:100vw; height:100vh; background:#333340; z-index:9999; display:flex; align-items:center; justify-content:center; flex-direction:column; color:white; font-family:sans-serif; text-align:center; overflow:hidden; }
    .ac-studentStatus { position: fixed; top: 20px; right: 20px; padding: 0.8em 1.2em; border-left: 8px solid; border-radius: 8px; font-weight: bold; font-size: 1.1em; box-shadow: 0 0 8px rgba(0,0,0,0.2); z-index: 9998; opacity: 0; transition: opacity 1s ease-in-out; }
    .ac-green { background: #d4edda; color: #155724; border-color: green; }
    .ac-blue { background: #cce5ff; color: #004085; border-color: blue; }
    .ac-yellow { background: #fff3cd; color: #856404; border-color: gold; }
    .ac-orange { background: #ffe5b4; color: #a05a00; border-color: orange; }
    .ac-red { background: #f8d7da; color: #721c24; border-color: red; }
    .ac-logsBox { position: fixed; top: 150px; right: 20px; width: 360px; max-height: 300px; overflow-y: auto; background: rgba(0,0,0,0.85); color: white; font-family: monospace; font-size: 0.85em; padding: 12px; border-radius: 6px; z-index: 10000; }
    .ac-cheatMetrics { position: fixed; bottom: 20px; right: 20px; background: rgba(0,0,0,0.7); color: white; padding: 10px; border-radius: 8px; z-index: 10000; }
    .ac-omniFocusBox { position: fixed; top: 150px; right: 400px; width: 260px; background: rgba(0,0,0,0.85); color: white; font-family: sans-serif; font-size: 0.9em; padding: 12px; border-radius: 6px; z-index: 10000; box-shadow: 0 0 10px rgba(0,0,0,0.5); }
    `);

    /* ------------------ Subsystems ------------------ */

    /* ------------------ 1) Mouse & AFK subsystem (from mouse.js) ------------------ */
    function initMouseModule() {
        try {
            // Scoreboard
            const scoreboard = document.createElement('div');
            scoreboard.className = 'ac-status-scoreboard';
            scoreboard.setAttribute('aria-live', 'polite');
            utils.safeAppend(scoreboard);

            let mouseOffCount = 0;
            let mouseOffTime = 0;
            let mouseOffStart = null;

            let afkCount = 0;
            let afkTime = 0;
            let afkStart = null;

            let isInside = true;
            let currentMousePos = { x: 0, y: 0 };
            let lastMousePos = { x: 0, y: 0 };
            let afkRadius = 0;
            let afkCheckTimer = null;
            let afkWarningShown = false;
            let redCircleVisible = false;
            let hasMouseMoved = false;
            const AFK_WAIT_TIME = 5000;

            // create invisible circles
            const orangeCircle = document.createElement('div');
            orangeCircle.className = 'ac-afk-radius';
            document.body.appendChild(orangeCircle);

            const redCircle = document.createElement('div');
            redCircle.className = 'ac-afk-confirmed';
            document.body.appendChild(redCircle);

            function showCircle(circle, x, y, radius) {
                circle.style.width = `${radius * 2}px`;
                circle.style.height = `${radius * 2}px`;
                circle.style.left = `${x - radius}px`;
                circle.style.top = `${y - radius}px`;
                circle.style.display = "block";
            }
            function hideCircle(circle) { circle.style.display = "none"; }

            function updateScoreboard() {
                const totalMouseOff = mouseOffTime + (mouseOffStart ? Date.now() - mouseOffStart : 0);
                const totalAfk = afkTime + (afkStart ? Date.now() - afkStart : 0);
                scoreboard.textContent = `Mouse ON/OFF Score
  • Mouse left for ${utils.formatTimeMs(totalMouseOff)} For ${mouseOffCount} times 
  • Afk for ${utils.formatTimeMs(totalAfk)} For ${afkCount} times`;
            }

            setInterval(updateScoreboard, 1000);

            // detect mouse leaving page
            window.addEventListener('mouseout', (e) => {
                // similar heuristics as original
                if ((!e.relatedTarget && e.clientY <= 0) || (!e.relatedTarget || !e.relatedTarget.nodeName)) {
                    if (isInside) {
                        isInside = false;
                        mouseOffStart = Date.now();
                        mouseOffCount++;
                        updateScoreboard();
                    }
                }
            });

            window.addEventListener('mouseover', () => {
                if (!isInside) {
                    isInside = true;
                    if (mouseOffStart) {
                        mouseOffTime += Date.now() - mouseOffStart;
                        mouseOffStart = null;
                        updateScoreboard();
                    }
                }
            });

            function startAfkTimer(pos) {
                clearTimeout(afkCheckTimer);
                afkRadius = Math.max(1, Math.min(window.innerWidth, window.innerHeight)) * 0.1;
                lastMousePos = { x: pos.x, y: pos.y };
                showCircle(orangeCircle, pos.x, pos.y, afkRadius);
                hideCircle(redCircle);
                redCircleVisible = false;

                afkCheckTimer = setTimeout(() => {
                    if (afkWarningShown) return;
                    const dx = currentMousePos.x - lastMousePos.x;
                    const dy = currentMousePos.y - lastMousePos.y;
                    const dist = Math.sqrt(dx * dx + dy * dy);
                    if (dist <= afkRadius) {
                        afkStart = Date.now();
                        afkCount++;
                        afkWarningShown = true;
                        const redRadius = afkRadius * 1.05;
                        showCircle(redCircle, currentMousePos.x, currentMousePos.y, redRadius);
                        redCircleVisible = true;
                        updateScoreboard();
                    } else {
                        startAfkTimer(currentMousePos);
                    }
                }, AFK_WAIT_TIME);
            }

            document.addEventListener('mousemove', (e) => {
                if (!hasMouseMoved) {
                    hasMouseMoved = true;
                    isInside = true;
                }
                currentMousePos = { x: e.clientX, y: e.clientY };
                const dx = currentMousePos.x - lastMousePos.x;
                const dy = currentMousePos.y - lastMousePos.y;
                const dist = Math.sqrt(dx * dx + dy * dy);

                if (dist > afkRadius) {
                    if (afkStart) {
                        afkTime += Date.now() - afkStart;
                        afkStart = null;
                    }
                    afkWarningShown = false;
                    hideCircle(orangeCircle);
                    if (redCircleVisible) {
                        const redRadius = afkRadius * 1.05;
                        const dxRed = currentMousePos.x - lastMousePos.x;
                        const dyRed = currentMousePos.y - lastMousePos.y;
                        const distRed = Math.sqrt(dxRed * dxRed + dyRed * dyRed);
                        if (distRed > redRadius) {
                            hideCircle(redCircle);
                            redCircleVisible = false;
                        }
                    }
                    updateScoreboard();
                    startAfkTimer(currentMousePos);
                }
            });

            // init: seed timer at center
            startAfkTimer({ x: window.innerWidth / 2, y: window.innerHeight / 2 });

            // expose minimal debug
            return { name: 'mouse', scoreboard, orangeCircle, redCircle };
        } catch (err) {
            console.error('AntiCheat.mouse module failed to init', err);
            return null;
        }
    }

    /* ------------------ 2) Interaction popup (copy/inspect/printscreen & mouse behavior) (from interact.js) ------------------ */
    function initInteractModule() {
        try {
            // Create popup
            const popup = document.createElement('div');
            popup.className = 'ac-interact-popup';
            popup.innerHTML = `
          <h3 style="margin:0 0 8px 0;">Interaction Score</h3>
          <p class="ac-copyText">• Copy paste record: 0</p>
          <p class="ac-inspectText">• Inspect record: 0</p>
          <p class="ac-printScreenText">• Print Screen record: 0</p>
          <p class="ac-mouseBehaviorText">• Mouse Behavior: 0</p>
        `;
            utils.safeAppend(popup);
            if (!window.__ac) window.__ac = {};
            window.__ac.startTs = Date.now();

            // Counters
            let copyCount = 0;
            let pasteCount = 0;
            let inspectCount = 0;
            let printScreenCount = 0;
            let mouseBehaviorCount = 0;
            let interactionScore = 0;
            let lastMouseMoveTs = 0;

            const copyText = popup.querySelector('.ac-copyText');
            const inspectText = popup.querySelector('.ac-inspectText');
            const printScreenText = popup.querySelector('.ac-printScreenText');
            const mouseBehaviorText = popup.querySelector('.ac-mouseBehaviorText');

            document.addEventListener('copy', () => {
                copyCount++;
                copyText.innerText = `• Copy paste record: ${copyCount + pasteCount}`;
                interactionScore += 2;
                window.__ac.copy = copyCount;
                window.__ac.score = interactionScore;
                sendEvent('copy_paste', { action: 'copy', count: copyCount });
            });

            document.addEventListener('paste', () => {
                pasteCount++;
                copyText.innerText = `• Copy paste record: ${copyCount + pasteCount}`;
                interactionScore += 2;
                window.__ac.copy = copyCount;
                window.__ac.paste = pasteCount;
                window.__ac.score = interactionScore;
                sendEvent('copy_paste', { action: 'paste', count: pasteCount });
            });

            // contextmenu used as heuristic for inspect / right-click
            document.addEventListener('contextmenu', (e) => {
                inspectCount++;
                inspectText.innerText = `• Inspect record: ${inspectCount}`;
                interactionScore += 1;
                window.__ac.inspect = inspectCount;
                window.__ac.score = interactionScore;
                sendEvent('inspect', { count: inspectCount });
            });

            document.addEventListener('keydown', (e) => {
                // 'PrintScreen' key
                if (e.key === 'PrintScreen' || e.code === 'PrintScreen') {
                    printScreenCount++;
                    printScreenText.innerText = `• Print Screen record: ${printScreenCount}`;
                    interactionScore += 1;
                    window.__ac.print = printScreenCount;
                    window.__ac.score = interactionScore;
                    sendEvent('print_screen', { count: printScreenCount });
                }
                const k = e.key.toLowerCase();
                if (k === 'f12' || (e.ctrlKey && e.shiftKey && k === 'i')) {
                    inspectCount++;
                    inspectText.innerText = `• Inspect record: ${inspectCount}`;
                    interactionScore += 1;
                    window.__ac.inspect = inspectCount;
                    window.__ac.score = interactionScore;
                    sendEvent('inspect', { count: inspectCount });
                }
            });

            // Heuristic for Ctrl/Cmd + C/V when clipboard events are blocked
            document.addEventListener('keyup', (e) => {
                const k = (e.key || '').toLowerCase();
                if ((e.ctrlKey || e.metaKey) && k === 'c') {
                    copyCount++;
                    copyText.innerText = `• Copy paste record: ${copyCount + pasteCount}`;
                    interactionScore += 1.5;
                    window.__ac.copy = copyCount;
                    window.__ac.score = interactionScore;
                    sendEvent('copy_paste', { action: 'copy', count: copyCount });
                } else if ((e.ctrlKey || e.metaKey) && k === 'v') {
                    pasteCount++;
                    copyText.innerText = `• Copy paste record: ${copyCount + pasteCount}`;
                    interactionScore += 1.5;
                    window.__ac.paste = pasteCount;
                    window.__ac.score = interactionScore;
                    sendEvent('copy_paste', { action: 'paste', count: pasteCount });
                }
            });

            // Mouse behavior detection heuristic: track bounding boxes of elements that set cursor style
            let mouseBehaviorCounter = 0;
            const cursorHistory = new Map();

            const detectInterval = setInterval(() => {
                try {
                    const elements = document.querySelectorAll('[style*="cursor"]');
                    let detected = 0;
                    elements.forEach((el) => {
                        const rect = el.getBoundingClientRect();
                        const id = el.dataset.acCursorId || Math.random().toString(36).slice(2);
                        el.dataset.acCursorId = id;
                        const prev = cursorHistory.get(id) || [];
                        const current = { x: Math.round(rect.left), y: Math.round(rect.top), t: Date.now() };
                        prev.push(current);
                        if (prev.length > 10) prev.shift();
                        cursorHistory.set(id, prev);

                        if (prev.length >= 3) {
                            const dx1 = prev[1].x - prev[0].x;
                            const dy1 = prev[1].y - prev[0].y;
                            const dx2 = prev[2].x - prev[1].x;
                            const dy2 = prev[2].y - prev[1].y;
                            const sameDir = dx1 === dx2 && dy1 === dy2;
                            const linearMotion = (dx1 === 0 || dy1 === 0 || dx1 === dy1 || dx1 === -dy1);
                            if (sameDir && linearMotion) detected++;
                        }
                    });
                    if (detected > 0) {
                        mouseBehaviorCounter++;
                        mouseBehaviorText.innerText = `• Mouse Behavior: ${mouseBehaviorCounter}`;
                        mouseBehaviorCount = mouseBehaviorCounter;
                        interactionScore += detected * 0.5;
                        window.__ac.mouse = mouseBehaviorCount;
                        window.__ac.score = interactionScore;
                    }
                } catch (e) {
                    // don't break if DOM has issues
                }
            }, 500);

            document.addEventListener('wheel', () => {
                mouseBehaviorCount++;
                mouseBehaviorText.innerText = `• Mouse Behavior: ${mouseBehaviorCount}`;
                window.__ac.mouse = mouseBehaviorCount;
                sendEvent('mouse_activity', { count: mouseBehaviorCount });
            }, { passive: true });

            document.addEventListener('mousemove', () => {
                const now = Date.now();
                if (now - lastMouseMoveTs > 300) {
                    mouseBehaviorCount++;
                    mouseBehaviorText.innerText = `• Mouse Behavior: ${mouseBehaviorCount}`;
                    window.__ac.mouse = mouseBehaviorCount;
                    interactionScore += 0.5;
                    window.__ac.score = interactionScore;
                    lastMouseMoveTs = now;
                    sendEvent('mouse_activity', { count: mouseBehaviorCount });
                }
            }, { passive: true });

            // Passive activity tick while page is visible
            setInterval(() => {
                if (!document.hidden) {
                    interactionScore += 1;
                    window.__ac.score = interactionScore;
                }
            }, 5000);

            function sendSummary() {
                try {
                    const path = (window.location.pathname || '').split('/').filter(Boolean);
                    const idx = path.findIndex(p => p.toLowerCase() === 'studentanswerassessment');
                    if (idx < 0 || path.length < idx + 3) return;
                    const classCode = path[idx + 1];
                    const contentId = path[idx + 2];
                    const duration = Math.floor(((Date.now() - (window.__ac.startTs || Date.now())))/1000);
                    const details = {
                        copyPasteCount: window.__ac.copy || 0,
                        inspectCount: window.__ac.inspect || 0,
                        printScreenCount: window.__ac.print || 0,
                        mouseBehaviorCount: window.__ac.mouse || 0,
                        interactionScore: window.__ac.score || 0,
                        durationSeconds: duration
                    };
                    const payload = {
                        resultId: window.__ac?.resultId || '',
                        eventType: 'summary',
                        details: JSON.stringify(details),
                        eventCount: 1,
                        eventDuration: duration,
                        severity: 'low',
                        flagged: false
                    };
                    fetch(`/StudentAnswerAssessment/${classCode}/${contentId}/log-event`, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify(payload),
                        credentials: 'same-origin'
                    }).catch(() => {});
                } catch (_) {}
            }

            window.addEventListener('beforeunload', sendSummary);
            setInterval(sendSummary, 60000);

            function sendEvent(type, obj) {
                try {
                    const path = (window.location.pathname || '').split('/').filter(Boolean);
                    const idx = path.findIndex(p => p.toLowerCase() === 'studentanswerassessment');
                    if (idx < 0 || path.length < idx + 3) return;
                    const classCode = path[idx + 1];
                    const contentId = path[idx + 2];
                    const payload = {
                        resultId: window.__ac?.resultId || '',
                        eventType: type,
                        details: JSON.stringify(obj || {}),
                        eventCount: (obj && obj.count) ? obj.count : 1,
                        eventDuration: 0,
                        severity: 'low',
                        flagged: false
                    };
                    fetch(`/StudentAnswerAssessment/${classCode}/${contentId}/log-event`, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify(payload),
                        credentials: 'same-origin'
                    }).catch(() => {});
                } catch (_) {}
            }

            return { name: 'interact', popup, stop: () => clearInterval(detectInterval) };
        } catch (err) {
            console.error('AntiCheat.interact module failed to init', err);
            return null;
        }
    }

    /* ------------------ 3) Fullscreen / Locking subsystem (from fullscreen.js) ------------------ */
    function initFullscreenModule() {
        try {
            let fullscreenStartTime = null;
            let fullscreenTotalTime = 0;
            let leftFullscreenCount = 0;
            let leftFullscreenTime = 0;
            let leftStart = null;

            // Stats overlay
            const fullscreenStats = document.createElement('div');
            fullscreenStats.className = 'ac-fullscreen-stats';
            fullscreenStats.innerHTML = `
          <div>
            • Full screen for <span class="ac-fullscreen-duration">0m, 0s</span><br>
            • Left full screen <span class="ac-fullscreen-leave-count">0</span> times<br>
            • Left full screen for a total <span class="ac-fullscreen-leave-secondscount">0m, 0s</span>
          </div>
        `;
            utils.safeAppend(fullscreenStats);

            // Unlock overlay
            const lockScreen = document.createElement('div');
            lockScreen.className = 'ac-unlock-screen';
            lockScreen.id = 'ac-unlock-screen';
            lockScreen.innerHTML = `
          <div style="text-align:center; z-index:10001;">
            <p style="font-size:22px; font-weight:500; color:#333; margin-bottom:20px;">🔒 Exam Locked - Please Enter Fullscreen Mode</p>
            <div style="display:inline-flex; gap:1rem; align-items:center; position:relative;">
              <button id="ac-enter-fullscreen-btn" class="ac-button">Enter Fullscreen Mode</button>
            </div>
          </div>
        `;
            utils.safeAppend(lockScreen);

            function updateStatsDisplay() {
                const now = Date.now();
                const activeTime = fullscreenStartTime ? Math.floor((now - fullscreenStartTime) / 1000) : 0;
                const totalTime = fullscreenTotalTime + activeTime;
                const totalLeftTime = leftFullscreenTime + (leftStart ? Math.floor((now - leftStart) / 1000) : 0);

                const durEl = fullscreenStats.querySelector('.ac-fullscreen-duration');
                const leaveCountEl = fullscreenStats.querySelector('.ac-fullscreen-leave-count');
                const leaveSecEl = fullscreenStats.querySelector('.ac-fullscreen-leave-secondscount');

                if (durEl) durEl.textContent = utils.formatTimeSec(totalTime);
                if (leaveCountEl) leaveCountEl.textContent = leftFullscreenCount;
                if (leaveSecEl) leaveSecEl.textContent = utils.formatTimeSec(totalLeftTime);
            }

            setInterval(updateStatsDisplay, 1000);

            function lockPage() {
                if (!document.body.classList.contains('ac-locked')) {
                    document.body.classList.add('ac-locked');
                    lockScreen.style.display = 'flex';
                    lockScreen.style.alignItems = 'center';
                    lockScreen.style.justifyContent = 'center';
                    if (fullscreenStartTime) {
                        fullscreenTotalTime += Math.floor((Date.now() - fullscreenStartTime) / 1000);
                        fullscreenStartTime = null;
                    }
                    leftStart = Date.now();
                    leftFullscreenCount++;
                }
            }
            function unlockPage() {
                document.body.classList.remove('ac-locked');
                lockScreen.style.display = 'none';
                fullscreenStartTime = Date.now();
                if (leftStart) {
                    leftFullscreenTime += Math.floor((Date.now() - leftStart) / 1000);
                    leftStart = null;
                }
            }
            function enterFullScreen() {
                if (document.documentElement.requestFullscreen) {
                    document.documentElement.requestFullscreen().catch(() => { /* ignore */ });
                } else if (document.documentElement.webkitRequestFullscreen) {
                    document.documentElement.webkitRequestFullscreen();
                }
            }

            const btn = lockScreen.querySelector('#ac-enter-fullscreen-btn');
            if (btn) btn.addEventListener('click', enterFullScreen);

            document.addEventListener('fullscreenchange', () => {
                if (!document.fullscreenElement) lockPage();
                else unlockPage();
            });

            document.addEventListener('visibilitychange', () => {
                if (document.hidden) lockPage();
            });

            // On load: wrap page content in page-content id if not present
            window.addEventListener('load', () => {
                if (!document.getElementById('page-content')) {
                    const wrapper = document.createElement('div');
                    wrapper.id = 'page-content';
                    // Move all nodes except our fullscreen stats & lock screen into wrapper
                    const preserve = new Set([fullscreenStats, lockScreen]);
                    while (document.body.firstChild) {
                        if (preserve.has(document.body.firstChild)) {
                            document.body.appendChild(document.body.firstChild); // keep in order
                            break;
                        }
                        wrapper.appendChild(document.body.firstChild);
                    }
                    // Insert wrapper at top of body
                    document.body.insertBefore(wrapper, document.body.firstChild);
                }
                // initially lock until user enters fullscreen (simulate earlier logic)
                // but only lock after "screen-sharing check" — keep simple and lock immediately for safety
                lockPage();
            });

            return { name: 'fullscreen', fullscreenStats, lockScreen };
        } catch (err) {
            console.error('AntiCheat.fullscreen module failed to init', err);
            return null;
        }
    }

    /* ------------------ 4) Focus / Screen-share monitoring (from focus.js & parts of focus module) ------------------ */
    function initFocusModule() {
        try {
            // Instead of document.write we create DOM nodes
            const overlay = document.createElement('div');
            overlay.className = 'ac-screenPromptOverlay';
            overlay.id = 'ac-screenPromptOverlay';
            overlay.innerHTML = `
          <div style="position:relative; z-index:1;">
            <button id="ac-startScreenShare" style="padding:10px 20px; font-size:1.2em; margin-bottom:1em; background:white; color:black; border:none; border-radius:12px;">Share Your Entire Screen</button>
          </div>
          <div style="z-index:1; margin-top:8px;">Please select "Entire screen" to access the exam.</div>
        `;
            utils.safeAppend(overlay);

            const studentStatus = document.createElement('div');
            studentStatus.id = 'ac-studentStatus';
            studentStatus.className = 'ac-studentStatus ac-green';
            studentStatus.textContent = '🟢 Focused Student';
            utils.safeAppend(studentStatus);

            // logs box
            const logsBox = document.createElement('div');
            logsBox.className = 'ac-logsBox';
            logsBox.innerHTML = `<table style="width:100%; border-collapse:collapse;"><thead><tr><th style="text-align:left; color:#ccc;">LOGS</th><th style="text-align:left; color:#ccc;">OFFENSE</th><th style="text-align:left; color:#ccc;">TIME</th></tr></thead><tbody id="ac-logsBody"></tbody></table>`;
            utils.safeAppend(logsBox);
            const logsBody = logsBox.querySelector('#ac-logsBody');

            const cheatMetrics = document.createElement('div');
            cheatMetrics.className = 'ac-cheatMetrics';
            cheatMetrics.innerHTML = `
          <div>Screen Share Off Count: <span class="ac-screen-turnoff-count">0</span></div>
          <div>Total Time Screen Share Was Off: <span class="ac-screen-turnoff-duration">0s</span></div>
        `;
            utils.safeAppend(cheatMetrics);

            const omniFocusBox = document.createElement('div');
            omniFocusBox.className = 'ac-omniFocusBox';
            omniFocusBox.innerHTML = `
          <strong style="display:block; margin-bottom:10px; color:#ffd700;">Omni Focus anti cheat</strong>
          <div>• Tab switching Count <span id="ac-tabSwitchCount">0</span> times</div>
          <div>• Open Programs Count <span id="ac-focusLossCount">0</span> times</div>
        `;
            utils.safeAppend(omniFocusBox);

            // State
            const state = {
                focusLossEvents: [],
                tabSwitchEvents: [],
                lastOffenseTime: null,
                lastLackFocusTime: null
            };

            let focusStart = null;
            let tabSwitchStart = null;
            let monitoringActive = false;
            let graceEnded = false;
            let stream = null;

            // screen off metrics
            let screenOffCount = 0;
            let screenOffStart = null;
            let totalScreenOffDuration = 0;
            const updateScreenOffMetrics = () => {
                const spanCount = cheatMetrics.querySelector('.ac-screen-turnoff-count');
                const spanDur = cheatMetrics.querySelector('.ac-screen-turnoff-duration');
                if (spanCount) spanCount.textContent = screenOffCount;
                const duration = screenOffStart ? totalScreenOffDuration + Math.floor((Date.now() - screenOffStart) / 1000) : totalScreenOffDuration;
                if (spanDur) spanDur.textContent = `${duration}s`;
            };
            setInterval(updateScreenOffMetrics, 1000);

            function onScreenShareStopped() {
                screenOffCount++;
                screenOffStart = Date.now();
                updateScreenOffMetrics();
                sendEvent('screen_share', { on: false });
            }
            function onScreenShareResumed() {
                if (screenOffStart) {
                    const duration = Math.floor((Date.now() - screenOffStart) / 1000);
                    totalScreenOffDuration += duration;
                    screenOffStart = null;
                    updateScreenOffMetrics();
                }
                sendEvent('screen_share', { on: true });
            }

            const videoElement = document.createElement('video');
            videoElement.id = 'ac-screenVideo';
            videoElement.autoplay = true;
            videoElement.style.display = 'none';
            document.body.appendChild(videoElement);

            const colorClasses = ['ac-green', 'ac-blue', 'ac-yellow', 'ac-orange', 'ac-red'];

            function updateStatus() {
                const totalEvents = state.focusLossEvents.length + state.tabSwitchEvents.length;
                const totalTime = [...state.focusLossEvents, ...state.tabSwitchEvents].reduce((a, b) => a + b, 0);
                const maxSingle = Math.max(...state.focusLossEvents, ...state.tabSwitchEvents, 0);

                let label = 'Focused Student', color = 'ac-green', emoji = '🟢';
                if (maxSingle >= 39 || totalTime > 78 || totalEvents > 13)
                    [emoji, label, color] = ['🔴', 'Marked as Cheating', 'ac-red'];
                else if (maxSingle >= 26 || totalTime > 39 || totalEvents > 8)
                    [emoji, label, color] = ['🟠', 'Suspicious Activity Detected', 'ac-orange'];
                else if (maxSingle >= 13 || totalEvents > 4)
                    [emoji, label, color] = ['🟡', 'Minor Activity Detected', 'ac-yellow'];
                else if (totalEvents > 0)
                    [emoji, label, color] = ['🔵', 'Lacking Focus', 'ac-blue'];

                studentStatus.textContent = `${emoji} ${label}`;
                colorClasses.forEach(c => studentStatus.classList.remove(c));
                studentStatus.classList.add(color);
            }

            function captureScreenshot() {
                if (!videoElement || videoElement.videoWidth === 0) return;
                const canvas = document.createElement('canvas');
                canvas.width = videoElement.videoWidth;
                canvas.height = videoElement.videoHeight;
                const ctx = canvas.getContext('2d');
                ctx.drawImage(videoElement, 0, 0, canvas.width, canvas.height);
                const img = new Image();
                img.src = canvas.toDataURL('image/png');
                img.style.display = 'none';
                document.body.appendChild(img);
            }

            const getElapsedSeconds = (start) => Math.round((Date.now() - start) / 1000);

            const updateOmniCounts = () => {
                const t = document.getElementById('ac-tabSwitchCount');
                const f = document.getElementById('ac-focusLossCount');
                if (t) t.textContent = state.tabSwitchEvents.length;
                if (f) f.textContent = state.focusLossEvents.length;
            };

            function appendLog(duration, type, time) {
                try {
                    const tr = document.createElement('tr');
                    tr.innerHTML = `<td style="padding:4px 6px; border-bottom:1px solid #555;">${duration}s</td><td style="padding:4px 6px; border-bottom:1px solid #555;">${type}</td><td style="padding:4px 6px; border-bottom:1px solid #555;">${time}</td>`;
                    logsBody.appendChild(tr);
                } catch (e) { /* ignore */ }
            }

            function handleFocusLossEnd() {
                if (!monitoringActive || !graceEnded) return;
                const duration = getElapsedSeconds(focusStart);
                if (duration > 0) {
                    state.focusLossEvents.push(duration);
                    appendLog(duration, 'window focus', new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }).toLowerCase());
                    if (duration >= 10) captureScreenshot();
                    state.lastOffenseTime = Date.now();
                    updateStatus();
                    updateOmniCounts();
                    sendEvent('open_programs', { duration });
                }
                focusStart = null;
            }

            function handleTabSwitchEnd() {
                if (!monitoringActive || !graceEnded) return;
                const duration = getElapsedSeconds(tabSwitchStart);
                if (duration > 0) {
                    state.tabSwitchEvents.push(duration);
                    appendLog(duration, 'tabswitching', new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }).toLowerCase());
                    if (duration >= 10) captureScreenshot();
                    state.lastOffenseTime = Date.now();
                    updateStatus();
                    updateOmniCounts();
                    sendEvent('tab_switch', { duration });
                }
                tabSwitchStart = null;
            }

            window.addEventListener('blur', () => {
                if (monitoringActive && !focusStart) focusStart = Date.now();
            });
            window.addEventListener('focus', () => {
                if (monitoringActive && focusStart) handleFocusLossEnd();
            });

            document.addEventListener('visibilitychange', () => {
                if (!monitoringActive) return;
                if (document.hidden && !tabSwitchStart) {
                    tabSwitchStart = Date.now();
                } else if (!document.hidden && tabSwitchStart) {
                    handleTabSwitchEnd();
                }
            });

            setInterval(() => {
                if (!graceEnded) return;
                if (state.lastOffenseTime && (Date.now() - state.lastOffenseTime > 30000)) {
                    if (state.lastLackFocusTime && (Date.now() - state.lastLackFocusTime > 30000)) {
                        state.focusLossEvents = [];
                        state.tabSwitchEvents = [];
                        updateStatus();
                        updateOmniCounts();
                    }
                }
            }, 1000);

            async function requestScreenShare() {
                try {
                    stream = await navigator.mediaDevices.getDisplayMedia({ video: true });
                    videoElement.srcObject = stream;
                    overlay.style.display = 'none';
                    monitoringActive = true;
                    graceEnded = false;
                    studentStatus.style.opacity = '0';

                    onScreenShareResumed();

                    const track = stream.getVideoTracks()[0];
                    const endedHandler = () => {
                        onScreenShareStopped();
                        overlay.style.display = 'flex';
                        monitoringActive = false;
                        graceEnded = false;
                        studentStatus.style.opacity = '0';
                        videoElement.srcObject = null;
                        stream = null;
                        track.removeEventListener('ended', endedHandler);
                    };
                    track.addEventListener('ended', endedHandler);

                    setTimeout(() => {
                        graceEnded = true;
                        studentStatus.style.opacity = '1';
                    }, 10000);
                } catch (err) {
                    // user denied screen share or error - keep overlay visible
                }
            }

            const shareBtn = document.getElementById('ac-startScreenShare') || overlay.querySelector('#ac-startScreenShare');
            if (shareBtn) shareBtn.addEventListener('click', requestScreenShare);

            return { name: 'focus', overlay, studentStatus, requestScreenShare };
        } catch (err) {
            console.error('AntiCheat.focus module failed to init', err);
            return null;
        }
    }

    /* ------------------ Initialization & safe boot ------------------ */
    NS.modules = {};

    function tryInit(name, initFn) {
        try {
            const res = initFn();
            NS.modules[name] = res;
        } catch (e) {
            console.error(`AntiCheat: failed to initialize ${name}`, e);
            NS.modules[name] = null;
        }
    }

    function initAll() {
        tryInit('mouse', initMouseModule);
        tryInit('interact', initInteractModule);
        tryInit('fullscreen', initFullscreenModule);
        tryInit('focus', initFocusModule);
        // done
        console.info('AntiCheat: modules initialized', Object.keys(NS.modules));
    }

    // Auto-init once DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAll, { once: true });
    } else {
        initAll();
    }

    // Expose a simple API for debugging
    NS.info = () => {
        return {
            loadedAt: new Date().toISOString(),
            modules: Object.keys(NS.modules).reduce((acc, k) => (acc[k] = !!NS.modules[k], acc), {})
        };
    };

})(window);
