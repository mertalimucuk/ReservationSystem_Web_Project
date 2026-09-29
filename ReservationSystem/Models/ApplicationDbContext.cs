using Microsoft.EntityFrameworkCore;
using ReservationSystem.Models; 
using System;

namespace ReservationSystem.Models // DbContext'in bulunduğu namespace
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Reservation> Reservations { get; set; } = null!;
        public DbSet<Term> Terms { get; set; } = null!;
        public DbSet<Class> Classes { get; set; } = null!;
        public DbSet<Feedback> Feedbacks { get; set; } = null!;
        
        
        public DbSet<BlockedHolidayAttempt> BlockedHolidayAttempts { get; set; } = null!;

        // LOG TABLOLARI İÇİN DbSet'LER
        public DbSet<SystemLog> SystemLogs { get; set; } = null!;
        public DbSet<ErrorLog> ErrorLogs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User - Reservation İlişkilerim
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.User) 
                .WithMany(u => u.BookedReservations) 
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict); 

            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Instructor) 
                .WithMany(u => u.TaughtReservations) 
                .HasForeignKey(r => r.InstructorId)
                .OnDelete(DeleteBehavior.Restrict); 

            // User - Feedback İlişkisim
            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.User) 
                .WithMany(u => u.FeedbacksGiven) 
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Restrict); 

            // Reservation - Feedback İlişkisim
            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.Reservation) 
                .WithMany(r => r.FeedbacksReceived) 
                .HasForeignKey(f => f.ReservationId)
                .OnDelete(DeleteBehavior.Cascade); 

            // Class - Instructor İlişkisim
            modelBuilder.Entity<Class>()
                .HasOne(c => c.Instructor) 
                .WithMany(u => u.PrimaryClassesManaged) 
                .HasForeignKey(c => c.InstructorId) 
                .OnDelete(DeleteBehavior.Restrict); 

            modelBuilder.Entity<Feedback>()
                .HasOne(f => f.Class) 
                .WithMany(c => c.Feedbacks) 
                .HasForeignKey(f => f.ClassId) 
                .OnDelete(DeleteBehavior.Restrict); 

            // BlockedHolidayAttempt için ilişki yapılandırmalarım
            modelBuilder.Entity<BlockedHolidayAttempt>()
                .HasOne(bha => bha.Instructor)
                .WithMany() 
                .HasForeignKey(bha => bha.InstructorId)
                .OnDelete(DeleteBehavior.Cascade); 

            modelBuilder.Entity<BlockedHolidayAttempt>()
                .HasOne(bha => bha.Class)
                .WithMany() 
                .HasForeignKey(bha => bha.ClassId)
                .OnDelete(DeleteBehavior.Cascade); 

            // BAŞLANGIÇ VERİLERİ 
            modelBuilder.Entity<User>().HasData(
                new User {
                    Id = 1, FullName = "Admin User", Email = "cengweb382@gmail.com",
                    PasswordHash = "AQAAAAIAAYagAAAAEEYZKV51W6JH9ZSoQ98mP5N+8H6Wv+5qqJ8VAk7r8cjDUh4lh63z23LEhkTkiSbh6g==", Role = "admin" },
                new User {
                    Id = 8, FullName = "Instructor User", Email = "instructor@test.com",
                    PasswordHash = "AQAAAAIAAYagAAAAEEYZKV51W6JH9ZSoQ98mP5N+8H6Wv+5qqJ8VAk7r8cjDUh4lh63z23LEhkTkiSbh6g==", Role = "instructor" },
                new User {
                    Id = 16, FullName = "Test Instructor", Email = "cengstmp@gmail.com",
                    PasswordHash = "AQAAAAIAAYagAAAAEEYZKV51W6JH9ZSoQ98mP5N+8H6Wv+5qqJ8VAk7r8cjDUh4lh63z23LEhkTkiSbh6g==", Role = "instructor" }
            );

            modelBuilder.Entity<Class>().HasData(
                new Class { Id = 1, Name = "CENG101", Description = "Intro to Computer Engineering", InstructorId = null },
                new Class { Id = 2, Name = "CENG102", Description = "Programming Fundamentals", InstructorId = null },
                new Class { Id = 3, Name = "CENG201", Description = "Data Structures", InstructorId = null },
                new Class { Id = 4, Name = "CENG202", Description = "Computer Architecture", InstructorId = null },
                new Class { Id = 5, Name = "CENG301", Description = "Operating Systems", InstructorId = null },
                new Class { Id = 6, Name = "CENG302", Description = "Database Systems", InstructorId = null },
                new Class { Id = 7, Name = "CENG303", Description = "Software Engineering", InstructorId = null },
                new Class { Id = 8, Name = "CENG304", Description = "Computer Networks", InstructorId = null },
                new Class { Id = 9, Name = "CENG401", Description = "AI and ML", InstructorId = null },
                new Class { Id = 10, Name = "CENG402", Description = "Capstone Project", InstructorId = null }
            );

            
        }
    }
}
