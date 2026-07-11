# Codex Cue Design QA

## Comparison target

- Source visual truth (full layout): `C:\Users\COOL84~1\AppData\Local\Temp\codex-clipboard-549679a5-2667-4127-888a-f1260e7c9c52.png`
- Source visual truth (final progress geometry instruction): `C:\Users\COOL84~1\AppData\Local\Temp\codex-clipboard-e9f696ce-a437-42b1-8074-9ee09030d69e.png`
- Rendered implementation: `TestResults/ui-captures/wizard-100.png`
- Focused implementation crop: `TestResults/ui-captures/wizard-top-100.png`
- Additional DPI evidence: `wizard-150.png` (1104×1224) and `wizard-200.png` (1472×1632) in the same capture directory.
- Viewport: normalized 736×816 window capture, containing a 696×776 white application surface.
- State: question 2 of 5, first option selected, Back and Next visible, Chinese Windows UI culture.

## Full-view comparison evidence

- The implementation surface is 696×776, matching the approximately 695×775 reference card.
- After aligning the different screenshot canvas margins, the title starts 101 px below the application surface in both images; the option and footer left/right insets align to within 1–2 px.
- The first selected card, second card, Other field, fixed footer, Back button, and Next button align to the reference heights and positions within approximately 0–2 px.
- Typography now uses Segoe UI / Microsoft YaHei UI at 31 px for the prompt, 24 px for option titles, 20–23 px for supporting text, and 20 px for actions. Hierarchy, wrapping, and optical weight match the source without clipping.
- The selected fill, blue border, white surfaces, muted text, dividers, corner radii, and shadow follow the reference tokens. The surrounding desktop differs from the mock's pale-blue preview canvas by design; it is outside the native application surface.
- The source's red guide and blue dot are superseded by the user's final instruction and are intentionally absent.

## Focused top-region comparison evidence

- The 6 px track occupies the complete top edge of the clipped 18 px outer surface.
- The fill begins at the outer left corner with no internal start cap; the outer-window clip supplies only the frame's corner geometry.
- The track reaches the right outer corner, while the 2/5 fill occupies exactly 40% of the surface width.
- The fill uses the approved dark-to-light blue gradient, 280 ms cubic width transition, and 2.6 s auto-reversing gradient motion. Reduced-motion mode sets final values immediately.
- The progress counter is right-aligned with a 30 px inset and lowered from the original header position. There is no title or blue dot in the drag region.

## Required fidelity surfaces

- Fonts and typography: passed. Native Segoe UI / Microsoft YaHei UI fallbacks, weight, scale, line height, hierarchy, and wrapping match; caller text remains untranslated.
- Spacing and layout rhythm: passed. Frame, header, content margins, option sizes/gaps, fixed footer, radii, borders, and button geometry align.
- Colors and visual tokens: passed. Ink, muted text, selected surface, line, primary blue, gradient, and shadow are consistent with the reference. High-contrast mode switches to Windows system brushes and native controls.
- Image quality and asset fidelity: passed. The target contains no logos, illustrations, icons, or raster assets to reproduce; no placeholders, custom SVGs, glyph icons, or generated substitutes were introduced.
- Copy and content: passed. Shell strings are localized in zh-CN/en-US; dynamic caller questions, labels, descriptions, and answers bypass localization.
- Accessibility and interaction: passed. RadioButton/CheckBox semantics, explicit automation names/IDs, focus outlines, visual tab order, keyboard navigation, reduced motion, high-contrast fallback, and 100%–200% capture sizes are implemented.

## Comparison history

1. Initial implementation finding — P2: description cards were roughly 18–20 px too short and vertical rhythm was compressed. Fix: increased card content padding and inter-card spacing while preserving footer position. Post-fix evidence: card top/bottom positions align to the full reference within 0–2 px.
2. Initial implementation finding — P2: Chinese prompt and option typography was visibly smaller than the source. Fix: adjusted the prompt to 31 px, option labels to 24 px, supporting copy to 20–23 px, and rebalanced padding. Post-fix evidence: text widths, hierarchy, and card geometry now match without overflow.
3. Capture finding — P2: early layered-window screenshots could contain black occlusion tiles. Fix: automation capture now foregrounds the window, waits for composition, detects unexpected black surface coverage, and retries before DPI normalization. Post-fix evidence: all three final captures are complete and non-empty.

## Findings

- No actionable P0, P1, or P2 visual differences remain.

## Residual test gaps / follow-up polish

- P3: high-contrast behavior is implemented with system colors and native control fallbacks, but the automated run did not temporarily change the user's Windows high-contrast setting.
- P3: the normalized 100% image is resampled from the machine's native 150% capture; use `wizard-150.png` when judging native text antialiasing.

## Primary interactions tested

- Single selection; multiple selection; free-text Other; required validation.
- Next, Back, automatic review, Edit, Submit, cancel confirmation, and answer preservation.
- Number keys, arrow movement, Space toggle, Enter, Alt+Left/Right, Escape, and Alt+F4.
- Radio/CheckBox UI Automation roles, accessible names, focus order, and three deterministic capture scales.

final result: passed
