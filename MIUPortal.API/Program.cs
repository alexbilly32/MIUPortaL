using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MIUPortal.API.Data;
using MIUPortal.API.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ========== ADD SERVICES TO THE CONTAINER ==========

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<MIUContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
);

// ========== JWT AUTHENTICATION ==========
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured.");
var tokenExpiryMinutes = int.Parse(jwtSettings["TokenExpiryMinutes"] ?? "120");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

       
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var path = context.HttpContext.Request.Path;
                if (path.StartsWithSegments("/api/timetable/download"))
                {
                    var tokenFromQuery = context.Request.Query["token"];
                    if (!string.IsNullOrEmpty(tokenFromQuery))
                    {
                        context.Token = tokenFromQuery;
                    }
                }
                return Task.CompletedTask;
            }
        };
    });

// ========== CORS ==========
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

// Email Services
builder.Services.AddScoped<IEmailService, EmailService>();


// Document Generation Services
builder.Services.AddScoped<AdmissionLetterService>();
builder.Services.AddScoped<SemesterRegistrationCardService>();
builder.Services.AddScoped<ExaminationPermitService>();
builder.Services.AddScoped<PaymentReceiptService>();
builder.Services.AddScoped<IGradeUploadService, GradeUploadService>();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<FeeScopingService>();



//QRCODE
builder.Services.AddScoped<QrCodeService>();

// ========== CUSTOM SERVICES - REGISTER HERE ==========

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IDocumentEmailService, DocumentEmailService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IIntentService, IntentService>();
builder.Services.AddScoped<PaymentFinalizationService>();
builder.Services.AddScoped<PassportPhotoService>();
builder.Services.AddScoped<StudentEligibilityService>();
builder.Services.AddScoped<StudentPromotionService>();
builder.Services.AddScoped<SemesterProgressionService>();

// AI Chatbot
builder.Services.AddHttpClient<IOpenRouterService, OpenRouterService>();
builder.Services.AddScoped<IMIUChatbotService, MIUChatbotService>();
builder.Services.AddHttpClient(); 
builder.Services.AddScoped<IUniversityKnowledgeService, UniversityKnowledgeService>();



// Document Generation Services
builder.Services.AddScoped<AdmissionLetterService>();
builder.Services.AddScoped<SemesterRegistrationCardService>();
builder.Services.AddScoped<ExaminationPermitService>();
builder.Services.AddScoped<PaymentReceiptService>();

// ========== NEW: GRADE UPLOAD SERVICE ==========
builder.Services.AddScoped<IGradeUploadService, GradeUploadService>();
// ========== BUILD THE APPLICATION ==========
var app = builder.Build();

// ========== CONFIGURE THE HTTP REQUEST PIPELINE ==========

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

// ========== USE CORS ==========
app.UseCors("AllowAll");

// ========== USE AUTHENTICATION ==========
app.UseAuthentication();
app.UseAuthorization();

// ========== MAP CONTROLLERS ==========
app.MapControllers();

// ========== CREATE REQUIRED FOLDERS ==========
var webroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
var folders = new[]
{
    Path.Combine(webroot, "admission-letters"),
    Path.Combine(webroot, "semester-cards"),
    Path.Combine(webroot, "exam-permits"),
    Path.Combine(webroot, "payment-receipts"),
    Path.Combine(webroot, "uploads"),
    Path.Combine(webroot, "uploads", "timetables"),
    Path.Combine(webroot, "qr-codes")
};
foreach (var folder in folders)
{
    if (!Directory.Exists(folder))
    {
        Directory.CreateDirectory(folder);
        Console.WriteLine($"✅ Created folder: {folder}");
    }
}

// ========== RUN THE APPLICATION ==========
app.Run();