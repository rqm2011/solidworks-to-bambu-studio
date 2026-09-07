using System;
using System.IO;

namespace SolidWorksToBambu
{
    internal static class BackupStoreTests
    {
        private static int Main()
        {
            try
            {
                return Run();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.GetType().FullName + ": " + exception.Message);
                Console.Error.WriteLine(exception.StackTrace);
                return 1;
            }
        }

        private static int Run()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "SolidWorksToBambu-BackupTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string source = Path.Combine(root, "source.3mf");
                File.WriteAllBytes(source, new byte[] { 0x33, 0x4D, 0x46, 0x01 });
                string backups = Path.Combine(root, "backups");
                MonthlyBackupStore store = new MonthlyBackupStore(backups);
                DateTime timestamp = new DateTime(2026, 9, 5, 14, 30, 25);

                string first = store.Backup(source, "零件:一.SLDPRT", timestamp);
                AssertEqual(
                    Path.Combine(backups, "2026-09", "20260905_143025_零件_一.3mf"),
                    first,
                    "first backup path");
                AssertBytesEqual(source, first, "first backup content");

                string second = store.Backup(source, "零件:一.SLDPRT", timestamp);
                AssertEqual(
                    Path.Combine(backups, "2026-09", "20260905_143025_零件_一_2.3mf"),
                    second,
                    "collision backup path");

                string nextMonth = store.Backup(
                    source,
                    "零件:一.SLDPRT",
                    new DateTime(2026, 10, 1, 0, 0, 1));
                AssertEqual(
                    Path.Combine(backups, "2026-10", "20261001_000001_零件_一.3mf"),
                    nextMonth,
                    "monthly backup path");

                Console.WriteLine("Monthly backup tests passed.");
                return 0;
            }
            finally
            {
                string expectedPrefix = Path.Combine(Path.GetTempPath(), "SolidWorksToBambu-BackupTest-");
                string resolvedRoot = Path.GetFullPath(root);
                if (resolvedRoot.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) &&
                    Directory.Exists(resolvedRoot))
                {
                    Directory.Delete(resolvedRoot, true);
                }
            }
        }

        private static void AssertEqual(string expected, string actual, string label)
        {
            if (!string.Equals(
                Path.GetFullPath(expected),
                Path.GetFullPath(actual),
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    label + " mismatch. Expected: " + expected + "; actual: " + actual);
            }
        }

        private static void AssertBytesEqual(string expectedPath, string actualPath, string label)
        {
            byte[] expected = File.ReadAllBytes(expectedPath);
            byte[] actual = File.ReadAllBytes(actualPath);
            if (expected.Length != actual.Length)
            {
                throw new InvalidOperationException(label + " length mismatch.");
            }

            for (int index = 0; index < expected.Length; index++)
            {
                if (expected[index] != actual[index])
                {
                    throw new InvalidOperationException(label + " byte mismatch at " + index + ".");
                }
            }
        }
    }
}
