using FamilyHub.Api.Chores;
using FamilyHub.Api.Households;
using Microsoft.EntityFrameworkCore;

namespace FamilyHub.Api.Data;

public class FamilyHubDbContext(DbContextOptions<FamilyHubDbContext> options) : DbContext(options)
{
    public DbSet<Household> Households => Set<Household>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();
    public DbSet<Chore> Chores => Set<Chore>();
    public DbSet<ChoreOccurrence> ChoreOccurrences => Set<ChoreOccurrence>();

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

        modelBuilder.Entity<Chore>(entity =>
        {
            entity.Property(chore => chore.Recurrence).HasConversion<string>();
            entity.HasOne<Household>().WithMany().HasForeignKey(chore => chore.HouseholdId);
            entity.HasOne<FamilyMember>().WithMany().HasForeignKey(chore => chore.AssignedFamilyMemberId);
        });

        modelBuilder.Entity<ChoreOccurrence>(entity =>
        {
            entity.HasOne<Chore>().WithMany().HasForeignKey(occurrence => occurrence.ChoreId);
        });
    }
}
