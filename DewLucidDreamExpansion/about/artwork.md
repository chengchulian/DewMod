# Steam Artwork

- `preview.png`: 1024 x 1024, Steam Workshop preview. The game reads this path for `SetItemPreview`.
- `icon.png`: 512 x 512, local Mod list icon and small thumbnail.
- `main.png`: 1920 x 1080, wide main image for the Workshop description or image gallery.

Created by compositing existing bitmap assets with PowerShell / System.Drawing; no image generation API was used.
Design brief: bilingual title, six original Mod icons in good / chaotic / evil colors, and Limbo depths 7, 8 and 9. The small thumbnail retains the same composition.

Sources:

- Six Mod icons: `assets/icons/LucidDream_*.png`, created for this Mod.
- Limbo emblem: `GameSource/asset/ExportedProject/Assets/Texture2D/diffLimbo.png`.
- Background ring: `GameSource/asset/ExportedProject/Assets/Texture2D/texBackgroundRing0.png`.

The two game textures are used as reference inputs during compositing. Their original files stay in the read-only game export. The finished images are Mod promotional artwork, not official game artwork.

Regenerate from the repository root with PowerShell 7:

```powershell
pwsh -File ./DewLucidDreamExpansion/tools/draw-steam-artwork.ps1
```
