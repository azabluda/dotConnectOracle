using System;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DevArt.Tests.dotConnect
{
    using System.Collections.Generic;

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
        public ICollection<Folder> OwnedFolders { get; set; }
    }

    public class TestDbContext : DbContext
    {
        private readonly Action<DbContextOptionsBuilder> configure;
        private readonly Action<ModelBuilder> postModelCreating;

        public TestDbContext(
            Action<DbContextOptionsBuilder> configure,
            Action<ModelBuilder> postModelCreating)
        {
            this.configure = configure;
            this.postModelCreating = postModelCreating;
        }

        public TestDbContext(string connectionString)
            : this(builder => builder.UseOracle(connectionString), null)
        {
        }

        public TestDbContext(DbConnection dbConnection)
            : this(builder => builder.UseOracle(dbConnection), null)
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
                .WithMany(u => u.OwnedFolders)
                .HasForeignKey(f => f.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            var user = modelBuilder.Entity<User>();
            user.HasKey(u => u.Id);
            user.Property(u => u.Id).ValueGeneratedOnAdd();
            user.Property(u => u.Name).IsRequired().HasMaxLength(100).IsConcurrencyToken();
            user.Property(u => u.LongDescription);

            postModelCreating?.Invoke(modelBuilder);
        }
    }
}
