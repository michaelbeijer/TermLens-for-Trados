# Help Link Reference

All context-sensitive help links in the plugin, mapped to their online documentation pages.

Help links are defined in [`HelpSystem.cs`](src/Supervertaler.Trados/Core/HelpSystem.cs) and opened via `HelpSystem.OpenHelp(topic)`. The base URL is `https://help.supervertaler.com` (the unified Workbench + Trados help site, served from Cloudflare Pages from the [`Supervertaler-Help`](https://github.com/Supervertaler/Supervertaler-Help) repo, migrated off GitBook in May 2026).

Last audited: 2026-05-17

---

## HelpSystem topics

These are the topic constants defined in `HelpSystem.Topics` and the documentation pages they link to. URLs are folder-based and mirror the on-disk path of the source `.md` file under `trados/` in the help repo, with trailing slashes (Astro's canonical form). The authoritative URL map is [`src/generated/sidebar.js`](https://github.com/Supervertaler/Supervertaler-Help/blob/main/src/generated/sidebar.js) in the help repo, which is itself generated from `SUMMARY.md`.

| Topic constant | Online URL | Used by |
|---|---|---|
| `Overview` | [/trados/](https://help.supervertaler.com/trados/) | OpenHelp fallback |
| `Installation` | [/trados/installation/](https://help.supervertaler.com/trados/installation/) | – (not yet used in UI) |
| `GettingStarted` | [/trados/getting-started/](https://help.supervertaler.com/trados/getting-started/) | – (not yet used in UI) |
| `Licensing` | [/trados/licensing/](https://help.supervertaler.com/trados/licensing/) | Settings dialog – Licence tab |
| `AiCostGuide` | [/trados/ai-cost-guide/](https://help.supervertaler.com/trados/ai-cost-guide/) | – (not yet used in UI) |
| `TermLensPanel` | [/trados/termlens/](https://help.supervertaler.com/trados/termlens/) | MainPanelControl (? button, F1 key) |
| `AddTermDialog` | [/trados/termlens/adding-terms/](https://help.supervertaler.com/trados/termlens/adding-terms/) | AddTermDialog, BulkAddNTDialog, TermEntryEditorDialog |
| `TermLensPopup` | [/trados/termlens/termlens-popup/](https://help.supervertaler.com/trados/termlens/termlens-popup/) | – (not yet used in UI) |
| `TermPickerDialog` | [/trados/termlens/term-picker/](https://help.supervertaler.com/trados/termlens/term-picker/) | TermPickerDialog |
| `MultiTermSupport` | [/trados/multiterm-support/](https://help.supervertaler.com/trados/multiterm-support/) | MainPanelControl (MultiTerm help link) |
| `AiAssistantChat` | [/trados/ai-assistant/](https://help.supervertaler.com/trados/ai-assistant/) | AiAssistantControl (? button when on Chat tab) |
| `StudioTools` | [/trados/ai-assistant/studio-tools/](https://help.supervertaler.com/trados/ai-assistant/studio-tools/) | – (not yet used in UI) |
| `SuperMemory` | [/trados/ai-assistant/super-memory/](https://help.supervertaler.com/trados/ai-assistant/super-memory/) | – (not yet used in UI) |
| `SuperMemoryQuickAdd` | [/trados/ai-assistant/super-memory/quick-add/](https://help.supervertaler.com/trados/ai-assistant/super-memory/quick-add/) | – (not yet used in UI) |
| `SuperMemoryInbox` | [/trados/ai-assistant/super-memory/process-inbox/](https://help.supervertaler.com/trados/ai-assistant/super-memory/process-inbox/) | – (not yet used in UI) |
| `SuperMemoryHealth` | [/trados/ai-assistant/super-memory/health-check/](https://help.supervertaler.com/trados/ai-assistant/super-memory/health-check/) | – (not yet used in UI) |
| `SuperMemoryDistill` | [/trados/ai-assistant/super-memory/distill/](https://help.supervertaler.com/trados/ai-assistant/super-memory/distill/) | – (not yet used in UI) |
| `SuperMemoryObsidian` | [/trados/ai-assistant/super-memory/obsidian-setup/](https://help.supervertaler.com/trados/ai-assistant/super-memory/obsidian-setup/) | – (not yet used in UI) |
| `SuperSearch` | [/trados/supersearch/](https://help.supervertaler.com/trados/supersearch/) | – (not yet used in UI) |
| `QuickLauncher` | [/trados/quicklauncher/](https://help.supervertaler.com/trados/quicklauncher/) | – (not yet used in UI) |
| `BatchOperations` | [/trados/batch-operations/](https://help.supervertaler.com/trados/batch-operations/) | AiAssistantControl (? button when on Batch tab) |
| `BatchTranslate` | [/trados/batch-translate/](https://help.supervertaler.com/trados/batch-translate/) | – (not yet used in UI) |
| `AiProofreader` | [/trados/ai-proofreader/](https://help.supervertaler.com/trados/ai-proofreader/) | – (not yet used in UI) |
| `AiProofreaderReports` | [/trados/ai-proofreader/#reports-tab](https://help.supervertaler.com/trados/ai-proofreader/#reports-tab) | Reports tab help link |
| `ClipboardMode` | [/trados/clipboard-mode/](https://help.supervertaler.com/trados/clipboard-mode/) | – (not yet used in UI) |
| `GeneratePrompt` | [/trados/generate-prompt/](https://help.supervertaler.com/trados/generate-prompt/) | – (not yet used in UI) |
| `TermbaseEditor` | [/trados/termbase-management/](https://help.supervertaler.com/trados/termbase-management/) | TermbaseEditorDialog, NewTermbaseDialog |
| `SettingsTermLens` | [/trados/settings/termlens/](https://help.supervertaler.com/trados/settings/termlens/) | Settings dialog – TermLens tab |
| `SettingsAi` | [/trados/settings/ai-settings/](https://help.supervertaler.com/trados/settings/ai-settings/) | Settings dialog – AI Settings tab |
| `PromptLogging` | [/trados/settings/ai-settings/#prompt-logging](https://help.supervertaler.com/trados/settings/ai-settings/#prompt-logging) | Prompt-logging help link |
| `SettingsPrompts` | [/trados/settings/prompts/](https://help.supervertaler.com/trados/settings/prompts/) | Settings dialog – Prompts tab, PromptEditorDialog |
| `SettingsBackup` | [/trados/settings/backup/](https://help.supervertaler.com/trados/settings/backup/) | Settings dialog – Backup tab |
| `SettingsUsageStats` | [/trados/settings/usage-statistics/](https://help.supervertaler.com/trados/settings/usage-statistics/) | – (not yet used in UI) |
| `SettingsGeneral` | [/trados/settings/usage-statistics/](https://help.supervertaler.com/trados/settings/usage-statistics/) | (alias for `SettingsUsageStats`) |
| `ProjectSettings` | [/trados/settings/project-settings/](https://help.supervertaler.com/trados/settings/project-settings/) | – (not yet used in UI) |
| `KeyboardShortcuts` | [/trados/keyboard-shortcuts/](https://help.supervertaler.com/trados/keyboard-shortcuts/) | – (not yet used in UI) |
| `Troubleshooting` | [/trados/troubleshooting/](https://help.supervertaler.com/trados/troubleshooting/) | – (not yet used in UI) |

## Other links in the plugin

These are hardcoded URLs outside `HelpSystem`, found in the About dialog and licence manager.

| Link | URL | Location |
|---|---|---|
| Documentation (home) | [help.supervertaler.com](https://help.supervertaler.com) | AboutDialog – "Documentation" link (`HelpSystem.OpenDocsHome()`) |
| Website | [supervertaler.com](https://supervertaler.com) | AboutDialog – "Website" link |
| Support & Community | [supervertaler.com/trados/#support](https://supervertaler.com/trados/#support) | AboutDialog – "Support & Community" link |
| Source code | [github.com/Supervertaler/Supervertaler-for-Trados](https://github.com/Supervertaler/Supervertaler-for-Trados) | AboutDialog – "Source Code" link |
| Changelog | [CHANGELOG.md on GitHub](https://github.com/Supervertaler/Supervertaler-for-Trados/blob/main/CHANGELOG.md) | Website trados page – nav bar + footer |
| Purchase page | supervertaler.com/trados/ | LicenseManager – shown in trial-expired / upgrade-required messages |

## Docs source

The documentation source files live in the standalone [`Supervertaler-Help`](https://github.com/Supervertaler/Supervertaler-Help) repo. The build (Astro + Starlight, pinned to Astro 5.x for Node 22.11+ compatibility) is deployed to Cloudflare Pages from the `main` branch on every push. Cloudflare runs `npm install && npm run build` and publishes `dist/`. Custom domain `help.supervertaler.com` is attached in the Pages project; SSL is auto-managed via Let's Encrypt.

The repo's root holds [`SUMMARY.md`](https://github.com/Supervertaler/Supervertaler-Help/blob/main/SUMMARY.md) plus `trados/`, `workbench/`, and `.gitbook/` subfolders. The `.gitbook/assets/` folder is preserved for backward-compatible image references; a post-build script (`_migrate/copy-gitbook-assets.mjs`) copies it into `dist/.gitbook/assets/` so existing `../.gitbook/...` image references in the `.md` files resolve. All such references have been rewritten to absolute paths (`/.gitbook/assets/...`) to be robust against URL nesting.

## URL shape

The new site uses folder-based URLs that mirror the on-disk path of the source `.md` file:

| Source file | URL |
|---|---|
| `trados/README.md` | `/trados/` |
| `trados/installation.md` | `/trados/installation/` |
| `trados/termlens.md` | `/trados/termlens/` |
| `trados/termlens/adding-terms.md` | `/trados/termlens/adding-terms/` |
| `trados/ai-assistant/super-memory/quick-add.md` | `/trados/ai-assistant/super-memory/quick-add/` |

The Workbench side uses the same shape under `/workbench/`. Trailing slashes are canonical; Astro 301-redirects from the slashless form, so omitting one still works but adds a hop.

To regenerate the topic-to-URL map after a SUMMARY.md restructure, run `python _migrate/generate_sidebar.py` in the help repo. That regenerates `src/generated/sidebar.js`, whose `link` fields line up 1:1 with the Topics constants here.

## Notes

- The old GitBook URLs (`supervertaler.gitbook.io/help/...`) continue to work during a short handover window after the migration. Once the window closes, GitBook will be replaced with a "we've moved" notice and unpublished. Older installed plugin versions that still hit GitBook URLs will then need to be updated.
- The Settings dialog help button (`?` in title bar) and F1 key both call `GetCurrentHelpTopic()` which maps the active tab index to a topic.
- Many topics are defined for future use even though they're not currently referenced in the UI. Removing unused topics is OK – the constants are public consts so the linker will tell you what's still wired up.
