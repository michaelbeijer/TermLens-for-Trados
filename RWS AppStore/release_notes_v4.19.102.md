# RWS App Store Manager – v4.19.102.0

**Version number:** `4.19.102.0`
**Minimum studio version:** `18.0`
**Maximum studio version:** `18.9`
**Checksum:** `e54257491f69a62edc5f40e8377d603751f4126eb9c1d85900907028ba852cd0`

---

## Changelog

Covers everything since the last App Store release (v4.19.97).

### Added

- **SuperSearch can now search your project's translation memories, not just its files.** A new mode dropdown in the search bar offers three modes:
  - **Project files** – search the project's bilingual files (the original behaviour).
  - **Files + TMs** – search the files and the project's TMs together, in one result list.
  - **TMs only** – a concordance search across the project's TMs, like Studio's built-in Concordance.
  The chosen mode is remembered between sessions. A new **TMs** button (next to **Files**) lets you pick which translation memories to include. TM hits respect the same case-sensitive / regex / whole-word options as file search, appear in the renamed **File/TM** column with the TM name shown in blue, and can be read and copied from the preview pane.
- **SuperSearch – "Match whole word".** A new **Word** checkbox next to **Aa** and **.\*** restricts matches to complete words – searching for "cat" no longer matches "category" or "scatter". Applies to both search and Replace.
- **SuperSearch can be docked as a tab in the Supervertaler Assistant panel.** If you prefer fewer floating panels, turn on *Settings → General → Panels → "Show SuperSearch as a tab in the Supervertaler Assistant panel"* and restart Trados Studio. Off by default; the standalone panel remains the default (this mode requires a Supervertaler Assistant licence).

### Improved

- **SuperSearch preview pane is now selectable and copyable.** The source and target boxes below the results grid have a right-click menu – **Copy**, **Select All**, **Copy source**, **Copy target** – plus Ctrl+A and Ctrl+C, so you can reuse a previous translation verbatim.
- **Tooltips on every SuperSearch control** – the search box, the Search / Stop buttons, the scope and mode dropdowns, the Replace bar, and the Files / TMs buttons now all explain themselves on hover.
- **"Workbench Chat" replaces "Sidekick" in the interface.** The QuickLauncher destination dropdown, the editor right-click submenu, and the Trados edit-history label now read "Workbench Chat" / "Supervertaler Workbench", reflecting that the Workbench's standalone Sidekick window was retired and its chat moved into the Workbench itself.

### Fixed

- **SuperSearch preview-pane text could not be selected or copied** – there was no way to copy a prior translation out of SuperSearch to reuse it. (Reported via the Workbench issue tracker.)
- **Proofreading report cards reordered out of sequence** after ticking an issue's checkbox to mark it addressed (e.g. 844, 633, 623, …). The report now keeps a stable, sequential order.

For the full changelog, see: https://github.com/Supervertaler/Supervertaler-for-Trados/releases
