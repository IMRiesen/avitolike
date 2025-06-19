using AvitoLike.Models;
using Microsoft.EntityFrameworkCore;

namespace AvitoLike.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Пользователи
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; } = null!;
        public DbSet<UserSetting> UserSettings { get; set; } = null!;

        // Объявления
        public DbSet<Ad> Ads { get; set; } = null!;
        public DbSet<AdImage> AdImages { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Favorite> Favorites { get; set; } = null!;

        // Взаимодействия
        public DbSet<Message> Messages { get; set; } = null!;
        public DbSet<Review> Reviews { get; set; } = null!;
        public DbSet<ViewHistory> ViewHistories { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Конфигурация User
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasIndex(u => u.Email).IsUnique();
                entity.HasIndex(u => u.Username).IsUnique();

                entity.Property(u => u.CreatedAt)
                    .HasDefaultValueSql("NOW()");

                entity.Property(u => u.IsActive)
                    .HasDefaultValue(true);

                // Добавлена связь с UserSetting
                entity.HasOne(u => u.Settings)
                    .WithOne()
                    .HasForeignKey<UserSetting>(us => us.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Конфигурация связей многие-ко-многим
            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            modelBuilder.Entity<Favorite>()
                .HasKey(f => new { f.UserId, f.AdId });

            // Конфигурация Category
            modelBuilder.Entity<Category>()
                .HasOne(c => c.ParentCategory)
                .WithMany(c => c.ChildCategories)
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Конфигурация UserSetting
            modelBuilder.Entity<User>()
                .HasOne(u => u.Settings)
                .WithOne(us => us.User)
                .HasForeignKey<UserSetting>(us => us.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Конфигурация Ad
            modelBuilder.Entity<Ad>(entity =>
            {
                entity.ToTable("ads");
                entity.Property(a => a.CreatedAt)
                    .HasDefaultValueSql("NOW()");

                entity.Property(a => a.UpdatedAt)
                    .HasDefaultValueSql("NOW()");

                entity.Property(a => a.Status)
                    .HasDefaultValue("active");
            });

            // Конфигурация AdImage
            modelBuilder.Entity<Ad>()
                .HasMany(a => a.Images)
                .WithOne(i => i.Ad)
                .OnDelete(DeleteBehavior.Cascade);

            // Индексы для оптимизации
            modelBuilder.Entity<Ad>()
                .HasIndex(a => a.CategoryId)
                .HasDatabaseName("IX_ads_category_id");

            modelBuilder.Entity<Ad>()
                .HasIndex(a => a.UserId)
                .HasDatabaseName("IX_ads_user_id");

            modelBuilder.Entity<Message>()
                .HasIndex(m => new { m.SenderId, m.ReceiverId })
                .HasDatabaseName("IX_messages_sender_receiver");

            modelBuilder.Entity<Review>()
                .HasIndex(r => r.TargetUserId)
                .HasDatabaseName("IX_reviews_target_user_id");

            // Настройка каскадного удаления для объявлений
            modelBuilder.Entity<Ad>()
                .HasMany(a => a.Images)
                .WithOne(i => i.Ad)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Ad>()
                .HasMany(a => a.Favorites)
                .WithOne(f => f.Ad)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Ad>()
                .HasMany(a => a.Reviews)
                .WithOne(r => r.Ad)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}