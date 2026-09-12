// admin-roles.js
// Handles the new "Role Assignment" tab: promote lecturers to Dean,
// revoke Dean status, and create/list Academic Registrar accounts.
//
// Kept deliberately separate from admin.js (which we don't have direct
// access to) to avoid colliding with existing function/variable names.
// Reuses the same API_URL / adminToken convention as the rest of the
// admin dashboard.

const ROLES_API_URL = 'https://localhost:44366/api';

function getAdminToken() {
    return localStorage.getItem('adminToken') || localStorage.getItem('token');
}

function authHeaders() {
    return {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${getAdminToken()}`
    };
}

// Reload the tab's data every time its sidebar link is clicked, in addition
// to once on page load (harmless if the tab is never opened — just a few
// idle fetches otherwise skipped).
document.addEventListener('DOMContentLoaded', () => {
    const rolesLink = document.querySelector('.menu-link[data-section="roles"]');
    if (rolesLink) {
        rolesLink.addEventListener('click', loadRoleAssignmentData);
    }
});

function loadRoleAssignmentData() {
    loadLecturersForPromotion();
    loadFacultiesForPromotion();
    loadDeans();
    loadAcademicRegistrars();
}

// =========================================================
// DEAN PROMOTION
// =========================================================

async function loadLecturersForPromotion() {
    const select = document.getElementById('promoteLecturerSelect');
    try {
        const res = await fetch(`${ROLES_API_URL}/admin/lecturers`, { headers: authHeaders() });
        const data = await res.json();

        if (!data.success) {
            select.innerHTML = '<option value="">Error loading lecturers</option>';
            return;
        }

        const activeLecturers = data.data.filter(l => l.status === 'Active');

        select.innerHTML = '<option value="">-- Select a lecturer --</option>' +
            activeLecturers.map(l =>
                `<option value="${l.lecturerId}">${l.firstName} ${l.lastName} (${l.facultyName || 'No Faculty'})</option>`
            ).join('');
    } catch (err) {
        select.innerHTML = '<option value="">Error loading lecturers</option>';
        console.error('loadLecturersForPromotion error:', err);
    }
}

async function loadFacultiesForPromotion() {
    const select = document.getElementById('promoteFacultySelect');
    try {
        const res = await fetch(`${ROLES_API_URL}/admin/faculties`, { headers: authHeaders() });
        const data = await res.json();

        if (!data.success) return;

        select.innerHTML = '<option value="">Use lecturer\'s existing faculty</option>' +
            data.data.map(f => `<option value="${f.facultyId}">${f.facultyName}</option>`).join('');
    } catch (err) {
        console.error('loadFacultiesForPromotion error:', err);
    }
}

async function promoteToDean() {
    const lecturerId = document.getElementById('promoteLecturerSelect').value;
    const facultyId = document.getElementById('promoteFacultySelect').value;
    const msgDiv = document.getElementById('promoteDeanMsg');

    if (!lecturerId) {
        msgDiv.style.color = '#dc3545';
        msgDiv.textContent = 'Please select a lecturer first.';
        return;
    }

    try {
        const res = await fetch(`${ROLES_API_URL}/admin/promote-to-dean/${lecturerId}`, {
            method: 'PUT',
            headers: authHeaders(),
            body: JSON.stringify({ facultyId: facultyId ? parseInt(facultyId) : null })
        });

        const data = await res.json();

        msgDiv.style.color = data.success ? '#28a745' : '#dc3545';
        msgDiv.textContent = data.message;

        if (data.success) {
            loadDeans();
            loadLecturersForPromotion();
        }
    } catch (err) {
        msgDiv.style.color = '#dc3545';
        msgDiv.textContent = 'Error promoting lecturer: ' + err.message;
        console.error('promoteToDean error:', err);
    }
}

async function revokeDean(lecturerId) {
    if (!confirm('Revoke Dean status for this lecturer?')) return;

    try {
        const res = await fetch(`${ROLES_API_URL}/admin/revoke-dean/${lecturerId}`, {
            method: 'PUT',
            headers: authHeaders()
        });

        const data = await res.json();
        alert(data.message);

        if (data.success) {
            loadDeans();
            loadLecturersForPromotion();
        }
    } catch (err) {
        alert('Error revoking Dean status: ' + err.message);
        console.error('revokeDean error:', err);
    }
}

async function loadDeans() {
    const container = document.getElementById('deansTable');
    try {
        const res = await fetch(`${ROLES_API_URL}/admin/deans`, { headers: authHeaders() });
        const data = await res.json();

        if (!data.success || data.data.length === 0) {
            container.innerHTML = '<p style="color:#666; padding:15px;">No Deans assigned yet.</p>';
            return;
        }

        container.innerHTML = `
            <div class="table-wrapper">
                <table class="applications-table">
                    <thead>
                        <tr>
                            <th>Name</th>
                            <th>Email</th>
                            <th>Faculty</th>
                            <th>Assigned Date</th>
                            <th>Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        ${data.data.map(d => `
                            <tr>
                                <td>${d.firstName} ${d.lastName}</td>
                                <td>${d.email}</td>
                                <td>${d.facultyName || 'N/A'}</td>
                                <td>${d.deanAssignedDate ? new Date(d.deanAssignedDate).toLocaleDateString() : 'N/A'}</td>
                                <td>
                                    <button class="btn-reject" onclick="revokeDean(${d.lecturerId})">Revoke</button>
                                </td>
                            </tr>
                        `).join('')}
                    </tbody>
                </table>
            </div>
        `;
    } catch (err) {
        container.innerHTML = '<p style="color:#dc3545;">Error loading deans.</p>';
        console.error('loadDeans error:', err);
    }
}

// =========================================================
// ACADEMIC REGISTRAR ACCOUNTS
// =========================================================

async function createAcademicRegistrar() {
    const firstName = document.getElementById('regFirstName').value.trim();
    const lastName = document.getElementById('regLastName').value.trim();
    const email = document.getElementById('regEmail').value.trim();
    const password = document.getElementById('regPassword').value;
    const phoneNumber = document.getElementById('regPhone').value.trim();
    const msgDiv = document.getElementById('createRegistrarMsg');

    if (!email || !password) {
        msgDiv.style.color = '#dc3545';
        msgDiv.textContent = 'Email and password are required.';
        return;
    }

    try {
        const res = await fetch(`${ROLES_API_URL}/admin/create-academic-registrar`, {
            method: 'POST',
            headers: authHeaders(),
            body: JSON.stringify({ firstName, lastName, email, password, phoneNumber })
        });

        const data = await res.json();

        msgDiv.style.color = data.success ? '#28a745' : '#dc3545';
        msgDiv.textContent = data.message;

        if (data.success) {
            document.getElementById('regFirstName').value = '';
            document.getElementById('regLastName').value = '';
            document.getElementById('regEmail').value = '';
            document.getElementById('regPassword').value = '';
            document.getElementById('regPhone').value = '';
            loadAcademicRegistrars();
        }
    } catch (err) {
        msgDiv.style.color = '#dc3545';
        msgDiv.textContent = 'Error creating account: ' + err.message;
        console.error('createAcademicRegistrar error:', err);
    }
}

async function loadAcademicRegistrars() {
    const container = document.getElementById('registrarsTable');
    try {
        const res = await fetch(`${ROLES_API_URL}/admin/academic-registrars`, { headers: authHeaders() });
        const data = await res.json();

        if (!data.success || data.data.length === 0) {
            container.innerHTML = '<p style="color:#666; padding:15px;">No Academic Registrar accounts yet.</p>';
            return;
        }

        container.innerHTML = `
            <div class="table-wrapper">
                <table class="applications-table">
                    <thead>
                        <tr>
                            <th>Name</th>
                            <th>Email</th>
                            <th>Phone</th>
                            <th>Status</th>
                        </tr>
                    </thead>
                    <tbody>
                        ${data.data.map(r => `
                            <tr>
                                <td>${r.firstName} ${r.lastName}</td>
                                <td>${r.email}</td>
                                <td>${r.phoneNumber || 'N/A'}</td>
                                <td><span class="status-badge ${r.status === 'Active' ? 'status-approved' : 'status-rejected'}">${r.status}</span></td>
                            </tr>
                        `).join('')}
                    </tbody>
                </table>
            </div>
        `;
    } catch (err) {
        container.innerHTML = '<p style="color:#dc3545;">Error loading academic registrars.</p>';
        console.error('loadAcademicRegistrars error:', err);
    }
}