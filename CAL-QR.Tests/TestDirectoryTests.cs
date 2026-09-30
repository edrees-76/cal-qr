using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CAL_QR.Tests
{
    public class TestDirectoryTests
    {
        [Fact]
        public void Delete_RemovesFolder_EvenWhenSqlitePoolStillHoldsTheDatabaseFile()
        {
            string dir = TestDirectory.Create("TestDirectoryTests_");
            string db = Path.Combine(dir, "pooled.db");

            using (var connection = new SqliteConnection($"Data Source={db}"))
            {
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "CREATE TABLE t (id INTEGER);";
                cmd.ExecuteNonQuery();
            } // الإغلاق يعيد الاتّصال إلى المجمّع فيبقى الملفّ مقفلاً

            TestDirectory.Delete(dir);

            Assert.False(Directory.Exists(dir));
        }

        [Fact]
        public void Delete_IsSafeForMissingOrEmptyPath()
        {
            TestDirectory.Delete(Path.Combine(Path.GetTempPath(), "does_not_exist_" + Guid.NewGuid().ToString("N")));
            TestDirectory.Delete(string.Empty);
        }
    }
}
