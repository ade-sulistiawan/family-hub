using FamilyHub.Api.Households;
using Microsoft.EntityFrameworkCore;

namespace FamilyHub.Api.Data;

public class FamilyHubDbContext(DbContextOptions<FamilyHubDbContext> options) : DbContext(options)
{
    public DbSet<Household> Households => Set<Household>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Household>(entity =>
        {
            entity.HasIndex(h => h.JoinCode).IsUnique();
        });

        modelBuilder.Entity<FamilyMember>(entity =>
        {
            entity.HasIndex(m => m.GoogleSubjectId).IsUnique();
            entity.HasOne<Household>().WithMany().HasForeignKey(m => m.HouseholdId);
        });
    }
}
