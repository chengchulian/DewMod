param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../about'))

$ErrorActionPreference = 'Stop'
# 直接绘制几何图形，以双倍采样输出 PNG；不依赖外部图片或在线服务。
Add-Type -AssemblyName System.Drawing
$drawingReferences = @('System.Drawing.Common', 'System.Drawing.Primitives')
foreach ($assembly in @('System.Private.Windows.Core', 'System.Private.Windows.GdiPlus')) {
    if (Test-Path -LiteralPath (Join-Path $PSHOME ($assembly + '.dll'))) { $drawingReferences += $assembly }
}
Add-Type -ReferencedAssemblies $drawingReferences -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

public static class GemSlotArtwork
{
    static readonly Color Ink = Color.FromArgb(11, 20, 32);
    static readonly Color Teal = Color.FromArgb(91, 224, 208);
    static readonly Color Gold = Color.FromArgb(246, 195, 108);
    static readonly Color White = Color.FromArgb(234, 244, 247);
    static readonly Color Muted = Color.FromArgb(142, 169, 186);

    static void Line(Graphics g, Color c, float w, float x1, float y1, float x2, float y2)
    { using (var p = new Pen(c, w)) g.DrawLine(p, x1, y1, x2, y2); }
    static void Circle(Graphics g, Color c, float w, float x, float y, float radius)
    { using (var p = new Pen(c, w)) g.DrawEllipse(p, x-radius, y-radius, radius*2, radius*2); }
    static void Disc(Graphics g, Color c, float x, float y, float radius)
    { using (var b = new SolidBrush(c)) g.FillEllipse(b, x-radius, y-radius, radius*2, radius*2); }
    static void Polygon(Graphics g, Color c, params PointF[] points)
    { using (var b = new SolidBrush(c)) g.FillPolygon(b, points); }
    static void Text(Graphics g, string text, float size, Color c, float x, float y, bool bold = false)
    {
        using (var font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(c))
            g.DrawString(text, font, brush, x, y, StringFormat.GenericTypographic);
    }
    static void CenterText(Graphics g, string text, float size, Color c, float x, float y)
    {
        using (var font = new Font("Microsoft YaHei UI", size, FontStyle.Regular, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(c))
        using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            g.DrawString(text, font, brush, x, y, format);
    }
    static void Background(Graphics g, int width, int height)
    {
        using (var brush = new LinearGradientBrush(new Rectangle(0,0,width,height), Ink, Color.FromArgb(23,43,59), 20f))
            g.FillRectangle(brush, 0,0,width,height);
        for (int i = -height; i < width; i += 80)
            Line(g, Color.FromArgb(12, Teal), 1, i, height, i+height, 0);
    }
    static void Gem(Graphics g, float x, float y, float r)
    {
        Disc(g, Color.FromArgb(23, Teal), x,y,r*1.24f);
        Disc(g, Color.FromArgb(17,37,48), x,y,r);
        Circle(g, Color.FromArgb(150, Teal), r*.065f, x,y,r);
        Polygon(g, Teal, new PointF(x,y-r*.63f), new PointF(x+r*.39f,y-r*.12f),
            new PointF(x+r*.30f,y+r*.34f), new PointF(x,y+r*.63f),
            new PointF(x-r*.30f,y+r*.34f), new PointF(x-r*.39f,y-r*.12f));
        Polygon(g, Color.FromArgb(189,255,239), new PointF(x,y-r*.63f),new PointF(x,y+r*.63f),
            new PointF(x-r*.39f,y-r*.12f));
        Line(g, Color.FromArgb(65,120,124), r*.045f, x-r*.39f,y-r*.12f,x+r*.39f,y-r*.12f);
    }
    // 上下各四槽；通过原生几何绘图保持缩小后的轮廓和槽数清晰。
    static void Emblem(Graphics g, float cx, float cy, float scale)
    {
        var before = g.Save();
        g.TranslateTransform(cx,cy); g.ScaleTransform(scale,scale);
        Disc(g, Color.FromArgb(17,Gold),0,0,69);
        Circle(g, Color.FromArgb(38,Gold),1,0,0,76);
        Disc(g, Color.FromArgb(25,37,45),0,0,57);
        Circle(g, Gold,2.8f,0,0,57);
        Circle(g, Color.FromArgb(85,Gold),.8f,0,0,51);
        Polygon(g, Gold, new PointF(9,-36),new PointF(-26,4),new PointF(-5,4),
            new PointF(-12,35),new PointF(28,-8),new PointF(6,-8));
        Polygon(g, Color.FromArgb(255,231,175), new PointF(9,-36),new PointF(-26,4),new PointF(-5,4));
        for (int i=0;i<4;i++)
        {
            float x=(i-1.5f)*48;
            float y=(i==0||i==3)?79:91;
            Gem(g,x,-y,19); Gem(g,x,y,19);
        }
        g.Restore(before);
    }
    static void MiniLayout(Graphics g, float cx, float cy, int count)
    {
        Disc(g, Color.FromArgb(30,46,59),cx,cy,18);
        Circle(g, Gold,1.2f,cx,cy,18);
        Polygon(g, Gold,new PointF(cx,cy-10),new PointF(cx+6,cy),new PointF(cx,cy+10),new PointF(cx-6,cy));
        for(int row=0;row<2;row++)
        {
            int n=row==0?count/2:(count+1)/2;
            for(int i=0;i<n;i++)
            {
                float x=cx+(i-(n-1)*.5f)*20;
                float y=cy+(row==0?-1:1)*(n==4&&(i==0||i==3)?32:37);
                Circle(g,Teal,1.6f,x,y,7);
                Disc(g,Color.FromArgb(90,Teal),x,y,3);
            }
        }
    }
    static void Render(string path, int width, int height, Action<Graphics> draw)
    {
        using (var large = new Bitmap(width*2,height*2,PixelFormat.Format32bppArgb))
        {
            using(var g = Graphics.FromImage(large))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.ScaleTransform(2,2); draw(g);
            }
            using(var output = new Bitmap(width,height,PixelFormat.Format32bppArgb))
            {
                using(var g = Graphics.FromImage(output))
                { g.InterpolationMode=InterpolationMode.HighQualityBicubic; g.DrawImage(large,0,0,width,height); }
                output.Save(path,ImageFormat.Png);
            }
        }
    }
    public static void Generate(string directory)
    {
        Render(Path.Combine(directory,"icon.png"),256,256,g=>
        {
            Background(g,256,256);
            Circle(g,Color.FromArgb(28,Teal),1,128,128,117);
            Emblem(g,128,128,1);
        });
        Render(Path.Combine(directory,"preview.png"),1600,900,g=>
        {
            Background(g,1600,900);
            Text(g,"SHAPE OF DREAMS  /  MOD",18,Teal,84,64,true);
            Text(g,"精华槽数量",76,White,78,130,true);
            Text(g,"SKILL GEM COUNT",25,Muted,84,232);
            Text(g,"0–8",130,Gold,75,292,true);
            Text(g,"槽",34,Gold,347,393,true);
            Text(g,"上下镜像 · 布局更清晰",29,White,84,470);
            Line(g,Color.FromArgb(70,Teal),1,84,535,620,535);
            Text(g,"HUD  /  TAB 面板  /  结算页面",24,Muted,84,563);
            Text(g,"位移技能编辑 · 手柄选择",24,Muted,84,609);
            Text(g,"安装 Mod 最多 8 槽",23,Teal,84,700,true);
            Text(g,"原版客机与其堕落混沌上限最多 4 槽",21,Muted,84,744);

            Circle(g,Color.FromArgb(23,Teal),1,1140,345,279);
            Circle(g,Color.FromArgb(16,Teal),1,1140,345,257);
            Emblem(g,1140,345,2.18f);
            CenterText(g,"8 槽 · 上 4 下 4",24,White,1140,638);
            Line(g,Color.FromArgb(54,Teal),1,844,684,1490,684);
            for(int i=0;i<4;i++)
            {
                float x=900+i*160;
                MiniLayout(g,x,752,i+5);
                CenterText(g,(i+5)+" 槽  "+((i+5)/2)+"+"+((i+6)/2),17,Muted,x,823);
            }
        });
    }
}
'@

$artworkOutput = (Resolve-Path -LiteralPath $OutputDirectory).Path
[GemSlotArtwork]::Generate($artworkOutput)
Get-Item -LiteralPath (Join-Path $artworkOutput 'icon.png'), (Join-Path $artworkOutput 'preview.png') |
    Select-Object Name, Length
