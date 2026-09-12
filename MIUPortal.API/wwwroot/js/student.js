const API_URL = 'https://localhost:44366/api';

document.addEventListener('DOMContentLoaded', function () {
    const student = JSON.parse(localStorage.getItem('student'));
    const token = localStorage.getItem('token');
    loadStudentPhoto(student);
    updateGreeting();
    updateTodayDate();
    updateDashboardHeader(student);

    console.log('✅ Student module processing standard initialization sequence...');

    if (!student || !token) {
        console.warn('❌ Authentication details missing. Evicting session.');
        redirectToLogin();
        return;
    }

    function updateDashboardHeader(student) {
        const welcomeName = document.getElementById("welcomeStudentName");
        if (welcomeName) {
            welcomeName.textContent = `${student.firstName || ""} ${student.lastName || ""}`;
        }

        const programme = document.getElementById("studentProgramme");
        if (programme) {
            programme.textContent = student.programme || "Programme Not Assigned";
        }

        const semesterEl = document.getElementById("currentSemester");
        if (semesterEl && student.regNumber) {
            fetch(`${API_URL}/student/${encodeURIComponent(student.regNumber)}`, { headers: getAuthHeaders() })
                .then(res => res.ok ? res.json() : null)
                .then(freshStudent => {
                    if (!freshStudent) return;
                    const year = freshStudent.currentYear ?? '?';
                    const sem = freshStudent.currentSemester ?? '?';
                    semesterEl.textContent = `Year ${year} · Semester ${sem}`;
                })
                .catch(err => console.error('Failed to load current semester:', err));
        }
    }

    const sidebarName = document.getElementById('sidebarStudentName');
    const sidebarReg = document.getElementById('sidebarRegNumber');

    if (sidebarName) {
        sidebarName.textContent = `${student.firstName || ''} ${student.lastName || ''}`;
    }

    if (sidebarReg) {
        sidebarReg.textContent = student.regNumber || 'REG NUMBER';
    }

    document.querySelectorAll('.menu-item').forEach(item => {
        item.addEventListener('click', handleMenuClick);
    });

    document.getElementById('sidebarLogout')?.addEventListener('click', logout);
    document.getElementById('submitPaymentBtn')?.addEventListener('click', submitPayment);

    loadProfileData(student);
    loadDashboardCounters(student.regNumber);
});

function getAuthHeaders() {
    const token = localStorage.getItem('token');
    return {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
    };
}

async function registerSelectedCourses() {
    const student = JSON.parse(localStorage.getItem('student'));
    const feedbackEl = document.getElementById('enrollmentFeedback');
    const semesterId = parseInt(document.getElementById('enrollSemester').value);
    const btn = document.getElementById('registerSelectedBtn');

    const selectedCourseIds = Array.from(document.querySelectorAll('.course-checkbox:checked'))
        .map(cb => parseInt(cb.value));

    if (selectedCourseIds.length === 0) {
        feedbackEl.style.color = 'orange';
        feedbackEl.innerText = 'Please select at least one course to register.';
        return;
    }

    if (!semesterId || isNaN(semesterId)) {
        feedbackEl.style.color = 'orange';
        feedbackEl.innerText = 'Please select a semester.';
        return;
    }

    btn.disabled = true;
    btn.textContent = 'Registering...';
    feedbackEl.style.color = 'orange';
    feedbackEl.innerText = `Processing registration for ${selectedCourseIds.length} course(s)...`;

    try {
        const response = await fetch(`${API_URL}/student/enroll-batch`, {
            method: 'POST',
            headers: getAuthHeaders(),
            body: JSON.stringify({
                regNumber: student.regNumber,
                courseIds: selectedCourseIds,
                semesterId: semesterId
            })
        });

        let data = {};
        if (response.headers.get("content-length") !== "0") {
            data = await response.json();
        }

        if (response.status === 401 || response.status === 403) { redirectToLogin(); return; }

        if (response.ok && data.registered && data.registered.length > 0) {
            const registeredNames = data.registered.map(c => c.courseCode).join(', ');
            let message = `Registered: ${registeredNames}`;

            if (data.failed && data.failed.length > 0) {
                const failedNames = data.failed.map(f => `${f.courseCode || f.courseId} (${f.reason})`).join(', ');
                message += ` — Could not register: ${failedNames}`;
            }

            feedbackEl.style.color = data.failed && data.failed.length > 0 ? '#e67e22' : 'green';
            feedbackEl.innerText = message;

            setTimeout(() => {
                loadCourses();
                loadAvailableCoursesCatalog();
                loadDocumentsList();
                loadDashboardCounters(student.regNumber);
            }, 500);
        } else {
            feedbackEl.style.color = 'red';
            feedbackEl.innerText = data.message || 'Registration failed.';
            console.error('Batch enrollment error:', data);
        }
    } catch (error) {
        console.error('Batch enrollment processing error:', error);
        feedbackEl.style.color = 'red';
        feedbackEl.innerText = 'Server error occurred.';
    } finally {
        btn.disabled = false;
        btn.textContent = 'Register Selected Courses';
    }
}

function redirectToLogin() {
    localStorage.removeItem('token');
    localStorage.removeItem('student');
    window.location.href = 'login.html';
}

function handleMenuClick(e) {
    e.preventDefault();
    const href = this.getAttribute('href');
    if (!href || !href.startsWith('#')) return;

    const sectionId = href.substring(1);

    document.querySelectorAll('main > .section').forEach(s => s.style.display = 'none');
    document.querySelectorAll('.menu-item').forEach(m => m.classList.remove('active'));

    const targetSection = document.getElementById(sectionId);
    if (targetSection) {
        targetSection.style.display = 'block';
    }
    this.classList.add('active');

    // Section Content Routers
    switch (sectionId) {
        case 'results':
            loadResults();
            break;
        case 'payments':
            loadFees();
            loadPaymentHistory();  // ✅ LOAD PAYMENT HISTORY
            break;
        case 'courses':
            loadCourses();
            loadAvailableCoursesCatalog();
            break;
        case 'documents':
            loadDocumentsList();  // ✅ LOAD DOCUMENTS
            break;
    }
}

// Loads background summary data directly onto the main summary card
function loadProfileData(student) {
    const profileInfo = document.getElementById('profileInfo');
    if (!profileInfo) return;

    profileInfo.innerHTML = `
        <table class="info-table">
            <tr><td>Full Name:</td><td>${escapeHtml(student.firstName)} ${escapeHtml(student.lastName || '')}</td></tr>
            <tr><td>Email Check:</td><td>${escapeHtml(student.email)}</td></tr>
            <tr><td>Account Type:</td><td><span class="status-badge">Student</span></td></tr>
            <tr><td>Registration ID:</td><td><strong>${escapeHtml(student.regNumber)}</strong></td></tr>
            <tr><td>Prog. Track:</td><td>${escapeHtml(student.programme || 'N/A')}</td></tr>
        </table>
    `;
}
function updateGreeting() {

    const hour = new Date().getHours();

    let greeting = "";

    if (hour >= 5 && hour < 12) {
        greeting = "Good Morning";
    }
    else if (hour >= 12 && hour < 17) {
        greeting = "Good Afternoon";
    }
    else if (hour >= 17 && hour < 24) {
        greeting = "Good Evening";
    }
    else {
        // 0:00–4:59 — the only remaining gap
        greeting = "Good Morning";
    }

    document.getElementById("welcomeGreeting").textContent = greeting;
}
function updateTodayDate() {

    const today = new Date();

    const options = {
        weekday: "long",
        day: "numeric",
        month: "long",
        year: "numeric"
    };

    document.getElementById("todayDate").innerHTML =
        `<i class="bi bi-calendar-event"></i> ${today.toLocaleDateString("en-UG", options)}`;

}


async function loadDashboardCounters(regNumber) {
    // A. Fetch Courses Length
    try {
        const response = await fetch(`${API_URL}/student/courses/${encodeURIComponent(regNumber)}`, { headers: getAuthHeaders() });
        if (response.ok) {
            const res = await response.json();
            const courseCount = Array.isArray(res) ? res.length : (res.data ? res.data.length : 0);
            document.getElementById('enrolledCoursesCount').innerText = courseCount;
        }
    } catch (e) { console.error("Counter load fault:", e); }

    // B. Fetch Fees Summary
    try {
        const response = await fetch(`${API_URL}/student/fees/${encodeURIComponent(regNumber)}`, { headers: getAuthHeaders() });
        if (response.ok) {
            const res = await response.json();
            const bal = res.totalShortfall || (res.data?.totalShortfall) || 0;
            document.getElementById('outstandingBalanceValue').innerText = bal > 0 ? `UGX ${Number(bal).toLocaleString()}` : "UGX 0";
        }
    } catch (e) { console.error("Counter load fault:", e); }

    // C. Fetch GPA
    
    updateCGPA(regNumber);
}

async function loadResults() {
    const student = JSON.parse(localStorage.getItem('student'));
    const resultsList = document.getElementById('resultsList');
    if (!resultsList) return;
    if (!student) { redirectToLogin(); return; }

    try {
        const response = await fetch(`${API_URL}/gpa/${encodeURIComponent(student.regNumber)}`,
            { headers: getAuthHeaders() });

        if (response.status === 401 || response.status === 403) {
            redirectToLogin();
            return;
        }

        let data = {};
        if (response.headers.get("content-length") !== "0") {
            data = await response.json();

            const dashboardGpa = document.getElementById("currentGpaValue");
            if (dashboardGpa && data.cgpa != null) {
                dashboardGpa.textContent = Number(data.cgpa).toFixed(2);
            }
        }

        if (data && data.transcript && data.transcript.length > 0) {
            const standingStyle = (standing) => {
                switch (standing) {
                    case 'NORMAL PROGRESS': return { bg: '#d4edda', color: '#155724' };
                    case 'RETAKE REQUIRED': return { bg: '#f8d7da', color: '#721c24' };
                    case 'RETAKEN – CLEARED': return { bg: '#cce5ff', color: '#004085' };
                    case 'RETAKEN – STILL FAILED': return { bg: '#f5c6cb', color: '#611a1f' };
                    default: return { bg: '#eee', color: '#333' };
                }
            };

            let gpaHtml = `
                <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 20px; margin-bottom: 40px;">
                    <div style="background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 30px; border-radius: 10px; color: white; box-shadow: 0 8px 16px rgba(102, 126, 234, 0.4);">
                        <p style="margin: 0 0 10px 0; font-size: 14px; opacity: 0.9;">CUMULATIVE GPA</p>
                        <h2 style="margin: 0 0 10px 0; font-size: 48px; font-weight: bold;">${data.cgpa.toFixed(2)}</h2>
                        <p style="margin: 0; font-size: 12px; opacity: 0.8; border-top: 1px solid rgba(255,255,255,0.3); padding-top: 10px;">${data.degreeClass}</p>
                    </div>

                    <div style="background: linear-gradient(135deg, #ff9966 0%, #ff5e62 100%); padding: 30px; border-radius: 10px; color: white; box-shadow: 0 8px 16px rgba(255, 94, 98, 0.4);">
                        <p style="margin: 0 0 10px 0; font-size: 14px; opacity: 0.9;">CURRENT SEMESTER GPA</p>
                        <h2 style="margin: 0 0 10px 0; font-size: 48px; font-weight: bold;">${data.currentSemesterGpa ? data.currentSemesterGpa.gpa.toFixed(2) : '—'}</h2>
                        <p style="margin: 0; font-size: 12px; opacity: 0.8; border-top: 1px solid rgba(255,255,255,0.3); padding-top: 10px;">Year ${data.currentYear} · Semester ${data.currentSemester}</p>
                    </div>

                    <div style="background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); padding: 30px; border-radius: 10px; color: white; box-shadow: 0 8px 16px rgba(245, 87, 108, 0.4);">
                        <p style="margin: 0 0 10px 0; font-size: 14px; opacity: 0.9;">TOTAL CREDITS</p>
                        <h2 style="margin: 0 0 10px 0; font-size: 48px; font-weight: bold;">${data.totalCreditsEarned}</h2>
                        <p style="margin: 0; font-size: 12px; opacity: 0.8;">Credits Earned</p>
                    </div>

                    <div style="background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%); padding: 30px; border-radius: 10px; color: white; box-shadow: 0 8px 16px rgba(79, 172, 254, 0.4);">
                        <p style="margin: 0 0 10px 0; font-size: 14px; opacity: 0.9;">TOTAL COURSES</p>
                        <h2 style="margin: 0 0 10px 0; font-size: 48px; font-weight: bold;">${data.transcript.length}</h2>
                        <p style="margin: 0; font-size: 12px; opacity: 0.8;">Courses Completed</p>
                    </div>
                </div>

               <h3 style="color: var(--primary-color); margin: 40px 0 20px 0; border-bottom: 2px solid var(--secondary-color); padding-bottom: 10px;"><i class="bi bi-bar-chart-line"></i> Semester-Wise GPA</h3>
                <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 15px; margin-bottom: 40px;">
                    ${data.semesterGpas.map(sem => `
                        <div style="background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); border-left: 4px solid ${sem.gpa >= 3.5 ? '#27ae60' : sem.gpa >= 2.0 ? '#f39c12' : '#e74c3c'}; position: relative;">
                            ${sem.isCurrent ? '<span style="position:absolute; top:10px; right:10px; background:#ff5e62; color:white; font-size:10px; padding:2px 8px; border-radius:10px; font-weight:bold;">CURRENT</span>' : ''}
                            <p style="margin: 0 0 5px 0; font-weight: bold; color: var(--primary-color);">📚 Year ${sem.year} · Semester ${sem.semester}</p>
                            <p style="margin: 5px 0; font-size: 14px; color: #666;">GPA: <strong style="font-size: 20px; color: ${sem.gpa >= 3.5 ? '#27ae60' : sem.gpa >= 2.0 ? '#f39c12' : '#e74c3c'};">${sem.gpa.toFixed(2)}</strong></p>
                            <p style="margin: 5px 0; font-size: 12px; color: #999;">Courses: ${sem.courses} | Credits: ${sem.creditsEarned}</p>
                        </div>
                    `).join('')}
                </div>
<h3 style="color: var(--primary-color); margin: 40px 0 20px 0; border-bottom: 2px solid var(--secondary-color); padding-bottom: 10px;"><i class="bi bi-clipboard-data"></i> Academic Transcript</h3>
                <div style="overflow-x: auto;">
                    <table style="width: 100%; border-collapse: collapse; background: white; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.05);">
                        <thead>
                            <tr style="background: var(--primary-color); color: white;">
                                <th style="padding: 15px; text-align: left; font-weight: 600;">Yr/Sem</th>
                                <th style="padding: 15px; text-align: left; font-weight: 600;">Course Code</th>
                                <th style="padding: 15px; text-align: left; font-weight: 600;">Course Name</th>
                                <th style="padding: 15px; text-align: center; font-weight: 600;">Credits</th>
                                <th style="padding: 15px; text-align: center; font-weight: 600;">Test</th>
                                <th style="padding: 15px; text-align: center; font-weight: 600;">Coursework</th>
                                <th style="padding: 15px; text-align: center; font-weight: 600;">Final Exam</th>
                                <th style="padding: 15px; text-align: center; font-weight: 600;">Average</th>
                                <th style="padding: 15px; text-align: center; font-weight: 600;">Grade</th>
                                <th style="padding: 15px; text-align: center; font-weight: 600;">Points</th>
                                <th style="padding: 15px; text-align: center; font-weight: 600;">Standing</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${data.transcript.map(course => {
                const st = standingStyle(course.academicStanding);
                return `
                                <tr style="border-bottom: 1px solid #eee;">
                                    <td style="padding: 12px 15px; white-space: nowrap;">Y${course.year}/S${course.semester}</td>
                                    <td style="padding: 12px 15px;"><strong>${escapeHtml(course.courseCode)}</strong></td>
                                    <td style="padding: 12px 15px;">${escapeHtml(course.courseName)}</td>
                                    <td style="padding: 12px 15px; text-align: center;">${course.creditHours}</td>
                                    <td style="padding: 12px 15px; text-align: center;">${course.testMark != null ? course.testMark.toFixed(1) : '—'}<div style="font-size:11px; color:#999;">${course.testWeight}%</div></td>
                                    <td style="padding: 12px 15px; text-align: center;">${course.courseworkMark != null ? course.courseworkMark.toFixed(1) : '—'}<div style="font-size:11px; color:#999;">${course.courseworkWeight}%</div></td>
                                    <td style="padding: 12px 15px; text-align: center;">${course.finalExamMark != null ? course.finalExamMark.toFixed(1) : '—'}<div style="font-size:11px; color:#999;">${course.finalExamWeight}%</div></td>
                                    <td style="padding: 12px 15px; text-align: center; font-weight: bold;">${course.mark != null ? course.mark.toFixed(1) : 'N/A'}</td>
                                    <td style="padding: 12px 15px; text-align: center;">
                                        <span style="display: inline-block; background: ${course.grade === 'A' ? '#27ae60' : course.grade === 'B+' || course.grade === 'B' ? '#3498db' : course.grade === 'C+' || course.grade === 'C' ? '#f39c12' : course.grade === 'D+' || course.grade === 'D' ? '#e67e22' : '#e74c3c'}; color: white; padding: 4px 10px; border-radius: 4px; font-weight: bold; font-size: 12px;">
                                            ${escapeHtml(course.grade)}
                                        </span>
                                    </td>
                                    <td style="padding: 12px 15px; text-align: center; font-weight: bold;">${course.gradePoint.toFixed(1)}</td>
                                    <td style="padding: 12px 15px; text-align: center;">
                                        <span style="display:inline-block; background:${st.bg}; color:${st.color}; padding:4px 8px; border-radius:4px; font-size:11px; font-weight:bold; white-space:nowrap;">${course.academicStanding}</span>
                                        ${course.isRetake ? `<div style="font-size:10px; color:#999; margin-top:3px;">Original: ${course.originalMark != null ? course.originalMark.toFixed(1) : 'N/A'} (${course.originalGrade})</div>` : ''}
                                    </td>
                                </tr>
                            `}).join('')}
                        </tbody>
                    </table>
                </div>

<h3 style="color: var(--primary-color); margin: 40px 0 20px 0; border-bottom: 2px solid var(--secondary-color); padding-bottom: 10px;"><i class="bi bi-book"></i> Grade Scale Reference</h3>
                <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 15px;">
                    <div style="background: #d4edda; padding: 15px; border-radius: 8px; border-left: 4px solid #27ae60;">
                        <p style="margin: 0; font-size: 12px; color: #155724;"><strong>A</strong> - 80+ marks = 5.0 points</p>
                        <p style="margin: 5px 0 0 0; font-size: 12px; color: #155724;"><strong>B+</strong> - 75-79 marks = 4.5 points</p>
                    </div>
                    <div style="background: #cce5ff; padding: 15px; border-radius: 8px; border-left: 4px solid #3498db;">
                        <p style="margin: 0; font-size: 12px; color: #004085;"><strong>B</strong> - 70-74 marks = 4.0 points</p>
                        <p style="margin: 5px 0 0 0; font-size: 12px; color: #004085;"><strong>C+</strong> - 65-69 marks = 3.5 points</p>
                    </div>
                    <div style="background: #fff3cd; padding: 15px; border-radius: 8px; border-left: 4px solid #f39c12;">
                        <p style="margin: 0; font-size: 12px; color: #856404;"><strong>C</strong> - 60-64 marks = 3.0 points</p>
                        <p style="margin: 5px 0 0 0; font-size: 12px; color: #856404;"><strong>D+</strong> - 55-59 marks = 2.5 points</p>
                    </div>
                    <div style="background: #f8d7da; padding: 15px; border-radius: 8px; border-left: 4px solid #e74c3c;">
                        <p style="margin: 0; font-size: 12px; color: #721c24;"><strong>D</strong> - 50-54 marks = 2.0 points</p>
                        <p style="margin: 5px 0 0 0; font-size: 12px; color: #721c24;"><strong>F</strong> - Below 50 marks = 0.0 points</p>
                    </div>
                </div>
            `;

            resultsList.innerHTML = gpaHtml;
        } else {
            resultsList.innerHTML = `
                <div style="background: #f0f0f0; padding: 40px; border-radius: 8px; text-align: center;">
                    <i class="bi bi-bar-chart" style="font-size: 2.5rem; color: #bbb; display: block; margin-bottom: 12px;"></i>
                    <p style="color: #666; font-size: 18px; margin: 0;">No grades published yet</p>
                    <p style="color: #999; font-size: 14px; margin: 10px 0 0 0;">Your transcript will appear here once results are approved and you meet fee clearance requirements</p>
                </div>
            `;
        }
    } catch (error) {
        console.error('Error loading results:', error);
        resultsList.innerHTML = '<p style="color:red;">Error loading results.</p>';
    }
}
async function updateCGPA(regNumber) {

    try {

        const response = await fetch(
            `${API_URL}/gpa/${encodeURIComponent(regNumber)}`,
            { headers: getAuthHeaders() }
        );

        if (!response.ok) {
            return;
        }

        const data = await response.json();


        const gpaElement =
            document.getElementById("currentGpaValue");

        if (!gpaElement)
            return;

        if (data.cgpa != null) {
            gpaElement.textContent =
                Number(data.cgpa).toFixed(2);
        }
        else {
            gpaElement.textContent = "-";
        }

    }
    catch (err) {
        console.error("Failed to load GPA", err);
    }

}



async function loadFees() {
    const student = JSON.parse(localStorage.getItem('student'));
    const feesInfo = document.getElementById('feesInfo');
    if (!feesInfo) return;

    try {
        const response = await fetch(`${API_URL}/student/fees/${encodeURIComponent(student.regNumber)}`, { headers: getAuthHeaders() });
        if (response.status === 401 || response.status === 403) { redirectToLogin(); return; }
        let data = {};

        if (response.headers.get("content-length") !== "0") {
            data = await response.json();
        }

        const tuitionShortfall = data.tuitionShortfall || 0;
        const functionalShortfall = data.functionalShortfall || 0;
        const totalShortfall = data.totalShortfall || 0;
        const tuitionPaid = data.tuitionPaid || 0;
        const functionalPaid = data.functionalPaid || 0;
        const totalPaid = data.totalPaid || 0;
        const tuitionPercentage = data.tuitionPercentagePaid || 0;
        const functionalPercentage = data.functionalPercentagePaid || 0;
        const canEnroll = data.canEnroll;

        feesInfo.innerHTML = `
            <div style="background:white; padding:20px; border-radius:8px; margin-bottom:15px;">
                <h4 style="color:var(--primary-color); margin-top:0;">📚 Tuition Fees</h4>
                <div style="display:flex; justify-content:space-between; margin-bottom:10px;">
                    <span>Paid: <strong>UGX ${Number(tuitionPaid).toLocaleString()}</strong></span>
                    <span style="color:${tuitionShortfall > 0 ? '#e74c3c' : '#2ecc71'}; font-weight:bold;">
                        ${tuitionShortfall > 0 ? `Due: UGX ${Number(tuitionShortfall).toLocaleString()}` : '✅ Paid in Full'}
                    </span>
                </div>
                <div style="background:#f0f0f0; border-radius:5px; height:20px; overflow:hidden;">
                    <div style="background:var(--primary-color); height:100%; width:${tuitionPercentage}%; display:flex; align-items:center; justify-content:center; color:white; font-size:12px; font-weight:bold;">
                        ${tuitionPercentage.toFixed(0)}%
                    </div>
                </div>
            </div>

            <div style="background:white; padding:20px; border-radius:8px; margin-bottom:15px;">
                <h4 style="color:var(--primary-color); margin-top:0;">💰 Functional Fees</h4>
                <div style="display:flex; justify-content:space-between; margin-bottom:10px;">
                    <span>Paid: <strong>UGX ${Number(functionalPaid).toLocaleString()}</strong></span>
                    <span style="color:${functionalShortfall > 0 ? '#e74c3c' : '#2ecc71'}; font-weight:bold;">
                        ${functionalShortfall > 0 ? `Due: UGX ${Number(functionalShortfall).toLocaleString()}` : '✅ Paid in Full'}
                    </span>
                </div>
                <div style="background:#f0f0f0; border-radius:5px; height:20px; overflow:hidden;">
                    <div style="background:#27ae60; height:100%; width:${functionalPercentage}%; display:flex; align-items:center; justify-content:center; color:white; font-size:12px; font-weight:bold;">
                        ${functionalPercentage.toFixed(0)}%
                    </div>
                </div>
            </div>

            <div style="background:${canEnroll ? '#d4edda' : '#f8d7da'}; padding:20px; border-radius:8px; border-left:5px solid ${canEnroll ? '#2ecc71' : '#e74c3c'};">
                <strong>${canEnroll ? '✅ Enrollment Ready' : '❌ Cannot Enroll Yet'}</strong>
                <p style="margin:10px 0 0 0; font-size:14px;">
                    ${data.enrollmentStatusMessage || (canEnroll ? 'You have paid the minimum required amount' : 'Pay at least 50% of tuition and 50% of functional fees')}
                </p>
            </div>
        `;

        const payFeesBtn = document.getElementById('payFeesBtn');
        if (payFeesBtn && !payFeesBtn.dataset.listenerAttached) {
            payFeesBtn.addEventListener('click', function () {
                const form = document.getElementById('paymentForm');
                if (form) form.style.display = form.style.display === 'none' ? 'block' : 'none';
            });
            payFeesBtn.dataset.listenerAttached = "true";
        }
    } catch (error) {
        console.error('Error loading fees:', error);
    }
}

// ✅ NEW FUNCTION: Load Payment History
async function loadPaymentHistory() {
    const student = JSON.parse(localStorage.getItem('student'));
    const historyDiv = document.getElementById('paymentHistoryDiv');
    if (!historyDiv) return;

    try {
        const response = await fetch(`${API_URL}/payment/history/${encodeURIComponent(student.regNumber)}`,
            { headers: getAuthHeaders() });

        if (response.ok) {
            const data = await response.json();

            if (data.paymentHistory && data.paymentHistory.length > 0) {
                historyDiv.innerHTML = `
                    <table class="payment-history-table">
                        <thead>
                            <tr>
                                <th>Date</th>
                                <th>Amount (UGX)</th>
                                <th>Category</th>
                                <th>Method</th>
                                <th>Status</th>
                                <th>Receipt</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${data.paymentHistory.map(p => `
                                <tr>
                                    <td>${new Date(p.paymentDate).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' })}</td>
                                    <td><strong>UGX ${Number(p.amount).toLocaleString()}</strong></td>
                                   <td>
                                        <span class="badge-${p.paymentCategory === 'TUITION' ? 'tuition' : 'functional'}">
                                            <i class="bi bi-${p.paymentCategory === 'TUITION' ? 'journal-bookmark' : 'cash-stack'}"></i> ${p.paymentCategory}
                                        </span>
                                    </td>
                                    <td>${p.paymentMethod || 'N/A'}</td>
                                    <td>${renderPaymentStatus(p)}</td>
                                    <td>
                                       ${p.status === 'Approved' && p.receiptPath ? `
                                            <a href="${p.receiptPath}" target="_blank" class="download-link" download><i class="bi bi-download"></i> Download</a>
                                        ` : p.status === 'Pending' ? '<span style="color:#f39c12;"><i class="bi bi-hourglass-split"></i> Awaiting bursar review</span>'
                                    : p.status === 'Rejected' ? '<span style="color:#999;">—</span>'
                                        : '<span style="color:#999;"><i class="bi bi-hourglass-split"></i> Generating</span>'}
                                    </td>
                                </tr>
                            `).join('')}
                        </tbody>
                    </table>
                `;
            } else {
                historyDiv.innerHTML = `
                    <div style="background: #f0f0f0; padding: 30px; border-radius: 8px; text-align: center;">
                        <i class="bi bi-credit-card" style="font-size: 2rem; color: #bbb; display:block; margin-bottom: 10px;"></i>
                        <p style="color: #666; font-size: 16px;">No payments recorded yet</p>
                        <p style="color: #999; font-size: 14px; margin-top: 10px;">Make your first payment to get started</p>
                    </div>
                `;
            }
        }
    } catch (error) {
        console.error('Error loading payment history:', error);
        historyDiv.innerHTML = '<p style="color: red;">Error loading payment history</p>';
    }
}

function renderPaymentStatus(p) {
    if (p.status === 'Approved') return '<span style="color: #2ecc71; font-weight: bold;"><i class="bi bi-check-circle-fill"></i> Approved</span>';
    if (p.status === 'Pending') return '<span style="color: #f39c12; font-weight: bold;"><i class="bi bi-hourglass-split"></i> Pending Verification</span>';
    if (p.status === 'Rejected') {
        return `<span style="color: #e74c3c; font-weight: bold;"><i class="bi bi-x-circle-fill"></i> Rejected${p.rejectionReason ? ' — ' + escapeHtml(p.rejectionReason) : ''}</span>`;
    }
    return escapeHtml(p.status || 'Unknown');
}
function getAuthHeadersNoContentType() {
    // Multipart uploads must NOT set Content-Type manually — the browser
    // needs to generate its own boundary string. getAuthHeaders() always
    // sets application/json, which would break the file upload silently.
    const token = localStorage.getItem('token');
    return { 'Authorization': `Bearer ${token}` };
}

async function submitPayment() {
    const student = JSON.parse(localStorage.getItem('student'));
    const amountEl = document.getElementById('amount');
    const paymentMethodEl = document.getElementById('paymentMethod');
    const paymentCategoryEl = document.getElementById('paymentCategory');
    const paymentSemesterEl = document.getElementById('paymentSemester');
    const proofFileEl = document.getElementById('paymentProofFile');

    if (!amountEl || !paymentMethodEl || !paymentCategoryEl || !paymentSemesterEl || !proofFileEl) return;

    const amount = parseFloat(amountEl.value);
    const paymentMethod = paymentMethodEl.value;
    const paymentCategory = paymentCategoryEl.value;
    const semesterId = parseInt(paymentSemesterEl.value);
    const proofFile = proofFileEl.files[0];

    if (!amount || amount <= 0) { alert('Please enter a valid amount.'); return; }
    if (!paymentCategory) { alert('Please select a payment category.'); return; }
    if (!semesterId || isNaN(semesterId)) { alert('Please select a semester for this payment.'); return; }
    if (!proofFile) { alert('Please attach proof of payment (a photo or PDF of your bank slip or mobile money receipt).'); return; }

    const submitBtn = document.getElementById('submitPaymentBtn');
    submitBtn.disabled = true;
    submitBtn.textContent = 'Uploading proof…';

    try {
        // Step 1: upload the proof file
        const formData = new FormData();
        formData.append('file', proofFile);

        const uploadResponse = await fetch(`${API_URL}/payment/upload-proof`, {
            method: 'POST',
            headers: getAuthHeadersNoContentType(),
            body: formData
        });

        if (uploadResponse.status === 401 || uploadResponse.status === 403) { redirectToLogin(); return; }

        const uploadData = await uploadResponse.json();
        if (!uploadResponse.ok || !uploadData.success) {
            alert(uploadData.message || 'Failed to upload proof of payment.');
            return;
        }

        submitBtn.textContent = 'Submitting for verification…';

        // Step 2: submit as Pending — no balance change or receipt until a bursar approves
        const response = await fetch(`${API_URL}/payment/submit-proof`, {
            method: 'POST',
            headers: getAuthHeaders(),
            body: JSON.stringify({
                studentRegNumber: student.regNumber,
                amount: amount,
                paymentMethod: paymentMethod,
                paymentCategory: paymentCategory,
                semesterId: semesterId,
                paymentProofPath: uploadData.paymentProofPath
            })
        });

        if (response.status === 401 || response.status === 403) { redirectToLogin(); return; }
        let data = {};
        if (response.headers.get("content-length") !== "0") {
            data = await response.json();
        }

        if (data.success || response.ok) {
            alert('✅ Payment proof submitted. A bursar will review it — your receipt and enrollment update will follow once approved.');
            amountEl.value = '';
            paymentCategoryEl.value = '';
            paymentMethodEl.value = '';
            proofFileEl.value = '';
            document.getElementById('paymentForm').style.display = 'none';
            loadFees();
            loadPaymentHistory();
            loadDashboardCounters(student.regNumber);
        } else {
            alert(data.message || 'Error submitting payment proof.');
        }
    } catch (error) {
        console.error('Error submitting payment proof:', error);
        alert('Server error occurred while submitting payment proof.');
    } finally {
        submitBtn.disabled = false;
        submitBtn.textContent = 'Submit for Verification';
    }
}

async function loadCourses() {
    const student = JSON.parse(localStorage.getItem('student'));
    const coursesList = document.getElementById('coursesList');
    if (!coursesList) return;

    try {
        const response = await fetch(`${API_URL}/student/courses/${encodeURIComponent(student.regNumber)}`, { headers: getAuthHeaders() });
        if (response.status === 401 || response.status === 403) { redirectToLogin(); return; }
        let data = {};

        if (response.headers.get("content-length") !== "0") {
            data = await response.json();
        }

        if (data && data.length > 0) {
            coursesList.innerHTML = data.map(enrollment => `
                <div class="course-item">
                    <p style="font-size:18px; color:var(--primary-color);"><strong>${escapeHtml(enrollment.courseName)}</strong></p>
                    <p style="margin:5px 0; color:#555;">Course Code: <strong>${escapeHtml(enrollment.courseCode)}</strong> | Credit Hours: ${enrollment.creditHours || 3}</p>
                    <p>Status: <span class="status-badge" style="background:${enrollment.status === 'ENROLLED' ? '#2ecc71' : '#f1c40f'}">${escapeHtml(enrollment.status)}</span></p>
                </div>
            `).join('');
        } else {
            coursesList.innerHTML = '<p>No courses enrolled for the current semester.</p>';
        }
    } catch (error) {
        console.error('Error loading courses:', error);
        coursesList.innerHTML = '<p style="color:red;">Failed to load courses.</p>';
    }
}

async function loadAvailableCoursesCatalog() {
    const container = document.getElementById('availableCoursesContainer');
    if (!container) return;

    try {
        const student = JSON.parse(localStorage.getItem('student'));

        const response = await fetch(
            `${API_URL}/student/available-courses/${encodeURIComponent(student.regNumber)}`,
            { headers: getAuthHeaders() }
        );
        if (!response.ok) {
            console.error('Failed to load courses:', response.statusText);
            container.innerHTML = '<p style="color:red;">Failed to load courses.</p>';
            return;
        }

        const result = await response.json();

        if (result.courses && result.courses.length > 0) {
            container.innerHTML = result.courses.map(course => `
                <label style="display:flex; align-items:center; gap:8px; font-weight:normal; padding:6px 0; border-bottom:1px solid #f0f0f0;">
                    <input type="checkbox" class="course-checkbox" value="${course.courseId}">
                    <span>${escapeHtml(course.courseName)} (${escapeHtml(course.courseCode)}) - ${course.creditHours || 3} CU${course.isRetake ? ' — <strong style="color:#e74c3c;">RETAKE</strong>' : ''}</span>
                </label>
            `).join('');

            // Wire "Select All" once the checkboxes exist
            const selectAll = document.getElementById('selectAllCourses');
            if (selectAll) {
                selectAll.checked = false;
                selectAll.onchange = function () {
                    document.querySelectorAll('.course-checkbox').forEach(cb => cb.checked = selectAll.checked);
                };
            }
        } else {
            container.innerHTML = '<p style="color:#666;">No available courses to register for right now.</p>';
        }
    } catch (error) {
        console.error('Failed to load course catalog:', error);
        container.innerHTML = '<p style="color:red;">Error loading courses.</p>';
    }
}

// ✅ NEW FUNCTION: Load Documents List - FIXED ENDPOINT
async function loadDocumentsList() {
    const student = JSON.parse(localStorage.getItem('student'));
    const docsDiv = document.getElementById('documentsListDiv');
    if (!docsDiv) return;

    try {
        const response = await fetch(`${API_URL}/student/documents/${encodeURIComponent(student.regNumber)}`,
            { headers: getAuthHeaders() });

        if (response.ok) {
            const documents = await response.json();

            if (documents && documents.length > 0) {
                docsDiv.innerHTML = `
                    <div style="display: grid; grid-template-columns: 1fr; gap: 15px;">
                        ${documents.map(doc => `
                            <div class="document-item" style="border-left-color: #e74c3c;">
                                <div style="display: flex; justify-content: space-between; align-items: center;">
                                    <div>
                                        <p style="margin: 0 0 5px 0; font-weight: bold; font-size: 16px; color: var(--primary-color);">
                                            ${doc.documentName}
                                        </p>
                                        <p style="margin: 5px 0; font-size: 12px; color: #666;">
                                            Generated: ${new Date(doc.createdAt).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })}
                                        </p>
                                    </div>
                                   <a href="${doc.documentPath}" target="_blank" download class="btn btn-secondary" style="white-space: nowrap;">
                                        <i class="bi bi-download"></i> Download PDF
                                    </a>
                                </div>
                            </div>
                        `).join('')}
                    </div>
                `;
            } else {
                docsDiv.innerHTML = `
                    <div style="background: #f0f0f0; padding: 30px; border-radius: 8px; text-align: center;">
                        <i class="bi bi-file-earmark-text" style="font-size: 2rem; color: #bbb; display:block; margin-bottom: 10px;"></i>
                        <p style="color: #666; font-size: 16px;">No documents generated yet</p>
                        <p style="color: #999; font-size: 14px; margin-top: 10px;">Enroll in courses to generate your semester registration card and exam permit</p>
                    </div>
                `;
            }
        } else if (response.status === 401 || response.status === 403) {
            redirectToLogin();
        } else {
            docsDiv.innerHTML = '<p style="color: red;">Error loading documents</p>';
        }
    } catch (error) {
        console.error('Error loading documents:', error);
        docsDiv.innerHTML = '<p style="color: red;">Error loading documents</p>';
    }
}

function logout() {
    if (confirm("Confirm logout from MIU Student Portal?")) {
        redirectToLogin();
    }
}

function escapeHtml(str) {
    if (!str) return '';
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

function loadStudentPhoto(student) {

    const photoElement =
        document.getElementById('studentProfilePhoto');

    if (!photoElement) return;


    let photoPath = null;


    if (student.passportPhoto) {
        photoPath = student.passportPhoto;
    }
    else if (student.profilePhoto) {
        photoPath = student.profilePhoto;
    }
    else if (student.photoPath) {
        photoPath = student.photoPath;
    }


    if (photoPath) {

        photoElement.src =
            photoPath.startsWith('http')
                ? photoPath
                : `${API_URL.replace('/api', '')}${photoPath}`;

    }
    else {

        photoElement.src =
            "/images/default-profile.png";

    }


    photoElement.onerror = function () {

        console.warn("Student photo not found:", this.src);

        this.src =
            "/images/default-profile.png";

    };

}