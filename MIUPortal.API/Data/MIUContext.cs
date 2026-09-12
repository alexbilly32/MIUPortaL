using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Models;

namespace MIUPortal.API.Data
{
    public class MIUContext : DbContext
    {
        public MIUContext(DbContextOptions<MIUContext> options) : base(options) { }

        // DbSets (NEVER nullable)
        public DbSet<Admin> Admins => Set<Admin>();
        public DbSet<Application> Applications => Set<Application>();
        public DbSet<Campus> Campuses => Set<Campus>();
        public DbSet<Enrollment> CourseEnrollments => Set<Enrollment>();
        public DbSet<Course> Courses => Set<Course>();
        public DbSet<Document> Documents => Set<Document>();
        public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
        public DbSet<FinancialTransaction> FinancialLedger => Set<FinancialTransaction>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<PaymentGateway> PaymentGateways => Set<PaymentGateway>();

        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<Programme> Programmes => Set<Programme>();
        public DbSet<Result> Results => Set<Result>();
        public DbSet<SemesterRegistration> SemesterRegistrations => Set<SemesterRegistration>();
        public DbSet<Student> Students => Set<Student>();
        public DbSet<Timetable> Timetables => Set<Timetable>();
        public DbSet<AcademicSemester> AcademicSemesters => Set<AcademicSemester>();
        public DbSet<Faculty> Faculties => Set<Faculty>();
        public DbSet<Lecturer> Lecturers => Set<Lecturer>();
        public DbSet<LecturerCourse> LecturerCourses => Set<LecturerCourse>();
        public DbSet<Bursar> Bursars => Set<Bursar>();
        public DbSet<School> Schools => Set<School>();
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
        public DbSet<FeeStructure> FeeStructures => Set<FeeStructure>();
        public DbSet<ClearanceOverride> ClearanceOverrides => Set<ClearanceOverride>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<AcademicRegistrar> AcademicRegistrars => Set<AcademicRegistrar>();
        public DbSet<TimetableDocument> TimetableDocuments { get; set; }


        public DbSet<KnowledgeArticle> KnowledgeArticles { get; set; }

        public DbSet<StudentSemesterEnrollment> StudentSemesterEnrollments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ================= PRIMARY KEYS =================
            modelBuilder.Entity<Student>().HasKey(s => s.RegNumber);
            modelBuilder.Entity<Campus>().HasKey(c => c.CampusCode);
            modelBuilder.Entity<Programme>().HasKey(p => p.ProgrammeCode);
            modelBuilder.Entity<Enrollment>()
     .HasOne(e => e.AcademicSemester)
     .WithMany()
     .HasForeignKey(e => e.SemesterId)
     .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StudentSemesterEnrollment>().ToTable("studentsemesterenrollments");

            // ================= UNIQUE CONSTRAINTS =================
            modelBuilder.Entity<Student>().HasIndex(s => s.Email).IsUnique();
            modelBuilder.Entity<Admin>().HasIndex(a => a.Email).IsUnique();
            modelBuilder.Entity<Application>().HasIndex(a => a.ApplicationNumber).IsUnique();
            modelBuilder.Entity<Course>().HasIndex(c => c.CourseCode).IsUnique();

            // Prevent duplicate course registration
            modelBuilder.Entity<Enrollment>()
                .HasIndex(e => new { e.RegNumber, e.CourseId, e.SemesterId })
                .IsUnique();

            // ================= RELATIONSHIPS =================

            // Payments
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Student)
                .WithMany(s => s.Payments)
                .HasForeignKey(p => p.RegNumber)
                .OnDelete(DeleteBehavior.Cascade);

            // Results
            modelBuilder.Entity<Result>()
                .HasOne(r => r.Student)
                .WithMany(s => s.Results)
                .HasForeignKey(r => r.RegNumber)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Result>()
                .HasOne(r => r.Course)
                .WithMany()
                .HasForeignKey(r => r.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            // Documents
            modelBuilder.Entity<Document>()
                .HasOne(d => d.Student)
                .WithMany(s => s.Documents)
                .HasForeignKey(d => d.RegNumber)
                .OnDelete(DeleteBehavior.Cascade);

            // Notifications
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Student)
                .WithMany(s => s.Notifications)
                .HasForeignKey(n => n.RegNumber)
                .OnDelete(DeleteBehavior.Cascade);

            // Course Enrollments
            modelBuilder.Entity<Enrollment>()
                .HasOne(e => e.Student)
                .WithMany(s => s.CourseEnrollments)
                .HasForeignKey(e => e.RegNumber)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Enrollment>()
                .HasOne(e => e.Course)
                .WithMany(c => c.CourseEnrollments)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FinancialTransaction>()
                            .HasOne(ft => ft.Payment)
                            .WithMany()
                            .HasForeignKey(ft => ft.PaymentId)
                            .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FinancialTransaction>()
                .HasOne(ft => ft.RelatedLedgerEntry)
                .WithMany()
                .HasForeignKey(ft => ft.RelatedLedgerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FinancialTransaction>()
                .HasOne(ft => ft.Bursar)
                .WithMany()
                .HasForeignKey(ft => ft.BursarId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.ProcessedByBursar)
                .WithMany()
                .HasForeignKey(p => p.ProcessedByBursarId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<FeeStructure>()
                .HasOne(fs => fs.Programme)
                .WithMany()
                .HasForeignKey(fs => fs.ProgrammeCode)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<FeeStructure>()
                .HasIndex(fs => new { fs.ProgrammeCode, fs.AcademicYear, fs.Semester })
                .IsUnique();

            modelBuilder.Entity<LecturerCourse>()
    .HasIndex(x => new
    {
        x.LecturerId,
        x.CourseId,
        x.AcademicYear,
        x.Semester
    })
    .IsUnique();

            modelBuilder.Entity<ClearanceOverride>()
                .HasOne(co => co.Student)
                .WithMany()
                .HasForeignKey(co => co.RegNumber)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ClearanceOverride>()
                .HasOne(co => co.Bursar)
                .WithMany()
                .HasForeignKey(co => co.BursarId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.ReviewedByBursar)
                .WithMany()
                .HasForeignKey(p => p.ReviewedByBursarId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.Bursar)
                .WithMany()
                .HasForeignKey(a => a.BursarId)
                .OnDelete(DeleteBehavior.SetNull);


            // ================= NEW TABLES FIX (IMPORTANT) =================

            // Financial Ledger -> Students (RegNumber FK)
            modelBuilder.Entity<FinancialTransaction>()
                .HasOne(ft => ft.Student)
                .WithMany(s => s.FinancialTransactions)
                .HasForeignKey(ft => ft.RegNumber)
                .OnDelete(DeleteBehavior.Cascade);

            // Semester Registration -> Students (RegNumber FK)
            modelBuilder.Entity<SemesterRegistration>()
                .HasOne(sr => sr.Student)
                .WithMany(s => s.SemesterRegistrations)
                .HasForeignKey(sr => sr.RegNumber)
                .OnDelete(DeleteBehavior.Cascade);

            // Lecturer -> Faculty
            modelBuilder.Entity<Lecturer>()
                .HasOne(l => l.Faculty)
                .WithMany(f => f.Lecturers)
                .HasForeignKey(l => l.FacultyId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Programme>()
    .HasOne(p => p.School)
    .WithMany(s => s.Programmes)
    .HasForeignKey(p => p.SchoolId)
    .OnDelete(DeleteBehavior.Restrict);

            // School -> Faculty
            modelBuilder.Entity<School>()
                .HasOne(s => s.Faculty)
                .WithMany()
                .HasForeignKey(s => s.FacultyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PasswordResetToken>()
                .HasIndex(x => x.ResetToken)
                .IsUnique();

            modelBuilder.Entity<PasswordResetToken>()
    .HasIndex(x => x.ResetToken)
    .IsUnique();

            modelBuilder.Entity<KnowledgeArticle>().ToTable("knowledge_articles");
        }
    }
}
