# RWS App Store Manager – v4.19.78.0

**Version number:** `4.19.78.0`
**Minimum studio version:** `18.0`
**Maximum studio version:** `18.9`
**Checksum:** `8430696d1ff0d9f3cc694efceff2a35c815d8bc7fd5481b0385b7eec434ad4ec`

---

## Changelog

### Fixed
- **At 150% Windows display scaling on Settings → AI Settings, the "Test Connection" button text wrapped to two lines (button was 120 px wide; the scaled text needed more), and the "Show" button next to the API Key field was clipping to "Sho" (button was only 50 px wide).** Same general cause as the NUD fix in 4.19.77 – design-time pixel widths were tight even at 100% and ran out of horizontal room once `AutoScaleMode.Dpi` finished scaling the control.
- Fix at [`AiSettingsPanel.cs`](src/Supervertaler.Trados/Controls/AiSettingsPanel.cs): widen `_btnShowKey` from 50 → 80 and `_btnTestConnection` from 120 → 160. Both now have comfortable padding at any DPI.

For the full changelog, see: https://github.com/Supervertaler/Supervertaler-for-Trados/releases