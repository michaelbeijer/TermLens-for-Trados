# RWS App Store Manager – v4.19.84.0

**Version number:** `4.19.84.0`
**Minimum studio version:** `18.0`
**Maximum studio version:** `18.9`
**Checksum:** `ddaff851547fcd5a1b8a2da55f18ac3439a936039ae7e333707aee44aa45582d`

---

## Changelog

### Fixed
- **At 150% Windows display scaling, the bottom-row chat buttons clipped to "Cle" / "Sto" / partial "Send".** The buttons had explicit `Size = new Size(UiScale.Pixels(60 / 48 / 48), 26)` – tight even at 100%, and once the rendered text width at the higher DPI exceeded the pre-scaled width, the labels chopped off.
- Fix at [`AiAssistantControl.cs`](src/Supervertaler.Trados/Controls/AiAssistantControl.cs): switch all three to `AutoSize = true` with `AutoSizeMode.GrowAndShrink` and the previous widths kept as `MinimumSize`. Internal padding (8 px each side) so the labels never touch the button border.
- **In the chat header, "Distill" clipped to "Disti..." at 150% scaling because the toolbar (`Memory Bank` label + dropdown + `?` + Process Inbox + Health Check + Distill) is a single non-wrapping row that ran out of horizontal space when the Trados side panel was at typical width and everything was scaled up.**
- Fix at [`SuperMemoryToolbar.cs`](src/Supervertaler.Trados/Controls/SuperMemoryToolbar.cs): trim the Memory Bank dropdown width from 180 → 130 logical px. Typical bank names ("default", "test-mb", "client-x") still display fully; the saved horizontal room is enough for "Distill" to fit at 150% scaling. Longer names still scroll inside the dropdown when opened.

For the full changelog, see: https://github.com/Supervertaler/Supervertaler-for-Trados/releases