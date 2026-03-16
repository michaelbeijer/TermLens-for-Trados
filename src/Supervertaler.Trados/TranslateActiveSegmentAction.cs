using System.Windows.Forms;
using Sdl.Desktop.IntegrationApi;
using Sdl.Desktop.IntegrationApi.Extensions;
using Sdl.TranslationStudioAutomation.IntegrationApi;
using Sdl.TranslationStudioAutomation.IntegrationApi.Presentation.DefaultLocations;
using Supervertaler.Trados.Licensing;

namespace Supervertaler.Trados
{
    /// <summary>
    /// Editor action: Ctrl+T translates the active segment using the batch translate
    /// settings (same provider, prompt, and termbase configuration).
    /// </summary>
    [Action("Supervertaler_TranslateActiveSegment", typeof(EditorController),
        Name = "Translate active segment",
        Description = "Translate the active segment using the batch translate settings")]
    [ActionLayout(
        typeof(TranslationStudioDefaultContextMenus.EditorDocumentContextMenuLocation), 8,
        DisplayType.Default, "", true)]
    [Shortcut(Keys.Control | Keys.T)]
    public class TranslateActiveSegmentAction : AbstractAction
    {
        protected override void Execute()
        {
            if (!LicenseManager.Instance.HasTier2Access)
            {
                LicenseManager.ShowUpgradeMessage();
                return;
            }

            AiAssistantViewPart.HandleTranslateActiveSegment();
        }
    }
}
