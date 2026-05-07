# RWS App Store Manager – v4.19.82.0

**Version number:** `4.19.82.0`
**Minimum studio version:** `18.0`
**Maximum studio version:** `18.9`
**Checksum:** `577f22160734ea728fa7d2dfd74b705c5cf58560fee8ad215ce89b4008275855`

---

## Changelog

### Fixed
- **At 150% Windows display scaling on Settings → Termbases, three different layout problems were visible at once:**
- The right-aligned button row clipped to "Ope Export Import − +" – the **Open** button cropped from "Open" to "Ope", and the **− Remove** / **+ Add** buttons lost their text labels entirely (only the symbol remained).
- In the termbases grid, the bold column headers **"Write"** and **"Terms"** clipped to "Wr..." and "Ter...".
- The bottom rows ("Panel font size", "Term shortcuts", "Shortcut delay") had their input controls overlapping the labels, because the labels' AutoSize widths grew past the fixed x=130 input column.
- Fix at [`TermLensSettingsForm.cs`](src/Supervertaler.Trados/Settings/TermLensSettingsForm.cs):
- All five buttons (Open / Export / Import / Remove / Add) now use `AutoSize = true` with `AutoSizeMode.GrowAndShrink` and `MinimumSize` for the previous fixed widths. The right-edge anchoring chain uses each button's measured `PreferredSize.Width` instead of literal pixel offsets.
- DataGridView column widths bumped: Read/Write 54→80, Project 72→90, CS 40→56, Terms 60→80. (DGV column widths don't participate in AutoScaleMode.Dpi scaling, so they need explicit headroom.)
- The bottom-row inputs are now positioned at a shared `inputX` computed from the widest of the three labels' actual `PreferredSize.Width`, with the trailing unit labels ("pt", "ms") chained off the input's `Right` edge. NUDs widened from 60/70 to 80/90 to give the digits a comfortable area after autoscale.

For the full changelog, see: https://github.com/Supervertaler/Supervertaler-for-Trados/releases