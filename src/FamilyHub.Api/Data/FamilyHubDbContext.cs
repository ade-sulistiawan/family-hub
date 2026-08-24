using FamilyHub.Api.Chores;
using FamilyHub.Api.Expiry;
using FamilyHub.Api.FirstAid;
using FamilyHub.Api.Households;
using FamilyHub.Api.Items;
using FamilyHub.Api.Medications;
using FamilyHub.Api.Notifications;
using FamilyHub.Api.Warranties;
using Microsoft.EntityFrameworkCore;

namespace FamilyHub.Api.Data;

public class FamilyHubDbContext(DbContextOptions<FamilyHubDbContext> options) : DbContext(options)
{
    public DbSet<Household> Households => Set<Household>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();
    public DbSet<Chore> Chores => Set<Chore>();
    public DbSet<ChoreOccurrence> ChoreOccurrences => Set<ChoreOccurrence>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ExpiryFacet> ExpiryFacets => Set<ExpiryFacet>();
    public DbSet<WarrantyFacet> WarrantyFacets => Set<WarrantyFacet>();
    public DbSet<StockFacet> StockFacets => Set<StockFacet>();
    public DbSet<Medication> Medications => Set<Medication>();
    public DbSet<DoseLog> DoseLogs => Set<DoseLog>();
    public DbSet<BrowserPushSubscription> BrowserPushSubscriptions => Set<BrowserPushSubscription>();
    public DbSet<MedicationReminderDelivery> MedicationReminderDeliveries => Set<MedicationReminderDelivery>();

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

        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasOne<Household>().WithMany().HasForeignKey(item => item.HouseholdId);
        });

        modelBuilder.Entity<ExpiryFacet>(entity =>
        {
            entity.HasKey(expiry => expiry.ItemId);
            entity.HasOne<Item>().WithOne().HasForeignKey<ExpiryFacet>(expiry => expiry.ItemId);
        });

        modelBuilder.Entity<WarrantyFacet>(entity =>
        {
            entity.HasKey(warranty => warranty.ItemId);
            entity.HasOne<Item>().WithOne().HasForeignKey<WarrantyFacet>(warranty => warranty.ItemId);
        });

        modelBuilder.Entity<StockFacet>(entity =>
        {
            entity.HasKey(stock => stock.ItemId);
            entity.HasOne<Item>().WithOne().HasForeignKey<StockFacet>(stock => stock.ItemId);
        });

        modelBuilder.Entity<Medication>(entity =>
        {
            entity.Property(medication => medication.Kind).HasConversion<string>();
            entity.HasOne<Household>().WithMany().HasForeignKey(medication => medication.HouseholdId);
            entity.HasOne<FamilyMember>().WithMany().HasForeignKey(medication => medication.AssignedFamilyMemberId);
        });

        modelBuilder.Entity<DoseLog>(entity =>
        {
            entity.Property(log => log.Status).HasConversion<string>();
            entity.HasOne<Medication>().WithMany().HasForeignKey(log => log.MedicationId);
            entity.HasOne<FamilyMember>().WithMany().HasForeignKey(log => log.FamilyMemberId);
        });

        modelBuilder.Entity<BrowserPushSubscription>(entity =>
        {
            entity.HasIndex(subscription => subscription.Endpoint).IsUnique();
            entity.HasOne<FamilyMember>().WithMany().HasForeignKey(subscription => subscription.FamilyMemberId);
        });

        modelBuilder.Entity<MedicationReminderDelivery>(entity =>
        {
            entity.HasIndex(delivery => new
            {
                delivery.MedicationId,
                delivery.BrowserPushSubscriptionId,
                delivery.ScheduledOn,
            }).IsUnique();
            entity.HasOne<Medication>().WithMany().HasForeignKey(delivery => delivery.MedicationId);
            entity.HasOne<BrowserPushSubscription>().WithMany()
                .HasForeignKey(delivery => delivery.BrowserPushSubscriptionId);
        });
    }
}
