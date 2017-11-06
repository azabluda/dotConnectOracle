using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Devart.Data.Oracle;
using Devart.Data.Oracle.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DevArt.Tests.dotConnect
{
    [TestClass]
    public class UnitTest
    {
        private const string ConnectionStringVarName = "Test_DotConnect_DefaultConnection";

        private string ConnectionString =>
            Environment.GetEnvironmentVariable(ConnectionStringVarName)
            ?? throw new Exception($"Environment variable '{ConnectionStringVarName}' must be defined, e.g. in the Debug section of the project properties.");

        // https://github.com/aspnet/Tooling/issues/456
        // Test runner should pick up environment variables from launchSettings.json
        [ClassInitialize]
        public static void LaunchSettingsWorkaround(TestContext context)
        {
            const string launchSettingsJson = @"Properties\launchSettings.json";
            if (!File.Exists(launchSettingsJson))
            {
                return;
            }

            using (var file = File.OpenText(launchSettingsJson))
            {
                using (var reader = new JsonTextReader(file))
                { 
                    var variables = JObject.Load(reader)
                        .GetValue("profiles")
                        .SelectMany(profiles => profiles.Children())
                        .SelectMany(profile => profile.Children<JProperty>())
                        .Where(prop => prop.Name == "environmentVariables")
                        .SelectMany(prop => prop.Value.Children<JProperty>())
                        .ToList();

                    foreach (var variable in variables)
                    {
                        Environment.SetEnvironmentVariable(variable.Name, variable.Value.ToString());
                    }
                }
            }
        }

        [TestMethod]
        public void DbContext_Select_Where_ClobField()
        {
            using (var dbContext = new TestDbContext(ConnectionString))
            {
                dbContext.Database.EnsureDeleted();
                dbContext.Database.EnsureCreated();

                // Works fine for varchar
                dbContext.Set<User>().SingleOrDefault(u => u.Name== "test");

                // Fails for clob
                dbContext.Set<User>().SingleOrDefault(u => u.LongDescription == "test");
            }
        }

        [TestMethod]
        public void DbContext_Select_Contains()
        {
            using (var dbContext = new TestDbContext(ConnectionString))
            {
                dbContext.Database.EnsureDeleted();
                dbContext.Database.EnsureCreated();

                dbContext.Set<User>()
                    .Select(u => u.Name)
                    .Contains("Hello");
            }
        }

        [TestMethod]
        public void DbContext_Select_Where_Take_Count()
        {
            using (var dbContext = new TestDbContext(ConnectionString))
            {
                dbContext.Database.EnsureDeleted();
                dbContext.Database.EnsureCreated();

                dbContext.Set<User>()
                    .Where(u => u.OwnedFolders.Take(2).Count() == 2)
                    .ToList();
            }
        }

        [TestMethod]
        public void DbContext_Include_OrderBy_First()
        {
            using (var dbContext = new TestDbContext(ConnectionString))
            {
                dbContext.Database.EnsureDeleted();
                dbContext.Database.EnsureCreated();

                var user = new User { Name = "John" };
                var folder = new Folder { Name = "test", Owner = user };

                dbContext.Add(folder);
                dbContext.SaveChanges();
            }

            // Works fine without .OrderBy
            using (var dbContext = new TestDbContext(ConnectionString))
            {
                dbContext.Set<Folder>()
                    .Include(f => f.Owner)
                    .First();
            }

            // Works fine without .First
            using (var dbContext = new TestDbContext(ConnectionString))
            {
                dbContext.Set<Folder>()
                    .Include(f => f.Owner)
                    .OrderBy(f => f.Id)
                    .ToList();
            }

            // Fails with .OrderBy.First
            using (var dbContext = new TestDbContext(ConnectionString))
            {
                dbContext.Set<Folder>()
                    .Include(f => f.Owner)
                    .OrderBy(f => f.Id)
                    .First();
            }
        }

        [TestMethod]
        public void DbContext_With_DbConnection_EnsureDeleted()
        {
            // Works fine with .UseOracle(ConnectionString)
            using (var dbContext = new TestDbContext(ConnectionString))
            {
                dbContext.Database.EnsureDeleted();
            }

            // Fails with .UseOracle(DbConnection)
            using (var dbConnection = new OracleConnection { ConnectionString = ConnectionString })
            {
                using (var dbContext = new TestDbContext(dbConnection))
                {
                    dbContext.Database.EnsureDeleted();
                }
            }
        }

        [TestMethod]
        public void DbContext_With_DbConnection_EnsureDeleted2()
        {
            var serviceCollection = new ServiceCollection();
            new OracleOptionsExtension().ApplyServices(serviceCollection);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            using (var dbConnection = new OracleConnection { ConnectionString = ConnectionString })
            {
                var optionsBuilder = new DbContextOptionsBuilder();
                optionsBuilder.UseInternalServiceProvider(serviceProvider);
                optionsBuilder.UseOracle(dbConnection);

                using (var dbContext = new DbContext(optionsBuilder.Options))
                {
                    dbContext.Database.EnsureDeleted();
                }
            }
        }

        [TestMethod]
        public void DbContext_With_DbConnection_EnsureDeleted3()
        {
            using (var dbConnection = new OracleConnection { ConnectionString = ConnectionString })
            {
                var optionsBuilder = new DbContextOptionsBuilder();
                optionsBuilder.UseOracle(dbConnection);

                using (var dbContext = new DbContext(optionsBuilder.Options))
                {
                    dbContext.Database.EnsureDeleted();
                }
            }
        }

        [TestMethod]
        public void DbContext_With_DbConnection_GetOracleConnection()
        {
            using (var dbConnection = new OracleConnection { ConnectionString = ConnectionString })
            {
                using (var dbContext = new TestDbContext(dbConnection))
                {
                    Assert.AreSame(dbConnection, dbContext.Database.GetOracleConnection());
                }
            }
        }

        [TestMethod]
        public void DbContext_With_DbConnection_UseTransaction()
        {
            using (var dbConnection = new OracleConnection { ConnectionString = ConnectionString })
            {
                dbConnection.Open();
                using (var dbTransaction = dbConnection.BeginTransaction())
                {
                    using (var dbContext = new TestDbContext(dbConnection))
                    {
                        dbContext.Database.UseTransaction(dbTransaction);
                        // InvalidOperationException: The specified transaction is not associated with the current connection.
                        // Only transactions associated with the current connection may be used.
                    }
                }
            }
        }

        [TestMethod]
        public void DbContext_With_DbConnection_Where_StringField_Equals_Property()
        {
            // Prepare the database with .UseOracle(ConnectionString), because .UseOracle(Connection) is broken.
            using (var dbContext = new TestDbContext(ConnectionString))
            {
                dbContext.Database.EnsureDeleted();
                dbContext.Database.EnsureCreated();

                // Works fine with .UseOracle(ConnectionString)
                dbContext.Set<User>().Where(u => u.Name == Environment.UserName).ToList();
            }

            using (var dbConnection = new OracleConnection { ConnectionString = ConnectionString })
            {
                using (var dbContext = new TestDbContext(dbConnection))
                {
                    // Works fine with string literals
                    dbContext.Set<User>().Where(u => u.Name == "Test").ToList();

                    // Fails with const property
                    dbContext.Set<User>().Where(u => u.Name == Environment.UserName).ToList();
                }
            }
        }

        [TestMethod]
        public void DbContext_With_DbConnection_SaveChanges()
        {
            // Prepare the database with .UseOracle(ConnectionString), because .UseOracle(Connection) is broken.
            using (var dbContext = new TestDbContext(ConnectionString))
            {
                dbContext.Database.EnsureDeleted();
                dbContext.Database.EnsureCreated();

                // Works fine with .UseOracle(ConnectionString)
                var user = new User { Name = "Oliver" };
                dbContext.Add(user);
                dbContext.SaveChanges();
            }

            // Fails with .UseOracle(DbConnection)
            using (var dbConnection = new OracleConnection { ConnectionString = ConnectionString })
            {
                using (var dbContext = new TestDbContext(dbConnection))
                {
                    var user = new User { Name = "John" };
                    dbContext.Add(user);
                    dbContext.SaveChanges();
                }
            }
        }

        [TestMethod]
        [ExpectedException(typeof(DbUpdateConcurrencyException))]
        public void DbContext_DbUpdateConcurrencyException()
        {
            using (var dbContext = new TestDbContext(ConnectionString))
            {
                dbContext.Database.EnsureDeleted();
                dbContext.Database.EnsureCreated();

                var user = new User { Name = "John" };
                dbContext.Add(user);
                dbContext.SaveChanges();

                using (var dbContext2 = new TestDbContext(ConnectionString))
                {
                    User user2 = dbContext2.Set<User>().Single(u => u.Name == "John");
                    user2.Name = "Oliver";
                    dbContext2.SaveChanges();
                }

                user.LongDescription = "hello";
                dbContext.SaveChanges();
            }
        }

        [TestMethod]
        [ExpectedException(typeof(DbUpdateConcurrencyException))]
        public void DbContext_DbUpdateConcurrencyException_Async()
        {
            using (var dbContext = new TestDbContext(ConnectionString))
            {
                dbContext.Database.EnsureDeleted();
                dbContext.Database.EnsureCreated();

                var user = new User { Name = "John" };
                dbContext.Add(user);
                dbContext.SaveChanges();

                using (var dbContext2 = new TestDbContext(ConnectionString))
                {
                    User user2 = dbContext2.Set<User>().Single(u => u.Name == "John");
                    user2.Name = "Oliver";
                    dbContext2.SaveChanges();
                }

                user.LongDescription = "hello";

                try
                {
                    dbContext.SaveChangesAsync().Wait();
                }
                catch (AggregateException ex)
                {
                    throw ex.InnerExceptions.Single();
                }
            }
        }

        [TestMethod]
        public void DbContext_With_LoggerProvider_And_Named_Index()
        {
            using (var dbContext = new TestDbContext(
                builder =>
                {
                    builder.UseOracle(ConnectionString);

                    // REMOVE after DbContext_Model_Caching is fixed
                    var loggerFactory = new LoggerFactory();
                    loggerFactory.AddProvider(new NullLoggerProvider());
                    builder.UseLoggerFactory(loggerFactory);
                },
                modelBuilder =>
                {
                    modelBuilder.Entity<User>().HasIndex(u => u.Name).HasName("USER_NAME_IDX");
                }))
            {
                dbContext.Database.EnsureDeleted();
                dbContext.Database.EnsureCreated();
            }
        }

        [TestMethod]
        public void SqlGenerator_DropForeignKey()
        {
            using (var dbContext = new TestDbContext(ConnectionString))
            {
                var op = new DropForeignKeyOperation
                {
                    IsDestructiveChange = false,
                    Name = "A",
                    Schema = "B",
                    Table = "C"
                };

                dbContext.GetService<IMigrationsSqlGenerator>().Generate(new List<MigrationOperation> { op });
            }
        }

        [TestMethod]
        public void SqlGenerator_DropUniqueConstraint()
        {
            using (var dbContext = new TestDbContext(ConnectionString))
            {
                var op = new DropUniqueConstraintOperation()
                {
                    IsDestructiveChange = false,
                    Name = "A",
                    Schema = "B",
                    Table = "C"
                };

                dbContext.GetService<IMigrationsSqlGenerator>().Generate(new List<MigrationOperation> { op });
            }
        }


        [TestMethod]
        public void ModelDiffer_ColumnType_Explicit_Vs_Conventional()
        {
            // DbContext with conventional column type for USERS.NAME
            using (var dbContext1 = new TestDbContext(ConnectionString))
            {
                // DbContext with explicit column type for USERS.NAME
                using (var dbContext2 = new TestDbContext(
                    builder =>
                    {
                        builder.UseOracle(ConnectionString);

                        // REMOVE after DbContext_Model_Caching is fixed
                        var loggerFactory = new LoggerFactory();
                        loggerFactory.AddProvider(new NullLoggerProvider());
                        builder.UseLoggerFactory(loggerFactory);
                    },
                    modelBuilder =>
                    {
                        modelBuilder.Entity<User>().Property(u => u.Name).HasColumnType("NCLOB");
                    }))
                {
                    var differ = dbContext1.GetService<IMigrationsModelDiffer>();
                    Func<IModel, string> generateDdl = model =>
                    {
                        IReadOnlyList<MigrationOperation> ops = differ.GetDifferences(null, model);
                        IReadOnlyList<MigrationCommand> cmds = dbContext1.GetService<IMigrationsSqlGenerator>().Generate(ops);
                        return string.Join(
                            Environment.NewLine,
                            cmds.Select(c => c.CommandText + Environment.NewLine + "/" + Environment.NewLine));
                    };

                    // DDLs for both models are identical
                    string ddl1 = generateDdl(dbContext1.Model);
                    string ddl2 = generateDdl(dbContext2.Model);
                    Assert.AreEqual(ddl1, ddl2);

                    // There should be no migration operations
                    IReadOnlyList<MigrationOperation> migrationOperations =
                        differ.GetDifferences(
                            dbContext1.Model,
                            dbContext2.Model);
                    Assert.IsFalse(migrationOperations.Any());
                }
            }
        }

        [TestMethod]
        public void DbContext_Model_Caching()
        {
            // DbContext with standard model
            using (var dbContext1 = new TestDbContext(ConnectionString))
            {
                // DbContext with modified model
                using (var dbContext2 = new TestDbContext(
                    builder => builder.UseOracle(ConnectionString),
                    modelBuilder => modelBuilder.Entity<User>().Property(u => u.Name).IsRequired(false)))
                {
                    Assert.IsFalse(dbContext1.Model.FindEntityType(typeof(User)).FindProperty("Name").IsNullable);
                    Assert.IsTrue(dbContext2.Model.FindEntityType(typeof(User)).FindProperty("Name").IsNullable);
                }
            }
        }

        private class NullLoggerProvider : ILoggerProvider
        {
            public ILogger CreateLogger(string categoryName) => new NullLogger();

            public void Dispose() { }

            private class NullLogger : ILogger
            {
                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(
                    LogLevel logLevel,
                    EventId eventId,
                    TState state,
                    Exception exception,
                    Func<TState, Exception, string> formatter)
                {
                }

                public IDisposable BeginScope<TState>(TState state) => null;
            }
        }
    }
}
