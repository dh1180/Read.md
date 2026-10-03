using Microsoft.EntityFrameworkCore;
using ReadMeApp.Models;

namespace ReadMeApp.Data;

public class ReadmeDbContext : DbContext
{
    public ReadmeDbContext(DbContextOptions<ReadmeDbContext> options) : base(options)
    {
    }

    public DbSet<Book> Books => Set<Book>();
    public DbSet<UserBook> UserBooks => Set<UserBook>();
    public DbSet<ReadingNote> ReadingNotes => Set<ReadingNote>();
    public DbSet<ReviewComment> ReviewComments => Set<ReviewComment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Book>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.HasIndex(b => b.Isbn);
            entity.Property(b => b.Title).IsRequired().HasMaxLength(200);
            entity.Property(b => b.Author).HasMaxLength(100);
            entity.Property(b => b.Publisher).HasMaxLength(100);
            entity.Property(b => b.CoverImageUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<UserBook>(entity =>
        {
            entity.HasKey(ub => ub.Id);
            entity.HasOne(ub => ub.Book)
                  .WithMany(b => b.UserBooks)
                  .HasForeignKey(ub => ub.BookId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReadingNote>(entity =>
        {
            entity.HasKey(rn => rn.Id);
            entity.HasOne(rn => rn.UserBook)
                  .WithMany(ub => ub.Notes)
                  .HasForeignKey(rn => rn.UserBookId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReviewComment>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.AuthorName).IsRequired().HasMaxLength(50);
            entity.Property(c => c.Content).IsRequired().HasMaxLength(1000);
            entity.HasIndex(c => c.UserBookId);

            entity.HasOne(c => c.UserBook)
                  .WithMany(ub => ub.Comments)
                  .HasForeignKey(c => c.UserBookId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public static void SeedSampleData(ReadmeDbContext context)
    {
        // 더미 데이터 제거: 사용자가 직접 작성한 실제 독서록만 관리
    }
}
