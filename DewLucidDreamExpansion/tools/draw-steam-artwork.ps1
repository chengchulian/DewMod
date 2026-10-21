param(
    [string]$GameAssets = (Join-Path $PSScriptRoot '../../GameSource/asset/ExportedProject/Assets/Texture2D')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing.Common
$source = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

public static class LucidDreamSteamArtwork
{
    private static readonly Color Paper = Color.FromArgb(244, 246, 242);
    private static readonly Color Muted = Color.FromArgb(160, 173, 170);
    private static readonly Color Good = Color.FromArgb(142, 222, 186);
    private static readonly Color Chaos = Color.FromArgb(245, 207, 115);
    private static readonly Color Evil = Color.FromArgb(242, 132, 137);
    private static readonly string[] Names = {
        "AstronomyMaster", "Telescope", "ResourceInversion", "PrimordialCurse", "FallenStarGod", "HeavyFootsteps"
    };
    private static readonly string[] Labels = { "天文大师", "望远镜", "资源反转", "最原始的诅咒", "星神的堕落", "沉足" };
    private static readonly string[] Captions = { "领主星座奖励", "两格节点揭示", "金币 / 梦尘互换", "记忆1级 / 精粹40%", "星座效果减弱", "闪避冷却 +50%" };

    // 统一边界与文字度量，在输出前检查每个文字块是否溢出。
    private static void Text(Graphics g, string text, float size, RectangleF rect, Color color,
        bool bold = false, bool center = false, string family = "Microsoft YaHei UI")
    {
        using (var font = new Font(family, size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(color))
        using (var format = new StringFormat(StringFormat.GenericTypographic))
        {
            format.Alignment = center ? StringAlignment.Center : StringAlignment.Near;
            format.LineAlignment = StringAlignment.Center;
            format.FormatFlags |= StringFormatFlags.NoWrap;
            SizeF measured = g.MeasureString(text, font, new PointF(0, 0), format);
            if (measured.Width > rect.Width || measured.Height > rect.Height)
                throw new InvalidOperationException("Text exceeds bounds: " + text);
            g.DrawString(text, font, brush, rect, format);
        }
    }

    // 保留素材原始透明度，按善、混沌、恶分类着色。
    private static void Image(Graphics g, string path, Rectangle rect, Color tint, float opacity = 1f)
    {
        using (var image = System.Drawing.Image.FromFile(path))
        using (var attributes = new ImageAttributes())
        {
            var matrix = new ColorMatrix(new[] {
                new float[] {0,0,0,0,0}, new float[] {0,0,0,0,0}, new float[] {0,0,0,0,0},
                new float[] {0,0,0,opacity,0},
                new float[] {tint.R/255f,tint.G/255f,tint.B/255f,0,1}
            });
            attributes.SetColorMatrix(matrix);
            g.DrawImage(image, rect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
        }
    }

    private static void Rule(Graphics g, Color color, float x1, float y, float x2, float width = 2)
    {
        using (var pen = new Pen(color, width)) g.DrawLine(pen, x1, y, x2, y);
    }

    private static Color Category(int index) => index < 2 ? Good : index == 2 ? Chaos : Evil;
    private static string Icon(string modRoot, int index) => Path.Combine(modRoot, "assets", "icons", "LucidDream_" + Names[index] + ".png");

    private static void Canvas(Graphics g)
    {
        g.Clear(Color.FromArgb(24, 29, 30));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
    }

    public static void Square(string modRoot, string gameAssets)
    {
        using (var bitmap = new Bitmap(1024, 1024, PixelFormat.Format24bppRgb))
        using (var g = Graphics.FromImage(bitmap))
        {
            Canvas(g);
            Image(g, Path.Combine(gameAssets, "texBackgroundRing0.png"), new Rectangle(210, 280, 600, 600), Paper, .07f);
            Text(g, "SHAPE OF DREAMS  /  WORKSHOP MOD", 21, new RectangleF(60, 46, 904, 40), Muted, center: true, family: "Segoe UI");
            Text(g, "清醒梦拓展", 106, new RectangleF(56, 100, 912, 150), Paper, true, true);
            Text(g, "LUCID DREAM EXPANSION", 34, new RectangleF(60, 256, 904, 60), Paper, center: true, family: "Segoe UI");
            Rule(g, Color.FromArgb(74, 88, 88), 78, 352, 946);
            for (int i = 0; i < 6; i++)
            {
                int x = 92 + i % 3 * 296;
                int y = 386 + i / 3 * 212;
                Image(g, Icon(modRoot, i), new Rectangle(x + 56, y, 128, 128), Category(i));
                Text(g, Labels[i], 27, new RectangleF(x - 10, y + 139, 260, 50), Paper, center: true);
            }
            Rule(g, Color.FromArgb(74, 88, 88), 78, 824, 946);
            Image(g, Path.Combine(gameAssets, "diffLimbo.png"), new Rectangle(80, 864, 106, 106), Evil);
            Text(g, "迷失域", 38, new RectangleF(208, 856, 240, 70), Paper, true);
            Text(g, "LIMBO", 23, new RectangleF(210, 920, 240, 40), Muted, family: "Segoe UI");
            Text(g, "7 / 8 / 9", 69, new RectangleF(470, 850, 470, 120), Evil, true, true, "Segoe UI");
            bitmap.Save(Path.Combine(modRoot, "about", "preview.png"), ImageFormat.Png);
            using (var icon = new Bitmap(512, 512, PixelFormat.Format24bppRgb))
            using (var small = Graphics.FromImage(icon))
            {
                small.InterpolationMode = InterpolationMode.HighQualityBicubic;
                small.DrawImage(bitmap, new Rectangle(0, 0, 512, 512));
                icon.Save(Path.Combine(modRoot, "about", "icon.png"), ImageFormat.Png);
            }
        }
    }

    public static void MainImage(string modRoot, string gameAssets)
    {
        using (var bitmap = new Bitmap(1920, 1080, PixelFormat.Format24bppRgb))
        using (var g = Graphics.FromImage(bitmap))
        {
            Canvas(g);
            Image(g, Path.Combine(gameAssets, "texBackgroundRing0.png"), new Rectangle(1120, -110, 800, 800), Paper, .06f);
            Text(g, "SHAPE OF DREAMS  /  WORKSHOP MOD", 26, new RectangleF(104, 70, 1040, 54), Muted, family: "Segoe UI");
            Text(g, "清醒梦拓展", 118, new RectangleF(96, 172, 1170, 174), Paper, true);
            Text(g, "LUCID DREAM EXPANSION", 40, new RectangleF(104, 363, 1160, 70), Paper, family: "Segoe UI");
            Text(g, "6个全新清醒梦", 37, new RectangleF(104, 464, 1050, 70), Muted);
            Image(g, Path.Combine(gameAssets, "diffLimbo.png"), new Rectangle(1440, 103, 268, 268), Evil, .6f);
            Text(g, "9", 210, new RectangleF(1435, 225, 280, 300), Paper, true, true, "Segoe UI");
            Text(g, "迷失域拓展至9级", 30, new RectangleF(1290, 485, 510, 60), Evil, center: true);
            Rule(g, Color.FromArgb(74, 88, 88), 104, 586, 1816);
            Text(g, "善 / GOOD", 23, new RectangleF(104, 608, 520, 50), Good, family: "Microsoft YaHei UI");
            Text(g, "混沌 / CHAOTIC", 23, new RectangleF(684, 608, 260, 50), Chaos);
            Text(g, "恶 / EVIL", 23, new RectangleF(974, 608, 840, 50), Evil);
            for (int i = 0; i < 6; i++)
            {
                int x = 104 + i * 290;
                Image(g, Icon(modRoot, i), new Rectangle(x + 62, 686, 140, 140), Category(i));
                Text(g, Labels[i], 32, new RectangleF(x - 6, 842, 276, 64), Paper, true, true);
                Text(g, Captions[i], 22, new RectangleF(x - 6, 910, 276, 54), Muted, center: true);
            }
            Rule(g, Good, 104, 1014, 624, 4);
            Rule(g, Chaos, 684, 1014, 914, 4);
            Rule(g, Evil, 974, 1014, 1816, 4);
            bitmap.Save(Path.Combine(modRoot, "about", "main.png"), ImageFormat.Png);
        }
    }
}
'@

$references = @(
    [Drawing.Graphics].Assembly.Location,
    [Drawing.Color].Assembly.Location,
    (Join-Path $PSHOME 'System.Private.Windows.Core.dll'),
    (Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll')
)
Add-Type -TypeDefinition $source -ReferencedAssemblies $references
$modRoot = Split-Path $PSScriptRoot -Parent
[LucidDreamSteamArtwork]::Square($modRoot, $GameAssets)
[LucidDreamSteamArtwork]::MainImage($modRoot, $GameAssets)
Get-ChildItem -LiteralPath (Join-Path $modRoot 'about') -Filter '*.png' | Select-Object Name, Length
