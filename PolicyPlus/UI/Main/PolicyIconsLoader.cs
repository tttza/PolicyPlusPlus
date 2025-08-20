using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Linq;

namespace PolicyPlus.UI.Main
{
    internal static class PolicyIconsLoader
    {
        internal enum IconSourceMode { BuiltInGenerated, EmbeddedResources, ExternalFolder }
        internal static IconSourceMode Mode = IconSourceMode.EmbeddedResources; // default

        // Fixed historical index ordering
        private static readonly string[] IconFileOrder = new[]
        {
            "folder.png","folder_error.png","folder_delete.png","folder_go.png","page_white.png","page_white_gear.png","arrow_up.png","page_white_error.png","delete.png","arrow_right.png","package.png","computer.png","database.png","cog.png","text_allcaps.png","calculator.png","cog_edit.png","accept.png","cross.png","application_xp_terminal.png","application_form.png","text_align_left.png","calculator_edit.png","wrench.png","textfield.png","tick.png","text_horizontalrule.png","table.png","table_sort.png","font_go.png","application_view_list.png","brick.png","error.png","style.png","sound_low.png","arrow_down.png","style_go.png","exclamation.png","application_cascade.png","page_copy.png","page.png","calculator_add.png","page_go.png"
        };

        private static Dictionary<string,string> _embeddedMap; // fileName -> full resource name
        internal static void LoadBuiltIn(ImageList list)
        {
            if (list == null)
                return;
            list.Images.Clear();
            list.ImageSize = new Size(16, 16);
            list.ColorDepth = ColorDepth.Depth32Bit;
            // Minimal set matching the first 8 indices used throughout UI logic
            for (int i = 0; i < 8 && i < IconFileOrder.Length; i++)
                list.Images.Add(IconFileOrder[i], CreateIcon(i));
        }

        internal static void LoadEmbedded(ImageList list)
        {
            if (list == null) return;
            list.Images.Clear();
            list.ImageSize = new Size(16,16);
            list.ColorDepth = ColorDepth.Depth32Bit;
            EnsureEmbeddedMap();
            for (int i = 0; i < IconFileOrder.Length; i++)
            {
                var file = IconFileOrder[i];
                string resName;
                if (_embeddedMap.TryGetValue(file, out resName))
                {
                    try
                    {
                        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName);
                        if (stream != null)
                        {
                            using var bmp = new Bitmap(stream);
                            list.Images.Add(file, new Bitmap(bmp));
                            continue;
                        }
                    }
                    catch { }
                }
                list.Images.Add(file, CreateIcon(i));
            }
        }

        internal static void Initialize(ImageList list)
        {
            switch (Mode)
            {
                case IconSourceMode.EmbeddedResources: LoadEmbedded(list); break;
                case IconSourceMode.ExternalFolder: LoadEmbedded(list); break; // placeholder external folder logic removed
                default: LoadBuiltIn(list); break;
            }
        }

    private static Bitmap CreateIcon(int index)
        {
            int s = 16;
            var bmp = new Bitmap(s, s, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            // transparent background by default
            switch (index)
            {
                case 0: // normal category (folder)
                    DrawFolder(g, new SolidBrush(Color.Goldenrod), s, open:false); break;
                case 1: // orphan category (folder red outline)
                    DrawFolder(g, new SolidBrush(Color.Goldenrod), s, open:false); using(var p=new Pen(Color.Red,2)) g.DrawRectangle(p,2,5, s-5, s-8); break;
                case 2: // empty category (grey folder)
                    DrawFolder(g, new SolidBrush(Color.Gray), s, open:false); break;
                case 3: // selected category (open folder)
                    DrawFolder(g, new SolidBrush(Color.Goldenrod), s, open:true); break;
                case 4: // normal policy (document)
                    DrawDocument(g, Color.SteelBlue, gear:false); break;
                case 5: // extra config policy (document + gear)
                    DrawDocument(g, Color.MediumPurple, gear:true); break;
                case 6: // up arrow
                    using(var b=new SolidBrush(Color.DarkCyan)) g.FillEllipse(b,1,1,s-3,s-3);
                    using(var f=new SolidBrush(Color.White)) g.FillPolygon(f, new[]{ new Point(s/2,3), new Point(4,s-5), new Point(s-5,s-5)}); break;
                case 7: // preference (exclamation)
                    using(var b=new SolidBrush(Color.Crimson)) g.FillEllipse(b,1,1,s-3,s-3);
                    using(var font=new Font(FontFamily.GenericSansSerif,9,FontStyle.Bold,GraphicsUnit.Pixel)) g.DrawString("!", font, Brushes.White, 5,2); break;
            }
            return bmp;
        }

    private static Color GetColorForIndex(int i)
        {
            return i switch
            {
                0 => Color.SteelBlue,
                1 => Color.OrangeRed,
                2 => Color.Gray,
                3 => Color.Goldenrod,
                4 => Color.ForestGreen,
                5 => Color.MediumPurple,
                6 => Color.DarkCyan,
                7 => Color.Crimson,
                _ => Color.DarkGray
            };
        }

    private static void DrawFolder(Graphics g, Brush fill, int s, bool open)
        {
            // tab
            g.FillRectangle(fill, 2, 3, 6, 4);
            // body
            g.FillRectangle(fill, 1, 6, s-3, s-8);
            if (open)
            {
                using var p = new Pen(Color.SaddleBrown,1);
                g.DrawRectangle(p,1,6,s-4,s-9);
                // simple open effect
                using var p2 = new Pen(Color.Peru,1);
                g.DrawLine(p2,1,6, s-4,6);
            }
            else
            {
                using var p = new Pen(Color.SaddleBrown,1);
                g.DrawRectangle(p,1,6,s-4,s-9);
            }
        }

        private static void DrawDocument(Graphics g, Color accent, bool gear)
        {
            int s = 16;
            using var body = new SolidBrush(Color.White);
            using var border = new Pen(Color.Black,1);
            g.FillRectangle(body,3,2,s-7,s-5);
            g.DrawRectangle(border,3,2,s-8,s-6);
            using var line = new Pen(accent,1);
            g.DrawLine(line,5,5,s-10+10,5);
            g.DrawLine(line,5,7,s-10+10,7);
            if (gear)
            {
                // tiny gear (circle + spokes)
                using var b = new SolidBrush(accent);
                g.FillEllipse(b, s-8, s-10, 6,6);
                using var p = new Pen(Color.White,1);
                g.DrawLine(p, s-5, s-10, s-5, s-4);
                g.DrawLine(p, s-8, s-7, s-2, s-7);
            }
        }

        private static void EnsureEmbeddedMap()
        {
            if (_embeddedMap != null) return;
            _embeddedMap = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            var asm = Assembly.GetExecutingAssembly();
            var resources = asm.GetManifestResourceNames();
            foreach (var full in resources)
            {
                foreach (var file in IconFileOrder)
                {
                    if (full.EndsWith("." + file, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!_embeddedMap.ContainsKey(file))
                            _embeddedMap[file] = full;
                    }
                }
            }
        }
    }
}
