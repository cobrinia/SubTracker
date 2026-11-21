using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SubTracker.Scaffolded.Entities;

namespace SubTracker.Scaffolded;

public partial class ScaffoldedDbContext : DbContext
{
    public ScaffoldedDbContext(DbContextOptions<ScaffoldedDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PaymentMethod> PaymentMethods { get; set; }

    public virtual DbSet<Subscription> Subscriptions { get; set; }

    public virtual DbSet<SubscriptionMember> SubscriptionMembers { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(e => e.Name, "UX_Categories_Name").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasIndex(e => e.PaymentMethodId, "IX_Payments_PaymentMethodId");

            entity.HasIndex(e => new { e.SubscriptionId, e.ChargedAt }, "IX_Payments_Subscription_ChargedAt");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ChargedAt).HasPrecision(0);
            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.PaymentMethod).WithMany(p => p.Payments)
                .HasForeignKey(d => d.PaymentMethodId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Subscription).WithMany(p => p.Payments).HasForeignKey(d => d.SubscriptionId);
        });

        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_PaymentMethods_UserId");

            entity.Property(e => e.Last4)
                .HasMaxLength(4)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.LastUsedAt).HasPrecision(0);
            entity.Property(e => e.Title).HasMaxLength(100);
            entity.Property(e => e.Type)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithMany(p => p.PaymentMethods).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasIndex(e => e.CategoryId, "IX_Subscriptions_CategoryId");

            entity.HasIndex(e => e.NextChargeDate, "IX_Subscriptions_NextChargeDate");

            entity.HasIndex(e => e.PaymentMethodId, "IX_Subscriptions_PaymentMethodId");

            entity.HasIndex(e => new { e.Status, e.NextChargeDate }, "IX_Subscriptions_Status_NextChargeDate");

            entity.HasIndex(e => new { e.OwnerId, e.Name }, "UX_Subscriptions_Owner_Name").IsUnique();

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Period)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Category).WithMany(p => p.Subscriptions)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Owner).WithMany(p => p.Subscriptions).HasForeignKey(d => d.OwnerId);

            entity.HasOne(d => d.PaymentMethod).WithMany(p => p.Subscriptions)
                .HasForeignKey(d => d.PaymentMethodId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<SubscriptionMember>(entity =>
        {
            entity.HasKey(e => new { e.SubscriptionId, e.UserId });

            entity.HasIndex(e => e.UserId, "IX_SubscriptionMembers_UserId");

            entity.Property(e => e.JoinedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.SharePercent).HasColumnType("decimal(5, 2)");

            entity.HasOne(d => d.Subscription).WithMany(p => p.SubscriptionMembers).HasForeignKey(d => d.SubscriptionId);

            entity.HasOne(d => d.User).WithMany(p => p.SubscriptionMembers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email, "UX_Users_Email").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
