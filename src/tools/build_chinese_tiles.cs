using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

public class PlaceItem {
    public string Name;
    public double Lat;
    public double Lon;
    public int Level; // 0 = country/ocean, 1 = city
}

class BuildChineseTiles {
    private static List<PlaceItem> places = new List<PlaceItem>();

    public static void LatLonToTilePixel(double lat, double lon, int z, out double px, out double py) {
        double n = Math.Pow(2.0, z);
        double xExact = (lon + 180.0) / 360.0 * n;
        double latRad = lat * Math.PI / 180.0;
        double yExact = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n;

        int tileX = (int)Math.Floor(xExact);
        int tileY = (int)Math.Floor(yExact);

        px = (xExact - tileX) * 256.0;
        py = (yExact - tileY) * 256.0;
    }

    public static void TileBounds(int z, int x, int y, out double minLon, out double maxLon, out double minLat, out double maxLat) {
        double n = Math.Pow(2.0, z);
        minLon = x / n * 360.0 - 180.0;
        maxLon = (x + 1) / n * 360.0 - 180.0;
        
        double latRadMax = Math.Atan(Math.Sinh(Math.PI * (1.0 - 2.0 * y / n)));
        maxLat = latRadMax * 180.0 / Math.PI;

        double latRadMin = Math.Atan(Math.Sinh(Math.PI * (1.0 - 2.0 * (y + 1) / n)));
        minLat = latRadMin * 180.0 / Math.PI;
    }

    private static void LoadWwp(string path, int level) {
        if (!File.Exists(path)) return;
        try {
            using (BinaryReader br = new BinaryReader(File.OpenRead(path), System.Text.Encoding.UTF8)) {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    string name = br.ReadString();
                    float lat = br.ReadSingle(); // first float in wwp is latitude
                    float lon = br.ReadSingle(); // second float in wwp is longitude
                    uint flags = br.ReadUInt32();
                    places.Add(new PlaceItem { Name = name, Lat = lat, Lon = lon, Level = level });
                }
            }
        } catch {}
    }

    static void Main(string[] args) {
        string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "--apply";
        string repoDir = @"G:\code\cmo-CN";
        string gameDir = @"G:\soft\steam\steamapps\common\Command - Modern Operations";
        string cacheDir = Path.Combine(gameDir, @"WW\Cache\Stamen Terrain - Labels");
        string backupDir = Path.Combine(gameDir, @"WW\Cache\Stamen Terrain - Labels.orig");

        if (!Directory.Exists(cacheDir)) {
            Console.WriteLine("Error: Stamen Terrain - Labels directory not found at " + cacheDir);
            return;
        }

        if (mode == "--revert") {
            if (Directory.Exists(backupDir)) {
                Directory.Delete(cacheDir, true);
                Directory.Move(backupDir, cacheDir);
                Console.WriteLine("Successfully reverted Stamen Terrain - Labels from backup.");
            } else {
                Console.WriteLine("No backup found at " + backupDir);
            }
            return;
        }

        // Backup original if not already backed up
        if (!Directory.Exists(backupDir)) {
            Console.WriteLine("Creating backup of original tiles to " + backupDir + " ...");
            CopyDirectory(cacheDir, backupDir);
            Console.WriteLine("Backup complete.");
        }

        string[] allPngs = Directory.GetFiles(cacheDir, "*.png", SearchOption.AllDirectories);
        Console.WriteLine(string.Format("Found {0} cached tiles in {1}", allPngs.Length, cacheDir));

        if (mode == "--clean") {
            Console.WriteLine("Mode: --clean (rendering fully transparent tiles to eliminate English labels)");
            using (Bitmap trans = new Bitmap(256, 256, PixelFormat.Format32bppArgb)) {
                using (Graphics g = Graphics.FromImage(trans)) g.Clear(Color.Transparent);
                byte[] pngBytes;
                using (MemoryStream ms = new MemoryStream()) {
                    trans.Save(ms, ImageFormat.Png);
                    pngBytes = ms.ToArray();
                }
                Parallel.ForEach(allPngs, png => {
                    try { File.WriteAllBytes(png, pngBytes); } catch {}
                });
            }
            Console.WriteLine("Clean mode complete: all tiles set to transparent.");
            return;
        }

        // Mode: --apply (Render localized Chinese labels)
        string synWwp = Path.Combine(gameDir, @"WW\PlacenameSets\Synoptic\0000_0000.wwp");
        string citWwp = Path.Combine(gameDir, @"WW\PlacenameSets\Cities\0000_0000.wwp");
        LoadWwp(synWwp, 0);
        LoadWwp(citWwp, 1);
        Console.WriteLine(string.Format("Loaded {0} placename landmarks from WWP files.", places.Count));

        StringFormat sf = new StringFormat();
        sf.Alignment = StringAlignment.Center;
        sf.LineAlignment = StringAlignment.Center;

        int processed = 0;
        Parallel.ForEach(allPngs, pngPath => {
            try {
                string norm = pngPath.Replace('\\', '/');
                string[] parts = norm.Split('/');
                int z = int.Parse(parts[parts.Length - 3]);
                int x = int.Parse(parts[parts.Length - 2]);
                string fName = parts[parts.Length - 1];
                int y = int.Parse(fName.Substring(0, fName.IndexOf('.')));

                double minLon, maxLon, minLat, maxLat;
                TileBounds(z, x, y, out minLon, out maxLon, out minLat, out maxLat);

                List<PlaceItem> tilePlaces = new List<PlaceItem>();
                foreach (var p in places) {
                    if (p.Lon >= minLon && p.Lon <= maxLon && p.Lat >= minLat && p.Lat <= maxLat) {
                        // z <= 5: only countries/seas (Level 0)
                        if (z <= 5) {
                            if (p.Level == 0) tilePlaces.Add(p);
                        }
                        // z == 6..7: countries + all cities
                        else if (z >= 6 && z <= 7) {
                            tilePlaces.Add(p);
                        }
                        // z >= 8: cities
                        else {
                            if (p.Level == 1) tilePlaces.Add(p);
                        }
                    }
                }

                using (Bitmap bmp = new Bitmap(256, 256, PixelFormat.Format32bppArgb)) {
                    using (Graphics g = Graphics.FromImage(bmp)) {
                        g.Clear(Color.Transparent);

                        if (tilePlaces.Count > 0) {
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                            // Sort: countries first, then cities
                            tilePlaces.Sort((a, b) => a.Level.CompareTo(b.Level));

                            List<RectangleF> occupied = new List<RectangleF>();

                            foreach (var p in tilePlaces) {
                                double rawPx, rawPy;
                                LatLonToTilePixel(p.Lat, p.Lon, z, out rawPx, out rawPy);

                                float fontSize = (p.Level == 0) ? (z <= 3 ? 9.5f : (z <= 4 ? 10.5f : 11.5f)) : (z <= 7 ? 9.0f : 8.5f);
                                FontStyle fs = (p.Level == 0) ? FontStyle.Bold : FontStyle.Regular;

                                using (Font testFont = new Font("Microsoft YaHei", fontSize, fs)) {
                                    SizeF sz = g.MeasureString(p.Name, testFont);
                                    float halfW = sz.Width * 0.5f;
                                    float halfH = sz.Height * 0.5f;

                                    // Clamp position inside tile margins so text is never cut off at seams
                                    float px = (float)Math.Max(halfW + 3f, Math.Min(256f - halfW - 3f, rawPx));
                                    float py = (float)Math.Max(halfH + 3f, Math.Min(256f - halfH - 3f, rawPy));

                                    RectangleF bbox = new RectangleF(px - halfW - 2f, py - halfH - 2f, sz.Width + 4f, sz.Height + 4f);

                                    // Check collision with already drawn labels
                                    bool collides = false;
                                    foreach (var occ in occupied) {
                                        if (bbox.IntersectsWith(occ)) {
                                            collides = true;
                                            break;
                                        }
                                    }
                                    if (collides) continue; // skip colliding labels at dense regions

                                    occupied.Add(bbox);

                                    // Draw text with outline
                                    using (GraphicsPath path = new GraphicsPath()) {
                                        path.AddString(p.Name, new FontFamily("Microsoft YaHei"), (int)fs, fontSize, new PointF(px, py), sf);
                                        Color penColor = (p.Level == 0) ? Color.FromArgb(220, 15, 15, 15) : Color.FromArgb(200, 25, 25, 25);
                                        float penW = (p.Level == 0) ? 3.0f : 2.2f;
                                        using (Pen pen = new Pen(penColor, penW)) {
                                            pen.LineJoin = LineJoin.Round;
                                            g.DrawPath(pen, path);
                                        }
                                        Color textColor = (p.Level == 0) ? Color.FromArgb(255, 255, 255) : Color.FromArgb(240, 240, 240);
                                        using (SolidBrush brush = new SolidBrush(textColor)) {
                                            g.FillPath(brush, path);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    bmp.Save(pngPath, ImageFormat.Png);
                }
                System.Threading.Interlocked.Increment(ref processed);
            } catch {}
        });

        Console.WriteLine(string.Format("Successfully processed and rendered {0} tiles with Chinese labels!", processed));
    }

    private static void CopyDirectory(string sourceDir, string destDir) {
        Directory.CreateDirectory(destDir);
        foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)) {
            string rel = file.Substring(sourceDir.Length).TrimStart('\\', '/');
            string dest = Path.Combine(destDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(file, dest, true);
        }
    }
}
