using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CAL_QR.Data;
using CAL_QR.Helpers;
using CAL_QR.Models;
using CAL_QR.Validation;
using Xunit;

namespace CAL_QR.Tests
{
    public class AttachmentLocationRulesTests
    {
        private const string Root = @"D:\NewRoot\attachments file";

        [Fact]
        public void Candidates_StoredPathFirst_ThenRecordFolder_ThenSigned()
        {
            var c = AttachmentLocationRules.Candidates(@"C:\Old\attachments file\7\scan.pdf", Root, 7, "scan.pdf");

            Assert.Equal(new[]
            {
                @"C:\Old\attachments file\7\scan.pdf",
                Path.Combine(Root, "7", "scan.pdf"),
                Path.Combine(Root, "7", "signed", "scan.pdf"),
            }, c);
        }

        [Fact]
        public void Candidates_StoredPathAlreadyUnderCurrentRoot_IsNotRepeated()
        {
            string stored = Path.Combine(Root, "7", "scan.pdf");
            var c = AttachmentLocationRules.Candidates(stored.ToUpperInvariant(), Root, 7, "scan.pdf");

            Assert.Equal(2, c.Count);
            Assert.Equal(stored.ToUpperInvariant(), c[0]);
            Assert.Equal(Path.Combine(Root, "7", "signed", "scan.pdf"), c[1]);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Candidates_WithoutRecordId_OnlyStoredPath(int recordId)
        {
            var c = AttachmentLocationRules.Candidates(@"C:\x\scan.pdf", Root, recordId, "scan.pdf");
            Assert.Equal(new[] { @"C:\x\scan.pdf" }, c);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Candidates_WithoutRoot_OnlyStoredPath(string? root)
        {
            var c = AttachmentLocationRules.Candidates(@"C:\x\scan.pdf", root, 7, "scan.pdf");
            Assert.Equal(new[] { @"C:\x\scan.pdf" }, c);
        }

        [Fact]
        public void Candidates_WithoutStoredPath_StillSearchesCurrentRoot()
        {
            var c = AttachmentLocationRules.Candidates(null, Root, 7, "scan.pdf");
            Assert.Equal(new[]
            {
                Path.Combine(Root, "7", "scan.pdf"),
                Path.Combine(Root, "7", "signed", "scan.pdf"),
            }, c);
        }

        [Theory]
        [InlineData(@"..\..\Windows\win.ini")]
        [InlineData(@"sub\scan.pdf")]
        [InlineData("sub/scan.pdf")]
        [InlineData(@"C:\other\scan.pdf")]
        [InlineData("..")]
        [InlineData(".")]
        [InlineData("a|b.pdf")]
        [InlineData("")]
        [InlineData(null)]
        public void Candidates_UnsafeFileName_NeverLeavesRecordFolder(string? fileName)
        {
            var c = AttachmentLocationRules.Candidates(@"C:\x\scan.pdf", Root, 7, fileName);
            Assert.Equal(new[] { @"C:\x\scan.pdf" }, c);
        }

        [Theory]
        [InlineData("scan.pdf", true)]
        [InlineData("تقرير المعايرة (2).pdf", true)]
        [InlineData(@"a\b.pdf", false)]
        [InlineData("..", false)]
        public void IsPlainFileName(string name, bool expected)
            => Assert.Equal(expected, AttachmentLocationRules.IsPlainFileName(name));

        [Fact]
        public void FirstExisting_ReturnsFirstMatchInOrder()
        {
            var existing = new HashSet<string> { "b", "c" };
            Assert.Equal("b", AttachmentLocationRules.FirstExisting(new[] { "a", "b", "c" }, existing.Contains));
            Assert.Null(AttachmentLocationRules.FirstExisting(new[] { "a" }, existing.Contains));
        }

        [Fact]
        public void NotFoundMessage_ListsEveryTriedPath()
        {
            var tried = new[] { @"C:\Old\7\scan.pdf", @"D:\New\7\scan.pdf" };
            string msg = AttachmentLocationRules.NotFoundMessage(tried);
            Assert.Contains(tried[0], msg);
            Assert.Contains(tried[1], msg);
        }

        // LocateAsync على قرص حقيقيّ: مرفق مساره المخزّن ميّت، والملفّ في مجلّد المرفقات الحاليّ.
        private static IDbContextFactory<CalQrDbContext> FactoryWithRoot(string? root)
        {
            var options = new DbContextOptionsBuilder<CalQrDbContext>()
                .UseInMemoryDatabase("attloc_" + Guid.NewGuid().ToString("N"))
                .Options;
            using (var ctx = new CalQrDbContext(options))
            {
                if (root != null)
                    ctx.AppSettings.Add(new AppSetting { Key = "AttachmentsPath", Value = root });
                ctx.SaveChanges();
            }
            return new InMemoryFactory(options);
        }

        private sealed class InMemoryFactory : IDbContextFactory<CalQrDbContext>
        {
            private readonly DbContextOptions<CalQrDbContext> _options;
            public InMemoryFactory(DbContextOptions<CalQrDbContext> options) => _options = options;
            public CalQrDbContext CreateDbContext() => new CalQrDbContext(_options);
        }

        [Fact]
        public async Task LocateAsync_DeadStoredPath_FindsFileInCurrentRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "calqr_attloc_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "12"));
                string real = Path.Combine(root, "12", "scan.pdf");
                File.WriteAllText(real, "x");

                var (path, _) = await AttachmentPaths.LocateAsync(
                    FactoryWithRoot(root), @"Z:\gone\attachments file\12\scan.pdf", 12, "scan.pdf");

                Assert.Equal(real, path);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Fact]
        public async Task LocateAsync_SignedCopy_FoundInSignedSubfolder()
        {
            string root = Path.Combine(Path.GetTempPath(), "calqr_attloc_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "12", "signed"));
                string real = Path.Combine(root, "12", "signed", "signed.pdf");
                File.WriteAllText(real, "x");

                var (path, _) = await AttachmentPaths.LocateAsync(
                    FactoryWithRoot(root), @"Z:\gone\attachments file\12\signed\signed.pdf", 12, "signed.pdf");

                Assert.Equal(real, path);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Fact]
        public async Task LocateAsync_NowhereOnDisk_ReturnsNullWithTriedPaths()
        {
            string root = Path.Combine(Path.GetTempPath(), "calqr_attloc_" + Guid.NewGuid().ToString("N"));
            string stored = @"Z:\gone\attachments file\12\scan.pdf";

            var (path, tried) = await AttachmentPaths.LocateAsync(FactoryWithRoot(root), stored, 12, "scan.pdf");

            Assert.Null(path);
            Assert.Equal(new[]
            {
                stored,
                Path.Combine(root, "12", "scan.pdf"),
                Path.Combine(root, "12", "signed", "scan.pdf"),
            }, tried);
        }

        [Fact]
        public async Task LocateAsync_LiveStoredPath_IsUsedAsIs()
        {
            string file = Path.Combine(Path.GetTempPath(), "calqr_attloc_" + Guid.NewGuid().ToString("N") + ".pdf");
            try
            {
                File.WriteAllText(file, "x");
                var (path, tried) = await AttachmentPaths.LocateAsync(FactoryWithRoot(null), file, 12, "other.pdf");
                Assert.Equal(file, path);
                Assert.Single(tried);
            }
            finally
            {
                if (File.Exists(file)) File.Delete(file);
            }
        }
    }
}
