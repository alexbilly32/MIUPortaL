var API_URL = 'https://localhost:44366/api';
// Extract the base host URL (https://localhost:44366) to properly serve physical images
var FILE_BASE_URL = API_URL.replace('/api', '');

var adminToken = localStorage.getItem('adminToken');
var adminUserRaw = localStorage.getItem('adminUser');

var userObj = {};
try {
    userObj = (adminUserRaw && adminUserRaw !== "undefined")
        ? JSON.parse(adminUserRaw)
        : {};
} catch (e) {
    console.error("Invalid adminUser in localStorage", e);
    userObj = {};
}

var currentApplications = [];
var currentStudents = [];
var currentFilter = 'all';
var currentSection = 'dashboard';
var currentApplicationId = null;

window.addEventListener('load', function () {

    if (!adminToken) {
        window.location.href = '/admin-login.html';
        return;
    }

    function isTokenValid() {
        return adminToken && adminToken.length > 10;
    }

    if (!isTokenValid()) {
        logout();
        return;
    }

    document.getElementById('adminName').textContent = userObj.firstName || 'Admin';
    document.getElementById('adminRole').textContent = userObj.role || 'Administrator';

    navigateToSection('dashboard');

    setTimeout(function () {
        loadDashboardStats();
    }, 500);

    setInterval(loadDashboardStats, 30000);
});

/* =========================================================
   SIDEBAR (mobile drawer toggle)
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
function navigateToSection(section, event) {

    console.log("Clicked section:", section);

    if (event) {
        event.preventDefault();
    }

    currentSection = section;
    closeSidebar(); // auto-close drawer on mobile after navigating

    document.querySelectorAll('.section').forEach(function (sec) {
        sec.classList.remove('active');
    });

    document.querySelectorAll('.menu-link').forEach(function (link) {
        link.classList.remove('active');
    });

    var selectedSection = document.getElementById(section + '-section');
    if (selectedSection) {
        selectedSection.classList.add('active');
    }

    document.querySelectorAll('.menu-link').forEach(function (link) {
        link.classList.remove('active');
        if (link.dataset.section === section) {
            link.classList.add('active');
        }
    });

    switch (section) {
        case 'dashboard':
            loadDashboardStats();
            break;
        case 'applications':
            loadApplications();
            break;
        case 'students':
            loadStudentsSection();
            break;
        case 'payments':
            loadPaymentsSection();
            break;
        case 'results':
            loadResultsSection();
            break;
        case 'documents':
            loadDocumentsSection();
            break;
        case 'lecturers':
            loadLecturers();
            break;

        case 'assignments':
            initialiseAssignmentModule();
            break;
    }
}

/* =========================================================
   DASHBOARD STATS
   ========================================================= */
function showStatsLoading() {
    const loader = document.getElementById('statsLoading');
    const grid = document.getElementById('statsGrid');
    if (loader) {
        loader.style.display = 'block';
        loader.innerHTML = '⏳ Loading statistics, please wait...';
    }
    if (grid) {
        grid.style.display = 'none';
    }
}
async function loadDashboardStats() {
    try {
        showStatsLoading();

        const response = await fetch(`${API_URL}/admin/dashboard/stats`, {
            headers: { 'Authorization': 'Bearer ' + adminToken }
        });

        if (!response.ok) throw new Error(`HTTP ${response.status}`);

        const result = await response.json();
        if (!result || !result.data) throw new Error("Invalid stats response");

        const stats = result.data;

        document.getElementById('totalApplications').textContent = stats.totalApplications || 0;
        document.getElementById('pendingApplications').textContent = stats.pendingApplications || 0;
        document.getElementById('totalStudents').textContent = stats.totalStudents || 0;

        document.getElementById('totalLecturers').textContent = stats.totalLecturers || 0;
        document.getElementById('activeLecturers').textContent = stats.activeLecturers || 0;
        document.getElementById('inactiveLecturers').textContent = stats.inactiveLecturers || 0;
        document.getElementById('totalDeans').textContent = stats.totalDeans || 0;
        document.getElementById('facultiesWithoutDean').textContent = stats.facultiesWithoutDean || 0;
        document.getElementById('facultiesWithoutDean').textContent = stats.facultiesWithoutDean || 0;
        renderFacultiesWithoutDean(stats.facultiesWithoutDeanList || []);   
        document.getElementById('totalRegistrars').textContent = stats.totalRegistrars || 0;
        document.getElementById('totalCourseAssignments').textContent = stats.totalCourseAssignments || 0;

        const loader = document.getElementById('statsLoading');
        const grid = document.getElementById('statsGrid');
        if (loader) loader.style.display = 'none';
        if (grid) grid.style.display = 'grid';

    } catch (error) {
        console.error('Dashboard stats error:', error);
        const loader = document.getElementById('statsLoading');
        if (loader) {
            loader.innerHTML = "❌ Failed to load statistics. Please refresh.";
        }
    }
}
/* =========================================================
   APPLICATIONS
   ========================================================= */
function loadApplications() {
    fetch(API_URL + '/admin/applications', {
        headers: { 'Authorization': 'Bearer ' + adminToken }
    })
        .then(r => r.json())
        .then(data => {
            if (!data.success) throw new Error(data.message);
            currentApplications = data.data || [];
            renderApplicationsTable(currentApplications);
        })
        .catch(err => { console.error(err); alert('Error loading applications: ' + err.message); });
}

function renderApplicationsTable(apps) {
    var container = document.getElementById('applicationsContainer');
    if (!container) return;

    var html = '<div class="table-wrapper"><div style="padding: 20px 0;"><div style="margin-bottom: 15px;"><button onclick="loadApplications()" style="padding: 8px 15px; background: #3498db; color: white; border: none; border-radius: 5px; cursor: pointer; margin-right: 10px;">All</button><button onclick="filterApplications(\'PENDING_REVIEW\')" style="padding: 8px 15px; background: #f39c12; color: white; border: none; border-radius: 5px; cursor: pointer; margin-right: 10px;">Pending</button><button onclick="filterApplications(\'APPROVED\')" style="padding: 8px 15px; background: #27ae60; color: white; border: none; border-radius: 5px; cursor: pointer; margin-right: 10px;">Approved</button><button onclick="filterApplications(\'REJECTED\')" style="padding: 8px 15px; background: #e74c3c; color: white; border: none; border-radius: 5px; cursor: pointer;">Rejected</button></div>';

    html += '<table style="width: 100%; border-collapse: collapse; margin-top: 20px;"><thead style="background: #34495e; color: white;"><tr><th style="padding: 12px; text-align: left;">App #</th><th style="padding: 12px; text-align: left;">Name</th><th style="padding: 12px; text-align: left;">Email</th><th style="padding: 12px; text-align: left;">Programme</th><th style="padding: 12px; text-align: left;">Status</th><th style="padding: 12px; text-align: left;">Payment</th><th style="padding: 12px; text-align: center;">Action</th></tr></thead><tbody>';

    apps.forEach(function (app) {
        var appId = app.applicationId || app.id || app.ApplicationId;
        var firstName = app.firstName || app.FirstName || '';
        var lastName = app.lastName || app.LastName || '';
        var email = app.email || app.Email || '';
        var prog = app.programmeName || app.ProgrammeName || 'N/A';
        var appStatus = app.applicationStatus || app.ApplicationStatus || 'N/A';
        var payStatusRaw = app.paymentStatus || app.PaymentStatus || 'PENDING_REVIEW';
        var payStatus = (payStatusRaw || '').toUpperCase();

        var statusColor = appStatus === 'APPROVED' ? '#27ae60' : (appStatus === 'PENDING_REVIEW' ? '#f39c12' : '#e74c3c');
        var payColor = payStatus === 'VERIFIED' ? '#27ae60' : (payStatus === 'REJECTED' ? '#e74c3c' : '#f39c12');

        html += '<tr style="border-bottom: 1px solid #ddd;"><td style="padding: 12px;">' + appId + '</td><td style="padding: 12px;">' + firstName + ' ' + lastName + '</td><td style="padding: 12px;">' + email + '</td><td style="padding: 12px;">' + prog + '</td><td style="padding: 12px;"><span style="background: ' + statusColor + '; color: white; padding: 5px 10px; border-radius: 3px; font-size: 12px;">' + appStatus + '</span></td><td style="padding: 12px;"><span style="background: ' + payColor + '; color: white; padding: 5px 10px; border-radius: 3px; font-size: 12px;">' + payStatus + '</span></td><td style="padding: 12px; text-align: center;"><button onclick="viewApp(' + appId + ')" style="padding: 6px 12px; background: #3498db; color: white; border: none; border-radius: 3px; cursor: pointer;">View</button></td></tr>';
    });

    html += '</tbody></table></div></div>';
    container.innerHTML = html;
}

function filterApplications(status) {
    var filtered = currentApplications.filter(function (app) {
        return (app.applicationStatus || app.ApplicationStatus) === status;
    });
    renderApplicationsTable(filtered);
}

function viewApp(id) {
    if (!id || id === "undefined") {
        console.error("Invalid application ID:", id);
        alert("Invalid application ID");
        return;
    }

    currentApplicationId = id;

    fetch(API_URL + '/admin/application/' + id, {
        headers: { 'Authorization': 'Bearer ' + adminToken }
    })
        .then(r => r.json())
        .then(data => {
            if (!data.success) throw new Error(data.message);
            var app = data.data;

            var firstName = app.firstName || app.FirstName || '';
            var lastName = app.lastName || app.LastName || '';
            var email = app.email || app.Email || '';
            var progName = app.programmeName || app.ProgrammeName || '';
            var campus = app.campusPreference || app.CampusPreference || '';
            var appStatus = app.applicationStatus || app.ApplicationStatus || '';
            var payStatus = (app.paymentStatus || app.PaymentStatus || 'PENDING_VERIFICATION').toUpperCase();
            var payAmount = app.paymentAmount || app.PaymentAmount || 0;
            var payProof = app.paymentProofPath || app.PaymentProofPath || '';
            var appId = app.applicationId || app.id || app.ApplicationId || '';

            var rejectReason = app.adminNotes || app.AdminNotes || '';

            var imagePath = '';
            if (payProof) {
                if (payProof.startsWith('http')) {
                    imagePath = payProof;
                } else if (payProof.startsWith('/')) {
                    imagePath = payProof;
                } else {
                    imagePath = '/uploads/payment-proofs/' + payProof;
                }
            }

            var actionButtons = '';
            var receiptSection = '';

            if (imagePath) {
                receiptSection =
                    '<div style="margin: 15px 0; padding: 15px; background: #ecf0f1; border-left: 4px solid #3498db; border-radius: 5px;">' +
                    '<p style="margin: 0 0 15px 0;"><strong>📄 Payment Proof Available</strong></p>' +
                    '<a href="' + imagePath + '" target="_blank" style="display:inline-block; padding:10px 18px; background:#3498db; color:white; text-decoration:none; border-radius:5px; font-weight:bold;">View Payment Proof</a>' +
                    '<small style="display:block; margin-top:10px; color:#666;">File: ' + payProof + '</small>' +
                    '</div>';
            } else {
                receiptSection =
                    '<div style="margin: 15px 0; padding: 15px; background: #fff3cd; border-left: 4px solid #f39c12; border-radius: 5px;">' +
                    '<p style="margin: 0; color: #856404;"><strong>⚠ No payment proof uploaded yet</strong></p>' +
                    '</div>';
            }

            if (appStatus === 'APPROVED') {
                actionButtons = '<p style="color: #27ae60; font-weight: bold; font-size: 18px;">✓ Application Approved</p>';
            } else if (appStatus === 'REJECTED') {
                actionButtons = '<div style="background: #fdf2f2; border-left: 4px solid #e74c3c; padding: 15px; border-radius: 5px;">' +
                    '<p style="color: #c0392b; font-weight: bold; margin: 0 0 5px 0;">✗ Application Rejected</p>' +
                    '<p style="margin: 0; color: #555;"><strong>Reason:</strong> ' + (rejectReason || 'No details provided.') + '</p>' +
                    '</div>';
            } else if (payStatus === 'REJECTED') {
                actionButtons = '<div style="background: #fdf2f2; border-left: 4px solid #e74c3c; padding: 15px; border-radius: 5px; margin-bottom: 15px;">' +
                    '<p style="color: #c0392b; font-weight: bold; margin: 0 0 5px 0;">✗ Payment Rejected</p>' +
                    '<p style="margin: 0; color: #555;"><strong>Reason:</strong> ' + (rejectReason || 'Invalid transaction receipt.') + '</p>' +
                    '</div>' +
                    '<p style="color: #f39c12; font-weight: bold;">Waiting for student to upload new verification details.</p>';
            } else if (payStatus !== 'VERIFIED') {
                actionButtons = '<div style="background: #eaf4fb; border-left: 4px solid #3498db; padding: 15px; border-radius: 5px;">' +
                    '<p style="margin: 0; color: #2c3e50;"><i class="bi bi-info-circle"></i> <strong>Admission fee payment verification is now handled by the Bursar.</strong></p>' +
                    '<p style="margin: 8px 0 0 0; color: #555; font-size: 13px;">Ask the Bursar to review this application under Admission Fee Verification in their dashboard.</p>' +
                    '</div>';
            } else {
                actionButtons = '<div style="background: #eaf4fb; border-left: 4px solid #3498db; padding: 15px; border-radius: 5px;">' +
                    '<p style="margin: 0; color: #2c3e50;"><i class="bi bi-info-circle"></i> <strong>Application approval is now handled by the Academic Registrar.</strong></p>' +
                    '<p style="margin: 8px 0 0 0; color: #555; font-size: 13px;">This application is payment-verified and awaiting Academic Registrar review.</p>' +
                    '</div>';
            }
            var html = '<div style="max-width: 700px; background: white; border-radius: 8px; overflow: hidden;"><div style="background: #34495e; color: white; padding: 20px; display: flex; justify-content: space-between; align-items: center;"><h2 style="margin: 0;">Application Details</h2><span onclick="closeModal()" style="font-size: 32px; cursor: pointer; font-weight: bold;">&times;</span></div><div style="padding: 30px;"><h3 style="color: #2c3e50; margin-bottom: 5px;">' + firstName + ' ' + lastName + '</h3><p style="color: #7f8c8d; margin-bottom: 20px;">' + email + '</p><div style="background: #ecf0f1; padding: 15px; border-radius: 5px; margin-bottom: 20px;"><p style="margin: 8px 0;"><strong>Programme:</strong> ' + progName + '</p><p style="margin: 8px 0;"><strong>Campus:</strong> ' + campus + '</p><p style="margin: 8px 0;"><strong>Application Status:</strong> <span style="background: ' + (appStatus === 'APPROVED' ? '#27ae60' : (appStatus === 'PENDING_REVIEW' ? '#f39c12' : '#e74c3c')) + '; color: white; padding: 3px 8px; border-radius: 3px; font-size: 12px;">' + appStatus + '</span></p></div><div style="background: #fff3cd; padding: 15px; border-radius: 5px; margin-bottom: 20px; border-left: 4px solid #f39c12;"><p style="margin: 8px 0;"><strong>Payment Status:</strong> <span style="background: ' + (payStatus === 'VERIFIED' ? '#27ae60' : (payStatus === 'REJECTED' ? '#e74c3c' : '#f39c12')) + '; color: white; padding: 3px 8px; border-radius: 3px; font-size: 12px;">' + payStatus + '</span></p><p style="margin: 8px 0;"><strong>Payment Amount:</strong> UGX ' + Number(payAmount).toLocaleString() + '</p></div>' + receiptSection + actionButtons + '</div></div>';

            var modalBody = document.getElementById('modalBody');
            if (modalBody) modalBody.innerHTML = html;
            document.getElementById('detailModal').classList.add('show');
        })
        .catch(err => { console.error(err); alert('Error: ' + err.message); });
}

/* =========================================================
   STUDENTS
   ========================================================= */
function loadStudentsSection() {
    fetch(API_URL + '/admin/students', {
        headers: { 'Authorization': 'Bearer ' + adminToken }
    })
        .then(r => r.json())
        .then(data => {
            if (!data.success) throw new Error(data.message);
            currentStudents = data.data || [];
            renderStudentsTable(currentStudents);
        })
        .catch(err => {
            console.error(err);
            var container = document.getElementById('students-section');
            if (container) container.innerHTML = '<p style="color: red; padding: 20px;">Error loading students: ' + err.message + '</p>';
        });
}

function renderStudentsTable(students) {
    var container = document.getElementById('students-section');
    if (!container) return;

    var html = '<div class="content-section"><h3 style="margin-bottom:15px;">Enrolled Students (' + students.length + ')</h3><div class="table-wrapper"><table><thead><tr style="background: var(--primary-color); color: white;"><th style="padding: 12px; text-align: left;">Reg Number</th><th style="padding: 12px; text-align: left;">Name</th><th style="padding: 12px; text-align: left;">Email</th><th style="padding: 12px; text-align: left;">Programme</th><th style="padding: 12px; text-align: left;">Status</th></tr></thead><tbody>';

    students.forEach(function (s) {
        var regNum = s.regNumber || s.RegNumber || 'N/A';
        var fname = s.firstName || s.FirstName || '';
        var lname = s.lastName || s.LastName || '';
        var email = s.email || s.Email || '';
        var prog = s.programmeName || s.ProgrammeName || '';
        var status = s.status || s.Status || 'ACTIVE';

        html += '<tr style="border-bottom: 1px solid #ddd;"><td style="padding: 12px;">' + regNum + '</td><td style="padding: 12px;">' + fname + ' ' + lname + '</td><td style="padding: 12px;">' + email + '</td><td style="padding: 12px;">' + prog + '</td><td style="padding: 12px;"><span class="status-badge status-approved">' + status + '</span></td></tr>';
    });

    html += '</tbody></table></div></div>';
    container.innerHTML = html;
}

/* =========================================================
   PAYMENTS
   ========================================================= */
function loadPaymentsSection(status = 'PENDING') {
    var container = document.getElementById('payments-section');
    if (!container) return;

    var url = (status === 'VERIFIED')
        ? API_URL + '/admin/verified-payments'
        : API_URL + '/admin/pending-payments';

    fetch(url, {
        headers: { 'Authorization': 'Bearer ' + adminToken }
    })
        .then(r => r.json())
        .then(data => {
            if (!data.success) throw new Error(data.message);
            var payments = data.data || [];

            var html = `
                <div class="content-section">
                    <h3 style="margin-bottom:15px;">${status} Payments (${payments.length})</h3>
                    <div style="margin-bottom:15px;">
                        <button onclick="loadPaymentsSection('PENDING')" style="padding:8px 15px; margin-right:8px; background:${status === 'PENDING' ? 'var(--primary-color)' : '#bdc3c7'}; color:white; border:none; border-radius:5px; cursor:pointer;">Pending</button>
                        <button onclick="loadPaymentsSection('VERIFIED')" style="padding:8px 15px; background:${status === 'VERIFIED' ? '#27ae60' : '#bdc3c7'}; color:white; border:none; border-radius:5px; cursor:pointer;">Verified</button>
                    </div>
            `;

            if (payments.length === 0) {
                html += `<p style="color:#7f8c8d;">No ${status.toLowerCase()} payments found.</p>`;
            } else {
                html += `<div class="table-wrapper"><table><thead><tr style="background:var(--primary-color); color:white;"><th style="padding:10px; text-align:left;">Name</th><th style="padding:10px; text-align:left;">Amount</th><th style="padding:10px; text-align:left;">Status</th><th style="padding:10px; text-align:center;">Action</th></tr></thead><tbody>`;

                payments.forEach(p => {
                    var name = (p.firstName || p.FirstName || '') + ' ' + (p.lastName || p.LastName || '');
                    var amount = p.paymentAmount || p.PaymentAmount || 0;
                    var pStatus = (p.paymentStatus || p.PaymentStatus || 'PENDING').toUpperCase();
                    var appId = p.applicationId || p.ApplicationId;

                    html += `<tr style="border-bottom:1px solid #ddd;"><td style="padding:10px;">${name}</td><td style="padding:10px;">UGX ${Number(amount).toLocaleString()}</td><td style="padding:10px;"><span class="status-badge status-pending">${pStatus}</span></td><td style="padding:10px; text-align:center;"><button class="btn-view" onclick="viewApp(${appId})">Review</button></td></tr>`;
                });

                html += `</tbody></table></div>`;
            }

            html += `</div>`;
            container.innerHTML = html;
        })
        .catch(err => {
            console.error(err);
            container.innerHTML = `<p style="color:red; padding:20px;">Error loading payments</p>`;
        });
}

/* =========================================================
   RESULTS / DOCUMENTS (placeholders)
   ========================================================= */
function loadResultsSection() {
    var container = document.getElementById('results-section');
    if (!container) return;

    container.innerHTML = `
        <div class="content-section">
            <h2><i class="bi bi-bar-chart-line"></i> Results Approval</h2>
            <div style="background: #eaf4fb; border-left: 4px solid #3498db; padding: 20px; border-radius: 5px; margin-top: 15px;">
                <p style="margin: 0 0 10px 0; color: #2c3e50; font-weight: 600;">
                    Results approval is now a two-stage process handled outside the Admin dashboard:
                </p>
                <ol style="margin: 0 0 10px 20px; color: #555;">
                    <li>The <strong>Dean</strong> of the relevant Faculty approves marks uploaded by lecturers.</li>
                    <li>The <strong>Academic Registrar</strong> signs off on Dean-approved results.</li>
                </ol>
                <p style="margin: 0; color: #555; font-size: 13px;">
                    Admin retains lecturer account management and course assignment above — results approval itself has moved to keep authority aligned with faculty structure.
                </p>
            </div>
        </div>
    `;
}

function loadDocumentsSection() {
    var container = document.getElementById('documents-section');
    if (!container) return;
    container.innerHTML = '<div class="content-section"><h2>Documents & Certificates</h2><p style="color:#7f8c8d;">Coming soon...</p></div>';
}

let faculties = [];

function loadFaculties() {

    const ddl = document.getElementById("lecFaculty");

    if (!ddl) {
        console.error("Faculty dropdown not found.");
        return;
    }

    ddl.innerHTML =
        "<option>Loading faculties...</option>";

    fetch(API_URL + "/admin/faculties", {

        headers: {
            Authorization: "Bearer " + adminToken
        }

    })

        .then(response => response.json())

        .then(result => {

            ddl.innerHTML =
                '<option value="">-- Select Faculty --</option>';

            if (!result.success)
                return;

            result.data.forEach(f => {

                ddl.innerHTML +=

                    `<option value="${f.facultyId}">
                    ${f.facultyName}
                </option>`;

            });

        })

        .catch(err => {

            console.error(err);

            ddl.innerHTML =
                "<option>Error loading faculties</option>";

        });

}
document.addEventListener("DOMContentLoaded", function () {

    loadFaculties();
    loadLecturers();

});
/* =========================================================
   LECTURERS
   (functions un-nested from loadLecturers — this was a bug:
   createLecturer/deleteLecturer/closeModal/logout previously
   only existed AFTER loadLecturers() had run once)
   ========================================================= */
let allLecturers = [];

function loadLecturers() {

    fetch(API_URL + "/admin/lecturers", {

        headers: {
            Authorization: "Bearer " + adminToken
        }

    })

        .then(r => r.json())

        .then(data => {

            if (data.success) {
                allLecturers = data.data;
                renderLecturersTable(allLecturers);
                updateLecturerResultCounter(allLecturers.length, "");
            }

        });

}
function renderLecturersTable(lecturers) {

    let html = `
<table class="table">
<thead>
<tr>
<th>Name</th>
<th>Email</th>
<th>Faculty</th>
<th>Phone</th>
<th>Status</th>
<th>Action</th>
</tr>
</thead>
<tbody>
`;

    lecturers.forEach(l => {

        const isActive = l.status === "Active";
        const badgeClass = isActive ? "status-approved" : "status-rejected";
        const btnClass = isActive ? "btn-reject" : "btn-approve";
        const btnLabel = isActive ? "🚫 Deactivate" : "✅ Activate";

        html += `
<tr>
    <td>${l.firstName} ${l.lastName}</td>
    <td>${l.email}</td>
    <td>${l.facultyName ?? "Not Assigned"}</td>
    <td>${l.phoneNumber ?? "-"}</td>
    <td><span class="status-badge ${badgeClass}">${l.status}</span></td>
    <td>
        <button class="${btnClass}" onclick="toggleLecturerStatus(${l.lecturerId})">
            ${btnLabel}
        </button>
    </td>
</tr>
`;

    });

    html += "</tbody></table>";

    document.getElementById("lecturerTable").innerHTML = html;
}

function filterLecturers() {

    const term = document
        .getElementById("lecturerSearch")
        .value
        .toLowerCase()
        .trim();

    let filtered;

    if (term === "") {
        filtered = allLecturers;
    } else {
        filtered = allLecturers.filter(l =>
            `${l.firstName} ${l.lastName}`.toLowerCase().includes(term) ||
            (l.email || "").toLowerCase().includes(term) ||
            (l.facultyName || "").toLowerCase().includes(term) ||
            (l.status || "").toLowerCase().includes(term)
        );
    }

    renderLecturersTable(filtered);
    updateLecturerResultCounter(filtered.length, term);
}

function updateLecturerResultCounter(count, term) {

    const counter = document.getElementById("lecturerResultCount");
    if (!counter) return;

    if (term === "") {
        counter.textContent = `${allLecturers.length.toLocaleString()} lecturers`;
    } else if (count === 0) {
        counter.textContent = "No results found";
    } else {
        counter.textContent = `${count.toLocaleString()} ${count === 1 ? "lecturer" : "lecturers"} found`;
    }
}

function setAllLecturersStatus(status) {

    const label = status === "Active" ? "activate" : "deactivate";

    if (!confirm(`Are you sure you want to ${label} ALL lecturers? This affects every lecturer account.`))
        return;

    fetch(API_URL + "/admin/lecturers/set-all-status?status=" + status, {
        method: "PUT",
        headers: { Authorization: "Bearer " + adminToken }
    })
        .then(r => r.json())
        .then(res => {
            if (res.success) {
                alert(res.message);
                loadLecturers();
            } else {
                alert(res.message);
            }
        })
        .catch(err => alert("Error: " + err.message));
}
function toggleLecturerStatus(id) {

    fetch(API_URL + "/admin/toggle-lecturer-status/" + id, {
        method: "PUT",
        headers: { Authorization: "Bearer " + adminToken }
    })
        .then(r => r.json())
        .then(res => {
            if (res.success) {
                loadLecturers();
            } else {
                alert(res.message);
            }
        })
        .catch(err => alert("Error: " + err.message));
}
function createLecturer() {

    const msg = document.getElementById("lecMsg");

    msg.innerHTML = "";

    const data = {

        firstName: document.getElementById("lecFirstName").value.trim(),

        lastName: document.getElementById("lecLastName").value.trim(),

        email: document.getElementById("lecEmail").value.trim(),

        password: document.getElementById("lecPassword").value,

        phoneNumber: document.getElementById("lecPhone").value.trim(),

        facultyId:

            document.getElementById("lecFaculty").value === ""

                ? null

                : parseInt(document.getElementById("lecFaculty").value)

    };

    if (!data.firstName ||
        !data.lastName ||
        !data.email ||
        !data.password) {

        msg.style.color = "red";

        msg.innerHTML = "Please complete all required fields.";

        return;

    }

    msg.style.color = "blue";

    msg.innerHTML = "Creating lecturer...";

    fetch(API_URL + "/admin/create-lecturer", {

        method: "POST",

        headers: {

            "Content-Type": "application/json",

            Authorization: "Bearer " + adminToken

        },

        body: JSON.stringify(data)

    })

        .then(r => r.json())

        .then(res => {

            if (res.success) {

                msg.style.color = "green";

                msg.innerHTML = "Lecturer created successfully.";

                document.getElementById("lecFirstName").value = "";

                document.getElementById("lecLastName").value = "";

                document.getElementById("lecEmail").value = "";

                document.getElementById("lecPassword").value = "";

                document.getElementById("lecPhone").value = "";

                document.getElementById("lecFaculty").selectedIndex = 0;

                loadLecturers();

            }
            else {

                msg.style.color = "red";

                msg.innerHTML = res.message;

            }

        })

        .catch(err => {

            msg.style.color = "red";

            msg.innerHTML = err.message;

        });

}
function deleteLecturer(id) {

    if (!confirm("Deactivate this lecturer?"))

        return;

    fetch(API_URL + "/admin/delete-lecturer/" + id, {

        method: "DELETE",

        headers: {

            Authorization: "Bearer " + adminToken

        }

    })

        .then(r => r.json())

        .then(res => {

            if (res.success) {

                loadLecturers();

            }
            else {

                alert(res.message);

            }

        });

}

let editingAssignmentId = null;

function initialiseAssignmentModule() {

    console.log("Assignment module started");


    loadAcademicYears();

    loadAssignmentFaculties();

    loadAssignmentsTable();

}
function showSection(section) {

    document.querySelectorAll(".section").forEach(s => {
        s.style.display = "none";
    });

    document.getElementById(section + "-section").style.display = "block";

    switch (section) {

        case "lecturers":
            loadFaculties();
            loadLecturers();
            break;

        case "assignments":
            initialiseAssignmentModule();
            break;

    }
}

    


function loadAcademicYears() {

    let ddl = document.getElementById("assignYear");

    ddl.innerHTML = "";

    let current = new Date().getFullYear();

    for (let y = current - 1; y <= current + 5; y++) {

        ddl.innerHTML += `<option value="${y}">${y}</option>`;

    }

}

function loadAssignmentFaculties() {

    fetch(API_URL + "/admin/faculties", {

        headers: {
            Authorization: "Bearer " + adminToken
        }

    })

        .then(r => r.json())

        .then(res => {

            let ddl = document.getElementById("assignFaculty");

            ddl.innerHTML = '<option value="">Select Faculty</option>';

            res.data.forEach(f => {

                ddl.innerHTML += `
<option value="${f.facultyId}">
${f.facultyName}
</option>`;

            });

        });

}

function loadProgrammes(schoolId) {

    let ddl = document.getElementById("assignProgramme");

    ddl.disabled = false;
    ddl.innerHTML = "<option>Loading...</option>";

    fetch(API_URL + "/admin/programmes-by-school/" + schoolId, {

        headers: {
            Authorization: "Bearer " + adminToken
        }

    })

        .then(r => r.json())

        .then(res => {

            ddl.innerHTML = '<option value="">Select Programme</option>';

            res.data.forEach(p => {

                ddl.innerHTML += `
                <option value="${p.programmeCode}">
                    ${p.programmeName}
                </option>`;

            });

        });

}

function loadSchools(facultyId) {

    let ddl = document.getElementById("assignSchool");

    ddl.disabled = false;
    ddl.innerHTML = "<option>Loading...</option>";

    fetch(API_URL + "/admin/schools-by-faculty/" + facultyId, {

        headers: {
            Authorization: "Bearer " + adminToken
        }

    })

        .then(r => r.json())

        .then(res => {

            ddl.innerHTML = '<option value="">Select School</option>';

            res.data.forEach(s => {

                ddl.innerHTML += `
                <option value="${s.schoolId}">
                    ${s.schoolName}
                </option>`;

            });

        });

}
function loadProgrammeCourses(code) {

    let ddl = document.getElementById("assignCourse");

    ddl.disabled = false;

    ddl.innerHTML = "<option>Loading...</option>";

    fetch(API_URL + "/admin/courses-by-programme/" + code, {

        headers: {
            Authorization: "Bearer " + adminToken
        }

    })

        .then(r => r.json())

        .then(res => {

            ddl.innerHTML = '<option value="">Select Course</option>';

            res.data.forEach(c => {

                ddl.innerHTML += `
<option value="${c.courseId}">
${c.courseCode} - ${c.courseName}
</option>`;

            });

        });

}

function loadFacultyLecturers(id) {

    let ddl = document.getElementById("assignLecturer");

    ddl.disabled = false;

    ddl.innerHTML = "<option>Loading...</option>";

    fetch(API_URL + "/admin/lecturers-by-faculty/" + id, {

        headers: {
            Authorization: "Bearer " + adminToken
        }

    })

        .then(r => r.json())

        .then(res => {

            ddl.innerHTML = '<option value="">Select Lecturer</option>';

            res.data.forEach(l => {

                ddl.innerHTML += `
<option value="${l.lecturerId}">
${l.fullName}
</option>`;

            });

        });

}


const facultyDDL = document.getElementById("assignFaculty");

if (facultyDDL) {
    facultyDDL.addEventListener("change", function () {

        loadSchools(this.value);

        loadFacultyLecturers(this.value);

    });
}

const schoolDDL = document.getElementById("assignSchool");

if (schoolDDL) {

    schoolDDL.addEventListener("change", function () {

        loadProgrammes(this.value);

    });

}
const programmeDDL = document.getElementById("assignProgramme");

if (programmeDDL) {
    programmeDDL.addEventListener("change", function () {
        loadProgrammeCourses(this.value);
    });
}

function assignCourse() {

    const data = {

        lecturerId:
            parseInt(document.getElementById("assignLecturer").value),

        courseId:
            parseInt(document.getElementById("assignCourse").value),

        academicYear:
            parseInt(document.getElementById("assignYear").value),

        semester:
            parseInt(document.getElementById("assignSemester").value)

    };

    let url = API_URL + "/admin/assign-course";
    let method = "POST";

    if (editingAssignmentId !== null) {

        url = API_URL + "/admin/course-assignment/" + editingAssignmentId;
        method = "PUT";

    }

    fetch(url, {

        method: method,

        headers: {

            "Content-Type": "application/json",
            Authorization: "Bearer " + adminToken

        },

        body: JSON.stringify(data)

    })

        .then(r => r.json())

        .then(res => {

            if (res.success) {

                alert(res.message);

                editingAssignmentId = null;

                document.querySelector("#assignments-section .btn-primary").innerHTML =
                    "Assign Course";

                clearAssignmentForm();

                loadAssignmentsTable();

            }
            else {

                alert(res.message);

            }

        });

}

function clearAssignmentForm() {

    document.getElementById("assignFaculty").selectedIndex = 0;

    document.getElementById("assignSchool").innerHTML =
        "<option>Select Faculty First</option>";

    document.getElementById("assignProgramme").innerHTML =
        "<option>Select School First</option>";

    document.getElementById("assignCourse").innerHTML =
        "<option>Select Programme First</option>";

    document.getElementById("assignLecturer").innerHTML =
        "<option>Select Faculty First</option>";

    document.getElementById("assignSchool").disabled = true;
    document.getElementById("assignProgramme").disabled = true;
    document.getElementById("assignCourse").disabled = true;
    document.getElementById("assignLecturer").disabled = true;

}
function loadAssignmentsTable() {

    console.log("Loading assignments from API");


    fetch(API_URL + "/admin/course-assignments", {


        headers: {
            Authorization: "Bearer " + adminToken
        }

    })
        .then(r => r.json())
        .then(res => {

            console.log("Assignment API response:", res);


            if (!res.success) {

                throw new Error(res.message);

            }

            window.assignmentCache = res.data;
            allAssignments = res.data;
            renderAssignmentsTable(allAssignments);

        })
        .catch(err => {

            console.error("Assignment loading error:", err);


            document.getElementById("assignmentTable").innerHTML =
                `
    <p style="color:red">
    ${err.message}
    </p>
    `;

        });

}

function renderAssignmentsTable(assignments) {
    const container = document.getElementById("assignmentTable");
    if (!container)
        return;
    if (!assignments || assignments.length === 0) {
        container.innerHTML = `
            <div class="content-section">
                <p style="text-align:center;padding:30px;color:#777;">
                    No course assignments found.
                </p>
            </div>
        `;
        return;
    }
    let html = `
    <div class="table-wrapper">
    <table class="applications-table">
        <thead>
            <tr>
                <th>Lecturer</th>
                <th>Faculty</th>
                <th>Programme</th>
                <th>Course</th>
                <th>Academic Year</th>
                <th>Semester</th>
                <th>Action</th>
            </tr>
        </thead>
        <tbody>
    `;
    assignments.forEach(a => {
        html += `
        <tr>
            <td>${a.lecturerName}</td>
            <td>${a.facultyName}</td>
            <td>${a.programmeName}</td>
            <td>
                <strong>${a.courseCode}</strong>
                <br>
                <small>${a.courseName}</small>
            </td>
            <td>${a.academicYear}</td>
            <td>Semester ${a.semester}</td>
            <td>
                <div style="display:flex; gap:8px; justify-content:center;">
                    <button class="btn-view" style="padding:8px 10px; font-size:14px; line-height:1;" onclick="editAssignment(${a.assignmentId})" title="Edit assignment">
                        ✏️
                    </button>
                    <button class="btn-reject" style="padding:8px 10px; font-size:14px; line-height:1;" onclick="removeAssignment(${a.assignmentId})" title="Remove assignment">
                        🗑️
                    </button>
                </div>
            </td>
        </tr>
        `;
    });
    html += `
        </tbody>
    </table>
    </div>
    `;
    container.innerHTML = html;
}
function filterAssignments() {

    const term = document
        .getElementById("assignmentSearch")
        .value
        .toLowerCase()
        .trim();


    let filtered;


    if (term === "") {

        filtered = allAssignments;

    }
    else {

        filtered = allAssignments.filter(a =>

            (a.lecturerName || "").toLowerCase().includes(term) ||

            (a.courseCode || "").toLowerCase().includes(term) ||

            (a.courseName || "").toLowerCase().includes(term) ||

            (a.programmeName || "").toLowerCase().includes(term) ||

            (a.facultyName || "").toLowerCase().includes(term) ||

            ("semester " + a.semester).toLowerCase().includes(term) ||

            String(a.academicYear).includes(term)

        );

    }


    // Render filtered data
    renderAssignmentsTable(filtered);


    // Update live search counter
    updateAssignmentResultCounter(filtered.length, term);

}



function updateAssignmentResultCounter(count, term) {

    const counter = document.getElementById(
        "assignmentResultCount"
    );


    if (!counter) return;


    if (term === "") {

        counter.textContent =
            `${allAssignments.length.toLocaleString()} assignments`;

    }
    else if (count === 0) {

        counter.textContent =
            "No results found";

    }
    else {

        counter.textContent =
            `${count.toLocaleString()} ${count === 1 ? "result" : "results"
            } found`;

    }

}
function removeAssignment(id) {

    if (!confirm("Remove this course assignment?"))
        return;

    fetch(API_URL + "/admin/course-assignment/" + id, {

        method: "DELETE",

        headers: {
            Authorization: "Bearer " + adminToken
        }

    })

        .then(async r => {

            if (!r.ok) {
                throw new Error("Request failed (" + r.status + ")");
            }

            return r.json();

        })

        .then(res => {

            if (res.success) {

                alert("Assignment removed successfully.");

                loadAssignmentsTable();

            } else {

                alert(res.message);

            }

        })

        .catch(err => {

            console.error(err);

            alert("Failed to remove assignment.");

        });

}

function editAssignment(id) {

    const assignment = window.assignmentCache.find(a => a.assignmentId === id);

    if (!assignment) {
        alert("Assignment not found.");
        return;
    }

    editingAssignmentId = id;

    document.getElementById("assignFaculty").value = assignment.facultyId;

    loadSchools(assignment.facultyId);

    loadFacultyLecturers(assignment.facultyId);

    // Give the dropdowns time to populate
    setTimeout(() => {

        document.getElementById("assignSchool").value = assignment.schoolId;

        loadProgrammes(assignment.schoolId);

        setTimeout(() => {

            document.getElementById("assignProgramme").value = assignment.programmeCode;

            loadProgrammeCourses(assignment.programmeCode);

            setTimeout(() => {

                document.getElementById("assignCourse").value = assignment.courseId;
                document.getElementById("assignLecturer").value = assignment.lecturerId;
                document.getElementById("assignYear").value = assignment.academicYear;
                document.getElementById("assignSemester").value = assignment.semester;

            }, 300);

        }, 300);

    }, 300);

    document.querySelector("#assignments-section .btn-primary").innerHTML =
        "💾 Update Assignment";

    document.querySelector("#assignments-section .btn-primary").scrollIntoView({
        behavior: "smooth"
    });

}

function renderFacultiesWithoutDean(list) {
    var panel = document.getElementById('facultiesWithoutDeanPanel');
    if (!panel) return;

    if (!list || list.length === 0) {
        panel.style.display = 'none';
        return;
    }

    var html = '<h3 style="color:#f39c12; margin-bottom:10px;"><i class="bi bi-exclamation-triangle-fill"></i> Faculties Without a Dean (' + list.length + ')</h3>' +
        '<ul style="list-style:none; padding:0; margin:0;">';

    list.forEach(function (f) {
        var safeName = (f.facultyName || '').replace(/'/g, "\\'");
        html += '<li style="padding:10px 15px; border-bottom:1px solid #f0f0f0; display:flex; justify-content:space-between; align-items:center;">' +
            '<span>' + f.facultyName + '</span>' +
            '<button class="btn-view" onclick="goToFacultyDean(' + f.facultyId + ', \'' + safeName + '\')">Assign Dean</button>' +
            '</li>';
    });

    html += '</ul>';
    panel.innerHTML = html;
    panel.style.display = 'block';
}

function goToFacultyDean(facultyId, facultyName) {
    navigateToSection('roles');

    // admin-roles.js loads the promote dropdowns async — give it a moment
    setTimeout(function () {
        var sel = document.getElementById('promoteFacultySelect');
        if (sel) sel.value = facultyId;

        var target = document.getElementById('roles-section');
        if (target) target.scrollIntoView({ behavior: 'smooth' });
    }, 400);
}
/* =========================================================
   MODAL / LOGOUT (global — previously nested and broken)
   ========================================================= */
function closeModal() {
    var modal = document.getElementById('detailModal');
    if (modal) modal.classList.remove('show');
}

function logout() {
    localStorage.removeItem('adminToken');
    localStorage.removeItem('adminUser');
    localStorage.removeItem('currentRole');
    localStorage.removeItem('studentToken');
    localStorage.removeItem('lecturerToken');
    localStorage.removeItem('bursarToken');
    localStorage.removeItem('student');
    localStorage.removeItem('lecturer');
    localStorage.removeItem('bursar');
    window.location.href = 'index.html';
}