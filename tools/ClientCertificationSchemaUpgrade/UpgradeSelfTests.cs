namespace ClientCertificationSchemaUpgrade;
internal static class UpgradeSelfTests
{
    internal static int Run()
    {
        var count = 0;
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); count++; Console.WriteLine("PASS: " + name); }
        bool Rejected(Action action) { try { action(); return false; } catch (UpgradeRejected) { return true; } }
        Check(!UpgradePolicy.ApplyMode([]), "Default cannot upgrade");
        Check(!UpgradePolicy.ApplyMode(["--preview"]), "Preview cannot upgrade");
        Check(UpgradePolicy.ApplyMode(["--apply-active-schema-79-to-91"]), "Only exact explicit apply flag accepted");
        Check(Rejected(() => UpgradePolicy.ApplyMode(["--apply"])), "Ambiguous apply flag rejected");
        Check(Rejected(() => UpgradePolicy.ApplyMode(["--apply-active-schema-79-to-91", "--database", "other"])), "Database overrides rejected");
        var prefix = Enumerable.Range(1, 79).Select(x => "synthetic-" + x).ToArray();
        var expected = prefix.Concat(UpgradePolicy.Delta).ToArray();
        UpgradePolicy.Baseline(prefix, expected); Check(true, "Exact 79 prefix and 12 reviewed migrations accepted");
        Check(Rejected(() => UpgradePolicy.Baseline(prefix.Take(78).ToArray(), expected)), "78 baseline rejected");
        Check(Rejected(() => UpgradePolicy.Baseline(prefix.Reverse().ToArray(), expected)), "Same count with different history rejected");
        Check(Rejected(() => UpgradePolicy.Baseline(expected, expected)), "Already migrated database not blindly rerun");
        Check(Rejected(() => UpgradePolicy.Baseline(prefix, expected.Append("future").ToArray())), "Future migration rejected");
        Check(SqlEvidence.Quote("a]b") == "[a]]b]", "Metadata identifiers escaped");
        Console.WriteLine($"{count} policy checks passed; no Windows inventory, SQL, backup or migration executed."); return 0;
    }
}
