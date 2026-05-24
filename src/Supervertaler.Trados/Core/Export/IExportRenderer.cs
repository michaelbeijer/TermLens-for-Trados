using System.Collections.Generic;

namespace Supervertaler.Trados.Core.Export
{
    /// <summary>Renders a list of <see cref="ExportSegment"/>s to a file on disk.</summary>
    public interface IExportRenderer
    {
        void Render(List<ExportSegment> segments, ExportOptions options, string outputPath);
    }
}
