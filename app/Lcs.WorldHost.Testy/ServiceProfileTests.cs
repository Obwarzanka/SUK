using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Lcs.WorldHost.Testy;

/// <summary>Profil i ACL są granicą konfiguracji lokalnej usługi, nie mechanizmem autoryzacji gry.</summary>
public sealed class ServiceProfileTests
{
    static ServiceProfile Valid(string? data = null) => new()
    {
        AccountSid = "S-1-5-21-1-2-3-1001", DataRoot = data ?? Path.Combine(Path.GetTempPath(), "SUK-service-profile-test")
    };

    static string Json(ServiceProfile p) => JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        [nameof(p.SchemaVersion)] = p.SchemaVersion, [nameof(p.ServiceName)] = p.ServiceName,
        [nameof(p.AccountSid)] = p.AccountSid, [nameof(p.HealthClientSid)] = p.HealthClientSid,
        [nameof(p.DataRoot)] = p.DataRoot, [nameof(p.WorldId)] = p.WorldId,
        [nameof(p.StationFile)] = p.StationFile, [nameof(p.Scenario)] = p.Scenario, [nameof(p.PipeName)] = p.PipeName,
        [nameof(p.StartupTimeoutSeconds)] = p.StartupTimeoutSeconds, [nameof(p.ShutdownTimeoutSeconds)] = p.ShutdownTimeoutSeconds,
        [nameof(p.FreshnessSeconds)] = p.FreshnessSeconds, [nameof(p.MaxWorkingSetMiB)] = p.MaxWorkingSetMiB
    });

    static string ProfileFile(string content)
    {
        string path = Path.Combine(TestArtifacts.Directory("service-profile-json"), "profile.json"); File.WriteAllText(path, content, new UTF8Encoding(false)); return path;
    }

    [Fact]
    public void ValidExplicitProfileLoadsAndUsesFixedInputAndRuntimeDirectories()
    {
        var profile = Valid(); profile.Validate(); var loaded = ServiceProfile.Load(ProfileFile(Json(profile)));
        Assert.Equal(profile, loaded); Assert.Equal(Path.Combine(profile.DataRoot, "Inputs", "station.json"), loaded.StationPath);
        Assert.Equal(Path.Combine(profile.DataRoot, "Logs"), loaded.LogsPath); Assert.Equal(Path.Combine(profile.DataRoot, "State"), loaded.StatePath);
    }

    [Theory]
    [InlineData(16384, true)]
    [InlineData(16385, false)]
    public void ProfileByteLimitAllowsExactSixteenKiBBoundaryAndRejectsExcess(int bytes, bool allowed)
    {
        string json = Json(Valid()); int used = Encoding.UTF8.GetByteCount(json);
        string path = ProfileFile(json + new string(' ', bytes - used)); Assert.Equal(bytes, new FileInfo(path).Length);
        if (allowed) Assert.NotNull(ServiceProfile.Load(path)); else Assert.Throws<ArgumentException>(() => ServiceProfile.Load(path));
    }

    [Theory]
    [InlineData("SchemaVersion")]
    [InlineData("AccountSid")]
    [InlineData("DataRoot")]
    public void DuplicateTopLevelFieldsCannotOverridePreviousConfiguration(string field)
    {
        string original = Json(Valid()); using var document = JsonDocument.Parse(original);
        string json = original[..^1] + ",\"" + field + "\":" + document.RootElement.GetProperty(field).GetRawText() + "}";
        Assert.Throws<ArgumentException>(() => ServiceProfile.Load(ProfileFile(json)));
    }

    [Theory]
    [InlineData("Password")]
    [InlineData("schemaVersion")]
    [InlineData("StationPath")]
    [InlineData("LogsPath")]
    [InlineData("StatePath")]
    public void UnknownOrDerivedPropertiesCannotBeSilentlyIgnored(string name)
    {
        string original = Json(Valid()); string json = original[..^1] + ",\"" + name + "\":\"nieoczekiwana-wartosc\"}";
        Assert.ThrowsAny<Exception>(() => ServiceProfile.Load(ProfileFile(json)));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"SchemaVersion\":1,}")]
    [InlineData("{\"SchemaVersion\":\"1\"}")]
    [InlineData("{ /* komentarz */ \"SchemaVersion\":1}")]
    public void ProfileRejectsNonObjectMalformedAndLenientJsonForms(string json) =>
        Assert.ThrowsAny<Exception>(() => ServiceProfile.Load(ProfileFile(json)));

    public static IEnumerable<object[]> InvalidProfiles()
    {
        var p = Valid();
        yield return [p with { SchemaVersion = 2 }]; yield return [p with { ServiceName = "inna-usluga" }];
        foreach (string sid in new[] { "", "S-1-5", "S-1-5-18", "S-1-5-32-544", "S-1-5-11", "S-1-5-2", "S-1-5-4294967296", "S-1-5-21-1-2-3-1001\n",
            "S-1-5-19", "S-1-5-20", "S-1-5-80-0", "S-1-5-21-1-2-3-500", "S-1-5-21-1-2-3-513", "S-1-5-21-1-2-3-4294967296", "S-1-5-21-1-2-1001" })
            yield return [p with { AccountSid = sid }];
        // Odbiorca health to jedno konto; szerokie grupy lokalne/domenowe dawałyby odczyt kanału wielu użytkownikom.
        foreach (string sid in new[] { "S-1-1-0", "S-1-5-11", "S-1-5-2", "S-1-5-4294967296", "S-1-5-32-545", "S-1-5-32-544", "S-1-5-4", "S-1-5-6",
            "S-1-5-7", "S-1-5-80-0", "S-1-5-21-1-2-3-513", "S-1-5-21-1-2-3-515", "S-1-12-1-1-2-3-4" })
            yield return [p with { HealthClientSid = sid }];
        foreach (string id in new[] { "", "a/b", "a b", new string('a', 49), "świat" }) yield return [p with { WorldId = id }];
        foreach (string pipe in new[] { "", "SUK.WorldHost.other.health", @"\\.\pipe\SUK.WorldHost.local.health", "SUK.WorldHost.local.health\n", new string('a', 101) })
            yield return [p with { PipeName = pipe }];
        yield return [p with { StationFile = "../station.json" }]; yield return [p with { Scenario = "brak-scenariusza" }];
        foreach (int n in new[] { 0, 121 }) yield return [p with { StartupTimeoutSeconds = n }];
        foreach (int n in new[] { 0, 31 }) yield return [p with { ShutdownTimeoutSeconds = n }];
        foreach (int n in new[] { 0, 11 }) yield return [p with { FreshnessSeconds = n }];
        foreach (int n in new[] { 63, 4097 }) yield return [p with { MaxWorkingSetMiB = n }];
        foreach (string path in new[] { "", ".", @"C:\", @"C:\dane\..\..", @"\\serwer\udzial\dane" }) yield return [p with { DataRoot = path }];
    }

    [Theory]
    [MemberData(nameof(InvalidProfiles))]
    public void InvalidIdentitiesNamesPathsAndBudgetsFailBeforeStartup(ServiceProfile profile) => Assert.ThrowsAny<ArgumentException>(profile.Validate);

    [Theory]
    [InlineData(ServiceProfile.VirtualAccountSid)]
    [InlineData("S-1-5-21-1965819934-3945776493-3898111153-1001")]
    [InlineData("S-1-5-21-1-2-3-1000")]
    [InlineData("S-1-5-21-1-2-3-4294967295")]
    public void IndividualAccountAndServiceSidsAreAccepted(string sid)
    {
        (Valid() with { AccountSid = sid }).Validate(); (Valid() with { HealthClientSid = sid }).Validate();
    }

    [Theory]
    [InlineData(1, 1, 1, 64)]
    [InlineData(120, 30, 10, 4096)]
    public void OperationalBudgetsAcceptExactDocumentedBounds(int start, int stop, int freshness, int memory) =>
        (Valid() with { StartupTimeoutSeconds = start, ShutdownTimeoutSeconds = stop, FreshnessSeconds = freshness, MaxWorkingSetMiB = memory }).Validate();

    [Theory]
    [InlineData("SUK.WorldHost.local.health", true)]
    [InlineData("a_b-c.1", true)]
    [InlineData("../health", false)]
    [InlineData("a/b", false)]
    [InlineData("a\\b", false)]
    [InlineData("a b", false)]
    [InlineData("zdrowie\n", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void PipeNameCannotBecomeFilesystemOrRemotePipePath(string? name, bool expected) => Assert.Equal(expected, ServiceProfile.ValidPipeName(name));

    [Fact]
    [SupportedOSPlatform("windows")]
    public void IsolatedPreparedEnvironmentWithReadOnlyInputsAndWritableLogsStatePasses()
    {
        if (!OperatingSystem.IsWindows()) return;
        var prepared = PreparedEnvironment(); prepared.Profile.ValidateEnvironment(prepared.Path, prepared.Binary);
    }

    [Theory]
    [InlineData("Data")]
    [InlineData("Config")]
    [InlineData("Inputs")]
    [InlineData("profile.json")]
    [InlineData("station.json")]
    [InlineData("Bin")]
    [InlineData("Lcs.WorldHost.dll")]
    [InlineData("Logs")]
    [InlineData("State")]
    [SupportedOSPlatform("windows")]
    public void ForeignWritePermissionIsRejectedForEveryProtectedServiceSurface(string target)
    {
        if (!OperatingSystem.IsWindows()) return;
        var prepared = PreparedEnvironment(writeTarget: target);
        Assert.Throws<UnauthorizedAccessException>(() => prepared.Profile.ValidateEnvironment(prepared.Path, prepared.Binary));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void ServiceAccountCannotWriteItsProfileInputOrBinaries()
    {
        if (!OperatingSystem.IsWindows()) return;
        foreach (string target in new[] { "profile.json", "station.json", "Inputs", "Bin", "Lcs.WorldHost.dll" })
        {
            var prepared = PreparedEnvironment(writeTarget: target, currentWriter: true);
            Assert.Throws<UnauthorizedAccessException>(() => prepared.Profile.ValidateEnvironment(prepared.Path, prepared.Binary));
        }
    }

    [Theory]
    [InlineData("station.json", "GW")]
    [InlineData("station.json", "GA")]
    [InlineData("Inputs", "GA")]
    [SupportedOSPlatform("windows")]
    public void GenericWriteOrAllAceForForeignSidIsRejected(string target, string right)
    {
        if (!OperatingSystem.IsWindows()) return;
        // Jawny ACE w postaci SDDL, jak po icacls (GW)/(GA), dodany razem z chronioną DACL (po niej OWNER RIGHTS blokuje zmianę).
        // Windows może zapisać go po mapowaniu na prawa pliku — oba warianty muszą być odrzucone; zapisana maska trafia do dowodów.
        var prepared = PreparedEnvironment(extraAceTarget: target, extraAce: "(A;;" + right + ";;;WD)");
        string path = prepared.Targets[target];
        var stored = (Directory.Exists(path) ? (FileSystemSecurity)new DirectoryInfo(path).GetAccessControl() : new FileInfo(path).GetAccessControl())
            .GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>()
            .Where(r => r.IdentityReference.Value == "S-1-1-0" && r.AccessControlType == AccessControlType.Allow).ToArray();
        File.WriteAllText(Path.Combine(TestArtifacts.Directory("service-profile-generic-ace"), "zapisany-ace.json"),
            JsonSerializer.Serialize(new { target, right, storedMasks = stored.Select(r => "0x" + ((int)r.FileSystemRights).ToString("X8")).ToArray() }));
        Assert.NotEmpty(stored);
        Assert.Throws<UnauthorizedAccessException>(() => prepared.Profile.ValidateEnvironment(prepared.Path, prepared.Binary));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void InheritedDataAclCannotReplaceExplicitProtectedAcl()
    {
        if (!OperatingSystem.IsWindows()) return;
        var prepared = PreparedEnvironment(inheritData: true);
        Assert.Throws<UnauthorizedAccessException>(() => prepared.Profile.ValidateEnvironment(prepared.Path, prepared.Binary));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void ForeignParentWriteCannotReplaceAnOtherwiseProtectedServiceTree()
    {
        if (!OperatingSystem.IsWindows()) return;
        var prepared = PreparedEnvironment(foreignParent: true);
        Assert.Throws<UnauthorizedAccessException>(() => prepared.Profile.ValidateEnvironment(prepared.Path, prepared.Binary));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void WrongProcessIdentityWrongProfileLocationAndBinaryOverlapAreRejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        var prepared = PreparedEnvironment();
        var other = prepared.Profile with { AccountSid = "S-1-5-21-1-2-3-2001" };
        Assert.Throws<UnauthorizedAccessException>(() => other.ValidateEnvironment(prepared.Path, prepared.Binary));
        Assert.Throws<ArgumentException>(() => prepared.Profile.ValidateEnvironment(Path.Combine(prepared.Profile.DataRoot, "profile.json"), prepared.Binary));
        Assert.Throws<ArgumentException>(() => prepared.Profile.ValidateEnvironment(prepared.Path, prepared.Profile.DataRoot));
        Assert.Throws<ArgumentException>(() => prepared.Profile.ValidateEnvironment(prepared.Path, Path.GetDirectoryName(prepared.Profile.DataRoot)));
    }

    [SupportedOSPlatform("windows")]
    static (ServiceProfile Profile, string Path, string Binary, Dictionary<string, string> Targets) PreparedEnvironment(
        string? writeTarget = null, bool currentWriter = false, bool inheritData = false, bool foreignParent = false,
        string? extraAceTarget = null, string? extraAce = null)
    {
        string evidence = TestArtifacts.Directory("service-profile-acl");
        string? configuredRoot = Environment.GetEnvironmentVariable("SUK_SERVICE_ACL_TEST_ROOT");
        string root = configuredRoot == null ? evidence : System.IO.Path.Combine(configuredRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string data = System.IO.Path.Combine(root, "Data"), binary = System.IO.Path.Combine(root, "Bin");
        File.WriteAllText(System.IO.Path.Combine(evidence, "fixture.json"), JsonSerializer.Serialize(new { root, writeTarget, currentWriter, inheritData, foreignParent }));
        var targets = new Dictionary<string, string> { ["Data"] = data, ["Bin"] = binary };
        foreach (string name in new[] { "Config", "Inputs", "Logs", "State" }) targets[name] = System.IO.Path.Combine(data, name);
        foreach (string directory in targets.Values) Directory.CreateDirectory(directory);
        string profilePath = System.IO.Path.Combine(targets["Config"], "profile.json"), station = System.IO.Path.Combine(targets["Inputs"], "station.json");
        string library = System.IO.Path.Combine(binary, "Lcs.WorldHost.dll");
        var account = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("Test wymaga SID bieżącego użytkownika.");
        var profile = Valid(data) with { AccountSid = account.Value }; File.WriteAllText(profilePath, Json(profile)); File.WriteAllText(station, "{}");
        File.WriteAllText(library, "Jawny plik testu ACL; nie jest biblioteką ani wynikiem budowania.");
        SecurityIdentifier writer = currentWriter ? account : new(WellKnownSidType.WorldSid, null);
        foreach (var (name, path) in targets) SecureDirectory(path, account, name is "Logs" or "State", name == writeTarget ? writer : null, !(inheritData && name == "Data"),
            name == extraAceTarget ? extraAce : null);
        SecureFile(profilePath, account, writeTarget == "profile.json" ? writer : null, extraAceTarget == "profile.json" ? extraAce : null);
        SecureFile(station, account, writeTarget == "station.json" ? writer : null, extraAceTarget == "station.json" ? extraAce : null);
        SecureFile(library, account, writeTarget == "Lcs.WorldHost.dll" ? writer : null, extraAceTarget == "Lcs.WorldHost.dll" ? extraAce : null);
        if (foreignParent) SecureDirectory(root, account, true, new(WellKnownSidType.WorldSid, null));
        targets["profile.json"] = profilePath; targets["station.json"] = station; targets["Lcs.WorldHost.dll"] = library;
        return (profile, profilePath, binary, targets);
    }

    [SupportedOSPlatform("windows")]
    static void SecureDirectory(string path, SecurityIdentifier account, bool writable, SecurityIdentifier? extraWriter = null, bool protect = true, string? extraAce = null)
    {
        var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(protect, false);
        const InheritanceFlags inherited = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        foreach (WellKnownSidType type in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
            acl.AddAccessRule(new(new SecurityIdentifier(type, null), FileSystemRights.FullControl, inherited, PropagationFlags.None, AccessControlType.Allow));
        acl.AddAccessRule(new(account, writable ? FileSystemRights.Modify : FileSystemRights.ReadAndExecute, inherited, PropagationFlags.None, AccessControlType.Allow));
        if (!writable) acl.AddAccessRule(new(new SecurityIdentifier("S-1-3-4"), FileSystemRights.ReadAndExecute, AccessControlType.Allow));
        if (extraWriter != null) acl.AddAccessRule(new(extraWriter, FileSystemRights.Modify, AccessControlType.Allow));
        if (extraAce != null) acl.SetSecurityDescriptorSddlForm(acl.GetSecurityDescriptorSddlForm(AccessControlSections.Access) + extraAce, AccessControlSections.Access);
        new DirectoryInfo(path).SetAccessControl(acl);
    }

    [SupportedOSPlatform("windows")]
    static void SecureFile(string path, SecurityIdentifier account, SecurityIdentifier? extraWriter = null, string? extraAce = null)
    {
        var acl = new FileSecurity(); acl.SetAccessRuleProtection(true, false);
        foreach (WellKnownSidType type in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
            acl.AddAccessRule(new(new SecurityIdentifier(type, null), FileSystemRights.FullControl, AccessControlType.Allow));
        acl.AddAccessRule(new(account, FileSystemRights.ReadAndExecute, AccessControlType.Allow));
        acl.AddAccessRule(new(new SecurityIdentifier("S-1-3-4"), FileSystemRights.ReadAndExecute, AccessControlType.Allow));
        if (extraWriter != null) acl.AddAccessRule(new(extraWriter, FileSystemRights.Modify, AccessControlType.Allow));
        if (extraAce != null) acl.SetSecurityDescriptorSddlForm(acl.GetSecurityDescriptorSddlForm(AccessControlSections.Access) + extraAce, AccessControlSections.Access);
        new FileInfo(path).SetAccessControl(acl);
    }
}
