using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Microsoft.Win32;

namespace JingPreview {
static class Program {
 [STAThread] static int Main(string[] args) {
  Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
  if(args.Length>1 && args[0].StartsWith("--") && args[0]!="--decode-job"){Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);Application.ThreadException+=delegate(object sender,System.Threading.ThreadExceptionEventArgs e){File.AppendAllText(args[1]+".thread-error.txt",e.Exception.ToString());Environment.Exit(2);};}
  try {
   if(args.Length>1 && (args[0]=="--screenshot" || args[0]=="--ui-smoke-test" || args[0]=="--layout-audit")) Core.StorageOverride=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1])),"test-profile");
   if (args.Length>0 && args[0]=="--self-test") return Tests.Run(args.Length>1?args[1]:"self-test.txt");
   if(args.Length==2 && args[0]=="--test-idle"){System.Threading.Thread.Sleep(Int32.Parse(args[1]));return 0;}
   if(args.Length==3 && args[0]=="--decode-job") return BatchCodec.Worker(args[1],args[2]);
   using(MainForm f=new MainForm()) {
    if(args.Length>1 && args[0]=="--layout-audit"){f.Show();Application.DoEvents();return f.LayoutAudit(args[1]);}
    if(args.Length>1 && args[0]=="--ui-smoke-test") { f.Show();Application.DoEvents();try{int code=f.UiSmokeTest(args[1]);f.Close();if(!f.IsDisposed)throw new InvalidOperationException("Normal window close did not finish");File.AppendAllText(args[1],"PASS UI normal close and resource disposal"+Environment.NewLine);return code;}catch(Exception error){File.WriteAllText(args[1]+".inner-error.txt",error.ToString());return 1;} }
    if(args.Length>1 && args[0]=="--screenshot") { if(args.Length>3){string[] size=args[3].Split('x');f.Size=new Size(Int32.Parse(size[0]),Int32.Parse(size[1]));}f.Show(); if(args.Length>2)f.SelectPage(args[2]); Application.DoEvents(); using(Bitmap b=new Bitmap(f.Width,f.Height)){ f.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height)); b.Save(args[1],ImageFormat.Png); } return 0; }
    if(args.Length==1 && File.Exists(args[0]))f.Shown+=delegate{f.OpenStartup(args[0]);};
    Application.Run(f);
   } return 0;
  } catch(Exception e) { if(args.Length>1&&(args[0]=="--screenshot"||args[0]=="--ui-smoke-test"||args[0]=="--layout-audit"))File.WriteAllText(args[1]+".error.txt",e.ToString());else MessageBox.Show(e.Message,"景览 · 启动失败"); return 1; }
 }
}
static class Core {
 public static string TestRegistryRoot,CacheOverride; public static bool SuppressNotifications;
 public static string Advanced {get{return TestRegistryRoot==null?@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced":TestRegistryRoot+@"\Advanced";}}
 public static string Dwm {get{return TestRegistryRoot==null?@"Software\Microsoft\Windows\DWM":TestRegistryRoot+@"\DWM";}}
 public const string Visual=@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects";
 public static string StorageOverride;
 public static string Data { get { string p=StorageOverride??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"JingPreviewDoctor"); Directory.CreateDirectory(p); return p; } }
 public static readonly string[] Formats={"PSD","PSB","AI","EPS","ESP","PDF","RAW","DNG","CR2","CR3","CRW","NEF","NRW","ARW","SR2","ORF","RW2","MRW","M2W","RAF","PEF","SRW","CDR","EXR","HDR","PNM","PPM","PGM","PBM","JPG","JPEG","PNG","BMP","GIF","TIFF","TIF","ICO","WEBP","HEIC","AVIF","SVG","TGA","DDS"};
 public static string ReadSetting(string path,string name) { using(RegistryKey k=Registry.CurrentUser.OpenSubKey(path)) { object v=k==null?null:k.GetValue(name); return v==null?"未设置":v.ToString(); } }
 public static bool PolicyBlocked() {
  if(TestRegistryRoot!=null){using(RegistryKey k=Registry.CurrentUser.OpenSubKey(TestRegistryRoot+@"\Policies"))return k!=null&&Convert.ToString(k.GetValue("DisableThumbnails"))=="1";}
  string[] paths={@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",@"Software\Policies\Microsoft\Windows\Explorer"};
  foreach(RegistryKey root in new[]{Registry.CurrentUser,Registry.LocalMachine}) foreach(string p in paths) using(RegistryKey k=root.OpenSubKey(p)) if(k!=null) foreach(string n in new[]{"DisableThumbnails","DisablePreviewPane"}) { object v=k.GetValue(n); if(v!=null && v.ToString()=="1") return true; }
  return false;
 }
 public static void Backup(string file) {
  XmlDocument d=new XmlDocument(); XmlElement r=d.CreateElement("settings"); d.AppendChild(r);
  string[,] items={{Advanced,"IconsOnly"},{Advanced,"ShowPreviewHandlers"},{Advanced,"DisableThumbnailCache"},{Dwm,"AlwaysHibernateThumbnails"}};
  for(int i=0;i<items.GetLength(0);i++) { XmlElement e=d.CreateElement("value"); e.SetAttribute("path",items[i,0]); e.SetAttribute("name",items[i,1]); using(RegistryKey k=Registry.CurrentUser.OpenSubKey(items[i,0])) { object v=k==null?null:k.GetValue(items[i,1]); e.SetAttribute("exists",v==null?"false":"true"); if(v!=null) { if(k.GetValueKind(items[i,1])!=RegistryValueKind.DWord) throw new InvalidOperationException("设置类型异常，已取消修改："+items[i,1]); e.InnerText=v.ToString(); } } r.AppendChild(e); } d.Save(file);
 }
 public static void Restore(string file) {
  XmlDocument d=UserPreferences.ReadXml(file);if(d.DocumentElement==null||d.DocumentElement.Name!="settings")throw new InvalidDataException("备份类型无效。");HashSet<string> unique=new HashSet<string>(StringComparer.Ordinal);
  List<XmlElement> entries=new List<XmlElement>();
  foreach(XmlElement e in d.DocumentElement.ChildNodes) {
   string p=e.GetAttribute("path"),n=e.GetAttribute("name");
   bool valid=(p==Advanced && (n=="IconsOnly"||n=="ShowPreviewHandlers"||n=="DisableThumbnailCache")) || (p==Dwm && n=="AlwaysHibernateThumbnails");
   int number; if(!valid || !unique.Add(p+"/"+n) || (e.GetAttribute("exists")!="false" && (e.GetAttribute("exists")!="true" || !Int32.TryParse(e.InnerText,out number)))) throw new InvalidDataException("备份内容无效，未执行恢复。"); entries.Add(e);
  }
  if(entries.Count!=4) throw new InvalidDataException("备份不完整。");
  foreach(XmlElement e in entries) using(RegistryKey k=Registry.CurrentUser.CreateSubKey(e.GetAttribute("path"))) { if(e.GetAttribute("exists")=="true") k.SetValue(e.GetAttribute("name"),Int32.Parse(e.InnerText),RegistryValueKind.DWord); else k.DeleteValue(e.GetAttribute("name"),false); }
  Notify();
 }
 public static string Repair() {
  if(PolicyBlocked()) return "检测到组织策略禁用了缩略图或预览窗格。请联系管理员调整策略；本次未修改设置。";
  string backup=Path.Combine(Data,"backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".xml"); Backup(backup);
  try {
   using(RegistryKey k=Registry.CurrentUser.CreateSubKey(Advanced)) { k.SetValue("IconsOnly",0,RegistryValueKind.DWord); k.SetValue("ShowPreviewHandlers",1,RegistryValueKind.DWord); k.SetValue("DisableThumbnailCache",0,RegistryValueKind.DWord); }
   using(RegistryKey k=Registry.CurrentUser.CreateSubKey(Dwm)) k.SetValue("AlwaysHibernateThumbnails",1,RegistryValueKind.DWord);
  } catch { Restore(backup); throw; }
  Notify(); return "已启用当前用户的缩略图、预览处理程序和缩略图缓存。\r\n"+CleanCache()+"\r\n设置备份："+backup+"\r\n请重新打开文件夹，切换为中/大图标。缺少解码器的格式仍需安装相应组件。";
 }
 public static string CleanCache() {
  string dir=CacheOverride??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Microsoft\Windows\Explorer"); int ok=0,skip=0;
  if(Directory.Exists(dir)) foreach(string file in Directory.GetFiles(dir,"thumbcache_*.db",SearchOption.TopDirectoryOnly)) { try { File.Delete(file); ok++; } catch(IOException) { skip++; } catch(UnauthorizedAccessException) { skip++; } }
  Notify(); return "缩略图缓存：已删除 "+ok+" 个，被占用或无权限 "+skip+" 个。被占用的缓存可注销后重试。";
 }
 [DllImport("shell32.dll")] static extern void SHChangeNotify(uint e,uint f,IntPtr a,IntPtr b);
 public static void Notify(){if(!SuppressNotifications)SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);}
 static string HandlerAt(RegistryKey root,string key,string iid) { using(RegistryKey k=root.OpenSubKey(key+@"\shellex\"+iid)) return k==null?null:Convert.ToString(k.GetValue(null)); }
 public static string Handler(string ext,string iid) {
  string e="."+ext.ToLowerInvariant(); var keys=new List<string>{e,@"SystemFileAssociations\"+e};
  using(RegistryKey k=Registry.ClassesRoot.OpenSubKey(e)) if(k!=null) { string prog=Convert.ToString(k.GetValue(null)); if(prog.Length>0) keys.Add(prog); string perceived=Convert.ToString(k.GetValue("PerceivedType")); if(perceived.Length>0) keys.Add(@"SystemFileAssociations\"+perceived); }
  foreach(string key in keys) { string id=HandlerAt(Registry.ClassesRoot,key,iid); if(!String.IsNullOrEmpty(id)) {
    using(RegistryKey server=Registry.ClassesRoot.OpenSubKey(@"CLSID\"+id+@"\InprocServer32")) return id+(server==null?"（未找到 COM 注册）":"");
  } } return "未检测到";
 }
 public static string Support(string ext) {
  if(Array.IndexOf(new[]{"JPG","JPEG","PNG","BMP","GIF","TIFF","TIF","ICO"},ext)>=0) return "内置图像预览；系统支持依 Windows 组件";
  if(ext=="PSD") return "内置 8 位 RGB/灰度合成图（原始/RLE）";
  if(ext=="PNM"||ext=="PPM"||ext=="PGM"||ext=="PBM") return "内置 P1–P6，8/16 位采样";
  if(ext=="ESP") return "非常见扩展名；若指 PostScript 请用 EPS";
  if(ext=="M2W") return "非常见 RAW 扩展名；可能指 MRW，需确认来源";
  return "依赖已安装的 Shell 处理程序或外部 ImageMagick";
 }
 public static string Diagnose() {
  StringBuilder s=new StringBuilder(); s.AppendLine("景览 · 图片预览修复助手 1.2.1"); s.AppendLine("时间："+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")); s.AppendLine("系统："+Environment.OSVersion+" / "+(Environment.Is64BitOperatingSystem?"64":"32")+" 位；进程 "+(IntPtr.Size*8)+" 位");
  s.AppendLine("运行时：.NET "+Environment.Version); s.AppendLine("IconsOnly="+ReadSetting(Advanced,"IconsOnly")+"；ShowPreviewHandlers="+ReadSetting(Advanced,"ShowPreviewHandlers")); s.AppendLine("限制策略："+(PolicyBlocked()?"发现禁用策略":"未发现已检查的禁用策略"));
  s.AppendLine("下表仅为当前进程注册表视图的注册检测；实际解码须用样本验证。WIC 路径可能无需 Shell 扩展。\r\n");
  foreach(string f in Formats) { s.AppendLine(f+" | "+Support(f)); s.AppendLine("  缩略图："+Handler(f,"{e357fccd-a995-4576-b01f-234630154e96}")+"；预览窗格："+Handler(f,"{8895b1c6-b41f-4c1c-a562-0d564250836f}")); }
  return s.ToString();
 }
}
static class Decoder {
 public static Bitmap Load(string path,out string method) {
  FileInfo fi=new FileInfo(path); if(fi.Length>512L*1024*1024) throw new InvalidDataException("文件超过内置预览的 512 MB 安全上限。");
  string ext=fi.Extension.ToLowerInvariant();
  if(ext==".psd") { method="内置 PSD 合成图解码"; return Psd(path); }
  if(ext==".pnm"||ext==".ppm"||ext==".pgm"||ext==".pbm") {method="内置 Netpbm 解码";return Pnm(path);}
  Image decoded=null;
  try {decoded=Image.FromFile(path);}catch(ArgumentException){}catch(ExternalException){}catch(OutOfMemoryException){}
  if(decoded!=null)using(decoded){Check(decoded.Width,decoded.Height);method="Windows GDI+ 解码（多页仅首帧）";return new Bitmap(decoded);}
  try { Bitmap b=ShellThumb(path); if(b!=null){method="Windows Shell 缩略图（最高 1024px）";return b;} } catch { }
  throw new InvalidDataException("当前没有可用解码器。可安装该格式的系统缩略图扩展，或在下方选择 ImageMagick magick.exe；PDF/EPS/部分 AI 还需要 Ghostscript，CDR 和 RAW 支持取决于构建及文件版本。");
 }
 public static void Check(int w,int h){if(w<1||h<1||w>30000||h>30000||(long)w*h>24000000)throw new InvalidDataException("图像尺寸无效或超过 2400 万像素预览上限。");}
 static int BE16(BinaryReader r){return r.ReadByte()*256+r.ReadByte();}
 static int BE32(BinaryReader r){uint v=(uint)r.ReadByte()<<24|(uint)r.ReadByte()<<16|(uint)r.ReadByte()<<8|r.ReadByte(); if(v>Int32.MaxValue)throw new InvalidDataException("区块过大");return (int)v;}
 static void Skip(BinaryReader r){int n=BE32(r);if(n>r.BaseStream.Length-r.BaseStream.Position)throw new InvalidDataException("PSD 区块损坏");r.BaseStream.Seek(n,SeekOrigin.Current);}
 public static Bitmap Psd(string path) {
  using(BinaryReader r=new BinaryReader(File.OpenRead(path))) {
   if(Encoding.ASCII.GetString(r.ReadBytes(4))!="8BPS"||BE16(r)!=1)throw new InvalidDataException("仅支持 PSD v1 合成图。");r.ReadBytes(6); int ch=BE16(r),h=BE32(r),w=BE32(r),depth=BE16(r),mode=BE16(r);Check(w,h);
   if(depth!=8||(mode!=1&&mode!=3)||ch<(mode==3?3:1)||ch>56)throw new InvalidDataException("内置 PSD 支持 8 位 RGB/灰度；CMYK、16/32 位和 PSB 请使用外部解码器。");
   Skip(r);Skip(r);Skip(r);int compression=BE16(r); if(compression>1)throw new InvalidDataException("PSD ZIP 合成图请使用外部解码器。");
   int[] sizes=null;if(compression==1){sizes=new int[ch*h];for(int i=0;i<sizes.Length;i++)sizes[i]=BE16(r);}
   int used=mode==3?3:1;byte[][] planes=new byte[used][];
   for(int c=0;c<used;c++){ planes[c]=new byte[w*h];for(int y=0;y<h;y++) {if(compression==0) {byte[] row=r.ReadBytes(w);if(row.Length!=w)throw new EndOfStreamException();Buffer.BlockCopy(row,0,planes[c],y*w,w);} else {byte[] row=r.ReadBytes(sizes[c*h+y]);int p=0,x=0;while(p<row.Length){int n=(sbyte)row[p++];if(n>=0){int len=n+1;if(p+len>row.Length||x+len>w)throw new InvalidDataException("PSD RLE 损坏");Buffer.BlockCopy(row,p,planes[c],y*w+x,len);p+=len;x+=len;}else if(n!=-128){int len=1-n;if(p>=row.Length||x+len>w)throw new InvalidDataException("PSD RLE 损坏");byte value=row[p++];for(int j=0;j<len;j++)planes[c][y*w+x++]=value;}}if(x!=w)throw new InvalidDataException("PSD 行不完整");}} }
   byte[] rgb=new byte[w*h*3];for(int i=0;i<w*h;i++){rgb[i*3]=planes[used==3?2:0][i];rgb[i*3+1]=planes[used==3?1:0][i];rgb[i*3+2]=planes[0][i];}return Pixels(w,h,rgb);
  }
 }
 static string Token(Stream s) {StringBuilder b=new StringBuilder();int c;while(true){c=s.ReadByte();if(c<0)throw new EndOfStreamException();if(c==35){while(c>=0&&c!=10)c=s.ReadByte();continue;}if(!Char.IsWhiteSpace((char)c))break;} do{b.Append((char)c);c=s.ReadByte();}while(c>=0&&!Char.IsWhiteSpace((char)c));if(c==13){long p=s.Position;if(s.ReadByte()!=10)s.Position=p;}return b.ToString();}
 public static Bitmap Pnm(string path) {
  using(Stream s=File.OpenRead(path)){string magic=Token(s);if(magic.Length!=2||magic[0]!='P'||magic[1]<'1'||magic[1]>'6')throw new InvalidDataException("仅支持 Netpbm P1–P6。");int kind=magic[1]-'0',w=Int32.Parse(Token(s)),h=Int32.Parse(Token(s));Check(w,h);int max=(kind==1||kind==4)?1:Int32.Parse(Token(s));if(max<1||max>65535)throw new InvalidDataException("采样范围无效");byte[] rgb=new byte[w*h*3];
   for(int y=0;y<h;y++){int packed=0;for(int x=0;x<w;x++){int[] v=new int[3];if(kind==4){if(x%8==0){packed=s.ReadByte();if(packed<0)throw new EndOfStreamException();}v[0]=v[1]=v[2]=((packed>>(7-x%8))&1)==1?0:255;}else {int channels=(kind==3||kind==6)?3:1;for(int c=0;c<channels;c++){int a;if(kind<=3)a=Int32.Parse(Token(s));else {a=s.ReadByte();if(a<0)throw new EndOfStreamException();if(max>255){int b=s.ReadByte();if(b<0)throw new EndOfStreamException();a=a*256+b;}}if(a<0||a>max)throw new InvalidDataException("像素越界");v[c]=kind==1?(a==1?0:255):(a*255/max);}if(channels==1)v[1]=v[2]=v[0];}int p=(y*w+x)*3;rgb[p]=(byte)v[2];rgb[p+1]=(byte)v[1];rgb[p+2]=(byte)v[0];}}return Pixels(w,h,rgb);}
 }
 static Bitmap Pixels(int w,int h,byte[] bgr){Bitmap b=new Bitmap(w,h,PixelFormat.Format24bppRgb);BitmapData d=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,b.PixelFormat);try{for(int y=0;y<h;y++)Marshal.Copy(bgr,y*w*3,IntPtr.Add(d.Scan0,y*d.Stride),w*3);}finally{b.UnlockBits(d);}return b;}
 [StructLayout(LayoutKind.Sequential)] struct SIZE{public int cx,cy;}
 [ComImport,Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IImageFactory{[PreserveSig]int GetImage(SIZE size,int flags,out IntPtr bitmap);}
 [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=false)]static extern void SHCreateItemFromParsingName(string path,IntPtr ctx,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out IImageFactory factory);
 [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
 static Bitmap ShellThumb(string path){Guid g=new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");IImageFactory f;SHCreateItemFromParsingName(Path.GetFullPath(path),IntPtr.Zero,ref g,out f);IntPtr h=IntPtr.Zero;try{int hr=f.GetImage(new SIZE{cx=1024,cy=1024},8,out h);if(hr<0||h==IntPtr.Zero)return null;using(Bitmap b=Image.FromHbitmap(h))return new Bitmap(b);}finally{if(h!=IntPtr.Zero)DeleteObject(h);Marshal.ReleaseComObject(f);}}
 public static Bitmap External(string exe,string path){string tmp=Path.Combine(Path.GetTempPath(),"jing-preview-"+Guid.NewGuid().ToString("N")+".png");try{ProcessStartInfo p=new ProcessStartInfo(exe,"-limit memory 256MiB -limit map 512MiB -limit disk 1GiB -limit time 45 "+Quote(Path.GetFullPath(path)+"[0]")+" -thumbnail 1600x1600 "+Quote(tmp));p.UseShellExecute=false;p.CreateNoWindow=true;using(Process proc=Process.Start(p)){if(!proc.WaitForExit(60000)){proc.Kill();throw new TimeoutException("解码超过 60 秒，已停止。");}if(proc.ExitCode!=0||!File.Exists(tmp))throw new InvalidDataException("外部解码失败。请检查文件、ImageMagick 格式支持与依赖；仅支持当前用户许可的格式策略。");}using(Image i=Image.FromFile(tmp))return new Bitmap(i);}finally{if(File.Exists(tmp))File.Delete(tmp);}}
 static string Quote(string s){if(s.IndexOf('"')>=0)throw new InvalidDataException("路径无效");return "\""+s+"\"";}
}
partial class MainForm:Form {
 readonly Color bg=Color.FromArgb(244,247,251),ink=Color.FromArgb(24,40,60),blue=Color.FromArgb(30,105,229);
 TextBox log;DataGridView grid;PictureBox picture;Label imageInfo;Button repair; string magick="";TabControl tabs;
 public MainForm(){Text="景览 · 图片预览修复助手";Size=new Size(1120,790);MinimumSize=new Size(940,680);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei UI",10);BackColor=bg;ForeColor=ink;AutoScaleMode=AutoScaleMode.Dpi;
  Panel head=new Panel{Dock=DockStyle.Top,Height=113,BackColor=ink,Padding=new Padding(28,16,20,10)};head.Controls.Add(new Label{Text="景览  /  PREVIEW DOCTOR",ForeColor=Color.White,Font=new Font(Font.FontFamily,23,FontStyle.Bold),Dock=DockStyle.Top,Height=49});head.Controls.Add(new Label{Text="让图片重新可见     ·     缩略图修复 / 格式诊断 / 本地预览",ForeColor=Color.LightSteelBlue,Dock=DockStyle.Bottom,Height=29});Controls.Add(head);
  tabs=new TabControl{Dock=DockStyle.Fill,Padding=new Point(20,10)};Controls.Add(tabs);tabs.BringToFront();
  TabPage home=Page("修复中心");FlowLayoutPanel actions=new FlowLayoutPanel{Dock=DockStyle.Top,Height=62,Padding=new Padding(14,10,0,0)};repair=Button("一键修复",delegate{Run("一键修复",Core.Repair);});actions.Controls.Add(repair);actions.Controls.Add(Button("诊断并生成报告",delegate{Run("诊断",Core.Diagnose);}));actions.Controls.Add(Button("清理缩略图缓存",delegate{Run("缓存清理",Core.CleanCache);}));actions.Controls.Add(Button("恢复设置备份",Restore));actions.Controls.Add(Button("导出日志",Export));
  Label intro=new Label{Dock=DockStyle.Top,Height=130,Padding=new Padding(20,17,20,0),Text="图片有图标，却没有缩略图？从这里开始。\r\n一键修复当前用户的显示设置，并清理可释放的缩略图缓存；每次修复先备份设置。\r\n系统预览需要解码器支持。本工具不会凭空补齐 AI / CDR 等专有格式解码器。\r\n适配目标：Windows 7 SP1–Windows 11 · .NET Framework 4.0 或更高版本"};
  log=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,Dock=DockStyle.Fill,BackColor=Color.White,BorderStyle=BorderStyle.None,Font=new Font("Microsoft YaHei UI",10),WordWrap=false};home.Controls.Add(log);home.Controls.Add(actions);home.Controls.Add(intro);
  TabPage formats=Page("格式与处理程序");grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,RowHeadersVisible=false,BackgroundColor=Color.White,BorderStyle=BorderStyle.None};grid.Columns.Add("ext","格式");grid.Columns.Add("support","应用内预览范围");grid.Columns.Add("thumb","系统缩略图处理程序");grid.Columns.Add("preview","系统预览窗格处理程序");grid.Columns[0].FillWeight=25;formats.Controls.Add(grid);FlowLayoutPanel top=new FlowLayoutPanel{Dock=DockStyle.Top,Height=57};top.Controls.Add(Button("刷新处理程序",delegate{RefreshFormats();}));top.Controls.Add(new Label{AutoSize=true,Text="注册检测不等于解码成功；请用真实样本验证。",Padding=new Padding(8,15,0,0)});formats.Controls.Add(top);
  TabPage preview=Page("图片预览与转存");picture=new ZoomPicture{Dock=DockStyle.Fill,BackColor=Color.FromArgb(228,233,240),SizeMode=PictureBoxSizeMode.Zoom,AllowDrop=true};picture.DragEnter+=delegate(object s,DragEventArgs e){if(e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};picture.DragDrop+=delegate(object s,DragEventArgs e){string[] files=(string[])e.Data.GetData(DataFormats.FileDrop);if(files.Length>0)OpenImage(files[0],false);};preview.Controls.Add(picture);imageInfo=new Label{Dock=DockStyle.Bottom,Height=55,Padding=new Padding(12),Text="打开或拖入文件 · 内置预览与系统 Shell 缩略图 · 多页文件仅首帧"};preview.Controls.Add(imageInfo);FlowLayoutPanel pbuttons=new FlowLayoutPanel{Dock=DockStyle.Top,Height=62};pbuttons.Controls.Add(Button("打开图片",delegate{using(OpenFileDialog d=new OpenFileDialog{Filter="所有文件|*.*"})if(d.ShowDialog()==DialogResult.OK)OpenImage(d.FileName,false);}));pbuttons.Controls.Add(Button("转存 PNG / JPG",SaveImage));pbuttons.Controls.Add(Button("选择 magick.exe",delegate{using(OpenFileDialog d=new OpenFileDialog{Filter="ImageMagick|magick.exe"})if(d.ShowDialog()==DialogResult.OK){magick=d.FileName;imageInfo.Text="已选择外部解码器："+magick;}}));pbuttons.Controls.Add(Button("用外部解码器打开",delegate{if(magick.Length==0){MessageBox.Show("请先选择您安装的 ImageMagick magick.exe。");return;}using(OpenFileDialog d=new OpenFileDialog{Filter="所有文件|*.*"})if(d.ShowDialog()==DialogResult.OK)OpenImage(d.FileName,true);}));preview.Controls.Add(pbuttons);
  TabPage about=Page("软件与作者");TextBox aboutText=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=Color.White,ScrollBars=ScrollBars.Vertical,Font=new Font("Microsoft YaHei UI",12),Text="\r\n  景览 · 图片预览修复助手  1.0\r\n\r\n  为设计师与摄影工作者准备的本地预览诊断工具。\r\n  让设置问题有修复入口，让格式限制有清楚说明。\r\n\r\n  联系作者\r\n  微信公众号：景同志\r\n  小红书：景同学\r\n\r\n  开源与下载\r\n  项目仓库：https://github.com/jingpanlong/JingPreviewDoctor\r\n  Releases 提供单文件版与安装版，建议从仓库获取最新版本。\r\n\r\n  使用建议\r\n  ① 在修复中心执行诊断，查看缩略图设置和系统处理程序。\r\n  ② 执行一键修复，然后重新打开文件夹并切换为大图标。\r\n  ③ 文件夹预览窗格可用 Alt + P 切换；文件过大时生成缩略图需要等待。\r\n  ④ 仍无预览的格式，检查是否安装与系统位数匹配的解码器。\r\n\r\n  支持边界\r\n  缩略图、预览窗格和应用内预览是不同能力。\r\n  内置 PSD 仅处理部分合成图，不渲染图层；不内置 AI/CDR/PDF/RAW 引擎。\r\n  ESP 不等于 EPS；M2W 不是本工具确认的相机格式。\r\n  外部 ImageMagick 及其依赖由用户自行安装，本包未附带。\r\n  缓存中被占用的文件会跳过；不会强制结束资源管理器。\r\n\r\n  隐私与数据\r\n  无联网、无遥测；原始图片保持不变，转存时由您选择目标。\r\n  设置备份和操作日志存放在当前用户 LocalAppData\\JingPreviewDoctor。\r\n  备份恢复只恢复显示设置；已删除的可重建缩略图缓存不恢复。\r\n\r\n  兼容性说明\r\n  使用 .NET Framework 4.0 API / AnyCPU，64 位系统运行 64 位进程。\r\n  Windows 7 需要 SP1 及 .NET 4.0+。未对全部 Windows 版本实机验证。"};about.Controls.Add(aboutText);
  InitProductivity(home,preview,formats,top);
  Label footer=new Label{Dock=DockStyle.Bottom,Height=31,Text="微信公众号：景同志     |     小红书：景同学                              本地运行 · 设置可恢复",TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.DimGray};Controls.Add(footer);footer.BringToFront();tabs.BringToFront();InitLayoutQuality();
  Append("欢迎使用景览。点击“诊断并生成报告”检查当前电脑。修复只作用于当前用户，无需管理员权限。");
  Shown+=delegate{RefreshFormats();};
 }
 TabPage Page(string name){TabPage p=new TabPage(name){BackColor=bg,Padding=new Padding(12)};tabs.TabPages.Add(p);return p;}
 Button Button(string text,EventHandler click){Button b=new Button{Text=text,AutoSize=true,Height=38,MinimumSize=new Size(130,38),FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=blue,Margin=new Padding(4,0,7,0)};b.FlatAppearance.BorderColor=Color.LightSteelBlue;b.Click+=click;return b;}
 void Append(string text){log.AppendText("["+DateTime.Now.ToString("HH:mm:ss")+"] "+text+"\r\n\r\n");try{File.AppendAllText(Path.Combine(Core.Data,"operations.log"),DateTime.Now+" "+text+Environment.NewLine,Encoding.UTF8);}catch{}}
 void Run(string name,Func<string> f){UseWaitCursor=true;try{Append(name+"\r\n"+f());}catch(Exception e){Append(name+"失败："+e.Message);}finally{UseWaitCursor=false;}}
 void RefreshFormats(){grid.Rows.Clear();foreach(string f in Core.Formats)grid.Rows.Add(f,Core.Support(f),Core.Handler(f,"{e357fccd-a995-4576-b01f-234630154e96}"),Core.Handler(f,"{8895b1c6-b41f-4c1c-a562-0d564250836f}"));ApplyFormatFilter();}
 void Restore(object sender,EventArgs e){using(OpenFileDialog d=new OpenFileDialog{InitialDirectory=Core.Data,Filter="设置备份|backup-*.xml"})if(d.ShowDialog()==DialogResult.OK)Run("恢复",delegate{Core.Restore(d.FileName);return "已恢复设置："+d.FileName;});}
 void Export(object sender,EventArgs e){using(SaveFileDialog d=new SaveFileDialog{Filter="文本报告|*.txt",FileName="景览诊断报告.txt"})if(d.ShowDialog()==DialogResult.OK)Run("导出报告",delegate{File.WriteAllText(d.FileName,Core.Diagnose()+"\r\n操作记录\r\n"+log.Text,Encoding.UTF8);return "已导出："+d.FileName;});}
 void OpenImage(string path,bool external){if(UseWaitCursor)return;UseWaitCursor=true;imageInfo.Text="正在读取，请稍候…";string method="";System.ComponentModel.BackgroundWorker worker=new System.ComponentModel.BackgroundWorker();worker.DoWork+=delegate(object s,System.ComponentModel.DoWorkEventArgs e){if(external){method="外部 ImageMagick 首帧预览";e.Result=Decoder.External(magick,path);}else e.Result=Decoder.Load(path,out method);};worker.RunWorkerCompleted+=delegate(object s,System.ComponentModel.RunWorkerCompletedEventArgs e){try{if(e.Error!=null){imageInfo.Text="预览失败："+Path.GetFileName(path);MessageBox.Show(e.Error.Message,"无法预览");return;}Bitmap b=(Bitmap)e.Result;if(IsDisposed){b.Dispose();return;}Image old=picture.Image;picture.Image=b;if(old!=null)old.Dispose();currentFile=Path.GetFullPath(path);RememberFile(currentFile);OnImageReady();imageInfo.Text=Path.GetFileName(path)+"  ·  "+b.Width+" × "+b.Height+"  ·  "+method;}finally{UseWaitCursor=false;worker.Dispose();}};worker.RunWorkerAsync();}
 void SaveImage(object sender,EventArgs e){if(picture.Image==null){MessageBox.Show("请先打开一张图片。");return;}using(SaveFileDialog d=new SaveFileDialog{Filter="PNG 图片|*.png|JPEG 图片|*.jpg",FileName="预览转存.png"})if(d.ShowDialog()==DialogResult.OK)try{picture.Image.Save(d.FileName,d.FilterIndex==2?ImageFormat.Jpeg:ImageFormat.Png);imageInfo.Text="已转存预览图："+d.FileName;}catch(Exception ex){MessageBox.Show(ex.Message);}}
 protected override void Dispose(bool disposing){if(disposing)ShutdownLayoutQuality();if(disposing&&picture!=null&&picture.Image!=null){Image image=picture.Image;picture.Image=null;image.Dispose();}if(disposing)DisposeDescendants(this);base.Dispose(disposing);}
}
static class Tests {
 public static int Run(string report) {var results=new List<string>();string temp=Path.Combine(Path.GetTempPath(),"jing-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);int failures=0;
  Action<string,Action> test=delegate(string n,Action a){try{a();results.Add("PASS "+n);}catch(Exception e){failures++;results.Add("FAIL "+n+": "+e.Message);}};
  test("Netpbm P1/P2/P3 ASCII",delegate{foreach(string data in new[]{"P1\n2 1\n0 1\n","P2\n2 1\n255\n255 0\n","P3\n2 1\n255\n255 255 255 0 0 0\n"}){string p=Path.Combine(temp,"ascii.pnm");File.WriteAllText(p,data,Encoding.ASCII);using(Bitmap b=Decoder.Pnm(p)){if(b.GetPixel(0,0).R!=255||b.GetPixel(1,0).R!=0)throw new Exception("pixels");}}});
  test("Netpbm P4/P5/P6 binary + 16bit",delegate{int[] kinds={4,5,6};foreach(int k in kinds){string p=Path.Combine(temp,"bin.pnm");using(FileStream s=File.Create(p)){byte[] head=Encoding.ASCII.GetBytes("P"+k+"\n2 1\n"+(k==4?"":"65535\n"));s.Write(head,0,head.Length);byte[] bytes=k==4?new byte[]{64}:k==5?new byte[]{255,255,0,0}:new byte[]{255,255,255,255,255,255,0,0,0,0,0,0};s.Write(bytes,0,bytes.Length);}using(Bitmap b=Decoder.Pnm(p)){if(b.GetPixel(0,0).R!=255||b.GetPixel(1,0).R!=0)throw new Exception("pixels");}}});
  test("PSD raw + RLE RGB",delegate{foreach(int comp in new[]{0,1}){string p=Path.Combine(temp,"test.psd");using(BinaryWriter w=new BinaryWriter(File.Create(p))){w.Write(Encoding.ASCII.GetBytes("8BPS"));W16(w,1);w.Write(new byte[6]);W16(w,3);W32(w,1);W32(w,2);W16(w,8);W16(w,3);W32(w,0);W32(w,0);W32(w,0);W16(w,comp);if(comp==1)for(int i=0;i<3;i++)W16(w,3);for(int c=0;c<3;c++){if(comp==1)w.Write((byte)1);w.Write((byte)(c==0?255:0));w.Write((byte)(c==1?255:0));}}using(Bitmap b=Decoder.Psd(p)){if(b.GetPixel(0,0).R!=255||b.GetPixel(1,0).G!=255)throw new Exception("pixels");}}});
  test("Oversized dimensions rejected",delegate{bool rejected=false;try{Decoder.Check(30000,30000);}catch(InvalidDataException){rejected=true;}if(!rejected)throw new Exception("not rejected");});
  test("Truncated PPM rejected",delegate{string p=Path.Combine(temp,"bad.ppm");File.WriteAllText(p,"P6\n2 2\n255\n");bool rejected=false;try{using(Bitmap b=Decoder.Pnm(p)){} }catch(EndOfStreamException){rejected=true;}if(!rejected)throw new Exception("not rejected");});
  test("GDI+ PNG load",delegate{string p=Path.Combine(temp,"sample.png");using(Bitmap b=new Bitmap(3,2)){b.SetPixel(0,0,Color.Red);b.Save(p,ImageFormat.Png);}string method;using(Bitmap b=Decoder.Load(p,out method)){if(b.GetPixel(0,0).R!=255)throw new Exception("pixel");}});
  test("Registry diagnostics read-only",delegate{string s=Core.Diagnose();if(!s.Contains("PSD")||!s.Contains("CDR"))throw new Exception("missing format");});
  ProductivityTests.Run(test,temp);ConvenienceTests.Run(test,temp);SystemChecks.Run(test,temp);
  results.Add("Total failures: "+failures+"; no actual Explorer settings modified; isolated test keys cleaned.");File.WriteAllLines(report,results.ToArray(),Encoding.UTF8);foreach(string f in Directory.GetFiles(temp))File.Delete(f);Directory.Delete(temp);return failures==0?0:1;
 }
 static void W16(BinaryWriter w,int v){w.Write((byte)(v>>8));w.Write((byte)v);}static void W32(BinaryWriter w,int v){w.Write((byte)(v>>24));w.Write((byte)(v>>16));w.Write((byte)(v>>8));w.Write((byte)v);}
}
}
