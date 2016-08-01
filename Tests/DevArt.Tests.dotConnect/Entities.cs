using System;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DevArt.Tests.dotConnect
{
    public class Folder
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int OwnerId { get; set; }
        public User Owner { get; set; }
    }

    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string LongDescription { get; set; }
    }

    public class TestDbContext : DbContext
    {
        private readonly Action<DbContextOptionsBuilder> configure;

        public TestDbContext(Action<DbContextOptionsBuilder> configure)
        {
            this.configure = configure;
        }

        public TestDbContext(string connectionString)
            : this(builder => builder.UseOracle(connectionString))
        {
        }

        public TestDbContext(DbConnection dbConnection)
            : this(builder => builder.UseOracle(dbConnection))
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            this.configure(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var folder = modelBuilder.Entity<Folder>();
            folder.HasKey(f => f.Id);
            folder.Property(f => f.Id).ValueGeneratedOnAdd();
            folder.Property(f => f.Name).IsRequired().HasMaxLength(100);
            folder
                .HasOne(f => f.Owner)
                .WithMany()
                .HasForeignKey(f => f.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            var user = modelBuilder.Entity<User>();
            user.HasKey(u => u.Id);
            user.Property(u => u.Id).ValueGeneratedOnAdd();
            user.Property(u => u.Name).IsRequired().HasMaxLength(100).IsConcurrencyToken();
            user.Property(u => u.LongDescription);
        }
    }
}
