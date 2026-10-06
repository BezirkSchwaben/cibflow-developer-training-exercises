// Testseite, Leitplanke für das ganze Sechseck: Architekturtests prüfen die Regel "Die Domäne kennt die Engine nicht"
// bei jedem Testlauf, statt sie nur aufzuschreiben. Der Compiler deckt schon viel ab: GenehmigungWorker.Domaene.csproj
// hat keine Referenz, ein using auf einen Adapter baut dort nicht. Diese Tests fangen, was der Compiler zulässt,
// etwa ein neues Paket in der Domäne, sobald sie einen Typ daraus benutzt, oder HttpClient aus .NET selbst.
// Auf der JVM leistet ArchUnit dasselbe.
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GenehmigungWorker.Tests;

/// <summary>
/// Architekturtests: ohne Engine, sie lesen nur die übersetzte Domäne per Reflection.
/// </summary>
public class ArchitekturTests
{
    private const string Regel = "Die Domäne kennt die Engine nicht";

    private static readonly Assembly Domaene = typeof(GenehmigungVerbuchen).Assembly;

    [Fact]
    public void Die_Domaene_kennt_die_Engine_nicht()
    {
        // Erlaubt ist nur .NET selbst (System...). Auch dort nicht HTTP und JSON:
        // Mit der REST-API der Engine sprechen nur die Adapter.
        var referenzen = Domaene.GetReferencedAssemblies().Select(a => a.Name ?? "").ToList();
        var verboten = referenzen
            .Where(name => !(name == "System" || name.StartsWith("System.", StringComparison.Ordinal))
                           || name.StartsWith("System.Net", StringComparison.Ordinal)
                           || name.StartsWith("System.Text.Json", StringComparison.Ordinal))
            .ToList();

        Assert.True(verboten.Count == 0,
            $"{Regel}: GenehmigungWorker.Domaene referenziert {string.Join(", ", verboten)}. " +
            "Technik gehört in einen Adapter unter src/GenehmigungWorker/Adapter/, die Domäne beschreibt " +
            "ihren Bedarf als Port unter src/GenehmigungWorker.Domaene/Ports/.");
    }

    [Fact]
    public void Kein_Typ_der_Domaene_liegt_in_einem_Adapter_Namespace()
    {
        // Typen, die der Compiler selbst erzeugt (etwa für Nullable), zählen nicht
        var falsch = Domaene.GetTypes()
            .Where(typ => !typ.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            .Where(typ => typ.Namespace is null
                          || !(typ.Namespace == "GenehmigungWorker.Domaene"
                               || typ.Namespace.StartsWith("GenehmigungWorker.Domaene.", StringComparison.Ordinal))
                          || typ.Namespace.Contains(".Adapter", StringComparison.Ordinal))
            .Select(typ => typ.FullName)
            .ToList();

        Assert.True(falsch.Count == 0,
            $"{Regel}: In GenehmigungWorker.Domaene liegen Typen außerhalb ihres Namespace oder in einem Adapter-Namespace: " +
            $"{string.Join(", ", falsch)}. Adapter gehören nach src/GenehmigungWorker/Adapter/.");
    }

    [Fact]
    public void Der_Use_Case_spricht_nach_aussen_nur_ueber_Ports()
    {
        // Alles, was der Use Case von außen bekommt, ist ein Interface aus GenehmigungWorker.Domaene.Ports
        var parameter = typeof(GenehmigungVerbuchen).GetConstructors()
            .SelectMany(konstruktor => konstruktor.GetParameters())
            .ToList();
        var keinPort = parameter
            .Where(p => !p.ParameterType.IsInterface || p.ParameterType.Namespace != "GenehmigungWorker.Domaene.Ports")
            .Select(p => $"{p.Name} ({p.ParameterType.FullName})")
            .ToList();

        Assert.NotEmpty(parameter);
        Assert.True(keinPort.Count == 0,
            $"{Regel}: GenehmigungVerbuchen bekommt {string.Join(", ", keinPort)}. " +
            "Der Use Case kennt nach außen nur Ports, die konkrete Technik steckt der Zusammenbau in Program.cs hinein.");
    }
}
