// Make script resilient when used on pages without login form
document.addEventListener('DOMContentLoaded', () => {
    // Simple slideshow (only run if slides exist)
    const slides = document.querySelectorAll('.slide');
    let index = 0;
    if (slides && slides.length > 0) {
        // Ensure first slide is active
        slides[0].classList.add('active');
        setInterval(() => {
            if (slides.length === 0) return;
            slides[index]?.classList.remove('active');
            index = (index + 1) % slides.length;
            slides[index]?.classList.add('active');
        }, 4000);
    }

    // Login form handler (only wire if form exists)
    const form = document.getElementById('loginForm');
    if (!form) return;

    form.addEventListener('submit', function (e) {
        e.preventDefault();

        const formData = new URLSearchParams(new FormData(this));

        const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenEl ? tokenEl.value : '';

        fetch('/Account/Login', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': token
            },
            body: formData
        })
            .then(res => res.json())
            .then(data => {
                const msg = document.getElementById('loginMessage');
                if (msg) {
                    msg.textContent = data.message;
                    msg.style.color = data.success ? 'green' : 'red';
                }

                if (data.success) {
                    setTimeout(() => {
                        window.location.href = '/Home/Index'; // redirect after success
                    }, 1000);
                }
            })
            .catch(() => {
                const msg = document.getElementById('loginMessage');
                if (msg) {
                    msg.textContent = 'Error logging in. Please try again later.';
                    msg.style.color = 'red';
                }
            });
    });
});