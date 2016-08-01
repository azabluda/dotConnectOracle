using System;
using System.Linq;
using Devart.Data.Oracle;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DevArt.Tests.dotConnect
{
    [TestClass]
    public class UnitTest
    {
        private const string ConnectionString = "User Id=pvs_c;Password=ic1;Server=demo11g;Direct=True;Sid=emp";

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
        public void DbContext_With_LoggerProvider()
        {
            using (var dbContext = new TestDbContext(
                builder =>
                {
                    builder.UseOracle(ConnectionString);

                    var loggerFactory = new LoggerFactory();
                    loggerFactory.AddProvider(new NullLoggerProvider());
                    builder.UseLoggerFactory(loggerFactory);
                }))
            {
                dbContext.Database.EnsureDeleted();
                dbContext.Database.EnsureCreated();
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
