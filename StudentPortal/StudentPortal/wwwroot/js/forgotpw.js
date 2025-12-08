const step1 = document.getElementById('step-1');
const step2 = document.getElementById('step-2');
const step3 = document.getElementById('step-3');
const userEmail = document.getElementById('user-email');

document.getElementById('to-step-2').addEventListener('click', async () => {
    const email = document.getElementById('email').value.trim();
    if (!email) return alert("Please enter your email.");

    const response = await fetch('/Account/SendVerificationCode', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email })
    });

    if (response.ok) {
        userEmail.textContent = email;
        step1.classList.add('hidden');
        step2.classList.remove('hidden');
    } else {
        alert(await response.text());
    }
});

document.getElementById('to-step-3').addEventListener('click', async () => {
    const code = document.getElementById('verification-code').value.trim();
    if (!code) return alert("Please enter the code.");

    const response = await fetch('/Account/VerifyCode', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ verificationCode: code })
    });

    if (response.ok) {
        step2.classList.add('hidden');
        step3.classList.remove('hidden');
    } else {
        alert(await response.text());
    }
});

document.querySelector('form').addEventListener('submit', async (e) => {
    e.preventDefault();
    const newPassword = document.getElementById('new-password').value.trim();
    const confirm = document.getElementById('confirm-password').value.trim();

    if (newPassword !== confirm) return alert("Passwords do not match.");

    const response = await fetch('/Account/ResetPassword', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ newPassword })
    });

    if (response.ok) {
        alert("Password successfully reset!");
        window.location.href = '/Account/Login';
    } else {
        alert(await response.text());
    }
});

function togglePassword(id) {
    const input = document.getElementById(id);
    input.type = input.type === 'password' ? 'text' : 'password';
}