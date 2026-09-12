// dean.js
// Dean dashboard: faculty-scoped pending marks approval (first stage,
// before Academic Registrar sign-off). Dean logs in through the same
// Lecturer login as everyone else — see login.html — so this reads the
// same "lecturer" / "lecturerToken" localStorage keys.

const API_URL = 'https://localhost:44366/api';

const deanToken = localStorage.getItem('lecturerToken') || localStorage.getItem('token');
const lecturerUserRaw = localStorage.getItem('lecturer');

let lecturerUser = {};
try {
    lecturerUser = (lecturerUserRaw && lecturerUserRaw !== "undefined")
        ? JSON.parse(lecturerUserRaw)
        : {};
} catch (e) {
    console.error("Invalid lecturer user in localStorage", e);
}

window.addEventListener('load', function () {
    if (!deanToken) {
        window.location.href = 'login.html';
        return;
    }

    // Guard: someone navigating here directly without Dean rights gets
    // bounced to the regular lecturer dashboard instead of a broken page.
    if (lecturerUser.isDean !== true) {
        window.location.href = 'lecturer-dashboard.html';
        return;
    }

    document.getElementById('deanName').textContent =
        'Welcome, ' + (lecturerUser.firstName || 'Dean');
    document.getElementById('deanFacultyLabel').textContent =
        'Dean — ' + (lecturerUser.facultyName || 'No Faculty Assigned');
    document.getElementById('sidebarFacultyName').textContent =
        lecturerUser.facultyName || 'No Faculty Assigned';

    if (!lecturerUser.facultyId) {
        document.getElementById('noFacultyBanner').style.display = 'block';
    }

    navigateToSection('dashboard');
});

function authHeaders() {
    return {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer ' + deanToken
    };
}

/* =========================================================
   SIDEBAR (mobile drawer)
   ========================================================= */
function toggleSidebar() {
    document.querySelector('.sidebar').classList.toggle('open');
    document.querySelector('.sidebar-overlay').classList.toggle('show');
}

function closeSidebar() {
    document.querySelector('.sidebar').classList.remove('open');
    document.querySelector('.sidebar-overlay').classList.remove('show');
}

/* =========================================================
   NAVIGATION
   ========================================================= */
function navigateToSection(section) {
    closeSidebar();

    document.querySelectorAll('.section').forEach(sec => sec.classList.remove('active'));
    document.querySelectorAll('.menu-link').forEach(link => link.classList.remove('active'));

    const target = document.getElementById(section + '-section');
    if (target) target.classList.add('active');

    document.querySelectorAll('.menu-link').forEach(link => {
        if (link.dataset.section === section) link.classList.add('active');
    });

    switch (section) {
        case 'dashboard':
            loadDashboardStats();
            break;
        case 'pending':
            loadPendingResults();
            break;

        case 'courses':               
            loadMyCourses();
            loadMyGrades();
            break;
    }
}

/* =========================================================
   DASHBOARD STATS
   ========================================================= */
async function loadDashboardStats() {
    try {
        const res = await fetch(`${API_URL}/dean/dashboard/stats`, { headers: authHeaders() });
        const data = await res.json();

        if (!data.success) throw new Error(data.message);

        const s = data.data;
        document.getElementById('statPending').textContent = s.pendingCount;
        document.getElementById('statDeanApproved').textContent = s.deanApprovedCount;
        document.getElementById('statSignedOff').textContent = s.signedOffCount;
        document.getElementById('statRejected').textContent = s.rejectedCount;
    } catch (err) {
        console.error('Dean dashboard stats error:', err);
        showMessage('Error loading dashboard stats: ' + err.message, 'error');
    }
}

/* =========================================================
   PENDING MARKS
   ========================================================= */
async function loadPendingResults() {
    const container = document.getElementById('pendingContainer');
    container.innerHTML = 'Loading pending marks...';

    try {
        const res = await fetch(`${API_URL}/dean/results/pending`, { headers: authHeaders() });
        const data = await res.json();

        if (!data.success) throw new Error(data.message);

        renderPendingTable(data.data || []);
    } catch (err) {
        console.error('Load pending results error:', err);
        container.innerHTML = `<p style="color:red;">Error loading pending marks: ${err.message}</p>`;
    }
}

function renderPendingTable(results) {
    const container = document.getElementById('pendingContainer');

    if (!results || results.length === 0) {
        container.innerHTML = '<p style="color:#666; padding:20px 0;">No pending marks awaiting your approval.</p>';
        return;
    }

    let html = `
        <div class="table-wrapper">
            <table class="data-table">
                <thead>
                    <tr>
                        <th>Reg Number</th>
                        <th>Student Name</th>
                        <th>Course</th>
                        <th>Mark</th>
                        <th>Grade</th>
                        <th>Uploaded</th>
                        <th style="text-align:center;">Action</th>
                    </tr>
                </thead>
                <tbody>
    `;

    results.forEach(r => {
        const dateStr = r.dateUploaded ? new Date(r.dateUploaded).toLocaleDateString() : 'N/A';

        html += `
            <tr>
                <td>${r.regNumber}</td>
                <td>${r.studentName}</td>
                <td><strong>${r.courseCode}</strong><br><small>${r.courseName || ''}</small></td>
                <td>${r.mark ?? 'N/A'}</td>
                <td>${r.grade ?? 'N/A'}</td>
                <td><span class="status-badge status-pending">${dateStr}</span></td>
                <td style="text-align:center;">
                    <button class="btn-approve" onclick="reviewResult(${r.resultId}, true)">Approve</button>
                    <button class="btn-reject" onclick="reviewResult(${r.resultId}, false)">Reject</button>
                </td>
            </tr>
        `;
    });

    html += `</tbody></table></div>`;
    container.innerHTML = html;
}

async function reviewResult(resultId, isApproved) {
    const label = isApproved ? 'approve' : 'reject';
    if (!confirm(`Are you sure you want to ${label} this result?`)) return;

    try {
        const res = await fetch(`${API_URL}/dean/results/approve`, {
            method: 'POST',
            headers: authHeaders(),
            body: JSON.stringify({ resultId, isApproved })
        });

        const data = await res.json();

        if (data.success) {
            showMessage(data.message, 'success');
            loadPendingResults();
            loadDashboardStats();
        } else {
            showMessage('Error: ' + (data.message || 'Failed to process result'), 'error');
        }
    } catch (err) {
        console.error('Review result error:', err);
        showMessage('Error: ' + err.message, 'error');
    }
}


const LECTURER_API = 'https://localhost:44366/api/lecturer';

async function loadMyCourses() {
    const container = document.getElementById('myCoursesContainer');
    container.innerHTML = 'Loading your courses...';

    try {
        const res = await fetch(`${LECTURER_API}/courses`, { headers: authHeaders() });
        const data = await res.json();

        if (!data.success) throw new Error(data.message);

        // Only show courses PERSONALLY assigned to this Dean — the
        // upload endpoint only accepts results for these, not every
        // course in their faculty (GetCourses returns a broader union
        // of assigned + faculty-derived courses for browsing purposes,
        // but UploadGrades checks LecturerCourses specifically).
        const myCourses = (data.data || []).filter(c => c.isAssignedToMe);
        renderMyCoursesTable(myCourses);
    } catch (err) {
        console.error('Load my courses error:', err);
        container.innerHTML = `<p style="color:red;">Error loading your courses: ${err.message}</p>`;
    }
}

function renderMyCoursesTable(courses) {
    const container = document.getElementById('myCoursesContainer');

    if (!courses || courses.length === 0) {
        container.innerHTML = '<p style="color:#666; padding:20px 0;">No courses are personally assigned to you yet. Contact Admin if this seems wrong.</p>';
        return;
    }

    let html = `
        <div class="table-wrapper">
            <table class="data-table">
                <thead>
                    <tr>
                        <th>Code</th>
                        <th>Course Name</th>
                        <th>Year</th>
                        <th>Semester</th>
                        <th>Credit Hrs</th>
                        <th>Students</th>
                    </tr>
                </thead>
                <tbody>
    `;

    courses.forEach(c => {
        html += `
            <tr>
                <td><strong>${c.courseCode}</strong></td>
                <td>${c.courseName}</td>
                <td>${c.year}</td>
                <td>${c.semester}</td>
                <td>${c.creditHours}</td>
                <td>${c.studentCount}</td>
            </tr>
        `;
    });

    html += `</tbody></table></div>`;
    container.innerHTML = html;
}

async function uploadMyResults() {
    const fileInput = document.getElementById('resultsFile');
    const btn = document.getElementById('resultsUploadBtn');
    const feedback = document.getElementById('uploadResultsFeedback');

    if (!fileInput.files.length) {
        showMessage('❌ Please choose a CSV or XLSX file to upload', 'error');
        return;
    }

    const formData = new FormData();
    formData.append('file', fileInput.files[0]);

    btn.disabled = true;
    btn.innerHTML = '<i class="bi bi-cloud-upload"></i> Uploading...';
    feedback.innerHTML = '';

    try {
        const res = await fetch(`${LECTURER_API}/grades/upload`, {
            method: 'POST',
            headers: { 'Authorization': 'Bearer ' + deanToken }, // no Content-Type — browser sets multipart boundary
            body: formData
        });
        const data = await res.json();

        if (data.success) {
            showMessage(`✅ ${data.message}`, 'success');
            fileInput.value = '';

            if (data.errorCount > 0 && data.errors?.length) {
                feedback.innerHTML = `
                    <div style="margin-top:15px; padding:12px 15px; background:#fff3cd; border-left:4px solid #f39c12; border-radius:6px;">
                        <strong>${data.errorCount} row(s) had issues:</strong>
                        <ul style="margin:8px 0 0 20px; font-size:13px;">
                            ${data.errors.map(e => `<li>${e}</li>`).join('')}
                        </ul>
                    </div>
                `;
            }

            loadMyGrades();
        } else {
            showMessage('❌ ' + (data.message || 'Upload failed'), 'error');
        }
    } catch (err) {
        console.error('Upload results error:', err);
        showMessage('❌ Error: ' + err.message, 'error');
    } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="bi bi-cloud-upload"></i> Upload Results';
    }
}

async function loadMyGrades() {
    const container = document.getElementById('myGradesContainer');
    container.innerHTML = 'Loading your uploaded results...';

    try {
        const res = await fetch(`${LECTURER_API}/grades`, { headers: authHeaders() });
        const data = await res.json();

        if (!data.success) throw new Error(data.message);

        renderMyGradesTable(data.data || []);
    } catch (err) {
        console.error('Load my grades error:', err);
        container.innerHTML = `<p style="color:red;">Error loading your results: ${err.message}</p>`;
    }
}

function renderMyGradesTable(grades) {
    const container = document.getElementById('myGradesContainer');

    if (!grades || grades.length === 0) {
        container.innerHTML = '<p style="color:#666; padding:20px 0;">You haven\'t uploaded any results yet.</p>';
        return;
    }

    const statusBadge = (status) => {
        const map = {
            'Pending': 'status-pending',
            'DeanApproved': 'status-pending',
            'RegistrarSignedOff': 'status-pending',
            'Rejected': 'status-pending'
        };
        // Reuse the existing status-pending badge style for all statuses
        // here — this dashboard doesn't define status-approved/rejected
        // variants, and adding new ones is a styling decision best left
        // to whoever owns the visual design, not bundled into this fix.
        return `<span class="status-badge ${map[status] || 'status-pending'}">${status}</span>`;
    };

    let html = `
        <div class="table-wrapper">
            <table class="data-table">
                <thead>
                    <tr>
                        <th>Reg Number</th>
                        <th>Student</th>
                        <th>Course</th>
                        <th>Mark</th>
                        <th>Grade</th>
                        <th>Status</th>
                        <th>Uploaded</th>
                    </tr>
                </thead>
                <tbody>
    `;

    grades.forEach(g => {
        const dateStr = g.dateUploaded ? new Date(g.dateUploaded).toLocaleDateString() : 'N/A';
        html += `
            <tr>
                <td>${g.regNumber}</td>
                <td>${g.name}</td>
                <td><strong>${g.courseCode}</strong><br><small>${g.courseName || ''}</small></td>
                <td>${g.mark ?? 'N/A'}</td>
                <td>${g.grade ?? 'N/A'}</td>
                <td>${statusBadge(g.status)}</td>
                <td>${dateStr}</td>
            </tr>
        `;
    });

    html += `</tbody></table></div>`;
    container.innerHTML = html;
}

// =====================================================
// TIMETABLE MANAGEMENT (Dean — own faculty only)
// =====================================================
const TT_API = 'https://localhost:44366/api/timetable';

function getDeanToken() {
    return localStorage.getItem('lecturerToken') || localStorage.getItem('token');
}

async function loadTimetableSemesters() {
    try {
        const res = await fetch(`${TT_API}/semesters`, {
            headers: { 'Authorization': `Bearer ${getDeanToken()}` }
        });
        const data = await res.json();
        if (!data.success) return;

        const select = document.getElementById('ttSemester');
        select.innerHTML = data.data.map(s =>
            `<option value="${s.semesterId}">${s.academicYear} - Semester ${s.semester}${s.isActive ? ' (Active)' : ''}</option>`
        ).join('');
    } catch (err) {
        console.error('Failed to load semesters', err);
    }
}

async function uploadTimetable() {
    const semesterId = document.getElementById('ttSemester').value;
    const fileInput = document.getElementById('ttFile');
    const btn = document.getElementById('ttUploadBtn');

    const dean = JSON.parse(localStorage.getItem('lecturer') || '{}');
    const facultyId = dean.facultyId;

    if (!facultyId) {
        showMessage('❌ No faculty assigned to your account. Contact Admin.', 'error');
        return;
    }

    if (!fileInput.files.length) {
        showMessage('❌ Please choose a PDF file to upload', 'error');
        return;
    }

    const formData = new FormData();
    formData.append('file', fileInput.files[0]);
    formData.append('facultyId', facultyId);
    formData.append('semesterId', semesterId);

    btn.disabled = true;
    btn.textContent = 'Uploading...';

    try {
        const res = await fetch(`${TT_API}/upload`, {
            method: 'POST',
            headers: { 'Authorization': `Bearer ${getDeanToken()}` },
            body: formData
        });
        const data = await res.json();

        if (data.success) {
            showMessage('✅ Timetable uploaded successfully', 'success');
            fileInput.value = '';
            loadTimetableHistory();
        } else {
            showMessage('❌ ' + (data.message || 'Upload failed'), 'error');
        }
    } catch (err) {
        showMessage('❌ Error: ' + err.message, 'error');
    } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="bi bi-cloud-upload"></i> Upload Timetable';
    }
}

async function loadTimetableHistory() {
    const dean = JSON.parse(localStorage.getItem('lecturer') || '{}');
    const facultyId = dean.facultyId;
    const container = document.getElementById('timetableHistoryContainer');

    if (!facultyId) {
        container.innerHTML = '<p style="color:#666;">No faculty assigned to your account.</p>';
        return;
    }

    try {
        const res = await fetch(`${TT_API}/history/${facultyId}`, {
            headers: { 'Authorization': `Bearer ${getDeanToken()}` }
        });
        const data = await res.json();

        if (!data.success || data.data.length === 0) {
            container.innerHTML = '<p style="color:#666;">No timetable has been uploaded for your faculty yet.</p>';
            return;
        }

        const rows = data.data.map(t => `
            <tr>
                <td>${t.fileName}</td>
                <td>${(t.fileSizeBytes / 1024).toFixed(0)} KB</td>
                <td>${t.uploadedByRole} — ${t.uploadedByName || 'N/A'}</td>
                <td>${new Date(t.uploadedAt).toLocaleString()}</td>
                <td>${t.isActive ? '<span class="status-badge status-approved">Active</span>' : '<span class="status-badge status-pending">Archived</span>'}</td>
                <td><a class="btn-view" href="${TT_API.replace('/api/timetable', '')}${t.downloadUrl}?token=${getDeanToken()}" target="_blank">View/Download</a></td>
            </tr>
        `).join('');

        container.innerHTML = `
            <div class="table-wrapper">
                <table class="data-table">
                    <thead>
                        <tr><th>File</th><th>Size</th><th>Uploaded By</th><th>Uploaded At</th><th>Status</th><th>Action</th></tr>
                    </thead>
                    <tbody>${rows}</tbody>
                </table>
            </div>`;
    } catch (err) {
        container.innerHTML = '<p style="color:#c0392b;">Failed to load history.</p>';
    }
}

document.addEventListener('DOMContentLoaded', () => {
    loadTimetableSemesters();
    loadTimetableHistory();
});


/* =========================================================
   SHARED HELPERS
   ========================================================= */
function showMessage(msg, type) {
    const messageDiv = document.getElementById('message');
    messageDiv.textContent = msg;
    messageDiv.className = `message show ${type}`;

    setTimeout(() => messageDiv.classList.remove('show'), 5000);
}

function logout() {
    localStorage.removeItem('lecturerToken');
    localStorage.removeItem('lecturer');
    localStorage.removeItem('currentRole');
    localStorage.removeItem('token');
    window.location.href = 'login.html';
}