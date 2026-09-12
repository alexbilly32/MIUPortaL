
const API_URL = 'https://localhost:44366/api';

document.addEventListener('DOMContentLoaded', function () {
    const loginBtn = document.getElementById('loginBtn');
    if (loginBtn) {
        loginBtn.addEventListener('click', handleLogin);
    }
});

async function handleLogin() {
    const emailEl = document.getElementById('email');
    const passwordEl = document.getElementById('password');
    const loginBtn = document.getElementById('loginBtn');

    if (!emailEl || !passwordEl) return;

    const email = emailEl.value.trim();
    const password = passwordEl.value.trim();

    if (!email || !password) {
        showMessage('⚠️ Please enter both email and password', 'error');
        return;
    }

    
    if (loginBtn) {
        loginBtn.disabled = true;
        loginBtn.textContent = 'Logging in...';
    }

    try {
        const response = await fetch(`${API_URL}/auth/student-login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password })
        });

        const data = await response.json();

        if (data.success) {
            
            localStorage.setItem('token', data.token);
            localStorage.setItem('student', JSON.stringify(data.user));

            showMessage('✅ Login successful! Redirecting...', 'success');

            setTimeout(() => {
                window.location.href = 'student-dashboard.html'; 
            }, 1500);
        } else {
            showMessage('❌ ' + (data.message || 'Login failed'), 'error');
            resetLoginButton(loginBtn);
        }
    } catch (error) {
        console.error('Login error:', error);
        showMessage('❌ Connection failed. Ensure the backend API is running.', 'error');
        resetLoginButton(loginBtn);
    }
}

function resetLoginButton(btn) {
    if (btn) {
        btn.disabled = false;
        btn.textContent = 'Login';
    }
}

function showMessage(message, type) {
    const messageDiv = document.getElementById('message');
    if (!messageDiv) return;

    messageDiv.textContent = message;
    messageDiv.className = `message show ${type}`;

    
    if (window.messageTimeout) {
        clearTimeout(window.messageTimeout);
    }

    window.messageTimeout = setTimeout(() => {
        messageDiv.classList.remove('show');
    }, 5000);
} const API_URL = 'https://localhost:7001/api';

const API_URL = 'https://localhost:44366/api';

document.addEventListener('DOMContentLoaded', function () {
    const loginBtn = document.getElementById('loginBtn');
    if (loginBtn) {
        loginBtn.addEventListener('click', handleLogin);
    }
});

async function handleLogin() {
    const email = document.getElementById('email').value.trim();
    const password = document.getElementById('password').value.trim();
    const messageDiv = document.getElementById('message');

    if (!email || !password) {
        showMessage('⚠️ Please enter both email and password', 'error');
        return;
    }

    try {
        const response = await fetch(`${API_URL}/auth/student-login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password })
        });

        const data = await response.json();

        if (data.success) {
            
            localStorage.setItem('token', data.token);
            localStorage.setItem('user', JSON.stringify(data.user));

            showMessage('✅ Login successful! Redirecting...', 'success');

            setTimeout(() => {
                window.location.href = '/student-dashboard.html';
            }, 1500);
        } else {
            showMessage('❌ ' + (data.message || 'Login failed'), 'error');
        }
    } catch (error) {
        showMessage('❌ Error: ' + error.message, 'error');
    }
}

function showMessage(message, type) {
    const messageDiv = document.getElementById('message');
    messageDiv.textContent = message;
    messageDiv.className = `message show ${type}`;
    setTimeout(() => {
        messageDiv.classList.remove('show');
    }, 5000);
}