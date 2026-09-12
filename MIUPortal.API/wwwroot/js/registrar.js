

const API_URL = 'https://localhost:44366/api';

const registrarToken = localStorage.getItem('registrarToken') || localStorage.getItem('token');
const registrarUserRaw = localStorage.getItem('registrar');

let registrarUser = {};
try {
    registrarUser = (registrarUserRaw && registrarUserRaw !== "undefined")
        ? JSON.parse(registrarUserRaw)
        : {};
} catch (e) {
    console.error("Invalid registrar user in localStorage", e);
}

let currentApplications = [];
let currentApplicationId = null;

window.addEventListener('load', function () {
    if (!registrarToken) {
        window.location.href = 'login.html';
        return;
    }

    document.getElementById('registrarName').textContent =
        'Welcome, ' + (registrarUser.firstName || 'Registrar');

    navigateToSection('dashboard');
});

function authHeaders() {
    return {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer ' + registrarToken
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
        case 'applications':
            loadApplications('PENDING_REVIEW');
            break;
        case 'signoff':
            loadSignoffQueue();
            break;
        case 'semesters':
            loadSemesterHistory();
            break;
        case 'loans':
            loadLoanApplications('Pending');
            break;
    }
}

/* =========================================================
   DASHBOARD STATS
   ========================================================= */
async function loadDashboardStats() {
    try {
        const res = await fetch(`${API_URL}/academic-registrar/dashboard/stats`, { headers: authHeaders() });
        const data = await res.json();

        if (!data.success) throw new Error(data.message);

        const s = data.data;
        document.getElementById('statPendingReview').textContent = s.pendingReview;
        document.getElementById('statApprovedTotal').textContent = s.approvedTotal;
        document.getElementById('statRejectedTotal').textContent = s.rejectedTotal;
        document.getElementById('statPendingSignoff').textContent = s.pendingSignoff;
        document.getElementById('statSignedOffTotal').textContent = s.signedOffTotal;
    } catch (err) {
        console.error('Dashboard stats error:', err);
        showMessage('Error loading dashboard stats: ' + err.message, 'error');
    }
}

/* =========================================================
   APPLICATIONS
   ========================================================= */
async function loadApplications(status) {
    // Highlight the matching filter button
    document.querySelectorAll('#applications-section .filter-btn').forEach(btn => btn.classList.remove('active'));
    const clickedBtn = Array.from(document.querySelectorAll('#applications-section .filter-btn'))
        .find(btn => btn.textContent.toUpperCase().replace(/\s/g, '_').includes(status === 'ALL' ? 'ALL' : status));
    if (clickedBtn) clickedBtn.classList.add('active');

    const container = document.getElementById('applicationsContainer');
    container.innerHTML = 'Loading applications...';

    try {
        const url = status === 'ALL'
            ? `${API_URL}/academic-registrar/applications`
            : `${API_URL}/academic-registrar/applications?status=${status}`;

        const res = await fetch(url, { headers: authHeaders() });
        const data = await res.json();

        if (!data.success) throw new Error(data.message);

        currentApplications = data.data || [];
        renderApplicationsTable(currentApplications);
    } catch (err) {
        console.error('Load applications error:', err);
        container.innerHTML = `<p style="color:red;">Error loading applications: ${err.message}</p>`;
    }
}

function renderApplicationsTable(apps) {
    const container = document.getElementById('applicationsContainer');

    if (!apps || apps.length === 0) {
        container.innerHTML = '<p style="color:#666; padding:20px 0;">No applications found for this filter.</p>';
        return;
    }

    let html = `
        <div class="table-wrapper">
            <table class="data-table">
                <thead>
                    <tr>
                        <th>App #</th>
                        <th>Name</th>
                        <th>Email</th>
                        <th>Programme</th>
                        <th>Status</th>
                        <th>Payment</th>
                        <th style="text-align:center;">Action</th>
                    </tr>
                </thead>
                <tbody>
    `;

    apps.forEach(app => {
        const statusClass = app.applicationStatus === 'APPROVED' ? 'status-approved'
            : app.applicationStatus === 'REJECTED' ? 'status-rejected'
                : 'status-pending';

        const payClass = app.paymentStatus === 'VERIFIED' ? 'status-approved'
            : app.paymentStatus === 'REJECTED' ? 'status-rejected'
                : 'status-pending';

        html += `
            <tr>
                <td>${app.applicationNumber}</td>
                <td>${app.firstName} ${app.lastName}</td>
                <td>${app.email}</td>
                <td>${app.programmeName || 'N/A'}</td>
                <td><span class="status-badge ${statusClass}">${app.applicationStatus}</span></td>
                <td><span class="status-badge ${payClass}">${app.paymentStatus}</span></td>
                <td style="text-align:center;">
                    <button class="btn-view" onclick="viewApplication(${app.applicationId})">View</button>
                </td>
            </tr>
        `;
    });

    html += `</tbody></table></div>`;
    container.innerHTML = html;
}

async function viewApplication(id) {
    currentApplicationId = id;

    try {
        const res = await fetch(`${API_URL}/academic-registrar/application/${id}`, { headers: authHeaders() });
        const data = await res.json();

        if (!data.success) throw new Error(data.message);

        const app = data.data;
        renderApplicationModal(app);
    } catch (err) {
        console.error('View application error:', err);
        showMessage('Error loading application: ' + err.message, 'error');
    }
}

function renderApplicationModal(app) {
    let actionButtons = '';

    if (app.applicationStatus === 'APPROVED') {
        actionButtons = `
            <p style="color:#27ae60; font-weight:bold; font-size:16px; margin-top:15px;">
                ✓ Approved — Reg Number: ${app.registrationNumber || 'N/A'}
            </p>
            ${app.admissionLetterPath ? `<a href="${app.admissionLetterPath}" target="_blank" style="display:inline-block; margin-top:10px; padding:10px 18px; background:#0B5345; color:white; text-decoration:none; border-radius:5px; font-weight:bold;">View Admission Letter</a>` : ''}
        `;
    } else if (app.applicationStatus === 'REJECTED') {
        actionButtons = `
            <div style="background:#fdf2f2; border-left:4px solid #e74c3c; padding:15px; border-radius:5px; margin-top:15px;">
                <p style="color:#c0392b; font-weight:bold; margin:0 0 5px 0;">✗ Rejected</p>
                <p style="margin:0; color:#555;"><strong>Reason:</strong> ${app.adminNotes || 'No details provided.'}</p>
            </div>
        `;
    } else if (app.paymentStatus !== 'VERIFIED') {
        actionButtons = `
            <div style="background:#fff3cd; border-left:4px solid #f39c12; padding:15px; border-radius:5px; margin-top:15px;">
                <p style="margin:0; color:#856404;"><strong>⚠ Payment not yet verified by Bursar.</strong></p>
                <p style="margin:8px 0 0 0; color:#555; font-size:13px;">This application cannot be approved until the Bursar verifies the admission fee payment.</p>
            </div>
        `;
    } else {
        actionButtons = `
            <div style="display:flex; gap:10px; margin-top:20px; flex-wrap:wrap;">
                <button onclick="approveApplication(${app.applicationId})" style="flex:1; min-width:140px; padding:12px 20px; background:#27ae60; color:white; border:none; border-radius:5px; cursor:pointer; font-weight:bold;">Approve Application</button>
                <button onclick="showRejectApplicationModal(${app.applicationId})" style="flex:1; min-width:140px; padding:12px 20px; background:#e74c3c; color:white; border:none; border-radius:5px; cursor:pointer; font-weight:bold;">Reject Application</button>
            </div>
        `;
    }

    const html = `
        <h3 style="color:#0B5345; margin-bottom:5px;">${app.firstName} ${app.lastName}</h3>
        <p style="color:#7f8c8d; margin-bottom:20px;">${app.email}</p>
        <div style="background:#f5f5f5; padding:15px; border-radius:5px; margin-bottom:15px;">
            <p style="margin:6px 0;"><strong>Application #:</strong> ${app.applicationNumber}</p>
            <p style="margin:6px 0;"><strong>Programme:</strong> ${app.programmeName || 'N/A'}</p>
            <p style="margin:6px 0;"><strong>Campus:</strong> ${app.campusPreference || 'N/A'}</p>
            <p style="margin:6px 0;"><strong>Application Status:</strong> <span class="status-badge ${app.applicationStatus === 'APPROVED' ? 'status-approved' : app.applicationStatus === 'REJECTED' ? 'status-rejected' : 'status-pending'}">${app.applicationStatus}</span></p>
            <p style="margin:6px 0;"><strong>Payment Status:</strong> <span class="status-badge ${app.paymentStatus === 'VERIFIED' ? 'status-approved' : app.paymentStatus === 'REJECTED' ? 'status-rejected' : 'status-pending'}">${app.paymentStatus}</span></p>
            <p style="margin:6px 0;"><strong>Payment Amount:</strong> UGX ${Number(app.paymentAmount || 0).toLocaleString()}</p>
        </div>
        ${actionButtons}
    `;

    document.getElementById('modalBody').innerHTML = html;
    document.getElementById('detailModal').classList.add('show');
}

function closeModal() {
    document.getElementById('detailModal').classList.remove('show');
}

async function approveApplication(id) {
    if (!confirm('Approve this application? This will generate a registration number and admission letter.')) return;

    try {
        const res = await fetch(`${API_URL}/academic-registrar/approve-application`, {
            method: 'POST',
            headers: authHeaders(),
            body: JSON.stringify({ applicationId: id })
        });

        const data = await res.json();

        if (data.success) {
            showMessage(`✓ Approved! Registration Number: ${data.data?.registrationNumber || 'Assigned'}`, 'success');
            closeModal();
            loadApplications('PENDING_REVIEW');
            loadDashboardStats();
        } else {
            showMessage('Error: ' + (data.message || 'Failed to approve application'), 'error');
        }
    } catch (err) {
        console.error('Approve application error:', err);
        showMessage('Error: ' + err.message, 'error');
    }
}

function showRejectApplicationModal(id) {
    currentApplicationId = id;
    document.getElementById('rejectApplicationModal').style.display = 'flex';
}

function closeRejectApplicationModal() {
    document.getElementById('rejectApplicationModal').style.display = 'none';
    document.getElementById('appRejectionReason').value = '';
}

async function submitRejectApplication() {
    const reason = document.getElementById('appRejectionReason').value.trim();

    if (!reason) {
        alert('Please enter a reason for rejection');
        return;
    }

    if (!currentApplicationId) {
        alert('Application reference missing');
        return;
    }

    try {
        const res = await fetch(`${API_URL}/academic-registrar/reject-application`, {
            method: 'POST',
            headers: authHeaders(),
            body: JSON.stringify({ applicationId: currentApplicationId, rejectionReason: reason })
        });

        const data = await res.json();

        if (data.success) {
            showMessage('✓ Application rejected successfully', 'success');
            closeRejectApplicationModal();
            closeModal();
            loadApplications('PENDING_REVIEW');
            loadDashboardStats();
        } else {
            showMessage('Error: ' + (data.message || 'Failed to reject application'), 'error');
        }
    } catch (err) {
        console.error('Reject application error:', err);
        showMessage('Error: ' + err.message, 'error');
    }
}

/* =========================================================
   RESULTS SIGN-OFF
   ========================================================= */
async function loadSignoffQueue() {
    const container = document.getElementById('signoffContainer');
    container.innerHTML = 'Loading results...';

    try {
        const res = await fetch(`${API_URL}/academic-registrar/results/pending-signoff`, { headers: authHeaders() });
        const data = await res.json();

        if (!data.success) throw new Error(data.message);

        renderSignoffTable(data.data || []);
    } catch (err) {
        console.error('Load sign-off queue error:', err);
        container.innerHTML = `<p style="color:red;">Error loading results: ${err.message}</p>`;
    }
}

function renderSignoffTable(results) {
    const container = document.getElementById('signoffContainer');

    if (!results || results.length === 0) {
        container.innerHTML = '<p style="color:#666; padding:20px 0;">No results awaiting sign-off.</p>';
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
                        <th>Dean Approved</th>
                        <th style="text-align:center;">Action</th>
                    </tr>
                </thead>
                <tbody>
    `;

    results.forEach(r => {
        const dateStr = r.deanApprovalDate ? new Date(r.deanApprovalDate).toLocaleDateString() : 'N/A';

        html += `
            <tr>
                <td>${r.regNumber}</td>
                <td>${r.studentName}</td>
                <td><strong>${r.courseCode}</strong><br><small>${r.courseName || ''}</small></td>
                <td>${r.mark ?? 'N/A'}</td>
                <td>${r.grade ?? 'N/A'}</td>
                <td><span class="status-badge status-deanapproved">${dateStr}</span></td>
                <td style="text-align:center;">
                    <button class="btn-approve" onclick="signOffResult(${r.resultId}, true)">Sign Off</button>
                    <button class="btn-reject" onclick="signOffResult(${r.resultId}, false)">Reject</button>
                </td>
            </tr>
        `;
    });

    html += `</tbody></table></div>`;
    container.innerHTML = html;
}

async function signOffResult(resultId, isApproved) {
    const label = isApproved ? 'sign off on' : 'reject';
    if (!confirm(`Are you sure you want to ${label} this result?`)) return;

    try {
        const res = await fetch(`${API_URL}/academic-registrar/results/sign-off`, {
            method: 'POST',
            headers: authHeaders(),
            body: JSON.stringify({ resultId, isApproved })
        });

        const data = await res.json();

        if (data.success) {
            showMessage(data.message, 'success');
            loadSignoffQueue();
            loadDashboardStats();
        } else {
            showMessage('Error: ' + (data.message || 'Failed to process result'), 'error');
        }
    } catch (err) {
        console.error('Sign off result error:', err);
        showMessage('Error: ' + err.message, 'error');
    }
}

// =====================================================
// TIMETABLE MANAGEMENT (Registrar — any faculty)
// =====================================================
const TT_API = 'https://localhost:44366/api/timetable';

function getRegistrarToken() {
    return localStorage.getItem('registrarToken') || localStorage.getItem('token');
}

async function loadTimetableFaculties() {
    try {
        const res = await fetch(`${TT_API}/faculties`, {
            headers: { 'Authorization': `Bearer ${getRegistrarToken()}` }
        });
        const data = await res.json();
        if (!data.success) return;

        const facultySelect = document.getElementById('ttFaculty');
        const historySelect = document.getElementById('ttHistoryFaculty');

        const options = data.data.map(f => `<option value="${f.facultyId}">${f.facultyName}</option>`).join('');
        facultySelect.innerHTML = options;
        historySelect.innerHTML = `<option value="">-- Select faculty --</option>` + options;
    } catch (err) {
        console.error('Failed to load faculties', err);
    }
}

async function loadTimetableSemesters() {
    try {
        const res = await fetch(`${TT_API}/semesters`, {
            headers: { 'Authorization': `Bearer ${getRegistrarToken()}` }
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
    const facultyId = document.getElementById('ttFaculty').value;
    const semesterId = document.getElementById('ttSemester').value;
    const fileInput = document.getElementById('ttFile');
    const btn = document.getElementById('ttUploadBtn');

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
            headers: { 'Authorization': `Bearer ${getRegistrarToken()}` },
            body: formData
        });
        const data = await res.json();

        if (data.success) {
            showMessage('✅ Timetable uploaded successfully', 'success');
            fileInput.value = '';
            if (document.getElementById('ttHistoryFaculty').value === facultyId) {
                loadTimetableHistory();
            }
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
    const facultyId = document.getElementById('ttHistoryFaculty').value;
    const container = document.getElementById('timetableHistoryContainer');

    if (!facultyId) {
        container.innerHTML = 'Select a faculty to view its timetable history.';
        return;
    }

    container.innerHTML = 'Loading...';

    try {
        const res = await fetch(`${TT_API}/history/${facultyId}`, {
            headers: { 'Authorization': `Bearer ${getRegistrarToken()}` }
        });
        const data = await res.json();

        if (!data.success || data.data.length === 0) {
            container.innerHTML = '<p style="color:#666;">No timetable has been uploaded for this faculty yet.</p>';
            return;
        }

        const rows = data.data.map(t => `
            <tr>
                <td>${t.fileName}</td>
                <td>${(t.fileSizeBytes / 1024).toFixed(0)} KB</td>
                <td>${t.uploadedByRole} — ${t.uploadedByName || 'N/A'}</td>
                <td>${new Date(t.uploadedAt).toLocaleString()}</td>
                <td>${t.isActive ? '<span class="status-badge status-approved">Active</span>' : '<span class="status-badge status-pending">Archived</span>'}</td>
               <td><a class="btn-view" href="${TT_API.replace('/api/timetable', '')}${t.downloadUrl}?token=${getRegistrarToken()}" target="_blank">View</a> <a class="btn-view" href="${TT_API.replace('/api/timetable', '')}${t.downloadUrl}?token=${getRegistrarToken()}&download=true">Download</a></td>
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

// Call this once when the Timetable section is first opened
document.addEventListener('DOMContentLoaded', () => {
    loadTimetableFaculties();
    loadTimetableSemesters();
});


/* =========================================================
   SEMESTER ACTIVATION
   ========================================================= */
async function loadSemesterHistory() {
    const container = document.getElementById('semesterHistoryContainer');
    container.innerHTML = 'Loading...';

    try {
        const res = await fetch(`${API_URL}/academic-registrar/semesters`, { headers: authHeaders() });
        const data = await res.json();
        if (!data.success) throw new Error(data.message);

        const semesters = data.data || [];
        if (semesters.length === 0) {
            container.innerHTML = '<p style="color:#666; padding:20px 0;">No semesters have been activated yet.</p>';
            return;
        }

        const rows = semesters.map(s => `
            <tr>
                <td>${s.academicYear}</td>
                <td>Semester ${s.semester}</td>
                <td>${s.startDate ? new Date(s.startDate).toLocaleDateString() : 'N/A'}</td>
                <td>${s.endDate ? new Date(s.endDate).toLocaleDateString() : 'N/A'}</td>
                <td>${s.isActive ? '<span class="status-badge status-approved">Active</span>' : '<span class="status-badge status-pending">Past</span>'}</td>
            </tr>
        `).join('');

        container.innerHTML = `
            <div class="table-wrapper">
                <table class="data-table">
                    <thead>
                        <tr><th>Academic Year</th><th>Semester</th><th>Start Date</th><th>End Date</th><th>Status</th></tr>
                    </thead>
                    <tbody>${rows}</tbody>
                </table>
            </div>`;
    } catch (err) {
        console.error('Load semester history error:', err);
        container.innerHTML = `<p style="color:red;">Error loading semester history: ${err.message}</p>`;
    }
}

async function activateSemester() {
    const academicYear = document.getElementById('semAcademicYear').value.trim();
    const semesterNumber = parseInt(document.getElementById('semSemesterNumber').value);
    const startDate = document.getElementById('semStartDate').value;
    const endDate = document.getElementById('semEndDate').value;
    const btn = document.getElementById('semActivateBtn');

    if (!academicYear || !startDate || !endDate) {
        showMessage('❌ Academic Year, Start Date, and End Date are all required', 'error');
        return;
    }

    if (new Date(startDate) >= new Date(endDate)) {
        showMessage('❌ Start Date must be before End Date', 'error');
        return;
    }

    let activeCount = '';
    try {
        const countRes = await fetch(`${API_URL}/academic-registrar/students/active-count`, { headers: authHeaders() });
        const countData = await countRes.json();
        if (countData.success) activeCount = ` This will advance approximately ${countData.count} active student(s).`;
    } catch (e) {
        // non-fatal — proceed with a generic warning if the count can't be fetched
    }

    if (!confirm(`Activate ${academicYear} Semester ${semesterNumber} (${startDate} to ${endDate})?${activeCount} This cannot be easily undone.`)) {
        return;
    }

    btn.disabled = true;
    btn.textContent = 'Activating...';

    try {
        const res = await fetch(`${API_URL}/academic-registrar/semesters/activate`, {
            method: 'POST',
            headers: authHeaders(),
            body: JSON.stringify({
                academicYear,
                semester: semesterNumber,
                startDate,
                endDate
            })
        });

        const data = await res.json();

        if (data.success) {
            showMessage(`✅ ${data.message}`, 'success');
            document.getElementById('semAcademicYear').value = '';
            document.getElementById('semStartDate').value = '';
            document.getElementById('semEndDate').value = '';
            loadSemesterHistory();
        } else {
            showMessage('❌ ' + (data.message || 'Failed to activate semester'), 'error');
        }
    } catch (err) {
        console.error('Activate semester error:', err);
        showMessage('❌ Error: ' + err.message, 'error');
    } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="bi bi-play-circle"></i> Activate Semester';
    }
}

/* =========================================================
   GOVERNMENT LOAN SCHEME REVIEW
   ========================================================= */
async function loadLoanApplications(status) {
    const container = document.getElementById('loansContainer');
    container.innerHTML = 'Loading loan applications...';

    try {
        const url = status === 'ALL'
            ? `${API_URL}/academic-registrar/loan-applications`
            : `${API_URL}/academic-registrar/loan-applications?status=${status}`;

        const res = await fetch(url, { headers: authHeaders() });
        const data = await res.json();

        if (!data.success) throw new Error(data.message);

        renderLoanApplicationsTable(data.data || []);
    } catch (err) {
        console.error('Load loan applications error:', err);
        container.innerHTML = `<p style="color:red;">Error loading loan applications: ${err.message}</p>`;
    }
}

function renderLoanApplicationsTable(apps) {
    const container = document.getElementById('loansContainer');

    if (!apps || apps.length === 0) {
        container.innerHTML = '<p style="color:#666; padding:20px 0;">No loan scheme applications found.</p>';
        return;
    }

    let html = `
        <div class="table-wrapper">
            <table class="data-table">
                <thead>
                    <tr>
                        <th>App #</th>
                        <th>Name</th>
                        <th>Email</th>
                        <th>Programme</th>
                        <th>Proof</th>
                        <th>Loan Status</th>
                        <th style="text-align:center;">Action</th>
                    </tr>
                </thead>
                <tbody>
    `;

    apps.forEach(app => {
        const statusClass = app.loanSchemeStatus === 'Approved' ? 'status-approved'
            : app.loanSchemeStatus === 'Rejected' ? 'status-rejected'
                : 'status-pending';

        const proofLink = app.loanProofPath
            ? `<a href="${app.loanProofPath}" target="_blank">View Proof</a>`
            : '<span style="color:#c0392b;">Not uploaded</span>';

        const actionCell = (app.loanSchemeStatus === 'Approved' || app.loanSchemeStatus === 'Rejected')
            ? `<span class="status-badge ${statusClass}">${app.loanSchemeStatus}</span>`
            : `
                <button class="btn-approve" onclick="reviewLoan(${app.applicationId}, true)" ${!app.loanProofPath ? 'disabled' : ''}>Approve</button>
                <button class="btn-reject" onclick="reviewLoan(${app.applicationId}, false)" ${!app.loanProofPath ? 'disabled' : ''}>Reject</button>
            `;

        html += `
            <tr>
                <td>${app.applicationNumber}</td>
                <td>${app.firstName} ${app.lastName}</td>
                <td>${app.email}</td>
                <td>${app.programmeName || 'N/A'}</td>
                <td>${proofLink}</td>
                <td><span class="status-badge ${statusClass}">${app.loanSchemeStatus || 'Pending'}</span></td>
                <td style="text-align:center;">${actionCell}</td>
            </tr>
        `;
    });

    html += `</tbody></table></div>`;
    container.innerHTML = html;
}

async function reviewLoan(applicationId, isApproved) {
    let rejectionReason = null;

    if (!isApproved) {
        rejectionReason = prompt('Reason for rejecting this loan scheme application:');
        if (!rejectionReason || !rejectionReason.trim()) {
            showMessage('❌ A rejection reason is required', 'error');
            return;
        }
    } else {
        if (!confirm('Approve this Government Loan Scheme application? Tuition will be waived once the student is admitted.')) return;
    }

    try {
        const res = await fetch(`${API_URL}/academic-registrar/loan-applications/${applicationId}/review`, {
            method: 'POST',
            headers: authHeaders(),
            body: JSON.stringify({ isApproved, rejectionReason })
        });

        const data = await res.json();

        if (data.success) {
            showMessage(`✅ ${data.message}`, 'success');
            loadLoanApplications('Pending');
        } else {
            showMessage('❌ ' + (data.message || 'Failed to process loan review'), 'error');
        }
    } catch (err) {
        console.error('Review loan error:', err);
        showMessage('❌ Error: ' + err.message, 'error');
    }
}


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
    localStorage.removeItem('registrarToken');
    localStorage.removeItem('registrar');
    localStorage.removeItem('currentRole');
    localStorage.removeItem('token');
    window.location.href = 'login.html';
}