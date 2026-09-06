using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NeoSTP.Infrastructure.Dte;

// Authorized one-host, two-tenant recovery probe. No host, EF, migration, key generation or business writes.
var mode = args.Length == 0 ? "existing" : "restored";
var repo = Path.GetFullPath(Environment.CurrentDirectory);
var original = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ASP.NET", "DataProtection-Keys"));
var path = original;
var output = Path.Combine(repo, "tmp", "neo-production", "key-recovery-" + mode + ".json");
var evidence = new Dictionary<string, object?> { ["GeneratedAtUtc"] = DateTime.UtcNow, ["Mode"] = mode,
    ["SqlWritesIssued"] = false, ["PlaintextOrCiphertextExported"] = false, ["AutomaticKeyGeneration"] = false };
var fields = new List<FieldResult>();
ReadOnlyRing? ring = null;
Dictionary<string, byte[]>? originalBefore = null;
Dictionary<string, byte[]>? testedBefore = null;
try
{
    if (!OperatingSystem.IsWindows() || WindowsIdentity.GetCurrent().Name != @"AzureAD\CarlosAntonioGarciaR")
        throw new InvalidOperationException();
    if (args.Length != 0)
    {
        if (args.Length != 2 || args[0] != "--restored-directory" || !Path.IsPathFullyQualified(args[1])) throw new InvalidOperationException();
        path = Path.GetFullPath(args[1]).TrimEnd(Path.DirectorySeparatorChar);
        if (!Regex.IsMatch(Path.GetFileName(path), "\\ANeoSTP-KeyRecovery-[a-f0-9]{32}\\z")
            || !string.Equals(Path.GetDirectoryName(path), Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException();
    }
    AssertDirectory(original); AssertDirectory(path);
    if (mode == "restored")
    {
        AssertProtectedAcl(path, true);
        foreach (var file in Directory.GetFiles(path, "*.xml")) AssertProtectedAcl(file, false);
        evidence["RestoredAclRestricted"] = true;
    }
    originalBefore = Hashes(original); testedBefore = Hashes(path);
    evidence["KeyDirectory"] = path;
    evidence["KeyFileCount"] = testedBefore.Count;
    evidence["OriginalAndTestedRingMatch"] = Equal(originalBefore, testedBefore);
    if (testedBefore.Count == 0 || !Equal(originalBefore, testedBefore)) throw new InvalidOperationException();
    ring = new ReadOnlyRing(new DirectoryInfo(path));
    var elements = ring.GetAllElements();
    var keyElements = elements.Where(x => x.Name.LocalName == "key").ToArray();
    var encrypted = keyElements.Length > 0 && keyElements.All(x => x.Descendants().Any(y => y.Name.LocalName == "encryptedSecret"));
    evidence["AllStoredKeyElementsEncrypted"] = encrypted;
    var services = new ServiceCollection();
    services.AddDataProtection().SetApplicationName("NeoSTP.Cloud").DisableAutomaticKeyGeneration()
        .AddKeyManagementOptions(options => options.XmlRepository = ring);
    using var provider = services.BuildServiceProvider();
    var protector = provider.GetRequiredService<IDataProtectionProvider>().CreateProtector(DataProtectionSecretProtector.Purpose);

    using var configuration = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(repo, "out/local-autostart/api/appsettings.Local.json")));
    var builder = new SqlConnectionStringBuilder(configuration.RootElement.GetProperty("ConnectionStrings").GetProperty("NeoStpDb").GetString())
    { ApplicationIntent = ApplicationIntent.ReadOnly, Pooling = false };
    if (builder.InitialCatalog != "NeoSTP_Cloud") throw new InvalidOperationException();
    await using var connection = new SqlConnection(builder.ConnectionString);
    await connection.OpenAsync();
    await using (var identity = connection.CreateCommand())
    {
        identity.CommandText = "SELECT DB_NAME(),CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)),CAST(SERVERPROPERTY('ProductMajorVersion') AS int)";
        await using var reader = await identity.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || reader.GetString(0) != "NeoSTP_Cloud"
            || !string.Equals(reader.GetString(1), Environment.MachineName, StringComparison.OrdinalIgnoreCase) || reader.GetInt32(2) != 16)
            throw new InvalidOperationException();
    }
    await using (var command = connection.CreateCommand())
    {
        command.CommandTimeout = 30;
        command.CommandText = "SELECT EmpresaId,PasswordMhCifrado,PasswordCertificadoCifrado,TokenMhCifrado FROM dbo.Dte_Configuracion WHERE EmpresaId IN (2,23) ORDER BY EmpresaId";
        await using var reader = await command.ExecuteReaderAsync();
        var tenants = new List<int>();
        while (await reader.ReadAsync())
        {
            var tenant = reader.GetInt32(0); tenants.Add(tenant);
            for (var ordinal = 1; ordinal <= 3; ordinal++)
            {
                var present = !reader.IsDBNull(ordinal) && !string.IsNullOrEmpty(reader.GetString(ordinal));
                var success = false; var nonEmpty = false;
                if (present)
                {
                    try
                    {
                        var plaintext = protector.Unprotect(reader.GetString(ordinal));
                        success = true; nonEmpty = !string.IsNullOrEmpty(plaintext);
                        plaintext = null; // No value, length or hash leaves process memory.
                    }
                    catch (CryptographicException) { }
                }
                fields.Add(new(tenant, reader.GetName(ordinal), present, success, nonEmpty));
            }
        }
        if (!tenants.SequenceEqual(new[] { 2, 23 })) throw new InvalidOperationException();
    }
    evidence["Fields"] = fields;
    evidence["AllPresentFieldsRecovered"] = fields.Where(x => x.Present).All(x => x.Decrypted && x.NonEmpty)
        && new[] { 2, 23 }.All(tenant => fields.Any(x => x.EmpresaId == tenant && x.Present));
    evidence["Passed"] = evidence["AllPresentFieldsRecovered"] is true && ring.WriteAttempts == 0;
}
catch (Exception exception)
{
    evidence["Passed"] = false;
    evidence["FailureType"] = exception.GetType().Name;
    // Deliberately omit messages/stack traces/inner exceptions from SQL, crypto and configuration providers.
}
finally
{
    var originalUnchanged = false; var testedUnchanged = false;
    try
    {
        originalUnchanged = originalBefore is not null && Equal(originalBefore, Hashes(original));
        testedUnchanged = testedBefore is not null && Equal(testedBefore, Hashes(path));
    }
    catch { }
    evidence["OriginalRingUnchanged"] = originalUnchanged;
    evidence["TestedRingUnchanged"] = testedUnchanged;
    evidence["KeyWriteAttempts"] = ring?.WriteAttempts ?? 0;
    evidence["Passed"] = evidence.GetValueOrDefault("Passed") is true && originalUnchanged && testedUnchanged;
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    var result = JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(output, result);
    Console.WriteLine(result);
    Environment.ExitCode = evidence["Passed"] is true ? 0 : 1;
}

static void AssertDirectory(string path)
{
    if (!Directory.Exists(path)) throw new InvalidOperationException();
    for (var item = new DirectoryInfo(path); item is not null; item = item.Parent)
        if ((item.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException();
    foreach (var file in new DirectoryInfo(path).GetFiles("*.xml"))
        if ((file.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException();
}
static Dictionary<string, byte[]> Hashes(string path)
    => Directory.GetFiles(path, "*.xml").ToDictionary(file => Path.GetFileName(file), file => SHA256.HashData(File.ReadAllBytes(file)), StringComparer.OrdinalIgnoreCase);
static bool Equal(Dictionary<string, byte[]> left, Dictionary<string, byte[]> right)
    => left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var hash) && CryptographicOperations.FixedTimeEquals(pair.Value, hash));
static void AssertProtectedAcl(string path, bool directory)
{
    if (!OperatingSystem.IsWindows()) throw new InvalidOperationException();
    FileSystemSecurity acl = directory ? new DirectoryInfo(path).GetAccessControl() : new FileInfo(path).GetAccessControl();
    if (directory && !acl.AreAccessRulesProtected) throw new InvalidOperationException();
    var allowed = new[] { WindowsIdentity.GetCurrent().User!.Value, "S-1-5-18", "S-1-5-32-544" };
    var rules = acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
    if (rules.Length != 3) throw new InvalidOperationException();
    foreach (var rule in rules)
        if (!allowed.Contains(rule.IdentityReference.Value) || rule.AccessControlType != AccessControlType.Allow
            || rule.FileSystemRights != FileSystemRights.FullControl) throw new InvalidOperationException();
}
sealed record FieldResult(int EmpresaId, string Field, bool Present, bool Decrypted, bool NonEmpty);
sealed class ReadOnlyRing(DirectoryInfo directory) : IXmlRepository
{
    private readonly FileSystemXmlRepository inner = new(directory, NullLoggerFactory.Instance);
    public int WriteAttempts { get; private set; }
    public IReadOnlyCollection<XElement> GetAllElements() => inner.GetAllElements();
    public void StoreElement(XElement element, string friendlyName)
    {
        WriteAttempts++;
        throw new InvalidOperationException("Key repository is read-only.");
    }
}
