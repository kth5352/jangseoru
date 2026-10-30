using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    /// <summary>The 인연 list is not part of the repository (Data/inyeon.tsv); tests that read it are skipped when it is absent.</summary>
    internal static class InyeonData
    {
        public static readonly string SkipReason = InyeonCatalog.Characters.Any() ? null : "Data/inyeon.tsv is not in this checkout";
    }

    public sealed class InyeonFactAttribute : FactAttribute
    {
        public InyeonFactAttribute() { Skip = InyeonData.SkipReason; }
    }

    public sealed class InyeonTheoryAttribute : TheoryAttribute
    {
        public InyeonTheoryAttribute() { Skip = InyeonData.SkipReason; }
    }
}
