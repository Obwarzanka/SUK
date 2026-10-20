using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Lcs.WorldHost;

/// <summary>Jawny profil operacyjny. Nie jest pakietem danych ani źródłem reguł srk.</summary>
public sealed record ServiceProfile
{
    public const string VirtualAccountSid = "S-1-5-80-1079715994-3052735783-2571636598-1640906306-2433607159";
    public int SchemaVersion { get; init; } = 1;
    public string ServiceName { get; init; } = "SUK.WorldHost";
    public string AccountSid { get; init; } = "";
    public string? HealthClientSid { get; init; }
    public string DataRoot { get; init; } = "";
    public string WorldId { get; init; } = "local";
    public string StationFile { get; init; } = "Inputs/station.json";
    public string Scenario { get; init; } = "podstawowy";
    public string PipeName { get; init; } = "SUK.WorldHost.local.health";
    public int StartupTimeoutSeconds { get; init; } = 30;
    public int ShutdownTimeoutSeconds { get; init; } = 10;
    public int FreshnessSeconds { get; init; } = 2;
    public int MaxWorkingSetMiB { get; init; } = 1024;
    [JsonIgnore] public string StationPath => Path.Combine(DataRoot, "Inputs", "station.json");
    [JsonIgnore] public string LogsPath => Path.Combine(DataRoot, "Logs");
    [JsonIgnore] public string StatePath => Path.Combine(DataRoot, "State");

    public static ServiceProfile Load(string path)
    {
        if (new FileInfo(path).Length > 16384) throw new ArgumentException("Profil przekracza limit 16 KiB.");
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > 16384) throw new ArgumentException("Profil przekracza limit 16 KiB.");
        using var document = JsonDocument.Parse(bytes);
        var fields = typeof(ServiceProfile).GetProperties().Where(p => p.SetMethod != null).Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            document.RootElement.EnumerateObject().Any(p => !fields.Contains(p.Name)) ||
            document.RootElement.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != document.RootElement.EnumerateObject().Count())
            throw new ArgumentException("Profil musi być obiektem bez powtórzonych pól.");
        var profile = JsonSerializer.Deserialize<ServiceProfile>(bytes, new JsonSerializerOptions
        { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow }) ?? throw new ArgumentException("Brak profilu.");
        profile.Validate(); return profile;
    }

    public void Validate()
    {
        if (SchemaVersion != 1 || ServiceName != "SUK.WorldHost" || !IndividualSid(AccountSid) ||
            HealthClientSid != null && !IndividualSid(HealthClientSid) ||
            !Regex.IsMatch(WorldId, @"^[A-Za-z0-9_-]{1,48}\z", RegexOptions.CultureInvariant) ||
            !ValidPipeName(PipeName) || PipeName != $"SUK.WorldHost.{WorldId}.health" ||
            StationFile != "Inputs/station.json" || !LcsWidzew.Sim.Rozklad.Scenarios.Any(s => s.Key == Scenario) ||
            StartupTimeoutSeconds is < 1 or > 120 || ShutdownTimeoutSeconds is < 1 or > 30 ||
            FreshnessSeconds is < 1 or > 10 || MaxWorkingSetMiB is < 64 or > 4096 ||
            !Path.IsPathFullyQualified(DataRoot) || DataRoot.StartsWith(@"\\", StringComparison.Ordinal) ||
            Path.GetFullPath(DataRoot).TrimEnd(Path.DirectorySeparatorChar).Length <= 3)
            throw new ArgumentException("Niepoprawny profil usługi.");
    }

    /// <summary>Pojedyncze konto: lokalne/domenowe (RID od 1000) albo SID konkretnej usługi. Grupy ogólne
    /// (Users, INTERACTIVE, ALL SERVICES, Domain Users), konta wbudowane i współdzielone LocalService/NetworkService są odrzucane.</summary>
    internal static bool IndividualSid(string? s)
    {
        if (s == null || !Regex.IsMatch(s, @"^S-1-5-(?:21-(?:[0-9]{1,10}-){3}|80-(?:[0-9]{1,10}-){4})[0-9]{1,10}\z", RegexOptions.CultureInvariant)) return false;
        var parts = s.Split('-').Skip(3).ToArray();
        if (!parts.All(part => uint.TryParse(part, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _))) return false;
        return parts[0] == "80" || uint.Parse(parts[^1], System.Globalization.CultureInfo.InvariantCulture) >= 1000;
    }

    /// <summary>GENERIC_ALL / GENERIC_WRITE w ACE nie są tłumaczone przez FileSystemRights na nazwane bity.</summary>
    const FileSystemRights GenericAll = (FileSystemRights)0x10000000, GenericWrite = (FileSystemRights)0x40000000;
    internal static bool Grants(FileSystemRights rights, FileSystemRights mask) => (rights & mask) != 0;

    public static bool ValidPipeName(string? name) => name is { Length: > 0 and <= 100 } &&
        name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');

    [SupportedOSPlatform("windows")]
    public void ValidateHealthReader(string profilePath)
    {
        Validate();
        if (!string.Equals(Path.GetFullPath(profilePath), Path.GetFullPath(Path.Combine(DataRoot, "Config", "profile.json")), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Profil musi znajdować się w Config/profile.json.");
        ValidateAcl(DataRoot, false, true); ValidateAcl(Path.Combine(DataRoot, "Config"), false, true); ValidateAcl(profilePath, false, true);
    }

    [SupportedOSPlatform("windows")]
    public void ValidateEnvironment(string profilePath, string? binaryDirectory = null)
    {
        Validate();
        if (WindowsIdentity.GetCurrent().User?.Value != new SecurityIdentifier(AccountSid).Value)
            throw new UnauthorizedAccessException("Konto procesu nie odpowiada profilowi usługi.");
        string expected = Path.Combine(DataRoot, "Config", "profile.json");
        if (!string.Equals(Path.GetFullPath(profilePath), Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Profil musi znajdować się w Config/profile.json.");
        string binary = Path.GetFullPath(binaryDirectory ?? AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        string data = Path.GetFullPath(DataRoot).TrimEnd(Path.DirectorySeparatorChar);
        if (IsWithin(data, binary) || IsWithin(binary, data)) throw new ArgumentException("Dane muszą być poza binariami.");
        foreach (string path in new[] { data, Path.Combine(data, "Config"), Path.Combine(data, "Inputs"), LogsPath, StatePath })
        {
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException("Brak przygotowanego katalogu usługi.");
            ValidateAcl(path, path == LogsPath || path == StatePath, true);
        }
        ValidateAcl(expected, false, true); ValidateAcl(binary, false, false);
        int binaryFiles = 0;
        foreach (string path in Directory.EnumerateFileSystemEntries(binary, "*", SearchOption.AllDirectories))
        {
            if (++binaryFiles > 4096) throw new ArgumentException("Przekroczono limit 4096 plików binariów.");
            ValidateAcl(path, false, false);
        }
        foreach (var (key, input) in SimulatorRuntime.KnownInputs(StationPath))
        {
            string? path = key switch { "signals" => Path.Combine(binary, "Data", "sygnaly_ie1.json"),
                "stock" => Path.Combine(binary, "Data", "tabor.json"), _ => input };
            if (path != null && File.Exists(path)) ValidateAcl(path, false, false);
        }
        if (!File.Exists(StationPath)) throw new FileNotFoundException("Brak zatwierdzonego wejścia świata.");
    }

    internal static bool IsWithin(string path, string root) => string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    [SupportedOSPlatform("windows")]
    void ValidateAcl(string path, bool writable, bool requireProtected)
    {
        RejectReparsePoints(path);
        FileSystemSecurity acl = Directory.Exists(path) ? new DirectoryInfo(path).GetAccessControl() : new FileInfo(path).GetAccessControl();
        if (requireProtected && !acl.AreAccessRulesProtected) throw new UnauthorizedAccessException("ACL katalogu danych musi mieć wyłączone dziedziczenie.");
        var rules = acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
        string owner = acl.GetOwner(typeof(SecurityIdentifier))?.Value ?? throw new UnauthorizedAccessException("Nieznany właściciel ACL.");
        // OWNER RIGHTS ACE usuwa domyślne WRITE_DAC właściciela. W planie wdrożenia właścicielem jest Administrator/SYSTEM.
        // Katalog zapisu (Logs/State) może należeć do samego konta usługi; obcy właściciel nadal mógłby zmienić jego DACL.
        if (!Administrative(owner) && !(writable && owner == AccountSid) && !rules.Any(r => r.IdentityReference.Value == "S-1-3-4" &&
            (r.PropagationFlags & PropagationFlags.InheritOnly) == 0 && r.AccessControlType == AccessControlType.Allow &&
            !Grants(r.FileSystemRights, FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership | GenericAll)))
            throw new UnauthorizedAccessException("Właściciel wejścia nie może mieć niejawnego prawa zmiany ACL.");
        const FileSystemRights write = FileSystemRights.Write | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles |
            FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership | GenericAll | GenericWrite;
        foreach (FileSystemAccessRule rule in rules)
        {
            if (rule.AccessControlType != AccessControlType.Allow || (rule.PropagationFlags & PropagationFlags.InheritOnly) != 0) continue;
            string sid = rule.IdentityReference.Value;
            bool administrative = Administrative(sid);
            if (Grants(rule.FileSystemRights, write) && !administrative && !(writable && sid == AccountSid))
                throw new UnauthorizedAccessException("Niedozwolone prawo zapisu w katalogu usługi.");
        }
        // Prawo usunięcia/przeniesienia rodzica pozwala podmienić całą chronioną gałąź mimo jej własnej DACL.
        for (string? parent = Path.GetDirectoryName(Path.GetFullPath(path)); parent != null; parent = Path.GetDirectoryName(parent))
        {
            var parentAcl = new DirectoryInfo(parent).GetAccessControl();
            // DELETE on a real volume root cannot rename the volume; DELETE_CHILD/WRITE_DAC/WRITE_OWNER still matter.
            FileSystemRights parentDanger = FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership | GenericAll;
            if (!WindowsVolume.IsPhysicalRoot(parent)) parentDanger |= FileSystemRights.Delete;
            var parentRules = parentAcl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
            string? parentOwner = parentAcl.GetOwner(typeof(SecurityIdentifier))?.Value;
            if (!Administrative(parentOwner ?? "") && !(AccountSid != VirtualAccountSid && parentOwner == AccountSid) &&
                !parentRules.Any(r => r.IdentityReference.Value == "S-1-3-4" && r.AccessControlType == AccessControlType.Allow &&
                    (r.PropagationFlags & PropagationFlags.InheritOnly) == 0 &&
                    !Grants(r.FileSystemRights, FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership | GenericAll)))
                throw new UnauthorizedAccessException("Właściciel rodzica może podmienić chronioną gałąź.");
            foreach (FileSystemAccessRule rule in parentRules)
                if (rule.AccessControlType == AccessControlType.Allow && (rule.PropagationFlags & PropagationFlags.InheritOnly) == 0 &&
                    !Administrative(rule.IdentityReference.Value) &&
                    !(AccountSid != VirtualAccountSid && rule.IdentityReference.Value == AccountSid) &&
                    Grants(rule.FileSystemRights, parentDanger))
                    throw new UnauthorizedAccessException("Rodzic katalogu pozwala na podmianę chronionej gałęzi.");
        }
    }

    static bool Administrative(string sid) => sid is "S-1-5-18" or "S-1-5-32-544" or
        "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464"; // Windows TrustedInstaller

    internal static void RejectReparsePoints(string path)
    {
        for (string? current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            if (File.Exists(current) || Directory.Exists(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new UnauthorizedAccessException("Ścieżka usługi nie może zawierać punktu ponownej analizy.");
    }
}
