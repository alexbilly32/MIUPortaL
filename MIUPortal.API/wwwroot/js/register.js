const API_URL = 'https://localhost:44366/api';

let formData = {};

document.addEventListener('DOMContentLoaded', function () {

    // =====================================================
    // PASSWORD STRENGTH CHECK
    // =====================================================
    const passwordInput = document.getElementById('password');

    if (passwordInput) {
        passwordInput.addEventListener('input', checkPasswordStrength);
    }


    // =====================================================
    // PAYMENT FILE UPLOAD
    // =====================================================
    setupFileUpload();


    // =====================================================
    // PASSPORT PHOTO UPLOAD
    // =====================================================
    setupPassportPhoto();

    // =====================================================
    // APPLICATION CATEGORY / LOAN PROOF TOGGLE
    // =====================================================
    setupApplicationCategory();

    // =====================================================
    // IDENTIFICATION TYPE SWITCH
    // =====================================================
    setupIdentificationType();


    // =====================================================
    // INITIALIZE NATIONAL ID VIEW
    // =====================================================
    toggleIdentificationFields();

    // Populate nationality list
    populateNationalities();

    setupNavigationButtons();

});

// =========================================================
// FILE UPLOAD WITH DRAG AND DROP
// =========================================================

function setupFileUpload() {
    const paymentProofInput = document.getElementById('paymentProof');
    const filePreview = document.getElementById('filePreview');
    const fileUpload = document.getElementById('fileUpload');

    // Safety check
    if (!paymentProofInput || !fileUpload || !filePreview) {
        console.error('Upload elements not found');
        return;
    }

    // =====================================================
    // CLICK TO OPEN FILE PICKER
    // =====================================================
    fileUpload.addEventListener('click', () => {
        paymentProofInput.click();
    });

    // =====================================================
    // FILE SELECT
    // =====================================================
    paymentProofInput.addEventListener('change', handleFileSelect);

    // =====================================================
    // DRAG OVER
    // =====================================================
    fileUpload.addEventListener('dragover', (e) => {
        e.preventDefault();
        e.stopPropagation();
        fileUpload.classList.add('dragover');
    });

    // =====================================================
    // DRAG LEAVE
    // =====================================================
    fileUpload.addEventListener('dragleave', (e) => {
        e.preventDefault();
        e.stopPropagation();
        fileUpload.classList.remove('dragover');
    });

    // =====================================================
    // DROP FILE
    // =====================================================
    fileUpload.addEventListener('drop', (e) => {
        e.preventDefault();
        e.stopPropagation();
        fileUpload.classList.remove('dragover');

        const files = e.dataTransfer.files;
        if (files.length > 0) {
            paymentProofInput.files = files;
            handleFileSelect();
        }
    });

    console.log('Drag and drop initialized successfully');
}


// =========================================================
// PASSPORT PHOTO UPLOAD
// =========================================================

function setupPassportPhoto() {

    const photoInput = document.getElementById('passportPhoto');
    const photoPreview = document.getElementById('photoPreview');


    if (!photoInput || !photoPreview) {
        console.log("Passport photo controls not found");
        return;
    }


    photoInput.addEventListener('change', function () {

        const file = this.files[0];


        if (!file) {
            photoPreview.style.display = "none";
            return;
        }


        // Validate file type
        const allowedTypes = [
            "image/jpeg",
            "image/png"
        ];


        if (!allowedTypes.includes(file.type)) {

            showMessage(
                "❌ Only JPG and PNG passport photos are allowed",
                "error"
            );

            photoInput.value = "";
            photoPreview.style.display = "none";

            return;
        }


        // Validate size (2MB)
        const maxSize = 2 * 1024 * 1024;


        if (file.size > maxSize) {

            showMessage(
                "❌ Passport photo must not exceed 2MB",
                "error"
            );

            photoInput.value = "";
            photoPreview.style.display = "none";

            return;
        }


        // Display preview
        const reader = new FileReader();


        reader.onload = function (e) {

            photoPreview.src = e.target.result;
            photoPreview.style.display = "inline-block";

        };


        reader.readAsDataURL(file);


        console.log(
            "Passport photo selected:",
            file.name
        );

    });

}


// =========================================================
// IDENTIFICATION TYPE SWITCHING
// =========================================================

function setupIdentificationType() {

    const identificationType =
        document.getElementById('identificationType');


    if (!identificationType) {
        console.log("Identification type control not found");
        return;
    }


    identificationType.addEventListener(
        'change',
        toggleIdentificationFields
    );

}

function setupApplicationCategory() {
    const select = document.getElementById('applicationCategory');
    const loanSection = document.getElementById('loanProofSection');
    const loanFileInput = document.getElementById('loanProofFile');

    if (!select || !loanSection) {
        console.log("Application category controls not found");
        return;
    }

    select.addEventListener('change', function () {
        if (this.value === 'GovernmentLoan') {
            loanSection.style.display = 'block';
            loanFileInput.setAttribute('required', 'required');
        } else {
            loanSection.style.display = 'none';
            loanFileInput.removeAttribute('required');
            loanFileInput.value = '';
        }
    });
}



// =========================================================
// SHOW / HIDE NATIONAL ID AND PASSPORT
// =========================================================

function toggleIdentificationFields() {


    const identificationType =
        document.getElementById('identificationType');


    const nationalId =
        document.getElementById('nationalId');


    const passportNumber =
        document.getElementById('passportNumber');



    if (!identificationType ||
        !nationalId ||
        !passportNumber) {

        return;

    }



    if (identificationType.value === "PASSPORT") {


        // Hide National ID
        nationalId.style.display = "none";
        nationalId.value = "";


        // Show Passport
        passportNumber.style.display = "block";


        nationalId.removeAttribute("required");
        passportNumber.setAttribute("required", "required");


    }

    else {


        // Show National ID
        nationalId.style.display = "block";


        // Hide Passport
        passportNumber.style.display = "none";
        passportNumber.value = "";


        passportNumber.removeAttribute("required");
        nationalId.setAttribute("required", "required");

    }

}

// =========================================================
// POPULATE NATIONALITY DROPDOWN
// Uses the COUNTRIES list (js/countries.js) instead of a
// hardcoded subset of ~20 countries.
// =========================================================

function populateNationalities() {

    const nationalitySelect =
        document.getElementById('nationality');

    if (!nationalitySelect) {
        return;
    }

    if (typeof COUNTRIES === 'undefined') {
        console.error('COUNTRIES list not found — make sure js/countries.js is included before register.js');
        return;
    }

    // Reset to just the placeholder, then rebuild from the full list
    nationalitySelect.innerHTML = '<option value="">Select Nationality</option>';

    COUNTRIES.forEach(country => {

        const option = document.createElement('option');
        option.value = country.name;
        option.textContent = country.name;

        nationalitySelect.appendChild(option);
    });

    // Fallback for anyone whose country genuinely isn't listed
    const otherOption = document.createElement('option');
    otherOption.value = 'Other';
    otherOption.textContent = 'Other';
    nationalitySelect.appendChild(otherOption);
}

// =========================================================
// BACKWARD COMPATIBLE STEP NAVIGATION
// (These match your HTML onclick handlers)
// =========================================================

function goToStep2() { goToStep(2); }
function goToStep1() { goToStep(1); }
function goToStep3() { goToStep(3); }
function goToStep4() { goToStep(4); }

// =========================================================
// MAIN STEP NAVIGATION FUNCTION
// =========================================================

function goToStep(stepNumber) {
    // Validate current step before moving forward
    if (stepNumber === 2 && !validateStep1()) return;
    if (stepNumber === 3 && !validateStep2()) return;
    if (stepNumber === 4 && !validateStep3()) return;

    // Collect form data for the step being left
    if (stepNumber > 1) {
        formData.firstName =
            document.getElementById('firstName')?.value.trim() || '';

        formData.middleName =
            document.getElementById('middleName')?.value.trim() || '';

        formData.lastName =
            document.getElementById('lastName')?.value.trim() || '';

        formData.dateOfBirth =
            document.getElementById('dob')?.value || '';

        formData.gender =
            document.getElementById('gender')?.value || '';

        formData.nationality =
            document.getElementById('nationality')?.value || '';


        // NEW IDENTIFICATION DATA

        formData.identificationType =
            document.getElementById('identificationType')?.value || '';

        formData.nationalId =
            document.getElementById('nationalId')?.value.trim() || '';

        formData.passportNumber =
            document.getElementById('passportNumber')?.value.trim() || '';


        // NEW PASSPORT PHOTO

        const photoInput =
            document.getElementById('passportPhoto');


        if (photoInput && photoInput.files.length > 0) {

            formData.passportPhoto =
                photoInput.files[0];

        }
    }

    if (stepNumber > 2) {
        const programmeSelect = document.getElementById('programme');
        const selectedText = programmeSelect?.options[programmeSelect?.selectedIndex]?.text || '';
        formData.programmeName = selectedText;

        formData.programmeCode =
            programmeSelect?.value || '';

        formData.campusCode =
            document.getElementById('campus')?.value || '';

        formData.intake =
            document.getElementById('intake')?.value || '';

        formData.academicYear =
            document.getElementById('academicYear')?.value || '';

        formData.entrySemester =
            document.getElementById('entrySemester')?.value || '';

        formData.studyMode =
            document.getElementById('studyMode')?.value || '';

        formData.applicationCategory = document.getElementById('applicationCategory')?.value || 'SelfSponsorship';
    }

    if (stepNumber > 3) {
        formData.email = document.getElementById('email')?.value.trim() || '';
        formData.primaryPhone = document.getElementById('primaryPhone')?.value.trim() || '';
        formData.whatsappPhone = document.getElementById('whatsapp')?.value.trim() || '';
        formData.passwordHash = document.getElementById('password')?.value || '';
        formData.campusPreference = "UMC";  // Default to Kampala
    }

    // Update UI
    updateStepIndicators(stepNumber);
    hideAllSteps();
    const stepForm = document.getElementById(`step${stepNumber}Form`);
    if (stepForm) {
        stepForm.classList.add('active');
    }
    window.scrollTo(0, 0);
}

// =========================================================
// STEP 4: HANDLE PAYMENT PROOF FILE
// =========================================================

function handleFileSelect() {
    const fileInput = document.getElementById('paymentProof');
    const filePreview = document.getElementById('filePreview');
    const file = fileInput.files[0];

    if (file) {
        const maxSize = 5 * 1024 * 1024; // 5MB
        const allowedTypes = ['image/jpeg', 'image/png', 'application/pdf'];

        if (file.size > maxSize) {
            showMessage('❌ File size exceeds 5MB limit', 'error');
            fileInput.value = '';
            if (filePreview) filePreview.classList.remove('show');
            return;
        }

        if (!allowedTypes.includes(file.type)) {
            showMessage('❌ Only JPG, PNG, PDF files allowed', 'error');
            fileInput.value = '';
            if (filePreview) filePreview.classList.remove('show');
            return;
        }

        // Show file preview
        if (filePreview) {
            filePreview.innerHTML = `
                <strong>✓ File Selected:</strong>
                <p id="fileName" style="margin: 8px 0 0 0; word-break: break-all; color: #155724;">
                    ${file.name} (${(file.size / 1024).toFixed(2)} KB)
                </p>
                <small style="color: #666;">Ready to upload</small>
            `;
            filePreview.classList.add('show');
        }

        console.log('File selected:', file.name);
    }
}

// =========================================================
// STEP 4: UPLOAD PAYMENT & REGISTER
// =========================================================

async function uploadPaymentProof() {
    const declarationCheckbox = document.getElementById('declarationAccepted');
    if (!declarationCheckbox || !declarationCheckbox.checked) {
        showMessage('⚠️ You must accept the declaration before submitting your application', 'error');
        return;
    }
    const fileInput = document.getElementById('paymentProof');
    const file = fileInput?.files[0];

    if (!file) {
        showMessage('⚠️ Please select a payment proof file', 'error');
        return;
    }

    const uploadBtn = document.getElementById('uploadPaymentBtn');
    if (uploadBtn) {
        uploadBtn.disabled = true;
        uploadBtn.textContent = 'Processing...';
    }

    try {
        // --- PHASE 1: SUBMIT REGISTRATION TEXT DATA ---
        console.log('Phase 1: Submitting complete registration details...');

        const rawPassword = document.getElementById('password')?.value || formData.passwordHash || '';
        const rawEmail = document.getElementById('email')?.value.trim() || formData.email || '';
        const rawDob = document.getElementById('dob')?.value || formData.dateOfBirth || '';
        const sanitizedDob = rawDob && rawDob.trim() !== "" ? rawDob : null;

        // 🛠️ SWITCHED KEYS TO CAMELCASE TO ALIGN WITH DEFAULT ASP.NET CORE SERIALIZATION
        const payload = {
            firstName: document.getElementById('firstName')?.value.trim() || formData.firstName || null,
            middleName: document.getElementById('middleName')?.value.trim() || formData.middleName || null,
            lastName: document.getElementById('lastName')?.value.trim() || formData.lastName || null,
            dateOfBirth: sanitizedDob,
            gender: document.getElementById('gender')?.value || formData.gender || null,
            nationality: document.getElementById('nationality')?.value || formData.nationality || null,
            identificationType: document.getElementById('identificationType')?.value || "NATIONAL_ID",
            nationalId: document.getElementById('nationalId')?.value.trim() || formData.nationalId || null,
            passportNumber: document.getElementById('passportNumber')?.value.trim() || formData.passportNumber || null,
            programmeCode: document.getElementById('programme')?.value || formData.programmeCode || null,
            intake: document.getElementById('intake')?.value || formData.intake || null,
            campusCode: document.getElementById('campus')?.value,

            academicYear: document.getElementById('academicYear')?.value,
            entrySemester: parseInt(document.getElementById('entrySemester')?.value),
            studyMode: document.getElementById('studyMode')?.value || formData.studyMode || null,
            email: rawEmail,
            primaryPhone: document.getElementById('primaryPhone')?.value.trim() || formData.primaryPhone || null,
            whatsappPhone: document.getElementById('whatsapp')?.value.trim() || formData.whatsappPhone || null,
            campusCode: formData.campusPreference || "UMC",
            paymentAmount: 50000,
            paymentProofPath: file.name,
            password: rawPassword,
            studyMode: document.getElementById('studyMode')?.value || formData.studyMode || null,
            email: rawEmail,
            applicationCategory: formData.applicationCategory || 'SelfSponsorship',
            applicationCategory: formData.applicationCategory || 'SelfSponsorship',
            agreementAccepted: true,
            password: rawPassword
        };

        console.log('Sending camelCase payload to backend:', payload);

        const registerResponse = await fetch(`${API_URL}/auth/register`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        const registerResult = await registerResponse.json();
        console.log('Registration response parsed:', registerResult);

        if (!registerResponse.ok || !registerResult.success) {


            let errorDetails = '';



            if (registerResult.errors) {

                errorDetails =
                    Object.entries(registerResult.errors)
                        .map(([field, msgs]) =>
                            `${field}: ${msgs.join(', ')}`
                        )
                        .join('\n');

            }



            const errorMsg =
                registerResult.message ||
                errorDetails ||
                "Registration failed. Please check your details.";



            // SHOW TO USER
            showMessage(
                "❌ " + errorMsg,
                "error"
            );


            // STOP PROCESS
            throw new Error(errorMsg);

        }

        const targetApplicationId = registerResult.data?.applicationId;

        if (!targetApplicationId) {
            throw new Error(
                "Could not retrieve application ID reference from the system context registration payload."
            );
        }


        // =====================================================
        // UPLOAD PASSPORT PHOTO
        // =====================================================

        const passportInput =
            document.getElementById('passportPhoto');

        const passportFile =
            passportInput?.files[0];


        if (passportFile) {

            console.log("Uploading passport photo...");

            const passportPayload = new FormData();

            passportPayload.append(
                "applicationId",
                targetApplicationId
            );

            passportPayload.append(
                "file",
                passportFile
            );


            const passportResponse =
                await fetch(
                    `${API_URL}/application/upload-passport-photo`,
                    {
                        method: "POST",
                        body: passportPayload
                    }
                );


            const passportResult =
                await passportResponse.json();


            console.log(
                "Passport upload response:",
                passportResult
            );


            if (!passportResult.success) {

                throw new Error(
                    passportResult.message ||
                    "Passport photo upload failed"
                );

            }

        }

        // =====================================================
        // UPLOAD LOAN SCHEME PROOF (if Government Loan category)
        // =====================================================
        const loanProofInput = document.getElementById('loanProofFile');
        const loanProofFile = loanProofInput?.files[0];

        if (formData.applicationCategory === 'GovernmentLoan' && loanProofFile) {
            console.log("Uploading loan scheme proof...");

            const loanPayload = new FormData();
            loanPayload.append("applicationId", targetApplicationId);
            loanPayload.append("file", loanProofFile);

            const loanResponse = await fetch(`${API_URL}/application/upload-loan-proof`, {
                method: "POST",
                body: loanPayload
            });

            const loanResult = await loanResponse.json();
            console.log("Loan proof upload response:", loanResult);

            if (!loanResult.success) {
                throw new Error(loanResult.message || "Loan scheme proof upload failed");
            }
        }

        // --- PHASE 2: UPLOAD THE PHYSICAL FILE ---
        console.log(`Phase 1 Success! Application ID: ${targetApplicationId}. Phase 2: Uploading physical image...`);

        const filePayload = new FormData();
        filePayload.append('applicationId', targetApplicationId);
        filePayload.append('file', file);

        const uploadResponse = await fetch(`${API_URL}/application/upload-payment-proof`, {
            method: 'POST',
            body: filePayload
        });

        const uploadResult = await uploadResponse.json();

        if (uploadResult.success) {
            sessionStorage.setItem('registrationEmail', rawEmail);
            showMessage('✅ Application & Payment submitted! Check email for OTP.', 'success');

            setTimeout(() => {
                updateStepIndicators(5);
                hideAllSteps();
                const step5Form = document.getElementById('step5Form');
                if (step5Form) step5Form.classList.add('active');
                window.scrollTo(0, 0);
            }, 2000);
        } else {
            showMessage('❌ ' + (uploadResult.message || 'Payment file upload failed'), 'error');
        }

    } catch (error) {
        console.error('Process error:', error);
        showMessage('❌ Error: ' + error.message, 'error');
    } finally {
        if (uploadBtn) {
            uploadBtn.disabled = false;
            uploadBtn.textContent = 'Upload & Continue';
        }
    }
}

// =========================================================
// STEP 5: VERIFY OTP
// =========================================================

async function verifyOtp() {
    const otp = document.getElementById('otp')?.value.trim() || '';
    const email = sessionStorage.getItem('registrationEmail') || formData.email;

    if (!otp || otp.length !== 6) {
        showMessage('⚠️ Please enter a valid 6-digit OTP', 'error');
        return;
    }

    if (!email) {
        showMessage('⚠️ Email not found. Please try again.', 'error');
        return;
    }

    const verifyBtn = document.getElementById('verifyOtpBtn');
    if (verifyBtn) {
        verifyBtn.disabled = true;
        verifyBtn.textContent = 'Verifying...';
    }

    try {
        console.log('Verifying OTP for:', email);

        const response = await fetch(`${API_URL}/auth/verify-otp`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                email: email,
                otpCode: otp
            })
        });

        const data = await response.json();
        console.log('OTP verification response:', data);

        if (data.success) {
            showMessage('✅ Email verified! Registration complete. Redirecting to login in 3 seconds...', 'success');

            sessionStorage.removeItem('registrationEmail');
            formData = {};

            setTimeout(() => {
                window.location.href = '/login.html';
            }, 3000);
        } else {
            showMessage('❌ ' + (data.message || 'Invalid OTP'), 'error');
        }
    } catch (error) {
        console.error('OTP verification error:', error);
        showMessage('❌ Error: ' + error.message, 'error');
    } finally {
        if (verifyBtn) {
            verifyBtn.disabled = false;
            verifyBtn.textContent = 'Verify & Complete Registration';
        }
    }
}



function validateStep1() {


    const firstName =
        document.getElementById('firstName')?.value.trim() || '';


    const lastName =
        document.getElementById('lastName')?.value.trim() || '';


    const dob =
        document.getElementById('dob')?.value || '';

    const age = calculateAge(dob);


    const gender =
        document.getElementById('gender')?.value || '';


    const nationality =
        document.getElementById('nationality')?.value || '';



    const identificationType =
        document.getElementById('identificationType')?.value || '';



    const nationalId =
        document.getElementById('nationalId')?.value.trim() || '';





    const passportNumber =
        document.getElementById('passportNumber')?.value.trim() || '';



    if (age < 16) {

        showMessage(
            "⚠️ Applicant must be at least 16 years old.",
            "error"
        );

        return false;

    }

    // Basic personal details validation

    if (
        !firstName ||
        !lastName ||
        !dob ||
        !gender ||
        !nationality ||
        (!nationalId &&
            document.getElementById('identificationType').value === "NATIONAL_ID")
    ) {

        showMessage(
            '⚠️ Please fill in all required personal details in Step 1',
            'error'
        );

        return false;

    }




    // Identification validation

    if (identificationType === "NATIONAL_ID") {


        if (!nationalId) {

            showMessage(
                '⚠️ Please enter your National ID Number',
                'error'
            );

            return false;

        }

    }



    if (identificationType === "PASSPORT") {


        if (!passportNumber) {

            showMessage(
                '⚠️ Please enter your Passport Number',
                'error'
            );

            return false;

        }

    }



    // Passport photo validation

    const passportPhoto =
        document.getElementById('passportPhoto');


    if (!passportPhoto || !passportPhoto.files.length) {


        showMessage(
            '⚠️ Please upload your passport photo',
            'error'
        );


        return false;

    }



    return true;

}

function validateStep2() {
    const programme = document.getElementById('programme')?.value || '';
    const campus = document.getElementById('campus')?.value || '';
    const intake = document.getElementById('intake')?.value || '';
    const academicYear = document.getElementById('academicYear')?.value || '';
    const entrySemester = document.getElementById('entrySemester')?.value || '';
    const studyMode = document.getElementById('studyMode')?.value || '';

    if (!programme || !campus || !intake || !academicYear || !entrySemester || !studyMode) {
        showMessage('⚠️ Please fill in all required fields in Step 2', 'error');
        return false;
    }

    // NEW — Government Loan Scheme requires proof before advancing
    const selectedCategory = document.getElementById('applicationCategory')?.value || 'SelfSponsorship';
    if (selectedCategory === 'GovernmentLoan') {
        const loanFile = document.getElementById('loanProofFile')?.files[0];
        if (!loanFile) {
            showMessage('⚠️ Please upload proof of your Government Loan Scheme before continuing', 'error');
            return false;
        }
    }

    return true;
}
function validateStep3() {
    const email = document.getElementById('email')?.value.trim() || '';
    const primaryPhone = document.getElementById('primaryPhone')?.value.trim() || '';
    const password = document.getElementById('password')?.value || '';
    const confirmPassword = document.getElementById('confirmPassword')?.value || '';

    if (!email || !primaryPhone || !password || !confirmPassword) {
        showMessage('⚠️ Please fill in all required fields in Step 3', 'error');
        return false;
    }

    if (!isStrongPassword(password)) {
        showMessage('❌ Password must have: 8+ chars, uppercase, lowercase, number, symbol', 'error');
        return false;
    }

    if (password !== confirmPassword) {
        showMessage('❌ Passwords do not match', 'error');
        return false;
    }

    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(email)) {
        showMessage('❌ Please enter a valid email address', 'error');
        return false;
    }

    return true;
}

function isStrongPassword(password) {
    if (password.length < 8) return false;
    if (!/[a-z]/.test(password)) return false;
    if (!/[A-Z]/.test(password)) return false;
    if (!/[0-9]/.test(password)) return false;
    if (!/[!@#$%^&*]/.test(password)) return false;
    return true;
}

// =========================================================
// PASSWORD STRENGTH CHECKER
// =========================================================

function checkPasswordStrength() {
    const password = document.getElementById('password')?.value || '';
    const strengthDiv = document.getElementById('passwordStrength');

    if (!strengthDiv) return;

    if (!password) {
        strengthDiv.style.display = 'none';
        return;
    }

    let strength = 0;
    let feedback = [];

    if (password.length >= 8) strength++; else feedback.push('At least 8 characters');
    if (/[a-z]/.test(password)) strength++; else feedback.push('Lowercase letter');
    if (/[A-Z]/.test(password)) strength++; else feedback.push('Uppercase letter');
    if (/[0-9]/.test(password)) strength++; else feedback.push('Number');
    if (/[!@#$%^&*]/.test(password)) strength++; else feedback.push('Symbol (!@#$%^&*)');

    strengthDiv.style.display = 'block';
    strengthDiv.className = 'password-strength';

    if (strength <= 2) {
        strengthDiv.classList.add('strength-weak');
        strengthDiv.textContent = '❌ Weak - Missing: ' + feedback.join(', ');
    } else if (strength === 3 || strength === 4) {
        strengthDiv.classList.add('strength-medium');
        strengthDiv.textContent = '⚠️ Medium - Add: ' + feedback.join(', ');
    } else {
        strengthDiv.classList.add('strength-strong');
        strengthDiv.textContent = '✅ Strong password';
    }
}

// =========================================================
// AGE VALIDATION
// =========================================================

function calculateAge(dateOfBirth) {

    const today = new Date();

    const birthDate = new Date(dateOfBirth);


    let age = today.getFullYear() - birthDate.getFullYear();


    const monthDifference =
        today.getMonth() - birthDate.getMonth();


    if (
        monthDifference < 0 ||
        (
            monthDifference === 0 &&
            today.getDate() < birthDate.getDate()
        )
    ) {

        age--;

    }


    return age;

}

// =========================================================
// UI HELPERS
// =========================================================

function hideAllSteps() {
    document.querySelectorAll('.form-section').forEach(section => {
        section.classList.remove('active');
    });
}

function updateStepIndicators(step) {


    document.querySelectorAll('.step')
        .forEach(item => {


            const stepNumber =
                parseInt(item.dataset.step);



            item.classList.remove(
                'active',
                'completed'
            );



            if (stepNumber < step) {

                item.classList.add('completed');

            }


            if (stepNumber === step) {

                item.classList.add('active');

            }


        });


}
function showMessage(message, type) {
    const messageDiv = document.getElementById('message');
    if (!messageDiv) return;

    messageDiv.textContent = message;
    messageDiv.className = `message show ${type}`;

    setTimeout(() => {
        messageDiv.classList.remove('show');
    }, 6000);
}

// =========================================================
// CENTRAL NAVIGATION CONTROLLER
// =========================================================

function setupNavigationButtons() {


    const nextButtons =
        document.querySelectorAll('.next-btn');


    const previousButtons =
        document.querySelectorAll('.prev-btn');



    // NEXT BUTTONS

    nextButtons.forEach(button => {


        button.addEventListener('click', function (e) {


            e.preventDefault();


            const nextStep =
                parseInt(this.dataset.next);



            goToStep(nextStep);


        });


    });




    // PREVIOUS BUTTONS

    previousButtons.forEach(button => {


        button.addEventListener('click', function (e) {


            e.preventDefault();


            const previousStep =
                parseInt(this.dataset.prev);



            goToStep(previousStep);


        });


    });


}