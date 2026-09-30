$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing.Common
$drawingType = [System.Drawing.Graphics].Assembly.Location
$drawingPrimitives = [System.Drawing.Color].Assembly.Location
$windowsCore = Join-Path $PSHOME 'System.Private.Windows.Core.dll'
$gdiPlus = Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'

$source = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

public static class RoomGuidanceArtwork
{
    private static Color C(string value) => ColorTranslator.FromHtml(value);
    private static PointF P(float x, float y, int width, int height) => new PointF(x * width, y * height);

    private static void Line(Graphics g, Pen pen, int w, int h, float x1, float y1, float x2, float y2) =>
        g.DrawLine(pen, P(x1, y1, w, h), P(x2, y2, w, h));

    private static void Dot(Graphics g, float x, float y, float r, int w, int h, Color color)
    {
        using (var brush = new SolidBrush(color))
            g.FillEllipse(brush, (x-r)*w, (y-r)*h, 2*r*w, 2*r*h);
    }

    private static void Ring(Graphics g, float x, float y, float r, int w, int h, Color color, float thickness)
    {
        using (var pen = new Pen(color, thickness * w / 512f))
            g.DrawEllipse(pen, (x-r)*w, (y-r)*h, 2*r*w, 2*r*h);
    }

    private static void Gem(Graphics g, float x, float y, float r, int w, int h, Color color)
    {
        var points = new[] { P(x,y-r,w,h), P(x+r*.72f,y-r*.1f,w,h), P(x+r*.48f,y+r*.75f,w,h),
            P(x-r*.48f,y+r*.75f,w,h), P(x-r*.72f,y-r*.1f,w,h) };
        using (var brush = new SolidBrush(color)) g.FillPolygon(brush, points);
        using (var pen = new Pen(Color.FromArgb(225,255,255,255), Math.Max(1,w*.003f))) g.DrawPolygon(pen, points);
    }

    private static void Target(Graphics g, float x, float y, float r, int w, int h, Color color, int type)
    {
        Dot(g,x,y,r*1.55f,w,h,Color.FromArgb(235,17,27,32));
        Ring(g,x,y,r*1.48f,w,h,Color.FromArgb(140,color),2);
        Ring(g,x,y,r*1.13f,w,h,color,1);
        if (type == 0) Gem(g,x,y,r*.7f,w,h,color);
        else if (type == 1) { Dot(g,x,y,r*.58f,w,h,color); Dot(g,x,y,r*.27f,w,h,C("#172126")); }
        else using (var pen = new Pen(color,Math.Max(1,w*.003f)))
            g.DrawArc(pen,(x-r*.62f)*w,(y-r*.62f)*h,r*1.24f*w,r*1.24f*h,200,280);
    }

    private static void Arrow(Graphics g, float x, float y, float r, int w, int h, Color color)
    {
        using (var brush = new SolidBrush(color))
            g.FillPolygon(brush,new[]{P(x-r,y+r,w,h),P(x,y-r,w,h),P(x+r,y+r,w,h),P(x,y+r*.38f,w,h)});
    }

    private static void Text(Graphics g, string text, float x, float y, float size, int w, int h,
        Color color, bool bold, bool centered)
    {
        using (var font = new Font("Microsoft YaHei UI",size*w,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(color))
        using (var format = new StringFormat())
        {
            format.Alignment = centered ? StringAlignment.Center : StringAlignment.Near;
            format.LineAlignment = StringAlignment.Center;
            float boxWidth = (centered ? .92f : .235f) * w;
            g.DrawString(text,font,brush,new RectangleF((centered ? x-.46f : x)*w,y*h,boxWidth,.09f*h),format);
        }
    }

    public static void Create(string path, int w, int h, bool square)
    {
        using (var bitmap = new Bitmap(w,h,PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (var bg = new LinearGradientBrush(new Rectangle(0,0,w,h),C("#111b20"),C("#29413e"),35))
                g.FillRectangle(bg,0,0,w,h);

            using (var grid = new Pen(Color.FromArgb(16,150,215,189),Math.Max(1,w/700f)))
                for (int i=0;i<9;i++) { Line(g,grid,w,h,.03f+i*.08f,.1f,.03f+i*.08f,.9f); Line(g,grid,w,h,.04f,.15f+i*.09f,.68f,.15f+i*.09f); }

            var room = new[]{P(.11f,.22f,w,h),P(.49f,.18f,w,h),P(.62f,.31f,w,h),P(.60f,.68f,w,h),P(.40f,.77f,w,h),P(.16f,.65f,w,h)};
            using (var brush = new SolidBrush(Color.FromArgb(225,31,49,48))) g.FillPolygon(brush,room);
            using (var pen = new Pen(C("#75d6ae"),w*.007f)) { pen.LineJoin=LineJoin.Round; g.DrawPolygon(pen,room); }
            using (var pen = new Pen(Color.FromArgb(75,151,226,191),w*.002f))
            {
                for(int i=1;i<5;i++) Line(g,pen,w,h,.17f+i*.075f,.28f,.17f+i*.075f,.66f);
                for(int i=1;i<4;i++) Line(g,pen,w,h,.17f,.28f+i*.09f,.56f,.28f+i*.09f);
                Line(g,pen,w,h,.13f,.47f,.59f,.47f);
            }
            using (var pen = new Pen(Color.FromArgb(130,91,133,117),w*.004f))
            { Line(g,pen,w,h,.50f,.42f,.69f,.42f); Line(g,pen,w,h,.69f,.42f,.76f,.53f); Line(g,pen,w,h,.76f,.53f,.91f,.53f); }
            Dot(g,.69f,.42f,.065f,w,h,Color.FromArgb(48,255,201,107));
            using (var pen = new Pen(C("#ffc96b"),w*.009f)) g.DrawArc(pen,.67f*w,.36f*h,.1f*w,.13f*h,205,130);
            Arrow(g,.72f,.42f,.015f,w,h,C("#ffc96b"));
            Ring(g,.38f,.49f,.045f,w,h,Color.FromArgb(110,103,229,193),2);
            Dot(g,.38f,.49f,.018f,w,h,C("#a5f0cf"));
            Target(g,.10f,.34f,.025f,w,h,C("#efad59"),1); Target(g,.55f,.24f,.024f,w,h,C("#58d4cd"),0);
            Target(g,.55f,.72f,.024f,w,h,C("#f07d75"),2);
            Arrow(g,.135f,.34f,.016f,w,h,C("#efad59")); Arrow(g,.51f,.255f,.016f,w,h,C("#58d4cd"));
            Arrow(g,.51f,.69f,.016f,w,h,C("#f07d75"));

            if (square)
            {
                using (var shade = new LinearGradientBrush(new Rectangle(0,(int)(.68f*h),w,(int)(.32f*h)),Color.FromArgb(0,14,22,24),Color.FromArgb(240,14,22,24),90))
                    g.FillRectangle(shade,0,(int)(.68f*h),w,(int)(.32f*h));
                Text(g,"房间指引",.5f,.75f,.105f,w,h,C("#f2f4e9"),true,true);
                Text(g,"ROOM GUIDANCE",.5f,.865f,.04f,w,h,C("#a5f0cf"),true,true);
                Ring(g,.5f,.5f,.465f,w,h,Color.FromArgb(90,165,240,207),1);
            }
            else
            {
                Text(g,"ROOM",.755f,.37f,.06f,w,h,C("#a5f0cf"),true,false);
                Text(g,"GUIDANCE",.755f,.445f,.032f,w,h,C("#f2f4e9"),true,false);
                using (var pen = new Pen(C("#efad59"),w*.004f)) Line(g,pen,w,h,.755f,.55f,.92f,.55f);
                Text(g,"房间指引",.755f,.57f,.038f,w,h,C("#f2f4e9"),true,false);
                Text(g,"SHRINES / GEMS / SKILLS",.755f,.65f,.014f,w,h,C("#b6c9bd"),false,false);
                Text(g,"NEVER MISS WHAT'S AROUND YOU",.755f,.70f,.01f,w,h,C("#91ab9d"),false,false);
                using (var pen = new Pen(Color.FromArgb(60,165,240,207),w*.0015f)) g.DrawRectangle(pen,.72f*w,.28f*h,.235f*w,.52f*h);
            }
            bitmap.Save(path,ImageFormat.Png);
        }
    }
}
'@

Add-Type -TypeDefinition $source -ReferencedAssemblies @($drawingType,$drawingPrimitives,$windowsCore,$gdiPlus)
$about = Join-Path $PSScriptRoot '..\about'
[RoomGuidanceArtwork]::Create((Join-Path $about 'icon.png'),512,512,$true)
[RoomGuidanceArtwork]::Create((Join-Path $about 'preview.png'),1920,1080,$false)
